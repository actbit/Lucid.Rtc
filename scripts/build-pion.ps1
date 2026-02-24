# Build Pion (Go) native library for local development
# Usage: ./scripts/build-pion.ps1 [-Target win-x64]

param(
    [string]$Target = "win-x64",
    [switch]$Help
)

$ErrorActionPreference = "Stop"

if ($Help) {
    Write-Host "Usage: ./scripts/build-pion.ps1 [-Target <rid>]"
    Write-Host ""
    Write-Host "Targets (NuGet RID):"
    Write-Host "  win-x64"
    Write-Host "  win-x86"
    Write-Host "  win-arm64"
    Write-Host "  linux-x64"
    Write-Host "  linux-arm64"
    Write-Host "  linux-arm"
    Write-Host "  osx-x64"
    Write-Host "  osx-arm64"
    exit 0
}

# Map RID to Go target
$RidToGo = @{
    "win-x64"     = "windows/amd64"
    "win-x86"     = "windows/386"
    "win-arm64"   = "windows/arm64"
    "linux-x64"   = "linux/amd64"
    "linux-arm64" = "linux/arm64"
    "linux-arm"   = "linux/arm"
    "osx-x64"     = "darwin/amd64"
    "osx-arm64"   = "darwin/arm64"
}

$GoTarget = $RidToGo[$Target]
if (-not $GoTarget) {
    Write-Error "Unknown target: $Target"
    exit 1
}

Write-Host "Building Pion for $Target ($GoTarget)..." -ForegroundColor Cyan

# Set Go environment variables
$env:GOOS = ($GoTarget -split "/")[0]
$env:GOARCH = ($GoTarget -split "/")[1]
if ($env:GOOS -eq "windows") {
    $env:CC = "gcc"
    $env:CXX = "g++"
}

# Build Go shared library
Write-Host "Building Go library..." -ForegroundColor Yellow
go build -buildmode=c-shared -o "pion/temp.dll" ./pion/
if ($LASTEXITCODE -ne 0) {
    Write-Error "Go build failed"
    exit 1
}

# Determine output file names
if ($Target -like "win-*") {
    $SrcFile = "pion/temp.dll"
    $DstFile = "dotnet/Lucid.Rtc.Pion/$Target/native/lucid_rtc.dll"
} elseif ($Target -like "linux-*") {
    $SrcFile = "pion/temp.so"
    $DstFile = "dotnet/Lucid.Rtc.Pion/$Target/native/liblucid_rtc.so"
} else {
    $SrcFile = "pion/temp.dylib"
    $DstFile = "dotnet/Lucid.Rtc.Pion/$Target/native/liblucid_rtc.dylib"
}

# Copy native library
$NativeDir = "dotnet/Lucid.Rtc.Pion/$Target/native"
New-Item -ItemType Directory -Force -Path $NativeDir | Out-Null

Write-Host "Copying $SrcFile to $DstFile..." -ForegroundColor Yellow
Copy-Item $SrcFile $DstFile -Force

# Clean up
Remove-Item "pion/temp.*" -Force -ErrorAction SilentlyContinue

Write-Host "Done!" -ForegroundColor Green
