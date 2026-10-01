# Builds the portable, obfuscated single-file release.
#
#   .\build-release.ps1              -> _release\ + the zip + a refreshed Download\
#   .\build-release.ps1 -Publish     -> ...and commits and pushes Download\ to GitHub
#   .\build-release.ps1 -SkipObfuscate
#
# The obfuscator has to run on the compiled assembly BEFORE the single-file bundler packs
# it, so the order is: build -> obfuscate -> drop the obfuscated dll into obj -> publish
# --no-build (the bundler picks its input up from obj).

param(
    [switch]$SkipObfuscate,
    # Commit and push Download\ once the build has passed its own verification. Off by default:
    # a build script that pushes without being asked will eventually push something unintended.
    [switch]$Publish,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "RageLightEditor\RageLightEditor.csproj"
$rel = Join-Path $root "_release"
$app = Join-Path $rel "app"
$obf = Join-Path $rel "obf"
$out = Join-Path $rel "RAGE Tools"
$tfm = "net8.0-windows"

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

Step "1/5 build"
# build for the same RID the publish uses, or publish --no-build can't find its assets.
# --no-incremental is not optional: step 3 plants the OBFUSCATED assembly in obj as the
# bundler's input, so a later run that MSBuild considers up to date would copy that
# already-obfuscated dll back into bin and obfuscate it a second time. Names are mangled by
# then, no SkipType can match, and every System.Text.Json type writes {} - which surfaces as
# the MLO Creator project seqtest failing on the packaged exe and nowhere else.
dotnet build $proj -c $Configuration -r win-x64 --self-contained true -v m --no-incremental
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$binDll = Join-Path $root "RageLightEditor\bin\$Configuration\$tfm\win-x64\RageLightEditor.dll"
$objDll = Join-Path $root "RageLightEditor\obj\$Configuration\$tfm\win-x64\RageLightEditor.dll"
if (-not (Test-Path $binDll)) { throw "built assembly not found at $binDll" }

if (-not $SkipObfuscate) {
    Step "2/5 obfuscate"
    if (Test-Path $app) { Remove-Item $app -Recurse -Force }
    New-Item -ItemType Directory -Force $app | Out-Null
    Copy-Item $binDll (Join-Path $app "RageLightEditor.dll") -Force
    # Obfuscar needs the referenced assemblies beside its input to resolve types
    Get-ChildItem (Split-Path $binDll) -Filter "*.dll" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $app $_.Name) -Force
    }
    if (Test-Path $obf) { Remove-Item $obf -Recurse -Force }
    & obfuscar.console (Join-Path $root "obfuscar.xml")
    if ($LASTEXITCODE -ne 0) { throw "obfuscar failed" }

    Step "3/5 swap the obfuscated assembly into obj (the bundler's input)"
    Copy-Item (Join-Path $obf "RageLightEditor.dll") $objDll -Force
} else {
    Step "2-3/5 obfuscation skipped"
}

