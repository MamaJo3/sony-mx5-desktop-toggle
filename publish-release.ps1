#Requires -Version 5
<#
.SYNOPSIS
  Build a self-contained, no-dependencies release bundle (zip) with a double-click installer.
.EXAMPLE
  .\publish-release.ps1                 # -> .\release\XM5Ambient-1.0.0-win-x64.zip
  .\publish-release.ps1 -Version v1.2.0
#>
[CmdletBinding()]
param([string]$Version = '1.0.0', [string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root  = $PSScriptRoot
$rel   = Join-Path $root 'release'
$stage = Join-Path $rel  "XM5Ambient-$Runtime"
$app   = Join-Path $stage 'app'

if (Test-Path $rel) { Remove-Item $rel -Recurse -Force }
New-Item -ItemType Directory -Force $app | Out-Null

Write-Host "Publishing self-contained ($Runtime)..."
$common = @('-c','Release','-r',$Runtime,'--self-contained','true','-p:PublishSingleFile=false','-o',$app)
dotnet publish "$root\src\SonyXm5.Daemon\SonyXm5.Daemon.csproj"     @common
dotnet publish "$root\src\SonyXm5.Settings\SonyXm5.Settings.csproj" @common

# Drop the double-click installer scripts alongside the app/ folder.
Copy-Item (Join-Path $root 'installer\*') $stage -Force

$zip = Join-Path $rel "XM5Ambient-$Version-$Runtime.zip"
Compress-Archive -Path $stage -DestinationPath $zip -Force

$sizeMB = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "Built release bundle: $zip ($sizeMB MB)"
Write-Host "Upload this zip to a GitHub Release (or push a version tag to let CI build it)."
