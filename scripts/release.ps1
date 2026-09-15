#requires -Version 5.1
<#
.SYNOPSIS
  Builds the portable Herald Helper release zip.

.DESCRIPTION
  Publishes a self-contained win-x64 build (no .NET install required),
  embeds the Playwright Chromium browser beside the app, trims the
  non-Windows driver runtimes and unused browser payloads, copies the
  abilities catalogs into data/, and zips the result into
  artifacts/release/HeraldHelper-<timestamp>.zip.

  Nothing on the target PC needs to be installed: no .NET runtime,
  no Node.js, no Playwright install step.
#>
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [switch]$SkipBrowserInstall,
    [switch]$SkipZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repoRoot "src\HeraldHelper.Desktop\HeraldHelper.Desktop.csproj"
$publishDir = Join-Path $OutputRoot "HeraldHelper"

Write-Host "==> Publishing HeraldHelper.Desktop ($Runtime, self-contained)"
dotnet publish $project -c Release -r $Runtime --self-contained -o $publishDir
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
}

# --- Ship the abilities catalogs ---------------------------------------------
Write-Host "==> Copying abilities catalogs (data\)"
Copy-Item (Join-Path $repoRoot "data") (Join-Path $publishDir "data") -Recurse -Force

# --- Cleanup -----------------------------------------------------------------
Get-ChildItem $publishDir -Recurse -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem $publishDir -Recurse -Filter "*.xml" |
    Where-Object { $_.Name -match '\.deps\.xml|\.runtimeconfig' } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$sizeMB = [math]::Round((Get-ChildItem $publishDir -Recurse -File |
    Measure-Object Length -Sum).Sum / 1MB)
Write-Host "==> Publish folder: $publishDir ($sizeMB MB)"

if (-not $SkipZip) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmm"
    $zipPath = Join-Path $OutputRoot "HeraldHelper-$stamp.zip"
    Write-Host "==> Creating $zipPath"
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Write-Host ("==> Zip size: {0} MB" -f [math]::Round((Get-Item $zipPath).Length / 1MB, 1))
}