Step "4/5 publish self-contained single file"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $proj -c $Configuration -r win-x64 --self-contained true --no-build `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:GenerateDocumentationFile=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# one friendly exe, no loose build junk. Keep the data files the core needs beside it:
# strings.txt (extra Jenkins names) and ShadersGen9Conversion.xml (enhanced-edition resources).
Get-ChildItem $out -Include *.pdb -Recurse -File | Remove-Item -Force -ErrorAction SilentlyContinue
$exe = Join-Path $out "RageLightEditor.exe"
$friendly = Join-Path $out "RAGE Tools.exe"
if (Test-Path $exe) { Move-Item $exe $friendly -Force }
foreach ($f in @("README.txt", "CREDITS.txt")) {
    $src = Join-Path $rel $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $out $f) -Force }
}
# the FiveM plugin ships beside the exe too, so it can be copied into FiveM's plugins folder by hand
$asi = Join-Path $root "RageLightEditor/Assets/fivem/RageToolsLive.asi"
if (Test-Path $asi) { Copy-Item $asi (Join-Path $out "RageToolsLive.asi") -Force }

Step "5/5 verify + zip"
$env:RLE_SHADERCACHE_DIR = Join-Path $out "shadercache"
# A WinExe launched with "& $exe args" does NOT block, so this used to check $LASTEXITCODE
# from whatever ran before it - the verification wasn't verifying anything. Piping the output
# forces PowerShell to read the process to completion.
$selftest = & $friendly --selftest 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $selftest -notmatch "SELF-TEST PASSED") {
    Write-Host $selftest
    throw "packaged exe failed --selftest"
}
Write-Host "  --selftest passed"

# ...and the SEQUENCE tests too, on the packaged exe. Only --selftest ran here, and a bug that
# exists ONLY in the packaged build (a renamed field breaking Marshal.OffsetOf, which poisoned
# the audio engine and crashed the tool on particles and on close) sailed straight through,
# because the checks that would have caught it live in --seqtest and --seqtest was never run
# against this exe. It needs the game to be present; when it is not, say so rather than
# pretending the gate ran.
$gta = "E:\SteamLibrary\steamapps\common\Grand Theft Auto V"
if (Test-Path $gta) {
    $seq = & $friendly --gta $gta --seqtest 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $seq -notmatch "SEQTEST PASSED") {
        Write-Host ($seq -split "`n" | Select-String -Pattern "FAIL|SEQTEST" | Select-Object -First 20)
        throw "packaged exe failed --seqtest"
    }
    Write-Host "  --seqtest passed"
} else {
    Write-Host "  --seqtest SKIPPED - no GTA V at $gta" -ForegroundColor Yellow
}
Remove-Item Env:RLE_SHADERCACHE_DIR -ErrorAction SilentlyContinue
$shipped = @(Get-ChildItem (Join-Path $out "shadercache") -Filter *.cso -ErrorAction SilentlyContinue).Count
Write-Host "  $shipped compiled shaders ship with the build (no compile on first launch)"

# The verification run above starts the packaged exe, which writes its runtime files beside itself.
# settings.json holds the GTA V install path, so on a machine where the release folder has been
# used once it would ship someone's private path - and a stranger's shortcuts and panel widths
# besides. crash.log and session.log are this machine's, not the release's. Remove them all after
# verifying and before zipping; a fresh install should start with nothing but what it was given.
foreach ($junk in @("settings.json", "crash.log", "session.log", "imgui.ini", "rage_selection.json", "light_presets.json")) {
    Remove-Item (Join-Path $out $junk) -Force -ErrorAction SilentlyContinue
}

$buildNumber = (git -C $root rev-list --count HEAD 2>$null)
if (-not $buildNumber) { $buildNumber = "0" }
$versionName = "RAGE_Tools_Portable_v" + $buildNumber.Trim() + "_" + (Get-Date -Format 'yyyy-MM-dd')
$zip = Join-Path $root "RAGE_Tools_Portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
$zipTries = 0
while ($true) {
    try { Compress-Archive -Path $out -DestinationPath $zip -CompressionLevel Optimal -ErrorAction Stop; break }
    catch {
        $zipTries++
        if ($zipTries -ge 6) { throw }
        Write-Host "  zip: $($_.Exception.Message.Trim()) - retrying in 5 s" -ForegroundColor Yellow
        Start-Sleep -Seconds 5
        if (Test-Path $zip) { Remove-Item $zip -Force }
    }
}

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "`nDone: $zip ($mb MB)" -ForegroundColor Green

Step "6/6 refresh Download\ (the copy the repo hands out)"
# Download\ is TRACKED, unlike _release\. Anyone who lands on the repo can grab a runnable
# build from here without installing a toolchain, and it is refreshed by the same command that
# produces the zip - so it can never quietly fall behind the source next to it.
#
# These two files are ~140 MB together and git keeps every version of them forever, so the
# repo grows by that much per release and none of it can be reclaimed without rewriting
# history. Drop the exe line below if that ever matters more than the convenience: the zip
# contains the same exe plus the data files.
$dl = Join-Path $root "Download"
New-Item -ItemType Directory -Force $dl | Out-Null
# WS-V39: the same scrub HERE. Download\ is tracked, and a run of the packaged exe from that
# folder leaves its runtime files behind - so without this a release could commit somebody's
# GTA path, panel layout or light presets into the repo.
foreach ($junk in @("settings.json", "crash.log", "session.log", "imgui.ini", "rage_selection.json", "light_presets.json")) {
    Remove-Item (Join-Path $dl $junk) -Force -ErrorAction SilentlyContinue
}
Copy-Item $friendly (Join-Path $dl "RAGE Tools.exe") -Force
Copy-Item $zip (Join-Path $dl "RAGE_Tools_Portable.zip") -Force

