# Build script for local development
# Usage: ./build.ps1 [-Target win-x64] [-Pack]

param(
    [string]$Target = "x86_64-pc-windows-msvc",
    [switch]$Pack,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

if ($Help) {
    Write-Host "Usage: ./build.ps1 [-Target <rust-target>] [-Pack]"
    Write-Host ""
    Write-Host "Targets:"
    Write-Host "  x86_64-pc-windows-msvc      (win-x64)"
    Write-Host "  i686-pc-windows-msvc        (win-x86)"
    Write-Host "  aarch64-pc-windows-msvc     (win-arm64)"
    Write-Host "  x86_64-unknown-linux-gnu    (linux-x64)"
    Write-Host "  aarch64-unknown-linux-gnu   (linux-arm64)"
    Write-Host "  arm-unknown-linux-gnueabihf (linux-arm)"
    Write-Host "  x86_64-apple-darwin         (osx-x64)"
    Write-Host "  aarch64-apple-darwin        (osx-arm64)"
    exit 0
}

# Map Rust target to NuGet RID
$TargetToRid = @{
    "x86_64-pc-windows-msvc"      = "win-x64"
    "i686-pc-windows-msvc"        = "win-x86"
    "aarch64-pc-windows-msvc"     = "win-arm64"
    "x86_64-unknown-linux-gnu"    = "linux-x64"
    "aarch64-unknown-linux-gnu"   = "linux-arm64"
    "arm-unknown-linux-gnueabihf" = "linux-arm"
    "x86_64-apple-darwin"         = "osx-x64"
    "aarch64-apple-darwin"        = "osx-arm64"
}

$Rid = $TargetToRid[$Target]
if (-not $Rid) {
    Write-Error "Unknown target: $Target"
    exit 1
}

Write-Host "Building for $Target ($Rid)..." -ForegroundColor Cyan

# Build Rust library
Write-Host "Building Rust library..." -ForegroundColor Yellow
cargo build --release -p lucid-rtc-sys --target $Target
if ($LASTEXITCODE -ne 0) {
    Write-Error "Rust build failed"
    exit 1
}

# Copy native library to NuGet structure
$NativeDir = "dotnet/Lucid.Rtc.Native/$Rid/native"
New-Item -ItemType Directory -Force -Path $NativeDir | Out-Null

if ($Target -like "*windows*") {
    $SrcFile = "target/$Target/release/lucid_rtc.dll"
    $DstFile = "$NativeDir/lucid_rtc.dll"
} elseif ($Target -like "*linux*") {
    $SrcFile = "target/$Target/release/liblucid_rtc.so"
    $DstFile = "$NativeDir/liblucid_rtc.so"
} else {
    $SrcFile = "target/$Target/release/liblucid_rtc.dylib"
    $DstFile = "$NativeDir/liblucid_rtc.dylib"
}

Write-Host "Copying $SrcFile to $DstFile..." -ForegroundColor Yellow
Copy-Item $SrcFile $DstFile -Force

# Build .NET
Write-Host "Building .NET..." -ForegroundColor Yellow
dotnet build Lucid.Rtc.slnx -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error ".NET build failed"
    exit 1
}

# Run tests
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test Lucid.Rtc.slnx -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed"
    exit 1
}

# Pack if requested
if ($Pack) {
    Write-Host "Packing NuGet packages..." -ForegroundColor Yellow

    $ArtifactsDir = "artifacts"
    New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

    # Pack Core
    dotnet pack dotnet/Lucid.Rtc.Core/Lucid.Rtc.Core.csproj -c Release -o $ArtifactsDir

    # Pack native package for this platform
    $NativeProj = "dotnet/Lucid.Rtc.Native/Lucid.Rtc.Native.$Rid.csproj"
    if (Test-Path $NativeProj) {
        dotnet pack $NativeProj -c Release -o $ArtifactsDir
    }

    Write-Host "Packages created in $ArtifactsDir/" -ForegroundColor Green
}

Write-Host "Done!" -ForegroundColor Green
