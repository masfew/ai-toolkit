<#
.SYNOPSIS
  Builds the single self-contained UAssetLens.exe into a folder, for a release.

.DESCRIPTION
  The release workflow runs this for a uasset-lens-v<version> tag and attaches everything in -OutDir to the
  GitHub release, so people without the .NET SDK can install the exe with `python install.py --exe <path>`.
  Needs the .NET 10 SDK.

.PARAMETER OutDir
  Where to put UAssetLens.exe. Made if it doesn't exist.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
$publish = Join-Path ([System.IO.Path]::GetTempPath()) "uassetlens-publish-$([guid]::NewGuid().ToString('N'))"
& dotnet publish (Join-Path $PSScriptRoot 'src\UAssetLens.App\UAssetLens.App.csproj') -c Release -o $publish --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }
New-Item -ItemType Directory -Force $OutDir | Out-Null
Copy-Item (Join-Path $publish 'UAssetLens.exe') $OutDir -Force
Remove-Item $publish -Recurse -Force
Write-Host "Built $(Join-Path $OutDir 'UAssetLens.exe')"
