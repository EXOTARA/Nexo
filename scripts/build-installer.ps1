[CmdletBinding()]
param(
    [string]$Version = "0.9.4-beta",
    [string]$Runtime = "win-x64",
    [string]$RepositoryUrl = "https://github.com/EXOTARA/Nexo",
    [switch]$SkipPublish,
    [string]$InnoSetupPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"
$outputDirectory = Join-Path $root "artifacts\installer"
$installerScript = Join-Path $root "installer\Kohana.iss"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish.ps1") `
        -Version $Version `
        -Runtime $Runtime `
        -RepositoryUrl $RepositoryUrl
}

if (-not (Test-Path (Join-Path $publishDirectory "Kohana.exe"))) {
    throw "No existe una publicación válida de Kohana en $publishDirectory."
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath)) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $InnoSetupPath = $candidates |
        Where-Object { $_ -and (Test-Path $_) } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath) -or -not (Test-Path $InnoSetupPath)) {
    throw "No encontré Inno Setup 6. Instálalo o usa -InnoSetupPath con la ruta de ISCC.exe."
}

$numericVersion = if ($Version -match '^(?<number>\d+\.\d+\.\d+)') {
    $Matches.number
}
else {
    throw "La versión '$Version' no tiene el formato X.Y.Z[-sufijo]."
}

New-Item $outputDirectory -ItemType Directory -Force | Out-Null
Remove-Item (Join-Path $outputDirectory "Kohana-*-Setup.exe") -Force -ErrorAction SilentlyContinue

Write-Host "==> Creando instalador de Kohana"
& $InnoSetupPath `
    "/DMyAppVersion=$Version" `
    "/DMyNumericVersion=$numericVersion.0" `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$outputDirectory" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup no pudo crear el instalador."
}

$installer = Get-ChildItem $outputDirectory -Filter "Kohana-*-Setup.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $installer) {
    throw "Inno Setup terminó, pero no encontré el instalador generado."
}

$hash = Get-FileHash $installer.FullName -Algorithm SHA256
$checksumFile = "$($installer.FullName).sha256"
"$($hash.Hash.ToLowerInvariant())  $($installer.Name)" |
    Set-Content $checksumFile -Encoding ASCII

Write-Host ""
Write-Host "Instalador listo:"
Write-Host "  EXE:    $($installer.FullName)"
Write-Host "  SHA256: $checksumFile"

[pscustomobject]@{
    Installer = $installer.FullName
    ChecksumFile = $checksumFile
}