# ...and the copy in the PROJECT FOLDER, which is the one actually double-clicked. Refreshing
# _release\ and Download\ but not this one left a stale exe at the top of the folder for a whole
# release cycle - so a fix was tested, shipped and reported against a build nobody was running.
# If it is locked, the running instance IS the old build: say so rather than carrying on.
$rootExe = Join-Path $root "RAGE Tools.exe"
try {
    Copy-Item $friendly $rootExe -Force -ErrorAction Stop
    Write-Host "  project-folder exe refreshed" -ForegroundColor Green
} catch {
    $script:rootExeStale = $true
    Write-Host "  could not replace $rootExe - RAGE Tools is running from it; it keeps the OLD build until you close it and copy Download\RAGE Tools.exe over it" -ForegroundColor Yellow
}

# Down to exactly these three. Running the exe from here - to check the published copy works -
# leaves its settings and the data files it writes out on first run lying beside it, and those
# would otherwise be committed as if they were part of the release.
if (Test-Path $asi) { Copy-Item $asi (Join-Path $dl "RageToolsLive.asi") -Force }
$keep = @("RAGE Tools.exe", "RAGE_Tools_Portable.zip", "BUILD.txt", "RageToolsLive.asi")
Get-ChildItem $dl -File | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Force

# a build stamp, so the published copy can be told apart from the one before it at a glance
$stamp = @(
    "RAGE Tools - published build",
    "",
    "  built     : $(Get-Date -Format 'yyyy-MM-dd HH:mm')",
    "  commit    : $(git -C $root rev-parse --short HEAD 2>$null)",
    "  exe       : $([math]::Round((Get-Item $friendly).Length / 1MB, 1)) MB",
    "  portable  : $mb MB",
    "  version   : V$([int]((Get-Content (Join-Path $root 'release-version.txt') -Raw -ErrorAction SilentlyContinue) -replace '\D','') + 1)",
    "",
    "RAGE Tools.exe runs on its own. The portable zip is the same exe plus the",
    "two data files the loader uses for hash names and enhanced-edition resources -",
    "prefer it unless you only want the binary.",
    "",
    "Nothing from GTA V ships here. The keys are derived from your own gta5.exe at",
    "runtime and the game assets are read from your own install."
) -join "`r`n"
Set-Content (Join-Path $dl "BUILD.txt") $stamp -Encoding utf8

Write-Host "  Download\ refreshed" -ForegroundColor Green
if ($script:rootExeStale) { Write-Host "  REMINDER: the project-folder exe is still the old build (it was locked)" -ForegroundColor Yellow }

Step "7/7 keep this version (Versions\ and Google Drive)"
# Every release gets its own numbered folder - V1, V2, ... - holding the portable zip, a zip of the
# source code as built, and BUILD.txt. Locally under Versions\ (ignored by git) and, when Google
# Drive for desktop syncs a folder on this PC, under "RAGE Tools" in Google Drive. The number lives
# in release-version.txt (tracked) so the count carries on from any clone.
$verFile = Join-Path $root "release-version.txt"
$verNum = 0
if (Test-Path $verFile) { [int]::TryParse((Get-Content $verFile -Raw).Trim(), [ref]$verNum) | Out-Null }
$verNum += 1
Set-Content $verFile ([string]$verNum) -Encoding ascii
$verTag = "V$verNum"
$verDir = Join-Path (Join-Path $root "Versions") $verTag
New-Item -ItemType Directory -Force $verDir | Out-Null
Copy-Item $zip (Join-Path $verDir "RAGE_Tools_Portable_$verTag.zip") -Force

