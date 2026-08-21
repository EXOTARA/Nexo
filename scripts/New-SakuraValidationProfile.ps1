<#
.SYNOPSIS
    Diseno D3.2 - infraestructura reutilizable para validar Sakura sin tocar los datos reales
    del usuario en %LocalAppData%\Sakura.

.DESCRIPTION
    Crea una carpeta de perfil aislada bajo artifacts\validation-profiles\<Sprint>-<timestamp>\ y,
    si se le da la ruta de un Sakura.exe, lo lanza con la variable de entorno SAKURA_DATA_ROOT
    apuntando SOLO a ese perfil, fijada unicamente para ese proceso hijo (nunca para la sesion de
    PowerShell que invoca este script, para no dejarla filtrada a ningun otro proceso que el
    usuario abra despues desde la misma consola).

    Este script nunca:
      - toca %LocalAppData%\Sakura ni %LocalAppData%\Nexo;
      - borra el perfil que crea (queda como evidencia; borralo tu mismo si ya no lo necesitas);
      - cierra ningun proceso de Sakura que no haya lanzado el mismo.

.PARAMETER Sprint
    Nombre corto del sprint o la validacion (p. ej. "d3.2-sandbox"). Se usa como prefijo de la
    carpeta del perfil.

.PARAMETER ExePath
    Ruta a Sakura.exe. Si se omite, el script solo crea la carpeta del perfil y muestra como
    lanzar Sakura con ella manualmente.

.PARAMETER Wait
    Si se pasa junto con -ExePath, espera a que el proceso lanzado termine antes de devolver el
    control (util para scripts que encadenan pasos). Sin esta opcion, el proceso queda en marcha y
    es responsabilidad de quien llama a este script detenerlo cuando corresponda.

.OUTPUTS
    Un objeto con ProfilePath y, si se lanzo Sakura, el Process iniciado.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Sprint,

    [string]$ExePath,

    [switch]$Wait
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$profilesRoot = Join-Path $root "artifacts\validation-profiles"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$profilePath = Join-Path $profilesRoot "$Sprint-$timestamp"

New-Item -ItemType Directory -Force -Path $profilePath | Out-Null

Write-Host "Perfil de validacion: $profilePath"
Write-Host "SAKURA_DATA_ROOT quedara fijado SOLO para el proceso de Sakura lanzado desde aqui - nunca para esta sesion de PowerShell ni para %LocalAppData%\Sakura."

if (-not $ExePath) {
    Write-Host ""
    Write-Host "Para lanzar Sakura manualmente con este perfil:"
    Write-Host "  `$startInfo = New-Object System.Diagnostics.ProcessStartInfo"
    Write-Host "  `$startInfo.FileName = '<ruta a Sakura.exe>'"
    Write-Host "  `$startInfo.UseShellExecute = `$false"
    Write-Host "  `$startInfo.EnvironmentVariables['SAKURA_DATA_ROOT'] = '$profilePath'"
    Write-Host "  [System.Diagnostics.Process]::Start(`$startInfo)"
    return [pscustomobject]@{ ProfilePath = $profilePath; Process = $null }
}

if (-not (Test-Path $ExePath)) {
    throw "No existe el ejecutable indicado: $ExePath"
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $ExePath
$startInfo.UseShellExecute = $false
$startInfo.EnvironmentVariables["SAKURA_DATA_ROOT"] = $profilePath

$process = [System.Diagnostics.Process]::Start($startInfo)
Write-Host "Sakura lanzado (PID $($process.Id)) con perfil de validacion aislado."

if ($Wait) {
    $process.WaitForExit()
}

[pscustomobject]@{ ProfilePath = $profilePath; Process = $process }
