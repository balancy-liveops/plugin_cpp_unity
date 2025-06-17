# Balancy WebView for Android

This directory contains the Android implementation of the Balancy WebView plugin for Unity.

## Files Structure

```
Android/
├── BalancyWebViewPlugin.java      # Main Java class for WebView management
├── BalancyWebViewJNI.cpp          # JNI bridge between Java and Unity C#
├── AndroidManifest.xml            # Android permissions and configuration
├── CMakeLists.txt                 # Build configuration for native library
├── build_android.sh               # Build script for creating .so libraries
├── arm64-v8a/                     # ARM64 architecture libraries
│   └── libBalancyWebViewAndroid.so
├── x86_64/                        # x86_64 architecture libraries  
│   └── libBalancyWebViewAndroid.so
└── README.md                      # This file
```

## Features

- ✅ **Overlay WebView** - Renders on top of Unity with transparency support
- ✅ **Emergency Exit** - Invisible button (10% x 10%) in top-right corner
- ✅ **Game UI Mode** - Disables text selection, context menus, zoom for game-like feel  
- ✅ **Viewport Control** - Configurable position/size as screen percentages
- ✅ **JavaScript Bridge** - Two-way communication Unity ↔ WebView
- ✅ **Event Callbacks** - Load completion, message handling, cache events
- ✅ **Debug Logging** - Configurable logging for development

## Building

To build the Android WebView library:

1. **Prerequisites:**
   - Android NDK (Unity's NDK is auto-detected)
   - CMake 3.26 or later

2. **Build Command:**
   ```bash
   cd Assets/Balancy/WebView/Plugins/Android
   ./build_android.sh
   ```

3. **Output:**
   - `arm64-v8a/libBalancyWebViewAndroid.so`
   - `x86_64/libBalancyWebViewAndroid.so`

## Integration

The Android WebView automatically integrates with your Unity project:

1. **C# Usage** (same API as iOS/macOS):
   ```csharp
   // Opens WebView overlay
   BalancyWebView.Instance.OpenWebView(url, ownerJson);
   
   // Send message to WebView
   BalancyWebView.Instance.SendMessageToWebView(message);
   
   // Configure transparency
   BalancyWebView.Instance.SetTransparentBackground(true);
   
   // Close WebView
   BalancyWebView.Instance.CloseWebView();
   ```

2. **Automatic Platform Detection:**
   - The C# code automatically uses the Android implementation when building for Android
   - No code changes needed - same API across iOS, macOS, and Android

## Architecture

### Java Layer (`BalancyWebViewPlugin.java`)
- Manages Android WebView creation and configuration
- Handles UI overlay and emergency exit button
- Provides settings for transparency, game UI mode, viewport control

### JNI Bridge (`BalancyWebViewJNI.cpp`)
- Connects Java WebView code with Unity C#
- Handles callback registration and message passing
- Uses Unity-compatible JNI patterns (no lifecycle conflicts)

### Unity Integration
- `DllImport("BalancyWebViewAndroid")` statements in C#
- Platform-specific conditional compilation
- Same public API as iOS/macOS implementations

## Emergency Exit

The Android WebView includes an emergency exit feature:

- **Location:** Top-right corner (10% x 10% of screen)
- **Visibility:** Invisible but touchable
- **Action:** Sends `//:balancy_close_view` message to Unity
- **Control:** Can be enabled/disabled via `SetEmergencyExitEnabled()`

## Troubleshooting

### Build Issues
- Ensure Android NDK is properly installed
- Check that Unity Android build support is installed
- Verify CMake version is 3.26 or later

### Runtime Issues
- Check Android logs with `adb logcat -s BalancyWebView`
- Enable debug logging via `SetDebugLogging(true)`
- Verify WebView permissions in AndroidManifest.xml

### Library Loading Issues
- Ensure both arm64-v8a and x86_64 libraries are built
- Check Unity's Android architecture settings
- Verify library naming matches `libBalancyWebViewAndroid.so`

## Development Notes

- Uses Android WebView API for web content rendering
- Implements overlay pattern similar to iOS/macOS versions
- Follows Unity Android plugin best practices
- Avoids JNI lifecycle conflicts with IL2CPP runtime

For more information, see the main Balancy WebView documentation.
