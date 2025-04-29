#!/bin/bash
# Build script for Balancy WebView macOS native library

# Exit on error
set -e

echo "Building Balancy WebView native library for macOS..."

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Define output path
OUTPUT_FILE="libBalancyWebViewMac.dylib"

# Build for both Intel and Apple Silicon
echo "Compiling for Intel and Apple Silicon..."
clang++ -std=c++11 -dynamiclib -framework Cocoa -framework WebKit \
    -arch x86_64 -arch arm64 \
    -o "$OUTPUT_FILE" BalancyWebviewMac.mm \
    -install_name @rpath/libBalancyWebViewMac.dylib

# Check if file was created
if [ -f "$OUTPUT_FILE" ]; then
    echo "Build successful: $OUTPUT_FILE"
    # Show file info
    file "$OUTPUT_FILE"
    otool -L "$OUTPUT_FILE"
else
    echo "Error: Build failed"
    exit 1
fi

echo "Done!"
