[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run on the Windows computer where BIM Arena is installed.' }
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close Revit before uninstalling BIM Arena.' }
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$manifestPath = Join-Path $addins 'BimArena.addin'
if (Test-Path $manifestPath) {
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
    if ($manifest.RevitAddIns.AddIn.AddInId -ne 'D86C2744-CC92-4831-93A6-EC33B437ACD0') { throw 'Manifest belongs to a different add-in; nothing was removed.' }
    Remove-Item -LiteralPath $manifestPath
}
$destination = Join-Path $addins 'BimArena'
if (Test-Path $destination) {
    # Remove only our known files, preserving anything else placed in this folder.
    foreach ($stem in @('BimArena.Revit', 'BimArena.Desktop', 'BimArena.Core')) {
        foreach ($extension in @('.dll', '.deps.json')) {
            $file = Join-Path $destination ($stem + $extension)
            if (Test-Path $file) { Remove-Item -LiteralPath $file }
        }
    }
}
Write-Host 'BIM Arena has been unregistered. Other add-ins were not changed.'
