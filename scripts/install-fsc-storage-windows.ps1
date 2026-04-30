#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string] $ServiceName = "fsc-storage",
    [string] $DisplayName = "FSC Storage",
    [string] $Description = "SCP File Storage Service",

    [string] $UserName = "fstore",
    [System.Security.SecureString] $Password,

    [string] $AppDirectory = "C:\Program Files\FSCStorage",
    [string] $BasePath = "C:\ProgramData\fsc-storage",
    [string] $LogsPath = "C:\ProgramData\fsc-storage\logs",
    [string] $DataPath = "C:\ProgramData\fsc-storage\data",

    [string] $Url = "http://0.0.0.0:5000",
    [string] $DotNetPath,

    [switch] $StartService
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string] $Message)
    Write-Host $Message
}

function Fail {
    param([string] $Message)
    throw $Message
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function New-RandomPassword {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $token = [Convert]::ToBase64String($bytes)
    return "Fsc-$token!1"
}

function ConvertTo-PlainText {
    param([Parameter(Mandatory)] [System.Security.SecureString] $SecureString)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Get-LocalAccountName {
    param([string] $Name)

    if ($Name -match "^[^\\]+\\[^\\]+$") {
        return $Name
    }

    return ".\$Name"
}

function Get-AccountSid {
    param([string] $AccountName)

    $ntAccount = [Security.Principal.NTAccount]::new($AccountName)
    return $ntAccount.Translate([Security.Principal.SecurityIdentifier]).Value
}

function Grant-LogOnAsService {
    param([string] $AccountName)

    Write-Step "Granting 'Log on as a service' to '$AccountName'."

    $sid = Get-AccountSid -AccountName $AccountName
    $tempRoot = Join-Path $env:TEMP "fsc-storage-service-rights-$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    $exportFile = Join-Path $tempRoot "export.inf"
    $importFile = Join-Path $tempRoot "import.inf"
    $dbFile = Join-Path $tempRoot "rights.sdb"

    try {
        secedit.exe /export /cfg $exportFile | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to export local security policy."
        }

        $content = Get-Content -LiteralPath $exportFile
        $rightName = "SeServiceLogonRight"
        $rightLine = $content | Where-Object { $_ -like "$rightName = *" } | Select-Object -First 1
        $sidToken = "*$sid"

        if ($rightLine) {
            $current = ($rightLine -replace "^$rightName\s*=\s*", "").Split(",") |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

            if ($current -contains $sidToken) {
                Write-Step "The account already has 'Log on as a service'."
                return
            }

            $newLine = "$rightName = " + (($current + $sidToken) -join ",")
            $content = $content | ForEach-Object {
                if ($_ -like "$rightName = *") { $newLine } else { $_ }
            }
        }
        else {
            $newLine = "$rightName = $sidToken"
            $privilegeIndex = [Array]::IndexOf($content, "[Privilege Rights]")
            if ($privilegeIndex -ge 0) {
                $before = $content[0..$privilegeIndex]
                $after = $content[($privilegeIndex + 1)..($content.Length - 1)]
                $content = @($before + $newLine + $after)
            }
            else {
                $content = @($content + "[Privilege Rights]" + $newLine)
            }
        }

        Set-Content -LiteralPath $importFile -Value $content -Encoding Unicode
        secedit.exe /configure /db $dbFile /cfg $importFile /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to update local security policy."
        }
    }
    finally {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Ensure-ServiceUser {
    param(
        [string] $Name,
        [System.Security.SecureString] $SecurePassword
    )

    $existing = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue

    if ($existing) {
        Write-Step "User '$Name' already exists."

        if (-not $SecurePassword) {
            Fail "User '$Name' already exists. Provide -Password so the Windows service can be registered with this account."
        }
    }
    else {
        if (-not $SecurePassword) {
            Write-Step "No password was provided. Generating a random password for the service account."
            $plainPassword = New-RandomPassword
            $SecurePassword = ConvertTo-SecureString $plainPassword -AsPlainText -Force
        }

        Write-Step "Creating local service user '$Name'."
        New-LocalUser `
            -Name $Name `
            -Password $SecurePassword `
            -Description "FSC Storage service account" `
            -PasswordNeverExpires `
            -UserMayNotChangePassword | Out-Null
    }

    $accountName = Get-LocalAccountName -Name $Name
    Grant-LogOnAsService -AccountName $accountName

    return [PSCredential]::new($accountName, $SecurePassword)
}

function Ensure-Directory {
    param(
        [string] $Path,
        [string] $AccountName,
        [string] $Rights
    )

    Write-Step "Preparing directory '$Path'."
    New-Item -ItemType Directory -Path $Path -Force | Out-Null

    $acl = Get-Acl -LiteralPath $Path
    $inheritanceFlags = [Security.AccessControl.InheritanceFlags]"ContainerInherit,ObjectInherit"
    $propagationFlags = [Security.AccessControl.PropagationFlags]"None"
    $accessRule = [Security.AccessControl.FileSystemAccessRule]::new(
        $AccountName,
        $Rights,
        $inheritanceFlags,
        $propagationFlags,
        [Security.AccessControl.AccessControlType]::Allow)

    $acl.SetAccessRule($accessRule)
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Ensure-ServiceEnvironment {
    param(
        [string] $Name,
        [string[]] $Variables
    )

    Write-Step "Writing service environment variables."
    $serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
    if (-not (Test-Path -LiteralPath $serviceRegistryPath)) {
        Fail "Service registry key was not found: $serviceRegistryPath"
    }

    New-ItemProperty `
        -LiteralPath $serviceRegistryPath `
        -Name "Environment" `
        -PropertyType MultiString `
        -Value $Variables `
        -Force | Out-Null
}

function Ensure-WindowsService {
    param(
        [string] $Name,
        [string] $Display,
        [string] $ServiceDescription,
        [string] $BinaryPath,
        [System.Management.Automation.PSCredential] $Credential
    )

    $existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Step "Service '$Name' already exists. Updating service registration."
        if ($existing.Status -ne "Stopped") {
            Stop-Service -Name $Name -Force -ErrorAction Stop
        }

        $plainPassword = ConvertTo-PlainText -SecureString $Credential.Password
        try {
            sc.exe config $Name binPath= $BinaryPath obj= $Credential.UserName password= $plainPassword start= auto DisplayName= $Display | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Fail "Failed to update Windows service '$Name'."
            }
        }
        finally {
            $plainPassword = $null
        }
    }
    else {
        Write-Step "Creating Windows service '$Name'."
        New-Service `
            -Name $Name `
            -DisplayName $Display `
            -Description $ServiceDescription `
            -BinaryPathName $BinaryPath `
            -StartupType Automatic `
            -Credential $Credential | Out-Null
    }

    sc.exe failure $Name reset= 300 actions= restart/10000/restart/30000/""/300000 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to configure service recovery actions."
    }
}

