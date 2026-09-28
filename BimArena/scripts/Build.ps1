[CmdletBinding()]
param([string]$RevitInstallDir = 'C:\Program Files\Autodesk\Revit 2027')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($name in @('RevitAPI.dll', 'RevitAPIUI.dll')) {
    $file = Join-Path $RevitInstallDir $name
    if (!(Test-Path $file)) { throw "Missing $file. Install Revit 2027 or pass -RevitInstallDir." }
    $version = [Reflection.AssemblyName]::GetAssemblyName($file).Version
    if ($version.Major -ne 27) { throw "$name is version $version; this project requires Revit 2027 (27.x)." }
}
Push-Location $root
try {
    & dotnet run --project tests/BimArena.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Engine regression tests failed.' }
    & dotnet publish src/BimArena.Revit/BimArena.Revit.csproj -c Release --no-self-contained "-p:RevitApiDir=$RevitInstallDir" -o package/BimArena
    if ($LASTEXITCODE -ne 0) { throw 'Revit add-in build failed.' }
    & dotnet publish src/BimArena.Demo/BimArena.Demo.csproj -c Release --no-self-contained -p:UseAppHost=false -o package/Demo
    if ($LASTEXITCODE -ne 0) { throw 'Standalone demo build failed.' }
    Write-Host 'Built and tested. Run .\scripts\Install.ps1 to install for this Windows user.'
} finally { Pop-Location }
