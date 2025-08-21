#!/bin/bash

echo "🔧 Making build script executable..."
chmod +x build_standalone.sh

echo "🚀 Starting Android build (Standalone Mode)..."
echo "📝 This version does NOT require Unity to be installed!"
echo ""

./build_standalone.sh
