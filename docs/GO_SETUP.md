# Go Environment Setup for Pion Backend

## Prerequisites

To build the Pion (Go) backend, you need:
1. **Go 1.21+** - The Go programming language
2. **GCC/MinGW** - C compiler for CGO (Go's C interop)

## Windows Setup

### Step 1: Install Go

**Option A: Using Chocolatey (Recommended)**
```powershell
# Run in elevated PowerShell
choco install golang -y
```

**Option B: Manual Installation**
1. Download from https://go.dev/dl/
2. Run the installer (go1.21.x.windows-amd64.msi)
3. Add to PATH if not automatic

### Step 2: Install MinGW (GCC for Windows)

**Option A: Using Chocolatey**
```powershell
choco install mingw -y
```

**Option B: Using MSYS2**
```powershell
# Install MSYS2 from https://www.msys2.org/
# Then in MSYS2 terminal:
pacman -S mingw-w64-x86_64-gcc

# Add to PATH: C:\msys64\mingw64\bin
```

### Step 3: Verify Installation

```powershell
# Check Go
go version
# Should output: go version go1.21.x windows/amd64

# Check GCC
gcc --version
# Should output: gcc (x86_64-posix-seh-rev0, Built by MinGW-W64 project) x.x.x
```

### Step 4: Build Pion

```powershell
cd C:\Users\Binary_number\source\repos\Lucid.Rtc
.\scripts\build-pion.ps1 -Target win-x64
```

## Troubleshooting

### "go: command not found"
- Make sure Go is in your PATH
- Restart your terminal/PowerShell after installation

### "gcc: command not found"
- Install MinGW as described above
- Add MinGW's bin folder to PATH

### CGO errors during build
- Make sure GCC is installed and in PATH
- On Windows, MinGW is required (not MSVC)

### "multiple definitions of..." linker errors
- This can happen with older Go versions
- Make sure you're using Go 1.21+

## Build Commands

```powershell
# Build for current platform (win-x64)
.\scripts\build-pion.ps1 -Target win-x64

# Build for all platforms (requires cross-compilers)
.\scripts\build-pion.ps1 -Target win-x86
.\scripts\build-pion.ps1 -Target win-arm64

# View help
.\scripts\build-pion.ps1 -Help
```

## After Successful Build

The compiled DLL will be placed in:
```
dotnet/Lucid.Rtc.Pion/win-x64/native/lucid_rtc.dll
```

You can then build the .NET solution:
```powershell
dotnet build Lucid.Rtc.slnx
```
