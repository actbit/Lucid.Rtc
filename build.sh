#!/bin/bash
# Build script for local development
# Usage: ./build.sh [-t <rust-target>] [-p]

set -e

TARGET="x86_64-unknown-linux-gnu"
PACK=false

# Parse arguments
while getopts "t:ph" opt; do
    case $opt in
        t) TARGET="$OPTARG" ;;
        p) PACK=true ;;
        h)
            echo "Usage: ./build.sh [-t <rust-target>] [-p]"
            echo ""
            echo "Targets:"
            echo "  x86_64-pc-windows-msvc      (win-x64)"
            echo "  i686-pc-windows-msvc        (win-x86)"
            echo "  aarch64-pc-windows-msvc     (win-arm64)"
            echo "  x86_64-unknown-linux-gnu    (linux-x64)"
            echo "  aarch64-unknown-linux-gnu   (linux-arm64)"
            echo "  arm-unknown-linux-gnueabihf (linux-arm)"
            echo "  x86_64-apple-darwin         (osx-x64)"
            echo "  aarch64-apple-darwin        (osx-arm64)"
            exit 0
            ;;
        \?)
            echo "Invalid option: -$OPTARG" >&2
            exit 1
            ;;
    esac
done

# Map Rust target to NuGet RID
declare -A TARGET_TO_RID
TARGET_TO_RID["x86_64-pc-windows-msvc"]="win-x64"
TARGET_TO_RID["i686-pc-windows-msvc"]="win-x86"
TARGET_TO_RID["aarch64-pc-windows-msvc"]="win-arm64"
TARGET_TO_RID["x86_64-unknown-linux-gnu"]="linux-x64"
TARGET_TO_RID["aarch64-unknown-linux-gnu"]="linux-arm64"
TARGET_TO_RID["arm-unknown-linux-gnueabihf"]="linux-arm"
TARGET_TO_RID["x86_64-apple-darwin"]="osx-x64"
TARGET_TO_RID["aarch64-apple-darwin"]="osx-arm64"

RID="${TARGET_TO_RID[$TARGET]}"
if [ -z "$RID" ]; then
    echo "Error: Unknown target: $TARGET"
    exit 1
fi

echo -e "\033[36mBuilding for $TARGET ($RID)...\033[0m"

# Build Rust library
echo -e "\033[33mBuilding Rust library...\033[0m"
cargo build --release -p lucid-rtc-sys --target "$TARGET"

# Copy native library to NuGet structure
NATIVE_DIR="dotnet/Lucid.Rtc.Native/$RID/native"
mkdir -p "$NATIVE_DIR"

if [[ "$TARGET" == *"windows"* ]]; then
    SRC_FILE="target/$TARGET/release/lucid_rtc.dll"
    DST_FILE="$NATIVE_DIR/lucid_rtc.dll"
elif [[ "$TARGET" == *"linux"* ]]; then
    SRC_FILE="target/$TARGET/release/liblucid_rtc.so"
    DST_FILE="$NATIVE_DIR/liblucid_rtc.so"
else
    SRC_FILE="target/$TARGET/release/liblucid_rtc.dylib"
    DST_FILE="$NATIVE_DIR/liblucid_rtc.dylib"
fi

echo -e "\033[33mCopying $SRC_FILE to $DST_FILE...\033[0m"
cp "$SRC_FILE" "$DST_FILE"

# Build .NET
echo -e "\033[33mBuilding .NET...\033[0m"
dotnet build Lucid.Rtc.slnx -c Release

# Run tests
echo -e "\033[33mRunning tests...\033[0m"
dotnet test Lucid.Rtc.slnx -c Release --no-build

# Pack if requested
if [ "$PACK" = true ]; then
    echo -e "\033[33mPacking NuGet packages...\033[0m"

    ARTIFACTS_DIR="artifacts"
    mkdir -p "$ARTIFACTS_DIR"

    # Pack Core
    dotnet pack dotnet/Lucid.Rtc.Core/Lucid.Rtc.Core.csproj -c Release -o "$ARTIFACTS_DIR"

    # Pack native package for this platform
    NATIVE_PROJ="dotnet/Lucid.Rtc.Native/Lucid.Rtc.Native.$RID.csproj"
    if [ -f "$NATIVE_PROJ" ]; then
        dotnet pack "$NATIVE_PROJ" -c Release -o "$ARTIFACTS_DIR"
    fi

    echo -e "\033[32mPackages created in $ARTIFACTS_DIR/\033[0m"
fi

echo -e "\033[32mDone!\033[0m"