$srcStage = Join-Path $env:TEMP "rage_tools_source_stage"
if (Test-Path $srcStage) { Remove-Item $srcStage -Recurse -Force }
New-Item -ItemType Directory -Force $srcStage | Out-Null
$tracked = git -C $root ls-files | Where-Object { $_ -notlike "Download/*" -and $_ -notlike "RAGE Tools/*" -and $_ -notmatch '[.](zip|exe)$' }
$copied = 0
foreach ($f in $tracked) {
    $from = Join-Path $root $f
    if (-not (Test-Path $from -PathType Leaf)) { continue }
    $to = Join-Path $srcStage $f
    New-Item -ItemType Directory -Force (Split-Path $to -Parent) | Out-Null
    Copy-Item $from $to -Force
    $copied++
}
$srcZip = Join-Path $verDir "RAGE_Tools_Source_$verTag.zip"
if (Test-Path $srcZip) { Remove-Item $srcZip -Force }
Compress-Archive -Path (Join-Path $srcStage "*") -DestinationPath $srcZip -CompressionLevel Optimal
Remove-Item $srcStage -Recurse -Force -ErrorAction SilentlyContinue

$verNote = @(
    "RAGE Tools - $verTag",
    "",
    "  built     : $(Get-Date -Format 'yyyy-MM-dd HH:mm')",
    "  commit    : $(git -C $root rev-parse --short HEAD 2>$null)",
    "  exe       : $([math]::Round((Get-Item $friendly).Length / 1MB, 1)) MB",
    "  portable  : $mb MB",
    "",
    "Files of this version",
    "  RAGE_Tools_Portable_$verTag.zip   the tool, ready to run (exe + data files + RageToolsLive.asi)",
    "  RAGE_Tools_Source_$verTag.zip     the source code as built ($copied files)"
) -join "`r`n"
Set-Content (Join-Path $verDir "BUILD.txt") $verNote -Encoding utf8
Write-Host "  kept as Versions\$verTag (portable + source + BUILD.txt)" -ForegroundColor Green

$driveRoot = $env:RAGE_TOOLS_DRIVE
$driveHint = Join-Path $root "release-drive.txt"
if (-not $driveRoot -and (Test-Path $driveHint)) { $driveRoot = (Get-Content $driveHint -Raw).Trim() }
if (-not $driveRoot) {
    $candidates = @((Join-Path $env:USERPROFILE "My Drive"), (Join-Path $env:USERPROFILE "Google Drive"))
    foreach ($letter in [char[]]"DEFGHIJKLMNOPQRSTUVWXYZ") { $candidates += ($letter + ":\My Drive") }
    foreach ($c in $candidates) { if (Test-Path $c) { $driveRoot = $c; break } }
}
if ($driveRoot -and (Test-Path $driveRoot)) {
    $driveDir = Join-Path (Join-Path $driveRoot "RAGE Tools") $verTag
    New-Item -ItemType Directory -Force $driveDir | Out-Null
    Get-ChildItem $verDir -File | ForEach-Object { Copy-Item $_.FullName (Join-Path $driveDir $_.Name) -Force }
    Set-Content (Join-Path (Join-Path $driveRoot "RAGE Tools") "LATEST.txt") $verTag -Encoding utf8
    Write-Host "  copied to Google Drive: $driveDir" -ForegroundColor Green
} else {
    Write-Host "  Google Drive folder not found - install Google Drive for desktop, or write its folder path into release-drive.txt" -ForegroundColor Yellow
}

if ($Publish) {
    Step "publish to GitHub"
    git -C $root add -- Download
    # --quiet: nothing to commit is a normal outcome when the build didn't change
    git -C $root commit -m "Publish build $(Get-Date -Format 'yyyy-MM-dd HH:mm')" --quiet
    if ($LASTEXITCODE -eq 0) {
        git -C $root push origin HEAD
        if ($LASTEXITCODE -ne 0) { throw "push failed - Download\ is committed but NOT on GitHub" }
        Write-Host "  pushed" -ForegroundColor Green
    } else {
        Write-Host "  nothing changed, nothing to push"
    }
}
