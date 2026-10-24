<#
.SYNOPSIS
  Builds UAsset Lens and registers it as Plastic's diff and merge tool for .uasset and .umap files.

.DESCRIPTION
  Publishes one self-contained UAssetLens.exe to %LOCALAPPDATA%\UAssetLens, then adds a DiffToolData and
  a MergeToolData entry for .uasset;.umap at the top of Plastic's client.conf, above Plastic's own entries.
  Safe to rerun: it replaces its own entries instead of adding more. client.conf is backed up first.

  Plastic rewrites client.conf when it closes, so close Plastic (and Gluon) before running this.

.PARAMETER Uninstall
  Removes UAsset Lens's entries from client.conf and deletes the installed exe.

.PARAMETER SkipBuild
  Only updates client.conf, using the exe already installed.

.PARAMETER ExePath
  Installs this UAssetLens.exe (for example one downloaded from a release) instead of building one, so the .NET SDK
  isn't needed.
#>
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$SkipBuild,
    [string]$ExePath
)

$ErrorActionPreference = 'Stop'

# The Claude desktop app is a packaged (MSIX) app, so Windows redirects AppData writes from anything it starts into
# %LOCALAPPDATA%\Packages\Claude_*\LocalCache. An exe installed from there is invisible to Plastic, which then reports
# "Diff tools in client.conf are incorrectly defined". Those processes don't report a package identity, so test for the
# redirect itself: write a probe file and see whether it lands in a package's LocalCache. If it does, rerun this script
# outside the package (WMI starts processes outside it) and pass its output back.
$probeName = "uassetlens-probe-$([guid]::NewGuid().ToString('N'))"
$probe = Join-Path $env:LOCALAPPDATA $probeName
Set-Content $probe ''
$redirected = Test-Path (Join-Path $env:LOCALAPPDATA "Packages\*\LocalCache\Local\$probeName")
Remove-Item $probe
if ($redirected) {
    $log = Join-Path $PSScriptRoot 'install.log'
    $codeFile = Join-Path $PSScriptRoot 'install.exitcode'
    Remove-Item $log, $codeFile -ErrorAction SilentlyContinue
    $flags = (@('Uninstall', 'SkipBuild') | Where-Object { $PSBoundParameters[$_] } | ForEach-Object { "-$_" }) -join ' '
    if ($ExePath) { $flags += " -ExePath '$((Resolve-Path $ExePath).Path)'" }
    $inner = "& '$PSCommandPath' $flags *> '$log'; Set-Content '$codeFile' ([int]`$LASTEXITCODE)"
    $command = "`"$((Get-Process -Id $PID).Path)`" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command `"$inner`""
    Write-Host "Running outside the Claude app's package so the install lands in the real $env:LOCALAPPDATA..."
    $started = Invoke-CimMethod Win32_Process -MethodName Create -Arguments @{ CommandLine = $command; CurrentDirectory = $PSScriptRoot }
    if ($started.ReturnValue -ne 0) { Write-Host "Couldn't start it (WMI returned $($started.ReturnValue))." -ForegroundColor Red; exit 1 }
    Wait-Process -Id $started.ProcessId -ErrorAction SilentlyContinue
    if (Test-Path $log) { Get-Content $log; Remove-Item $log }
    $code = if (Test-Path $codeFile) { [int](Get-Content $codeFile) } else { 1 }
    Remove-Item $codeFile -ErrorAction SilentlyContinue
    exit $code
}

$InstallDir = Join-Path $env:LOCALAPPDATA 'UAssetLens'
$Exe = Join-Path $InstallDir 'UAssetLens.exe'
$ClientConf = Join-Path $env:LOCALAPPDATA 'plastic4\client.conf'
$Extensions = '.uasset;.umap'
$Marker = 'UAssetLens.exe'

$running = Get-Process -Name plastic, gluon, plasticx, mergetool, binmergetool -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Close Plastic first ($(($running | Select-Object -ExpandProperty ProcessName -Unique) -join ', ') is running)." -ForegroundColor Yellow
    Write-Host "Plastic rewrites client.conf when it closes, which would undo this."
    exit 1
}
if (-not (Test-Path $ClientConf)) {
    Write-Host "Plastic's settings file wasn't found at $ClientConf. Open and close Plastic once, then rerun." -ForegroundColor Yellow
    exit 1
}

