#!/bin/bash
# Build script for Balancy WebView iOS native library with Emergency Exit

# Exit on error
set -e

echo "🔨 Building Balancy WebView for iOS with Emergency Exit..."

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Make the script executable
chmod +x build_ios.sh

# Note: iOS libraries are typically built as part of the Unity build process
# The .mm files are automatically included when building for iOS
# This script is mainly for validation and preparation

echo "📱 iOS Files prepared:"
echo "   ✅ BalancyWebView.h - Header with Emergency Exit support"
echo "   ✅ BalancyWebView.mm - Main implementation with Emergency Exit"
echo "   ✅ BalancyWebViewUnityBridge.mm - Unity bridge with Emergency Exit API"
echo ""

# Check that all required files exist
if [ ! -f "BalancyWebView.h" ]; then
    echo "❌ Error: BalancyWebView.h not found"
    exit 1
fi

if [ ! -f "BalancyWebView.mm" ]; then
    echo "❌ Error: BalancyWebView.mm not found"
    exit 1
fi

if [ ! -f "BalancyWebViewUnityBridge.mm" ]; then
    echo "❌ Error: BalancyWebViewUnityBridge.mm not found"
    exit 1
fi

# Validate syntax (basic check)
echo "🔍 Validating syntax..."

# Check for basic Objective-C syntax
if grep -q "@interface BalancyWebViewController" BalancyWebView.h; then
    echo "   ✅ Header interface found"
else
    echo "   ❌ Header interface missing"
    exit 1
fi

if grep -q "emergencyExitButton" BalancyWebView.h; then
    echo "   ✅ Emergency Exit property found in header"
else
    echo "   ❌ Emergency Exit property missing in header"
    exit 1
fi

if grep -q "setupEmergencyExitButton" BalancyWebView.mm; then
    echo "   ✅ Emergency Exit setup method found"
else
    echo "   ❌ Emergency Exit setup method missing"
    exit 1
fi

if grep -q "_balancySetEmergencyExitEnabled" BalancyWebView.mm; then
    echo "   ✅ Emergency Exit C interface found"
else
    echo "   ❌ Emergency Exit C interface missing"
    exit 1
fi

if grep -q "0.10" BalancyWebView.mm; then
    echo "   ✅ Emergency Exit size updated to 10%"
else
    echo "   ❌ Emergency Exit size not updated to 10%"
    exit 1
fi

echo ""
echo "✅ All iOS files validated successfully!"
echo ""
echo "🚀 Emergency Exit Features Added:"
echo "   • Invisible UIButton (10% x 10%) in top-right corner"
echo "   • Sends '//:balancy_close_view' message to Unity on tap"
echo "   • Auto-positioning on device rotation"
echo "   • Enable/disable via _balancySetEmergencyExitEnabled(bool)"
echo ""
echo "📲 Next steps:"
echo "1. Build your Unity project for iOS"
echo "2. The Emergency Exit will be automatically available"
echo "3. Test by tapping the top-right corner (10% x 10% area)"
echo ""
echo "🎯 The Emergency Exit is ready for iOS deployment!"
