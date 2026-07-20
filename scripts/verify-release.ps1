[CmdletBinding()]
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"

$required = @(
    "Nexo.exe",
    "Nexo.dll",
    "Nexo.deps.json",
    "Nexo.runtimeconfig.json",
    "Nexo.Core.dll",
    "Nexo.Windows.dll"
)

$missing = $required | Where-Object {
    -not (Test-Path (Join-Path $publishDirectory $_))
}

if ($missing) {
    throw "Faltan archivos de publicación: $($missing -join ', ')"
}

$forbidden = Get-ChildItem $publishDirectory -Recurse -File | Where-Object {
    $_.Name -in @(
        "settings.json",
        "tasks.json",
        "focus.json",
        "routines.json",
        "conversation.json"
    ) -or
    $_.Extension -in @(".key", ".pfx")
}

if ($forbidden) {
    throw "La publicación contiene datos que no deben distribuirse: $($forbidden.FullName -join ', ')"
}

$size = (Get-ChildItem $publishDirectory -Recurse -File |
    Measure-Object Length -Sum).Sum

Write-Host "Publicación verificada."
Write-Host ("Archivos: {0}" -f (Get-ChildItem $publishDirectory -Recurse -File).Count)
Write-Host ("Tamaño:   {0:N1} MB" -f ($size / 1MB))
