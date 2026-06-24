#Requires -Version 5
<#
.SYNOPSIS
  Build and install XM5 Ambient: copies to an install dir, starts the daemon,
  enables auto-start at login, and adds a Settings shortcut to the Desktop.
.EXAMPLE
  .\install.ps1
  .\install.ps1 -InstallDir "C:\Tools\XM5Ambient"
#>
[CmdletBinding()]
param([string]$InstallDir = "$env:LOCALAPPDATA\XM5Ambient")
$ErrorActionPreference = 'Stop'

# 1) Stop any running daemon FIRST so it doesn't lock files we're about to overwrite.
Get-Process -Name 'sony-ambient-daemon' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

# 2) Build straight into the install dir.
& "$PSScriptRoot\build.ps1" -Output $InstallDir

$ws = New-Object -ComObject WScript.Shell

# 3) Auto-start the daemon at login.
$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'SonyAmbientDaemon.lnk'
$s = $ws.CreateShortcut($startupLnk)
$s.TargetPath = (Join-Path $InstallDir 'sony-ambient-daemon.exe')
$s.WorkingDirectory = $InstallDir
$s.WindowStyle = 7
$s.Description = 'Sony XM5 ambient daemon'
$s.Save()

# 4) Desktop shortcut for the Settings GUI.
$gui = Join-Path $InstallDir 'sony-ambient-config.exe'
$desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'XM5 Ambient Settings.lnk'
$g = $ws.CreateShortcut($desktopLnk)
$g.TargetPath = $gui
$g.WorkingDirectory = $InstallDir
$g.IconLocation = "$gui,0"
$g.Description = 'Configure the Sony XM5 ambient hotkey'
$g.Save()

# 5) Start it now.
Start-Process -FilePath (Join-Path $InstallDir 'sony-ambient-daemon.exe') -WorkingDirectory $InstallDir

Write-Host ""
Write-Host "Installed to: $InstallDir"
Write-Host "  - Daemon started and set to auto-start at login."
Write-Host "  - 'XM5 Ambient Settings' shortcut added to your Desktop."
Write-Host "  - Default hotkey: Ctrl+Alt+A  (change it in the Settings app)."
