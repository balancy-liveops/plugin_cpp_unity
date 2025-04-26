# Embedded WebView for Unity Editor

This native plugin provides an embedded WebView that works directly in the Unity Editor's Game view on macOS. It allows you to test your WebView functionality without needing to build to a mobile device.

## Building the Plugin

1. Open Terminal
2. Navigate to this directory:
   ```
   cd /path/to/Assets/Plugins/macOS/EmbeddedWebView
   ```
3. Make the build script executable:
   ```
   chmod +x build.sh
   ```
4. Run the build script:
   ```
   ./build.sh
   ```

This will compile the native plugin and place it in the `Contents/MacOS` directory.

## Setting Up in Unity

1. Make sure the plugin is built successfully (see above)
2. Add a RawImage component to your UI Canvas to display the WebView
3. Reference this RawImage in your BalancyWebViewExample script

## Troubleshooting

If you encounter any issues:

1. **Plugin not loading**: Make sure the plugin is properly built and set to macOS/Editor only in Unity's inspector
2. **Black screen**: Check that the RawImage is properly configured and visible
3. **No input**: Make sure the RawImage is interactive and can receive input events
4. **Compilation errors**: Ensure you have Xcode Command Line Tools installed:
   ```
   xcode-select --install
   ```

## System Requirements

- macOS 10.14 or later
- Unity 2019.4 or later
- Xcode Command Line Tools
