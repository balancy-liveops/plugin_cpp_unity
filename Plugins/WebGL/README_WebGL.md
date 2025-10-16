# Balancy Unity WebGL Integration

This directory contains the JavaScript bridge (`.jslib`) that connects Unity WebGL builds to the Balancy WASM library.

## Files in This Directory

- **Balancy.jslib** - JavaScript bridge that loads and interfaces with the WASM module
- **Balancy.js** - WASM loader (auto-generated, placed here for reference but loaded from StreamingAssets)
- **Balancy.wasm** - Compiled C++ library (auto-generated, placed here for reference but loaded from StreamingAssets)

## How It Works

```
Unity C# (UNITY_WEBGL)
    ↓ [DllImport("__Internal")]
Balancy.jslib (JavaScript Bridge)
    ↓ Module.BalancyBridge
WASM Module (from StreamingAssets/)
    ↓ Emscripten Network Handler
Browser Fetch API
```

## Building for Unity WebGL

1. **Build the WASM library:**
   ```bash
   cd /path/to/balancy_cpp
   ./build_unity_webgl.sh
   ```

2. **Files are automatically copied to:**
   - `Assets/StreamingAssets/Balancy.js`
   - `Assets/StreamingAssets/Balancy.wasm`

3. **Build your Unity project for WebGL platform**

## Implementation Details

### C# Side (LibraryMethods.cs)
```csharp
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
    internal const string DllName = "__Internal";
#endif
```

### JavaScript Bridge (Balancy.jslib)
- Dynamically loads `Balancy.js` from StreamingAssets at runtime
- Creates a `Module.BalancyBridge` singleton to manage the WASM instance
- Forwards all C# calls to the WASM module via `Module.BalancyBridge.module._functionName()`

### Initialization (BalancyLoader.cs)
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    BalancyLoader.Init(() => {
        // Balancy WASM is ready, now initialize Balancy
    });
#endif
```

### Network Handler
- Uses `EmscriptenNetworkHandler.cpp` for HTTP requests
- Leverages browser's Fetch API via Emscripten
- WebSocket support via `EmscriptenWebSocketHandler.cpp`

## Key Configuration Flags

The build uses these CMake flags:
- `-DWEBGL=ON` - Enables WebGL/Emscripten mode
- `-DUNITY_BUILD=ON` - Enables Unity-specific code paths
- `-DENABLE_WEBSOCKETS=ON` - Includes WebSocket support
- `-DBUILD_ENVIRONMENT=unity_web` - Configures output format for Unity

## Troubleshooting

### "Module.BalancyBridge is undefined"
The WASM module failed to load. Check browser console for errors.

### "Failed to load Balancy.js"
Ensure files are in `StreamingAssets/` and your WebGL build includes them.

### Network requests fail
Check browser CORS policies and ensure your server is properly configured.

## Differences from Native Builds

| Feature | Native (iOS/Android) | Unity WebGL |
|---------|---------------------|-------------|
| Library | .a / .so | .wasm |
| DllImport | `__Internal` | `__Internal` |
| Loading | Direct link | Dynamic via .jslib |
| Network | Platform-specific | Fetch API |
| WebSocket | Platform-specific | Browser WebSocket |

## References

- [Emscripten Documentation](https://emscripten.org/)
- [Unity WebGL Manual](https://docs.unity3d.com/Manual/webgl.html)
- [Unity WebGL Plugins](https://docs.unity3d.com/Manual/webgl-interactingwithbrowserscripting.html)
