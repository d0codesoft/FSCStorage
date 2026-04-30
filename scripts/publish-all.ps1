param(
    [string] $Configuration = "Release",
    [string] $OutputRoot = "publish",
    [string[]] $RuntimeIdentifiers = @("win-x64", "linux-x64"),
    [bool] $SelfContained = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceProject = Join-Path $repoRoot "SCP.StorageFSC/scp.filestorage.csproj"
$adminCliProject = Join-Path $repoRoot "fsc_adm_cli/fsc_adm_cli.csproj"
$outputRootPath = Join-Path $repoRoot $OutputRoot

foreach ($rid in $RuntimeIdentifiers) {
    $outputPath = Join-Path $outputRootPath $rid

    if (Test-Path $outputPath) {
        Remove-Item -LiteralPath $outputPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

    dotnet publish $serviceProject `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained $SelfContained `
        --output $outputPath `
        /p:PublishSingleFile=false

    dotnet publish $adminCliProject `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained $SelfContained `
        --output $outputPath `
        /p:PublishSingleFile=false
}

Write-Host "Published service and admin CLI to: $outputRootPath"
