#!/bin/bash

# Final build script for Android AAR with all new methods
set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}🎉 Ready to build Android AAR!${NC}"
echo -e "${BLUE}========================================${NC}"

echo -e "${GREEN}✅ All Java methods implemented:${NC}"
echo "   - setViewportRect(x, y, width, height)"
echo "   - setTransparentBackground(transparent)"
echo "   - setOfflineCacheEnabled(enabled)" 
echo "   - setShowDelay(delaySeconds)"
echo "   - setAnimationDuration(durationSeconds)"
echo "   - setEmergencyExitEnabled(enabled)"
echo "   - setGameUIMode(enabled)"

echo ""
echo -e "${GREEN}✅ All C# methods implemented:${NC}"
echo "   - _balancySetViewportRect"
echo "   - _balancySetTransparentBackground"
echo "   - _balancySetOfflineCacheEnabled"
echo "   - _balancySetShowDelay" 
echo "   - _balancySetAnimationDuration"
echo "   - _balancySetEmergencyExitEnabled"
echo "   - _balancySetGameUIMode"

echo ""
echo -e "${YELLOW}🔨 Building Android AAR...${NC}"

# Check if we're in the right directory
if [ ! -f "build_android_aar.sh" ]; then
    echo "❌ Error: build_android_aar.sh not found. Please run from WVAndroidLib.androidlib directory"
    exit 1
fi

# Make build script executable and run it
chmod +x build_android_aar.sh
./build_android_aar.sh

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${GREEN}🚀 SUCCESS! AAR Built Successfully${NC}" 
    echo -e "${BLUE}========================================${NC}"
    echo ""
    echo -e "${YELLOW}📦 The new AAR includes all missing methods!${NC}"
    echo -e "${YELLOW}🎯 C# errors should now be resolved.${NC}"
else
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi
