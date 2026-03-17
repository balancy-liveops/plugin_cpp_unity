#!/bin/bash
# Build script for Balancy WebView macOS native library with viewport fixes

# Exit on error
set -e

echo "🔨 Building Balancy WebView with VIEWPORT FIXES..."

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Define output path
OUTPUT_FILE="libBalancyWebViewMac.dylib"

# Remove old library if it exists
if [ -f "$OUTPUT_FILE" ]; then
    echo "🗑️  Removing old library..."
    rm "$OUTPUT_FILE"
fi

# Build for both Intel and Apple Silicon
echo "⚙️  Compiling for Intel and Apple Silicon..."
xcrun clang++ -std=c++11 -dynamiclib -framework Cocoa -framework WebKit -framework QuartzCore -framework CoreGraphics \
    -arch x86_64 -arch arm64 \
    -Wl,-undefined,dynamic_lookup \
    -o "$OUTPUT_FILE" BalancyWebviewMac.mm \
    -install_name @rpath/libBalancyWebViewMac.dylib

# Check if file was created
if [ -f "$OUTPUT_FILE" ]; then
    echo "✅ Build successful: $OUTPUT_FILE"
    echo ""
    echo "🎯 VIEWPORT FIXES APPLIED:"
    echo "   • Proper WebView scaling and sizing"
    echo "   • CSS injection for viewport meta tag"
    echo "   • Fixed coordinate system transformations"
    echo "   • Proper content scaling in render context"
    echo ""
    echo "🚀 Next steps:"
    echo "1. Restart Unity to reload the .dylib"
    echo "2. Test - the website should now render properly!"
    echo ""
    echo "📱 Expected improvements:"
    echo "   - Correct website layout and sizing"
    echo "   - No more ruined/corrupted content"
    echo "   - Proper viewport dimensions (1024x768)"
else
    echo "❌ Error: Build failed"
    exit 1
fi

echo "🌐 The website should now display correctly!"
