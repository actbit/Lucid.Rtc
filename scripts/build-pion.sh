#!/bin/bash
# Build Pion (Go) native library for local development
# Usage: ./scripts/build-pion.sh [-t <rid>]

set -e

TARGET="linux/amd64"  # Go target format: os/arch

# Parse arguments
while getopts "t:h" opt; do
    case $opt in
        t) TARGET="$OPTARG" ;;
        h)
            echo "Usage: ./scripts/build-pion.sh [-t <go-target>]"
            echo ""
            echo "Targets (Go format):"
            echo "  linux/amd64    (linux-x64)"
            echo "  linux/arm64    (linux-arm64)"
            echo "  linux/arm      (linux-arm)"
            echo "  windows/amd64  (win-x64)"
            echo "  windows/386    (win-x86)"
            echo "  windows/arm64  (win-arm64)"
            echo "  darwin/amd64   (osx-x64)"
            echo "  darwin/arm64   (osx-arm64)"
            exit 0
            ;;
        \?)
            echo "Invalid option: -$OPTARG" >&2
            exit 1
            ;;
    esac
done

# Map Go target to NuGet RID
declare -A GO_TO_RID
GO_TO_RID["linux/amd64"]="linux-x64"
GO_TO_RID["linux/arm64"]="linux-arm64"
GO_TO_RID["linux/arm"]="linux-arm"
GO_TO_RID["windows/amd64"]="win-x64"
GO_TO_RID["windows/386"]="win-x86"
GO_TO_RID["windows/arm64"]="win-arm64"
GO_TO_RID["darwin/amd64"]="osx-x64"
GO_TO_RID["darwin/arm64"]="osx-arm64"

RID="${GO_TO_RID[$TARGET]}"
if [ -z "$RID" ]; then
    echo "Error: Unknown target: $TARGET"
    exit 1
fi

echo -e "\033[36mBuilding Pion for $TARGET ($RID)...\033[0m"

# Parse Go target
GOOS=$(echo $TARGET | cut -d'/' -f1)
GOARCH=$(echo $TARGET | cut -d'/' -f2)

# Set Go environment variables
export GOOS=$GOOS
export GOARCH=$GOARCH

# Build Go shared library
echo -e "\033[33mBuilding Go library...\033[0m"
cd pion
if [[ "$GOOS" == "windows" ]]; then
    go build -buildmode=c-shared -o "temp.dll" .
elif [[ "$GOOS" == "darwin" ]]; then
    go build -buildmode=c-shared -o "temp.dylib" .
else
    go build -buildmode=c-shared -o "temp.so" .
fi
cd ..

# Determine output file names
if [[ "$GOOS" == "windows" ]]; then
    SRC_FILE="pion/temp.dll"
    DST_FILE="dotnet/Lucid.Rtc.Pion/$RID/native/lucid_rtc.dll"
elif [[ "$GOOS" == "darwin" ]]; then
    SRC_FILE="pion/temp.dylib"
    DST_FILE="dotnet/Lucid.Rtc.Pion/$RID/native/liblucid_rtc.dylib"
else
    SRC_FILE="pion/temp.so"
    DST_FILE="dotnet/Lucid.Rtc.Pion/$RID/native/liblucid_rtc.so"
fi

# Copy native library
NATIVE_DIR="dotnet/Lucid.Rtc.Pion/$RID/native"
mkdir -p "$NATIVE_DIR"

echo -e "\033[33mCopying $SRC_FILE to $DST_FILE...\033[0m"
cp "$SRC_FILE" "$DST_FILE"

# Clean up
rm -f pion/temp.*

echo -e "\033[32mDone!\033[0m"
