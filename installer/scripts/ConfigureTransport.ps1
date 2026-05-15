param(
    [Alias("i")]
    [string] $InstallDir,

    [Alias("a")]
    [string] $AppSettingsPath,

    [Alias("m")]
    [ValidateSet("HTTP", "EXISTING_PFX", "SELF_SIGNED")]
    [string] $Mode = "HTTP",

    [Alias("x")]
    [string] $ExistingPfxPath,

    [Alias("xp")]
    [string] $ExistingPfxPassword,

    [Alias("g")]
    [string] $GeneratedPfxPath,

    [Alias("gp")]
    [string] $GeneratedPfxPassword,

    [Alias("n")]
    [string] $DnsNames
)

$ErrorActionPreference = "Stop"

function Ensure-ObjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if (-not $Object.PSObject.Properties[$Name]) {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value ([pscustomobject]@{})
    }

    return $Object.$Name
}

function Set-ObjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [AllowNull()]
        [object] $Value
    )

    if ($Object.PSObject.Properties[$Name]) {
        $Object.$Name = $Value
        return
    }

    $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
}

function Remove-ObjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if ($Object.PSObject.Properties[$Name]) {
        $Object.PSObject.Properties.Remove($Name)
    }
}

function Get-SanTextExtension {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Names
    )

    $sanParts = foreach ($name in $Names) {
        $address = $null
        if ([System.Net.IPAddress]::TryParse($name, [ref] $address)) {
            "ipaddress=$name"
        }
        else {
            "dns=$name"
        }
    }

    "2.5.29.17={text}" + ($sanParts -join "&")
}

if ([string]::IsNullOrWhiteSpace($AppSettingsPath)) {
    if ([string]::IsNullOrWhiteSpace($InstallDir)) {
        throw "InstallDir or AppSettingsPath is required."
    }

    $AppSettingsPath = Join-Path $InstallDir "appsettings.json"
}

if ([string]::IsNullOrWhiteSpace($GeneratedPfxPath) -and -not [string]::IsNullOrWhiteSpace($InstallDir)) {
    $GeneratedPfxPath = Join-Path $InstallDir "certs\server.pfx"
}

if (-not (Test-Path -LiteralPath $AppSettingsPath)) {
    throw "App settings file was not found: $AppSettingsPath"
}

$settings = Get-Content -LiteralPath $AppSettingsPath -Raw | ConvertFrom-Json
$kestrel = Ensure-ObjectProperty -Object $settings -Name "Kestrel"
$endpoints = Ensure-ObjectProperty -Object $kestrel -Name "Endpoints"

if (-not $endpoints.PSObject.Properties["Http"]) {
    Set-ObjectProperty -Object $endpoints -Name "Http" -Value ([pscustomobject]@{
        Url = "http://0.0.0.0:5770"
    })
}

switch ($Mode) {
    "HTTP" {
        Remove-ObjectProperty -Object $endpoints -Name "Https"
    }

    "EXISTING_PFX" {
        if ([string]::IsNullOrWhiteSpace($ExistingPfxPath)) {
            throw "Existing PFX path is required."
        }

        if ([string]::IsNullOrWhiteSpace($ExistingPfxPassword)) {
            throw "Existing PFX password is required."
        }

        if (-not (Test-Path -LiteralPath $ExistingPfxPath)) {
            throw "Existing PFX file was not found: $ExistingPfxPath"
        }

        Set-ObjectProperty -Object $endpoints -Name "Https" -Value ([pscustomobject]@{
            Url = "https://0.0.0.0:5771"
            Certificate = [pscustomobject]@{
                Path = $ExistingPfxPath
                Password = $ExistingPfxPassword
            }
        })
    }

    "SELF_SIGNED" {
        if ([string]::IsNullOrWhiteSpace($DnsNames)) {
            throw "DNS names or IP addresses are required for a self-signed certificate."
        }

        if ([string]::IsNullOrWhiteSpace($GeneratedPfxPath)) {
            throw "Generated PFX path is required."
        }

        if ([string]::IsNullOrWhiteSpace($GeneratedPfxPassword)) {
            throw "Generated PFX password is required."
        }

        $names = $DnsNames.Split(",") |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

        if ($names.Count -eq 0) {
            throw "DNS names or IP addresses are required for a self-signed certificate."
        }

        $certificateDirectory = Split-Path -Parent $GeneratedPfxPath
        if (-not [string]::IsNullOrWhiteSpace($certificateDirectory)) {
            New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
        }

        $securePassword = ConvertTo-SecureString $GeneratedPfxPassword -AsPlainText -Force
        $certificate = New-SelfSignedCertificate `
            -Subject "CN=$($names[0])" `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -KeyExportPolicy Exportable `
            -CertStoreLocation "Cert:\LocalMachine\My" `
            -NotAfter (Get-Date).AddYears(3) `
            -TextExtension (Get-SanTextExtension -Names $names)

        try {
            Export-PfxCertificate `
                -Cert $certificate `
                -FilePath $GeneratedPfxPath `
                -Password $securePassword `
                -Force | Out-Null
        }
        finally {
            Remove-Item -LiteralPath "Cert:\LocalMachine\My\$($certificate.Thumbprint)" -ErrorAction SilentlyContinue
        }

        Set-ObjectProperty -Object $endpoints -Name "Https" -Value ([pscustomobject]@{
            Url = "https://0.0.0.0:5771"
            Certificate = [pscustomobject]@{
                Path = $GeneratedPfxPath
                Password = $GeneratedPfxPassword
            }
        })
    }
}

$settings |
    ConvertTo-Json -Depth 100 |
    Set-Content -LiteralPath $AppSettingsPath -Encoding UTF8
