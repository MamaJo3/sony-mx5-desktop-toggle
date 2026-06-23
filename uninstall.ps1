#Requires -Version 5
<#
.SYNOPSIS
  Stop the daemon, remove the auto-start and Desktop shortcuts, and delete the install dir.
.EXAMPLE
  .\uninstall.ps1
  .\uninstall.ps1 -InstallDir "C:\Tools\XM5Ambient"
#>
[CmdletBinding()]
param([string]$InstallDir = "$env:LOCALAPPDATA\XM5Ambient")

Get-Process -Name 'sony-ambient-daemon' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'SonyAmbientDaemon.lnk'
if (Test-Path $startupLnk) { Remove-Item $startupLnk -Force }

$desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'XM5 Ambient Settings.lnk'
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force }

if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

Write-Host "Uninstalled XM5 Ambient."
