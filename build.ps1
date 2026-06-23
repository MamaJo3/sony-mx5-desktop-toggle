#Requires -Version 5
<#
.SYNOPSIS
  Build the XM5 Ambient apps (daemon + settings GUI) into an output folder.
.EXAMPLE
  .\build.ps1                       # publishes to .\dist
  .\build.ps1 -Output C:\Tools\XM5  # custom output
#>
[CmdletBinding()]
param(
    [string]$Output = "$PSScriptRoot\dist",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = 'Stop'

Write-Host "Building XM5 Ambient ($Configuration) -> $Output"
dotnet publish "$PSScriptRoot\src\SonyXm5.Daemon\SonyXm5.Daemon.csproj"     -c $Configuration -o $Output
dotnet publish "$PSScriptRoot\src\SonyXm5.Settings\SonyXm5.Settings.csproj" -c $Configuration -o $Output
Write-Host "Done. Output: $Output"
