#!/bin/bash

# Quick Android SDK finder
echo "🔍 Looking for Android SDK..."

# Check common locations
if [ -d "$HOME/Library/Android/sdk" ]; then
    echo "✅ Found Android SDK at: $HOME/Library/Android/sdk"
    ANDROID_SDK="$HOME/Library/Android/sdk"
elif [ -d "$HOME/Android/Sdk" ]; then
    echo "✅ Found Android SDK at: $HOME/Android/Sdk"  
    ANDROID_SDK="$HOME/Android/Sdk"
else
    echo "❌ Android SDK not found in common locations"
    echo "Please check:"
    echo "  ls -la ~/Library/Android/"
    echo "  ls -la ~/Android/"
    exit 1
fi

# Check for platforms
if [ -d "$ANDROID_SDK/platforms" ]; then
    echo "📋 Available Android APIs:"
    ls -1 "$ANDROID_SDK/platforms" | grep "android-" | sort -V | tail -5
    
    # Find highest API
    HIGHEST_API=$(ls -1 "$ANDROID_SDK/platforms" | grep "android-" | sed 's/android-//' | sort -n | tail -1)
    echo "🎯 Highest API available: $HIGHEST_API"
    
    if [ -f "$ANDROID_SDK/platforms/android-35/android.jar" ]; then
        echo "✅ Android API 35 found!"
        echo "🚀 Ready to build:"
        echo "export ANDROID_HOME=\"$ANDROID_SDK\""
    elif [ -n "$HIGHEST_API" ]; then
        echo "⚠️  Android API 35 not found, but API $HIGHEST_API is available"
        echo "💡 Solution options:"
        echo "1. Install API 35: Open Android Studio > SDK Manager > Install Android API 35"
        echo "2. Or modify build.gradle: change compileSdkVersion from 35 to $HIGHEST_API"
        echo ""
        echo "For option 2, run:"
        echo "export ANDROID_HOME=\"$ANDROID_SDK\""
        echo "# Then edit build.gradle file"
    fi
else
    echo "❌ Platforms directory not found in SDK"
fi
