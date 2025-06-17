#!/bin/bash
# Build script for Balancy WebView Android native library

# Exit on error
set -e

echo "🔨 Building Balancy WebView for Android..."

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

echo "📂 Working directory: $SCRIPT_DIR"

# Clean up any previous failed builds first
echo "🧹 Pre-build cleanup..."
for ABI in "arm64-v8a" "armeabi-v7a" "x86_64"; do
    BUILD_DIR="build_$ABI"
    if [ -d "$BUILD_DIR" ]; then
        echo "   🗑️ Removing previous build directory: $BUILD_DIR"
        rm -rf "$BUILD_DIR" || echo "   ⚠️ Warning: Could not remove $BUILD_DIR"
    fi
done

# Make the script executable
chmod +x build_android.sh

# Configuration
ANDROID_NDK_HOME="${ANDROID_NDK_HOME:-/Applications/Unity/Hub/Editor/2022.3.25f1/PlaybackEngines/AndroidPlayer/NDK}"
ARCHS=("arm64-v8a" "armeabi-v7a" "x86_64")

echo "📱 Using Android NDK: $ANDROID_NDK_HOME"

# Check if NDK exists
if [ ! -d "$ANDROID_NDK_HOME" ]; then
    echo "❌ Error: Android NDK not found at $ANDROID_NDK_HOME"
    echo "Please set ANDROID_NDK_HOME environment variable or install Unity Android build support"
    exit 1
fi

# Check that all required files exist
echo "🔍 Validating source files..."

if [ ! -f "BalancyWebViewPlugin.java" ]; then
    echo "❌ Error: BalancyWebViewPlugin.java not found"
    exit 1
fi

if [ ! -f "BalancyWebViewJNI.cpp" ]; then
    echo "❌ Error: BalancyWebViewJNI.cpp not found"
    exit 1
fi

if [ ! -f "AndroidManifest.xml" ]; then
    echo "❌ Error: AndroidManifest.xml not found"
    exit 1
fi

if [ ! -f "CMakeLists.txt" ]; then
    echo "❌ Error: CMakeLists.txt not found"
    exit 1
fi

echo "   ✅ All source files found"

# Validate Java syntax (basic check)
echo "🔍 Validating Java syntax..."

if grep -q "class BalancyWebViewPlugin" BalancyWebViewPlugin.java; then
    echo "   ✅ Java class found"
else
    echo "   ❌ Java class missing"
    exit 1
fi

if grep -q "emergencyExitButton" BalancyWebViewPlugin.java; then
    echo "   ✅ Emergency Exit button found in Java"
else
    echo "   ❌ Emergency Exit button missing in Java"
    exit 1
fi

if grep -q "BalancyWebViewAndroid" BalancyWebViewPlugin.java; then
    echo "   ✅ Correct library name found in Java"
else
    echo "   ❌ Library name not updated in Java"
    exit 1
fi

# Check that deprecated methods are removed
if grep -q "setAppCacheEnabled" BalancyWebViewPlugin.java && ! grep -q "// REMOVED - deprecated" BalancyWebViewPlugin.java; then
    echo "   ⚠️  Warning: Deprecated setAppCacheEnabled found - should be removed for modern Android"
else
    echo "   ✅ Deprecated Android methods properly handled"
fi

# Validate JNI syntax
echo "🔍 Validating JNI syntax..."

if grep -q "_balancyInitializeAndroid" BalancyWebViewJNI.cpp; then
    echo "   ✅ JNI initialization function found"
else
    echo "   ❌ JNI initialization function missing"
    exit 1
fi

if grep -q "Java_com_balancy_webview_BalancyWebViewPlugin_nativeOnMessageReceived" BalancyWebViewJNI.cpp; then
    echo "   ✅ JNI callback functions found"
else
    echo "   ❌ JNI callback functions missing"
    exit 1
fi

# Build for each architecture
for ABI in "${ARCHS[@]}"; do
    echo ""
    echo "=== Building for $ABI ==="
    
    # Create build directory
    BUILD_DIR="build_$ABI"
    mkdir -p "$BUILD_DIR"
    cd "$BUILD_DIR"
    
    # Set environment variables for 16KB alignment
    export LDFLAGS="-Wl,-z,max-page-size=16384 -Wl,-z,separate-code"
    # Unset any conflicting environment variables
    unset CMAKE_SYSTEM_VERSION
    unset ANDROID_PLATFORM
    
    # Configure with CMake
    cmake .. \
        -DANDROID=ON \
        -DCMAKE_SYSTEM_NAME=Android \
        -DCMAKE_SYSTEM_VERSION=21 \
        -DCMAKE_ANDROID_NDK="$ANDROID_NDK_HOME" \
        -DANDROID_ABI="$ABI" \
        -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
        -DANDROID_PLATFORM=android-21 \
        -DCMAKE_ANDROID_API=21 \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384 -Wl,-z,separate-code" \
        -DCMAKE_EXE_LINKER_FLAGS="-Wl,-z,max-page-size=16384 -Wl,-z,separate-code" \
        -DANDROID_LD_FLAGS="-Wl,-z,max-page-size=16384 -Wl,-z,separate-code"
    
    # Build
    cmake --build . --config Release
    
    # Check if library was created
    if [ ! -f "libBalancyWebViewAndroid.so" ]; then
        echo "❌ Build failed for $ABI - libBalancyWebViewAndroid.so not created"
        echo "Looking for library files..."
        find . -name "*.so" -type f
        cd ..
        exit 1
    fi
    
    echo "✅ Successfully built libBalancyWebViewAndroid.so for $ABI"
    
    # Copy to architecture-specific directory
    mkdir -p "../$ABI"
    cp "libBalancyWebViewAndroid.so" "../$ABI/"
    
    echo "✅ Copied to $ABI/ directory"
    
    cd ..
