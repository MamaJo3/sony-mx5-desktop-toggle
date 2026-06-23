#Requires -Version 5
$dest = Join-Path $env:LOCALAPPDATA 'XM5Ambient'

Get-Process -Name 'sony-ambient-daemon' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$links = @(
    (Join-Path ([Environment]::GetFolderPath('Startup'))  'SonyAmbientDaemon.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop'))  'XM5 Ambient Settings.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'XM5 Ambient Settings.lnk')
)
foreach ($l in $links) { if (Test-Path $l) { Remove-Item $l -Force } }

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }

Write-Host "XM5 Ambient has been removed."
