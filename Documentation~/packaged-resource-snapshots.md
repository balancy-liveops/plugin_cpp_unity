# Packaged resource snapshot architecture

The C++ core should keep its synchronous file API. Startup should expose a complete,
immutable packaged snapshot plus a writable persistent overlay before core
initialization. Reads resolve the persistent overlay first and the packaged snapshot
second; writes always target the overlay.

## Platform adapters

- **Android:** mount `AssetManager` in native code. `AAssetManager_open` and
  `AAsset_read` provide synchronous, lazy access to files inside the APK. No manifest
  walk, bulk copy, or C#/JNI string transfer is required. WebView URLs may continue
  to use `file:///android_asset/`; persistent updates use regular `file://` paths.
- **iOS/macOS:** packaged StreamingAssets are ordinary read-only files in the app
  bundle and are already synchronously readable. The present iOS copy is needed for
  the writable resource directory and the WebView's local-file access model, not for
  core reads. The low-risk next step is a versioned copy-once snapshot: calculate a
  build-time content ID, copy to a staging directory only when it changes, verify it,
  atomically publish it, and keep a completion marker. Later, a two-root native file
  helper plus a `WKURLSchemeHandler` can remove the first-install copy as well.
- **WebGL:** browser storage cannot satisfy the current synchronous core calls on the
  main thread. Package the minimal boot snapshot into one Emscripten `.data` bundle
  and await its mount into MEMFS before initializing core. Hydrate the persistent
  overlay from IndexedDB in one transaction/blob. Large optional assets remain URL
  based and lazy. OPFS synchronous handles are worker-only, so they are not a
  drop-in main-thread replacement.

## Rollout

1. Ship and device-test the Android native provider, retaining the old preload ABI as
   an import-order fallback.
2. Add an iOS snapshot ID and atomic copy-once preparation. This removes repeated
   launch copying without changing C++ or WebView behavior.
3. Separate packaged and writable roots in `IFileHelper`. Validate local views and
   persistent views before removing the iOS first-install copy.
4. Replace WebGL's per-file StreamingAssets requests and full IndexedDB object walk
   with one boot package plus one persistent snapshot hydration.
5. Only after these adapters are stable, consider moving individual high-level core
   operations to async APIs. A whole-core async conversion is unnecessary and has a
   much larger regression surface.