done

echo ""
echo "🎉 Android WebView build completed successfully!"
echo ""
echo "📱 Built libraries:"
for ABI in "${ARCHS[@]}"; do
    if [ -f "$ABI/libBalancyWebViewAndroid.so" ]; then
        echo "   ✅ $ABI/libBalancyWebViewAndroid.so"
    else
        echo "   ❌ $ABI/libBalancyWebViewAndroid.so (missing)"
    fi
done

# Clean up build directories to keep Unity project clean
echo ""
echo "🧹 Cleaning up build artifacts..."

# Function to force remove directory with retry
force_remove_dir() {
    local dir="$1"
    local max_attempts=3
    local attempt=1
    
    while [ $attempt -le $max_attempts ] && [ -d "$dir" ]; do
        echo "   🔄 Attempt $attempt to remove $dir"
        rm -rf "$dir" 2>/dev/null || true
        
        # Check if removal was successful
        if [ ! -d "$dir" ]; then
            echo "   ✅ Successfully removed $dir"
            break
        else
            echo "   ⚠️  Failed to remove $dir on attempt $attempt"
            sleep 1
            ((attempt++))
        fi
    done
    
    # Final check
    if [ -d "$dir" ]; then
        echo "   ❌ Failed to remove $dir after $max_attempts attempts"
        echo "   📝 Manual cleanup required: rm -rf $dir"
        return 1
    fi
    
    return 0
}

# Clean up build directories
for ABI in "${ARCHS[@]}"; do
    BUILD_DIR="build_$ABI"
    if [ -d "$BUILD_DIR" ]; then
        force_remove_dir "$BUILD_DIR"
    fi
    
    # Also remove Unity meta files for build directories if they exist
    if [ -f "$BUILD_DIR.meta" ]; then
        rm -f "$BUILD_DIR.meta"
        echo "   ✅ Removed $BUILD_DIR.meta"
    fi
done

# Also clean up any leftover CMake files from the root
echo "🧹 Cleaning up any remaining CMake artifacts..."
if [ -f "CMakeCache.txt" ]; then
    rm -f "CMakeCache.txt"
    echo "   ✅ Removed CMakeCache.txt"
fi

if [ -d "CMakeFiles" ]; then
    force_remove_dir "CMakeFiles"
fi

if [ -f "Makefile" ]; then
    rm -f "Makefile"
    echo "   ✅ Removed Makefile"
fi

if [ -f "cmake_install.cmake" ]; then
    rm -f "cmake_install.cmake"
    echo "   ✅ Removed cmake_install.cmake"
fi

if [ -f "compile_commands.json" ]; then
    rm -f "compile_commands.json"
    echo "   ✅ Removed compile_commands.json"
fi

# Make sure .gitignore exists to prevent future build artifacts
if [ ! -f ".gitignore" ]; then
    echo "# Build artifacts - ignore these directories" > .gitignore
    echo "build_*/" >> .gitignore
    echo "CMakeCache.txt" >> .gitignore
    echo "CMakeFiles/" >> .gitignore
    echo "*.o" >> .gitignore
    echo "*.d" >> .gitignore
    echo ".DS_Store" >> .gitignore
    echo "   ✅ Created .gitignore"
fi

echo ""
echo "🚀 Android WebView Features:"
echo "   • Overlay WebView with transparency support"
echo "   • Emergency Exit button (invisible, 10% x 10%) in top-right corner"
echo "   • Game UI mode - disables text selection, context menus, zoom"
echo "   • Viewport control - configurable position/size as screen percentages"
echo "   • JavaScript bridge - two-way communication Unity ↔ WebView"
echo "   • Event callbacks - load completion, message handling, cache events"
echo "   • Debug logging - configurable logging for development"
echo ""
echo "📲 Integration:"
echo "1. Libraries are ready for Unity Android builds"
echo "2. Java classes will be automatically included by Unity"
echo "3. Use BalancyWebView.Instance.OpenWebView() in your Unity code"
echo "4. Test Emergency Exit by tapping top-right corner (10% x 10% area)"
echo ""
echo "✅ Android WebView plugin is ready for deployment!"