function Validate-Installation {
    param(
        [string] $Name,
        [string] $DllPath,
        [string] $AccountName
    )

    Write-Step "Validating installation."

    if (-not (Test-Path -LiteralPath $DllPath -PathType Leaf)) {
        Fail "Service DLL was not found: $DllPath"
    }

    foreach ($path in @($AppDirectory, $BasePath, $LogsPath, $DataPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            Fail "Directory was not created: $path"
        }
    }

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        Fail "Windows service was not registered: $Name"
    }

    $testFile = Join-Path $DataPath ".fsc-storage-access-test"
    $process = Start-Process `
        -FilePath "cmd.exe" `
        -ArgumentList "/c", "echo ok > `"$testFile`"" `
        -Credential ([PSCredential]::new($AccountName, $script:ServiceCredential.Password)) `
        -LoadUserProfile `
        -Wait `
        -PassThru `
        -WindowStyle Hidden

    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $testFile)) {
        Fail "Service user cannot write to the data directory."
    }

    Remove-Item -LiteralPath $testFile -Force -ErrorAction SilentlyContinue
}

if (-not (Test-IsAdministrator)) {
    Fail "This script must be run as Administrator."
}

if (-not $DotNetPath) {
    $dotnetCommand = Get-Command "dotnet.exe" -ErrorAction SilentlyContinue
    if (-not $dotnetCommand) {
        Fail "dotnet.exe was not found. Provide -DotNetPath or install the .NET runtime."
    }

    $DotNetPath = $dotnetCommand.Source
}

$serviceDllPath = Join-Path $AppDirectory "scp.filestorage.dll"
$binaryPath = "`"$DotNetPath`" `"$serviceDllPath`""

Write-Step "Installing FSC Storage Windows service."

$script:ServiceCredential = Ensure-ServiceUser -Name $UserName -SecurePassword $Password
$serviceAccount = $script:ServiceCredential.UserName

Ensure-Directory -Path $AppDirectory -AccountName $serviceAccount -Rights "Modify"
Ensure-Directory -Path $BasePath -AccountName $serviceAccount -Rights "Modify"
Ensure-Directory -Path $LogsPath -AccountName $serviceAccount -Rights "Modify"
Ensure-Directory -Path $DataPath -AccountName $serviceAccount -Rights "Modify"

Ensure-WindowsService `
    -Name $ServiceName `
    -Display $DisplayName `
    -ServiceDescription $Description `
    -BinaryPath $binaryPath `
    -Credential $script:ServiceCredential

Ensure-ServiceEnvironment `
    -Name $ServiceName `
    -Variables @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ASPNETCORE_URLS=$Url",
        "DOTNET_PRINT_TELEMETRY_MESSAGE=false",
        "DOTNET_CLI_TELEMETRY_OPTOUT=1",
        "Paths__BasePath=$BasePath",
        "Paths__LogsPath=$LogsPath",
        "Paths__DataPath=$DataPath",
        "FileStorageMultipart__RootPath=$BasePath",
        "FileStorageMultipart__TempFolderName=_multipart",
        "FileStorageMultipart__FilesFolderName=files"
    )

Validate-Installation -Name $ServiceName -DllPath $serviceDllPath -AccountName $serviceAccount

if ($StartService) {
    Write-Step "Starting service '$ServiceName'."
    Start-Service -Name $ServiceName
}

Write-Step "Installation completed successfully."
Write-Step "Service name: $ServiceName"
Write-Step "Application directory: $AppDirectory"
Write-Step "Base path: $BasePath"
Write-Step "Logs path: $LogsPath"
Write-Step "Data path: $DataPath"