if ($ExePath -and -not $Uninstall) {
    if (-not (Test-Path $ExePath -PathType Leaf)) {
        Write-Host "$ExePath doesn't exist." -ForegroundColor Red
        exit 1
    }
    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    Copy-Item $ExePath $Exe -Force
    Write-Host "Installed $ExePath as $Exe"
}
elseif (-not $Uninstall -and -not $SkipBuild) {
    $project = Join-Path $PSScriptRoot 'src\UAssetLens.App\UAssetLens.App.csproj'
    Write-Host "Building UAsset Lens into $InstallDir"
    & dotnet publish $project -c Release -o $InstallDir --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed. If a UAsset Lens window is open, close it and rerun." -ForegroundColor Red
        exit 1
    }
}
if (-not $Uninstall -and -not (Test-Path $Exe)) {
    Write-Host "$Exe is missing. Run without -SkipBuild." -ForegroundColor Red
    exit 1
}

# Back up client.conf before touching it.
$backup = "$ClientConf.bak-uassetlens-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Copy-Item $ClientConf $backup
Write-Host "Backed up client.conf to $backup"

$xml = New-Object System.Xml.XmlDocument
$xml.PreserveWhitespace = $true
$xml.Load($ClientConf)

function Remove-OurEntries([System.Xml.XmlNode]$parent) {
    if (-not $parent) { return 0 }
    $removed = 0
    foreach ($node in @($parent.ChildNodes)) {
        if ($node.NodeType -ne 'Element') { continue }
        $tools = @($node.SelectNodes('Tools/string') | ForEach-Object { $_.InnerText })
        if ($tools | Where-Object { $_ -like "*$Marker*" }) {
            $prev = $node.PreviousSibling
            if ($prev -and $prev.NodeType -eq 'Whitespace') { [void]$parent.RemoveChild($prev) }
            [void]$parent.RemoveChild($node)
            $removed++
        }
    }
    return $removed
}

function Add-Entry([System.Xml.XmlNode]$parent, [string]$elementName, [string]$command) {
    $entry = $xml.CreateElement($elementName)
    $ext = $xml.CreateElement('FileExtensions'); $ext.InnerText = $Extensions; [void]$entry.AppendChild($ext)
    $tools = $xml.CreateElement('Tools')
    $string = $xml.CreateElement('string'); $string.InnerText = $command; [void]$tools.AppendChild($string)
    [void]$entry.AppendChild($tools)
    # Plastic uses the first entry whose extensions match, so ours goes first.
    [void]$parent.InsertBefore($entry, $parent.FirstChild)
    [void]$parent.InsertBefore($xml.CreateWhitespace("`r`n    "), $entry)
}

$root = $xml.DocumentElement
$mergeTools = $root.SelectSingleNode('MergeTools')
$diffTools = $root.SelectSingleNode('DiffTools')
$removed = (Remove-OurEntries $mergeTools) + (Remove-OurEntries $diffTools)

if ($Uninstall) {
    $xml.Save($ClientConf)
    Write-Host "Removed $removed UAsset Lens entries from client.conf."
    if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force; Write-Host "Deleted $InstallDir." }
    exit 0
}

foreach ($name in 'MergeTools', 'DiffTools') {
    if (-not $root.SelectSingleNode($name)) {
        [void]$root.AppendChild($xml.CreateElement($name))
    }
}
$mergeTools = $root.SelectSingleNode('MergeTools')
$diffTools = $root.SelectSingleNode('DiffTools')

# Plastic's "source" is the version being merged in (theirs) and "destination" is your workspace (yours).
$mergeCommand = "`"$Exe`" merge --base `"@basefile`" --theirs `"@sourcefile`" --yours `"@destinationfile`" --out `"@output`" " +
    "--base-name `"@basesymbolic`" --theirs-name `"@sourcesymbolic`" --yours-name `"@destinationsymbolic`""
$diffCommand = "`"$Exe`" diff --before `"@sourcefile`" --after `"@destinationfile`" " +
    "--before-name `"@sourcesymbolic`" --after-name `"@destinationsymbolic`""

Add-Entry $mergeTools 'MergeToolData' $mergeCommand
Add-Entry $diffTools 'DiffToolData' $diffCommand
$xml.Save($ClientConf)

Write-Host "UAsset Lens is Plastic's diff and merge tool for $Extensions files." -ForegroundColor Green
Write-Host "Undo with: .\install.ps1 -Uninstall"
