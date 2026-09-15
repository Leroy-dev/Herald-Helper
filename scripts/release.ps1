#requires -Version 5.1
<#
.SYNOPSIS
  Builds the portable Herald Helper release archive.

.DESCRIPTION
  Publishes a self-contained, single-file, compressed win-x64 build
  (no .NET install required), embeds the Playwright Chromium browser
  beside the app, trims the non-Windows driver runtimes, unused browser
  payloads and non-English Chromium/.NET locale resources, copies the
  abilities catalogs into data/, and packages the result into
  artifacts/release/HeraldHelper-<timestamp>.exe (self-extracting 7z)
  plus a plain .7z; falls back to .zip when 7-Zip is unavailable.

  Nothing on the target PC needs to be installed: no .NET runtime,
  no Node.js, no Playwright install step, no archiver for the .exe.
#>
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [switch]$SkipBrowserInstall,
    [switch]$SkipZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $scriptDir "..\artifacts\release"
}
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
$project = Join-Path $repoRoot "src\HeraldHelper.Desktop\HeraldHelper.Desktop.csproj"
$publishDir = Join-Path $OutputRoot "HeraldHelper"

# Rebuild from scratch: incremental publishes leave stale payloads behind
# (e.g. data\data from a previous catalog copy).
if (Test-Path $publishDir) {
    Write-Host "==> Clearing previous publish folder"
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "==> Publishing HeraldHelper.Desktop ($Runtime, self-contained single-file)"
dotnet publish $project -c Release -r $Runtime --self-contained `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:SatelliteResourceLanguages=en `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# --- Trim the Playwright driver to Windows only -----------------------------
$nodeDir = Join-Path $publishDir ".playwright\node"
if (Test-Path $nodeDir) {
    Get-ChildItem $nodeDir -Directory |
        Where-Object { $_.Name -notlike "win32*" } |
        ForEach-Object {
            Write-Host "==> Removing non-Windows node runtime $($_.Name)"
            Remove-Item $_.FullName -Recurse -Force
        }
}
if (-not (Test-Path (Join-Path $nodeDir "win32_x64\node.exe"))) {
    throw ".playwright\node\win32_x64\node.exe missing from publish output"
}

# --- Install bundled Chromium (PLAYWRIGHT_BROWSERS_PATH=0) -------------------
$browserDir = Join-Path $publishDir ".playwright\package\.local-browsers"
if (-not $SkipBrowserInstall) {
    $playwrightPs1 = Join-Path $publishDir "playwright.ps1"
    if (-not (Test-Path $playwrightPs1)) {
        throw "playwright.ps1 missing from publish output: $playwrightPs1"
    }

    Write-Host "==> Installing Chromium into the package (browsers path = app-local)"
    $env:PLAYWRIGHT_BROWSERS_PATH = "0"
    try {
        & powershell -ExecutionPolicy Bypass -File $playwrightPs1 install chromium
        if ($LASTEXITCODE -ne 0) { throw "playwright install chromium failed ($LASTEXITCODE)" }
    }
    finally {
        Remove-Item Env:PLAYWRIGHT_BROWSERS_PATH -ErrorAction SilentlyContinue
    }

    # Only the headed Chromium is used (shard login); drop the rest.
    foreach ($name in @("chromium_headless_shell-*", "ffmpeg-*", "winldd-*", ".links")) {
        Get-ChildItem $browserDir -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like $name } |
            ForEach-Object {
                Write-Host "==> Removing unused browser payload $($_.Name)"
                Remove-Item $_.FullName -Recurse -Force
            }
    }

    # The browser UI is never localized (driven programmatically) - keep en-US only.
    $localesDir = Get-ChildItem $browserDir -Recurse -Directory -Filter "locales" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($localesDir) {
        Get-ChildItem $localesDir.FullName -Filter "*.pak" |
            Where-Object { $_.Name -ne "en-US.pak" } |
            ForEach-Object {
                Write-Host "==> Removing Chromium locale $($_.Name)"
                Remove-Item $_.FullName -Force
            }
    }
}

# --- Optional bundled Tesseract ----------------------------------------------
# Stage a slim Tesseract (tesseract.exe + its DLLs + tessdata\eng.traineddata)
# under tools\tesseract\ in the repo to ship it; the adaptive OCR picks it up
# automatically via tools\tesseract\tesseract.exe next to the app. Absent is
# fine: the engine is skipped and Windows OCR covers the generic path.
$tesseractDir = Join-Path $repoRoot "tools\tesseract"
if (Test-Path (Join-Path $tesseractDir "tesseract.exe")) {
    Write-Host "==> Bundling tools\tesseract"
    Copy-Item $tesseractDir (Join-Path $publishDir "tools\tesseract") -Recurse -Force
}

# --- Ship the abilities catalogs ---------------------------------------------
Write-Host "==> Copying abilities catalogs (data\)"
Copy-Item (Join-Path $repoRoot "data") (Join-Path $publishDir "data") -Recurse -Force

# --- Cleanup -----------------------------------------------------------------
Get-ChildItem $publishDir -Recurse -Filter "*.pdb" | Remove-Item -Force
# SatelliteResourceLanguages=en suppresses satellite contents but empty
# culture folders are still created.
Get-ChildItem $publishDir -Directory |
    Where-Object { (Get-ChildItem $_.FullName -Recurse -File).Count -eq 0 } |
    Remove-Item -Recurse -Force
Get-ChildItem $publishDir -Recurse -Filter "*.xml" |
    Where-Object { $_.Name -match '\.deps\.xml|\.runtimeconfig' } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$sizeMB = [math]::Round((Get-ChildItem $publishDir -Recurse -File |
    Measure-Object Length -Sum).Sum / 1MB)
Write-Host "==> Publish folder: $publishDir ($sizeMB MB)"

if (-not $SkipZip) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmm"
    $sevenZip = Get-Command 7z -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source
    if (-not $sevenZip -and (Test-Path "C:\Program Files\7-Zip\7z.exe")) {
        $sevenZip = "C:\Program Files\7-Zip\7z.exe"
    }

    if ($sevenZip) {
        # Self-extracting .exe: LZMA2 compression (~40% smaller than zip) and the
        # target PC needs nothing installed to unpack it.
        $sfx = "C:\Program Files\7-Zip\7z.sfx"
        $archivePath = Join-Path $OutputRoot "HeraldHelper-$stamp.7z"
        Write-Host "==> Creating $archivePath"
        & $sevenZip a -t7z -mx=9 -m0=lzma2 $archivePath (Join-Path $publishDir "*") | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "7z failed" }
        if (Test-Path $sfx) {
            $sfxPath = Join-Path $OutputRoot "HeraldHelper-$stamp.exe"
            Write-Host "==> Creating self-extracting $sfxPath"
            & $sevenZip a -t7z -mx=9 -m0=lzma2 "-sfx$sfx" $sfxPath (Join-Path $publishDir "*") | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "7z sfx failed" }
            Write-Host ("==> SFX size: {0} MB" -f [math]::Round((Get-Item $sfxPath).Length / 1MB, 1))
        }
        Write-Host ("==> 7z size: {0} MB" -f [math]::Round((Get-Item $archivePath).Length / 1MB, 1))
    }
    else {
        $zipPath = Join-Path $OutputRoot "HeraldHelper-$stamp.zip"
        Write-Host "==> Creating $zipPath (7-Zip not found - falling back to zip)"
        Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
        Write-Host ("==> Zip size: {0} MB" -f [math]::Round((Get-Item $zipPath).Length / 1MB, 1))
    }
}
