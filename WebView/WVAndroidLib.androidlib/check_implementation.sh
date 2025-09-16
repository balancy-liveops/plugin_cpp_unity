#!/bin/bash

# Quick check script to verify our implementation
set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}🔍 Checking Android WebView Implementation...${NC}"

JAVA_FILE="AndroidProject/app/src/main/java/com/balancy/webview/BalancyWebViewPlugin.java"

# Check if Java file exists
if [ ! -f "$JAVA_FILE" ]; then
    echo -e "${RED}❌ Java file not found: $JAVA_FILE${NC}"
    exit 1
fi

# Required methods that C# calls
METHODS=(
    "setViewportRect"
    "setTransparentBackground"
    "setOfflineCacheEnabled"
    "setShowDelay" 
    "setAnimationDuration"
    "setEmergencyExitEnabled"
)

echo -e "${YELLOW}Checking for required methods:${NC}"

for method in "${METHODS[@]}"; do
    if grep -q "public void $method" "$JAVA_FILE"; then
        echo -e "   ${GREEN}✅ $method${NC}"
    else
        echo -e "   ${RED}❌ $method - MISSING${NC}"
        exit 1
    fi
done

# Check for proper field declarations
echo -e "${YELLOW}Checking for required fields:${NC}"

FIELDS=(
    "emergencyExitEnabled"
    "offlineCacheEnabled"
)

for field in "${FIELDS[@]}"; do
    if grep -q "$field" "$JAVA_FILE"; then
        echo -e "   ${GREEN}✅ $field${NC}"
    else
        echo -e "   ${RED}❌ $field - MISSING${NC}"
        exit 1
    fi
done

echo -e "${GREEN}✅ All required methods and fields are present!${NC}"
echo -e "${YELLOW}Ready for AAR build.${NC}"
