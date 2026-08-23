<#
    Switches the emulated mascon model and relaunches the bridge.

    Usage:
        .\try-model.ps1 ZKNS-002
        .\try-model.ps1 ZKNS-011 -Mode run

    Defaults to 'test', which cycles the 15 notches on its own without touching
    the HOTAS. That is the quickest way to see whether the game reacts.

    PowerShell blocks scripts by default. Lift it for this window only:
        Set-ExecutionPolicy -Scope Process Bypass
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('ZKNS-001', 'ZKNS-001b', 'ZKNS-002', 'ZKNS-011', 'ZKNS-012', 'ZKNS-013')]
    [string]$Model,

    [ValidateSet('test', 'run')]
    [string]$Mode = 'test'
)

$ErrorActionPreference = 'Stop'

$ids = @{
    'ZKNS-001'  = '0F0D:00C1'
    'ZKNS-001b' = '33DD:0001'
    'ZKNS-002'  = '33DD:0002'
    'ZKNS-011'  = '33DD:0003'
    'ZKNS-012'  = '33DD:0004'
    'ZKNS-013'  = '33DD:0005'
}

$cfg = Join-Path $PSScriptRoot 'config.json'
$exe = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows10.0.26100.0/win-x64/mascon-bridge.exe'

if (-not (Test-Path $exe)) { throw "Executable not found: $exe" }
if (-not (Test-Path $cfg)) { throw "Configuration not found: $cfg" }

# --- 1. Stop the previous instance -------------------------------------------
# mascon-bridge runs elevated (app.manifest), so killing it needs UAC.
$p = Get-Process -Name mascon-bridge -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "Stopping mascon-bridge (PID $($p.Id))... accept the UAC prompt." -ForegroundColor Yellow
    Start-Process taskkill.exe -ArgumentList '/PID', $p.Id, '/F' -Verb RunAs
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Process -Name mascon-bridge -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
    }
    if (Get-Process -Name mascon-bridge -ErrorAction SilentlyContinue) {
        throw "Could not stop the previous instance. Kill it manually: taskkill /IM mascon-bridge.exe /F"
    }
    # The virtual device is left orphaned in PnP; the next start evicts it.
    Start-Sleep -Milliseconds 500
}

# --- 2. Switch the model ------------------------------------------------------
$text = Get-Content $cfg -Raw
$updated = $text -replace '("Model"\s*:\s*")[^"]*(")', "`${1}$Model`${2}"
if ($updated -notmatch [regex]::Escape("`"Model`": `"$Model`"")) {
    throw "Could not write the model to $cfg. Check it by hand."
}
Set-Content -Path $cfg -Value $updated -Encoding utf8 -NoNewline
Write-Host "Model = $Model  ($($ids[$Model]))" -ForegroundColor Green

# --- 3. Relaunch --------------------------------------------------------------
Write-Host "Launching '$Mode'... accept the UAC prompt. It opens in its own window." -ForegroundColor Cyan
Start-Process $exe -ArgumentList $Mode -Verb RunAs
