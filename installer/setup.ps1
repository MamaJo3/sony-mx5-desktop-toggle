#Requires -Version 5
# Deploy-only installer (used inside the release bundle; does not build anything).
$ErrorActionPreference = 'Stop'
$app  = Join-Path $PSScriptRoot 'app'
$dest = Join-Path $env:LOCALAPPDATA 'XM5Ambient'

if (-not (Test-Path (Join-Path $app 'sony-ambient-daemon.exe'))) {
    Write-Host "Could not find the app files next to this installer (expected an 'app' folder)."
    Write-Host "Make sure you extracted the whole zip before running Setup.cmd."
    exit 1
}

Write-Host "Installing XM5 Ambient to: $dest"
Get-Process -Name 'sony-ambient-daemon' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $app '*') $dest -Recurse -Force

$ws = New-Object -ComObject WScript.Shell

# Auto-start the daemon at login.
$startup = $ws.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'SonyAmbientDaemon.lnk'))
$startup.TargetPath = (Join-Path $dest 'sony-ambient-daemon.exe')
$startup.WorkingDirectory = $dest
$startup.WindowStyle = 7
$startup.Description = 'Sony XM5 ambient daemon'
$startup.Save()

# Settings shortcut on Desktop and in the Start menu.
$gui = Join-Path $dest 'sony-ambient-config.exe'
foreach ($folder in 'Desktop', 'Programs') {
    $lnk = $ws.CreateShortcut((Join-Path ([Environment]::GetFolderPath($folder)) 'XM5 Ambient Settings.lnk'))
    $lnk.TargetPath = $gui
    $lnk.WorkingDirectory = $dest
    $lnk.IconLocation = "$gui,0"
    $lnk.Description = 'Configure the Sony XM5 ambient hotkey'
    $lnk.Save()
}

Start-Process -FilePath (Join-Path $dest 'sony-ambient-daemon.exe') -WorkingDirectory $dest

Write-Host ""
Write-Host "Installed!  Press  Ctrl + Alt + A  to toggle Ambient / Noise Cancelling."
Write-Host "Open 'XM5 Ambient Settings' (Desktop or Start menu) to change the shortcut or behaviour."
