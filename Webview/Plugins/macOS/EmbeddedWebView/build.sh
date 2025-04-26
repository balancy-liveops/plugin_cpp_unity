#!/bin/bash
# Build script for EmbeddedWebView plugin

# Navigate to the plugin directory (where this script is located)
cd "$(dirname "$0")"

# Run make to build the plugin
make clean
make

# Check if build was successful
if [ $? -eq 0 ]; then
  echo "Build successful! Plugin is ready to use."
  echo "The plugin is located at: $(pwd)/Contents/MacOS/EmbeddedWebView"
else
  echo "Build failed. Check the errors above."
  exit 1
fi

# Make the plugin executable
chmod +x Contents/MacOS/EmbeddedWebView

echo "Done!"
