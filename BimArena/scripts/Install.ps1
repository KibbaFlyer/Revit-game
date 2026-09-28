[CmdletBinding()]
param([switch]$Build, [string]$RevitInstallDir = 'C:\Program Files\Autodesk\Revit 2027')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($env:OS -ne 'Windows_NT') { throw 'Install this add-in on the Windows computer running Revit 2027.' }
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close Revit before installing or updating BIM Arena.' }
$payload = Join-Path $root 'package\BimArena'
if ($Build -or !(Test-Path (Join-Path $payload 'BimArena.Revit.dll'))) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -RevitInstallDir $RevitInstallDir
}
foreach ($name in @('BimArena.Revit.dll', 'BimArena.Desktop.dll', 'BimArena.Core.dll')) {
    if (!(Test-Path (Join-Path $payload $name))) { throw "Incomplete build: $name is missing." }
}
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$destination = Join-Path $addins 'BimArena'
$manifestPath = Join-Path $addins 'BimArena.addin'
$expectedId = 'D86C2744-CC92-4831-93A6-EC33B437ACD0'
if (Test-Path $manifestPath) {
    [xml]$existing = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
    if ($existing.RevitAddIns.AddIn.AddInId -ne $expectedId) { throw 'An unrelated BimArena.addin exists. Rename it before installing.' }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
# Only this add-in's assemblies are deployed; Autodesk API/runtime DLLs are supplied by Revit.
Get-ChildItem -LiteralPath $payload -File | Where-Object { $_.Name -like 'BimArena.*.dll' -or $_.Name -like 'BimArena.*.deps.json' } | Copy-Item -Destination $destination -Force
[xml]$manifest = Get-Content -LiteralPath (Join-Path $root 'BimArena.addin') -Raw -Encoding UTF8
$manifest.RevitAddIns.AddIn.Assembly = Join-Path $destination 'BimArena.Revit.dll'
$manifest.Save($manifestPath)
Write-Host "Installed to $destination"
Write-Host 'Restart Revit 2027. Open a 3D view, then Add-Ins > BIM Arena > Play BIM Arena.'
Write-Host 'Click a clear floor point, then press Enter to play. Esc returns to Revit.'
