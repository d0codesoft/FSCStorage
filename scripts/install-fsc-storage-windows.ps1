[CmdletBinding()]
param(
    [ValidateSet("Install", "Remove")]
    [string] $Action,

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

function Show-Usage {
    $scriptName = Split-Path -Leaf $PSCommandPath

    Write-Host "FSC Storage Windows service installer"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\$scriptName -Action Install -Password <SecureString> [options]"
    Write-Host "  .\$scriptName -Action Remove [options]"
    Write-Host ""
    Write-Host "Common parameters:"
    Write-Host "  -Action        Install or Remove. Required."
    Write-Host "  -ServiceName   Windows service name. Default: fsc-storage"
    Write-Host "  -UserName      Local service user name. Default: fstore"
    Write-Host "  -Password      Service user password as SecureString."
    Write-Host "  -AppDirectory  Directory containing scp.filestorage.dll."
    Write-Host "  -BasePath      Metadata/database directory."
    Write-Host "  -LogsPath      Log directory."
    Write-Host "  -DataPath      File data directory."
    Write-Host "  -Url           ASP.NET Core listen URL. Default: http://0.0.0.0:5000"
    Write-Host "  -StartService  Start the service after installation."
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  `$pwd = Read-Host `"Service user password`" -AsSecureString"
    Write-Host "  .\$scriptName -Action Install -Password `$pwd -AppDirectory `"C:\Program Files\FSCStorage`" -StartService"
    Write-Host "  .\$scriptName -Action Remove -ServiceName `"fsc-storage`""
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Initialize-LsaRightsApi {
    if ("FscStorage.LsaRights" -as [type]) {
        return
    }

    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace FscStorage
{
    public static class LsaRights
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_UNICODE_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_OBJECT_ATTRIBUTES
        {
            public UInt32 Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public UInt32 Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        private static extern UInt32 LsaOpenPolicy(
            IntPtr systemName,
            ref LSA_OBJECT_ATTRIBUTES objectAttributes,
            UInt32 desiredAccess,
            out IntPtr policyHandle);

        [DllImport("advapi32.dll", PreserveSig = true)]
        private static extern UInt32 LsaAddAccountRights(
            IntPtr policyHandle,
            byte[] accountSid,
            LSA_UNICODE_STRING[] userRights,
            UInt32 countOfRights);

        [DllImport("advapi32.dll", PreserveSig = true)]
        private static extern UInt32 LsaClose(IntPtr policyHandle);

        [DllImport("advapi32.dll")]
        private static extern UInt32 LsaNtStatusToWinError(UInt32 status);

        public static void AddAccountRight(byte[] sidBytes, string rightName)
        {
            const UInt32 POLICY_LOOKUP_NAMES = 0x00000800;
            const UInt32 POLICY_CREATE_ACCOUNT = 0x00000010;

            var objectAttributes = new LSA_OBJECT_ATTRIBUTES();
            objectAttributes.Length = (UInt32)Marshal.SizeOf(typeof(LSA_OBJECT_ATTRIBUTES));

            IntPtr policyHandle;
            UInt32 status = LsaOpenPolicy(
                IntPtr.Zero,
                ref objectAttributes,
                POLICY_LOOKUP_NAMES | POLICY_CREATE_ACCOUNT,
                out policyHandle);

            if (status != 0)
            {
                throw new InvalidOperationException("LsaOpenPolicy failed. Win32Error=" + LsaNtStatusToWinError(status));
            }

            IntPtr rightBuffer = IntPtr.Zero;

            try
            {
                rightBuffer = Marshal.StringToHGlobalUni(rightName);
                var right = new LSA_UNICODE_STRING
                {
                    Buffer = rightBuffer,
                    Length = (UInt16)(rightName.Length * 2),
                    MaximumLength = (UInt16)((rightName.Length + 1) * 2)
                };

                status = LsaAddAccountRights(policyHandle, sidBytes, new[] { right }, 1);
                if (status != 0)
                {
                    throw new InvalidOperationException("LsaAddAccountRights failed for " + rightName + ". Win32Error=" + LsaNtStatusToWinError(status));
                }
            }
            finally
            {
                if (rightBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(rightBuffer);
                }

                LsaClose(policyHandle);
            }
        }
    }
}
"@
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

    if ($AccountName -match "^\.\\(?<LocalName>.+)$") {
        $localUser = Get-LocalUser -Name $Matches.LocalName -ErrorAction Stop
        return $localUser.SID.Value
    }

    if ($AccountName -notmatch "^[^\\]+\\[^\\]+$") {
        $localUser = Get-LocalUser -Name $AccountName -ErrorAction Stop
        return $localUser.SID.Value
    }

    $ntAccount = [Security.Principal.NTAccount]::new($AccountName)
    return $ntAccount.Translate([Security.Principal.SecurityIdentifier]).Value
}

function Grant-LogOnAsService {
    param([string] $AccountName)

    Add-AccountToUserRight -AccountName $AccountName -RightName "SeServiceLogonRight" -Description "Log on as a service"
}

function Deny-InteractiveLogon {
    param([string] $AccountName)

    Add-AccountToUserRight -AccountName $AccountName -RightName "SeDenyInteractiveLogonRight" -Description "Deny log on locally"
    Add-AccountToUserRight -AccountName $AccountName -RightName "SeDenyRemoteInteractiveLogonRight" -Description "Deny log on through Remote Desktop Services"
}

function Add-AccountToUserRight {
    param(
        [string] $AccountName,
        [string] $RightName,
        [string] $Description
    )

    Write-Step "Granting '$Description' to '$AccountName'."

    $sid = Get-AccountSid -AccountName $AccountName
    $sidObject = [Security.Principal.SecurityIdentifier]::new($sid)
    $sidBytes = New-Object byte[] $sidObject.BinaryLength
    $sidObject.GetBinaryForm($sidBytes, 0)

    Initialize-LsaRightsApi
    [FscStorage.LsaRights]::AddAccountRight($sidBytes, $RightName)
}

function Ensure-ServiceUser {
    param(
        [string] $Name,
        [System.Security.SecureString] $SecurePassword
    )

    $existing = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue

    if ($existing) {
        Write-Step "User '$Name' already exists. Skipping user creation."

        if (-not $SecurePassword) {
            Write-Step "No password was provided. Generating a new password for the existing service account."
            $plainPassword = New-RandomPassword
            $SecurePassword = ConvertTo-SecureString $plainPassword -AsPlainText -Force
            Set-LocalUser -Name $Name -Password $SecurePassword
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
    Deny-InteractiveLogon -AccountName $accountName

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
    $accountSid = [Security.Principal.SecurityIdentifier]::new((Get-AccountSid -AccountName $AccountName))
    $inheritanceFlags = [Security.AccessControl.InheritanceFlags]"ContainerInherit,ObjectInherit"
    $propagationFlags = [Security.AccessControl.PropagationFlags]"None"
    $accessRule = [Security.AccessControl.FileSystemAccessRule]::new(
        $accountSid,
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

function Remove-WindowsService {
    param([string] $Name)

    Write-Step "Removing Windows service '$Name'."

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Step "Service '$Name' does not exist."
        return
    }

    if ($service.Status -ne "Stopped") {
        Write-Step "Stopping service '$Name'."
        Stop-Service -Name $Name -Force -ErrorAction Stop

        $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }

    sc.exe delete $Name | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to delete Windows service '$Name'."
    }

    Write-Step "Service '$Name' was deleted."
}

if (-not $PSBoundParameters.ContainsKey("Action")) {
    Show-Usage
    return
}

if (-not (Test-IsAdministrator)) {
    Fail "This script must be run as Administrator."
}

if ($Action -eq "Remove") {
    Remove-WindowsService -Name $ServiceName
    Write-Step "Removal completed successfully."
    return
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
