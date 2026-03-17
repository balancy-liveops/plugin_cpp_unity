#!/bin/bash

# Android SDK Setup and Build Script
set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${YELLOW}🔍 Setting up Android SDK and building AAR...${NC}"

# Try to find Android SDK
ANDROID_SDK_PATHS=(
    "$HOME/Library/Android/sdk"
    "$HOME/Android/Sdk"
    "$HOME/android-sdk"
)

ANDROID_SDK=""
for path in "${ANDROID_SDK_PATHS[@]}"; do
    if [ -d "$path/platforms" ]; then
        ANDROID_SDK="$path"
        echo -e "${GREEN}✅ Found Android SDK at: $ANDROID_SDK${NC}"
        break
    fi
done

if [ -z "$ANDROID_SDK" ]; then
    echo -e "${RED}❌ Android SDK not found!${NC}"
    echo "Please install Android SDK via Android Studio"
    echo "Or set ANDROID_HOME manually:"
    echo "  export ANDROID_HOME=/path/to/your/android/sdk"
    exit 1
fi

# Set ANDROID_HOME
export ANDROID_HOME="$ANDROID_SDK"
echo "📍 ANDROID_HOME set to: $ANDROID_HOME"

# Find available API levels
if [ -d "$ANDROID_HOME/platforms" ]; then
    AVAILABLE_APIS=($(ls -1 "$ANDROID_HOME/platforms" | grep "android-" | sed 's/android-//' | sort -n))
    echo -e "${YELLOW}📋 Available API levels: ${AVAILABLE_APIS[*]}${NC}"
    
    # Find the best API to use (prefer 34, then 33, then highest available)
    TARGET_API=""
    for preferred in 34 33 32 31 30; do
        for api in "${AVAILABLE_APIS[@]}"; do
            if [ "$api" == "$preferred" ]; then
                TARGET_API="$api"
                break 2
            fi
        done
    done
    
    # If no preferred API found, use the highest available
    if [ -z "$TARGET_API" ]; then
        TARGET_API="${AVAILABLE_APIS[-1]}"
    fi
    
    echo -e "${GREEN}🎯 Using Android API: $TARGET_API${NC}"
    
    # Check if we need to modify build.gradle
    if [ "$TARGET_API" != "35" ]; then
        echo -e "${YELLOW}⚙️  Updating build.gradle to use API $TARGET_API...${NC}"
        
        # Create backup
        cp AndroidProject/app/build.gradle AndroidProject/app/build.gradle.backup
        
        # Update API versions
        sed -i.tmp "s/compileSdkVersion 35/compileSdkVersion $TARGET_API/g" AndroidProject/app/build.gradle
        sed -i.tmp "s/targetSdkVersion 35/targetSdkVersion $TARGET_API/g" AndroidProject/app/build.gradle
        
        # Clean up temp files
        rm -f AndroidProject/app/build.gradle.tmp
        
        echo -e "${GREEN}✅ Updated build.gradle to use API $TARGET_API${NC}"
    fi
    
    # Verify the required android.jar exists
    ANDROID_JAR="$ANDROID_HOME/platforms/android-$TARGET_API/android.jar"
    if [ -f "$ANDROID_JAR" ]; then
        echo -e "${GREEN}✅ Android JAR found: $ANDROID_JAR${NC}"
    else
        echo -e "${RED}❌ Android JAR not found: $ANDROID_JAR${NC}"
        exit 1
    fi
    
else
    echo -e "${RED}❌ Platforms directory not found in Android SDK${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}🔨 Starting AAR build...${NC}"

# Run the build
if [ -f "build_android_aar.sh" ]; then
    chmod +x build_android_aar.sh
    ./build_android_aar.sh
    
    if [ $? -eq 0 ]; then
        echo ""
        echo -e "${GREEN}🎉 SUCCESS! Android AAR built successfully!${NC}"
        
        # Restore original build.gradle if we modified it
        if [ -f "AndroidProject/app/build.gradle.backup" ]; then
            echo -e "${YELLOW}📋 Restoring original build.gradle...${NC}"
            mv AndroidProject/app/build.gradle.backup AndroidProject/app/build.gradle
            echo -e "${GREEN}✅ Original build.gradle restored${NC}"
        fi
        
        echo ""
        echo -e "${YELLOW}📦 AAR file should be at:${NC}"
        echo "   ../Plugins/Android/balancywebview.aar"
        
        # Check if AAR was created
        AAR_PATH="../Plugins/Android/balancywebview.aar"
        if [ -f "$AAR_PATH" ]; then
            AAR_SIZE=$(du -h "$AAR_PATH" | cut -f1)
            echo -e "${GREEN}✅ AAR created: $AAR_SIZE${NC}"
        else
            echo -e "${YELLOW}⚠️  AAR file not found at expected location${NC}"
        fi
        
    else
        echo -e "${RED}❌ Build failed!${NC}"
        exit 1
    fi
else
    echo -e "${RED}❌ build_android_aar.sh not found!${NC}"
    exit 1
fi
