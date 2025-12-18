# Image/Sprite Loading Implementation for Unity WebGL

## Problems

### Problem 1: Sprite Loading
Files were being saved to IndexedDB but attempts to load them via IDBFS (`FS.readFile()`) were failing because IndexedDB and IDBFS are separate storage systems. When sprites were requested, the code tried to read from the Emscripten virtual filesystem (IDBFS), but the files were actually stored in browser IndexedDB.

### Problem 2: WebView Message Handling
When sending messages to the WebView iframe, the code tried to access `Module.FS` to convert image paths to blob URLs. This caused crashes with error: `'FS' was not exported. add it to EXPORTED_RUNTIME_METHODS (see the FAQ)`. Unity doesn't export FS by default, and even checking for its existence triggers an abort.

## Solution

Read files directly from IndexedDB instead of trying to access them through IDBFS. The solution uses blob URLs as an intermediary format that:
1. Unity's UnityWebRequest can load (for Sprite objects)
2. WebView iframe can reference (for displaying images in HTML)

A shared blob URL cache (`window._balancyBlobUrlCache`) ensures both systems use the same blob URLs.

## Architecture

### Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ Saving (already working)                                        │
├─────────────────────────────────────────────────────────────────┤
│ C++ FileHelperUnityWebGL::saveFileInCache()                    │
│   → balancy_indexeddb_saveFileBinary() [jslib]                 │
│     → BalancyIndexedDBFileHelper.saveFile() [JavaScript]       │
│       → IndexedDB storage                                        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Loading for Unity Sprites                                       │
├─────────────────────────────────────────────────────────────────┤
│ C# DataObjectsManager.LoadTextureWebGL()                       │
│   → _balancyPreloadFileAsBlobUrl() [P/Invoke]                  │
│     → BalancyIndexedDBFileHelper.loadFile() [JavaScript]       │
│       ← data from IndexedDB                                     │
│     → URL.createObjectURL(blob)                                 │
│       → blob URL (blob:http://...)                              │
│       → window._balancyBlobUrlCache[path] = blobUrl            │
│         ✅ CACHED FOR REUSE                                     │
│     → OnFilePreloaded callback [static C# method]               │
│       → PreloadContext.Complete = true                          │
│   ← blob URL from context                                       │
│   → UnityWebRequest.GetTexture(blobUrl)                         │
│     → Texture2D                                                  │
│       → Sprite.Create()                                          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Loading for WebView (uses cached blob URLs)                    │
├─────────────────────────────────────────────────────────────────┤
│ C# sends message to WebView with image paths                   │
│   → _balancySendMessage(messagePtr) [jslib]                    │
│     → Parse batch-response JSON                                 │
│     → For each response with image path:                        │
│       → Check window._balancyBlobUrlCache[path]                │
│         ✅ Found → Replace path with cached blob URL           │
│         ❌ Not found → Warn, leave path as-is                   │
│     → Send processed message to iframe                          │
│       → WebView displays images using blob URLs                 │
└─────────────────────────────────────────────────────────────────┘
```

## Files Modified

### 1. BalancyWebView.jslib

Location: `/Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/Plugins/WebGL/BalancyWebView.jslib`

#### Changes for Sprite Loading:

**Modified `_balancyReadFileAsBlobUrl` (lines 503-529)**
- Changed from reading IDBFS to checking cache only
- Now synchronous - returns cached blob URLs immediately
- Returns 0 (null) if blob URL not cached

```javascript
_balancyReadFileAsBlobUrl: function(pathPtr) {
  var path = UTF8ToString(pathPtr);

  // Check cache
  if (!window._balancyBlobUrlCache) {
    window._balancyBlobUrlCache = {};
  }

  var cachedUrl = window._balancyBlobUrlCache[path];
  if (cachedUrl) {
    // Return cached blob URL
    var bufferSize = lengthBytesUTF8(cachedUrl) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(cachedUrl, buffer, bufferSize);
    return buffer;
  }

  return 0; // Not cached
}
```

**Added `_balancyPreloadFileAsBlobUrl` (lines 531-608)**
- New async function that loads from IndexedDB
- Creates blob URLs and caches them
- Calls C# callback when complete

```javascript
_balancyPreloadFileAsBlobUrl: function(directoryPtr, fileNamePtr, callback, userData) {
  var directory = UTF8ToString(directoryPtr);
  var fileName = UTF8ToString(fileNamePtr);
  var fullPath = directory + '/' + fileName;

  // Load from IndexedDB
  BalancyIndexedDBFileHelper.loadFile(directory, fileName).then(function(data) {
    if (!data) {
      {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
      return;
    }

    // Determine MIME type
    var ext = fileName.split('.').pop().toLowerCase();
    var mimeType = /* ... */;

    // Create blob URL
    var blob = new Blob([data], { type: mimeType });
    var blobUrl = URL.createObjectURL(blob);

    // Cache it
    window._balancyBlobUrlCache[fullPath] = blobUrl;

    // Return to C#
    var buffer = _malloc(lengthBytesUTF8(blobUrl) + 1);
    stringToUTF8(blobUrl, buffer, bufferSize);
    {{{ makeDynCall('vii', 'callback') }}}(userData, buffer);
    _free(buffer);
  });
}
```

#### Changes for WebView Message Handling:

**Modified `_balancySendMessage` (lines 270-403)**
- **REMOVED**: All attempts to access `Module.FS` or `FS.readFile()`
- **ADDED**: Blob URL cache lookup for image paths in messages

**Before** (caused FS export error):
```javascript
// Try different FS access methods
if (typeof Module !== 'undefined' && Module.FS && typeof Module.FS.readFile === 'function') {
  fsObject = Module.FS;  // ❌ CRASHES - triggers export error
}
// Read file from IDBFS
fileData = fsObject.readFile(resp.result);
// Create blob from file data
var blob = new Blob([fileData], { type: mimeType });
var blobUrl = URL.createObjectURL(blob);
resp.result = blobUrl;
```

**After** (uses cached blob URLs):
```javascript
// Check blob URL cache (populated by _balancyPreloadFileAsBlobUrl)
if (window._balancyBlobUrlCache && window._balancyBlobUrlCache[resp.result]) {
  var cachedBlobUrl = window._balancyBlobUrlCache[resp.result];
  console.log('[BalancyWebView Plugin] ✅ Using cached blob URL:', cachedBlobUrl);
  resp.result = cachedBlobUrl;  // ✅ Use cached blob URL
} else {
  console.warn('[BalancyWebView Plugin] ⚠️ Image not in cache');
  // Leave path as-is, warn that it should have been preloaded
}
```

**Key Benefits**:
- No FS dependency - completely eliminated FS access
- Synchronous operation - no async complexity in message sending
- Unified caching - WebView uses same blob URLs as Unity sprites
- Better performance - cached blob URLs are instant lookups

### 2. DataObjectsManager.cs

Location: `/Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/Runtime/DataObjectsManager.cs`

#### Changes:

**Added P/Invoke declarations (lines 54-64)**
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
[DllImport("__Internal")]
private static extern IntPtr _balancyReadFileAsBlobUrl(string path);

private delegate void PreloadCallback(IntPtr userData, IntPtr blobUrlPtr);

[DllImport("__Internal")]
private static extern void _balancyPreloadFileAsBlobUrl(
    string directory,
    string fileName,
    PreloadCallback callback,
    IntPtr userData);
#endif
```

**Added context tracking (lines 75-102)**
```csharp
// Context for tracking async operations
private class PreloadContext
{
    public bool Complete;
    public string BlobUrl;
}

// Static tracking (required for IL2CPP)
private static Dictionary<int, PreloadContext> _preloadContexts =
    new Dictionary<int, PreloadContext>();
private static int _nextContextId = 1;

// Static callback (required for IL2CPP - no lambdas allowed)
[AOT.MonoPInvokeCallback(typeof(PreloadCallback))]
private static void OnFilePreloaded(IntPtr userDataPtr, IntPtr blobUrlPtr)
{
    int contextId = userDataPtr.ToInt32();

    if (_preloadContexts.TryGetValue(contextId, out var context))
    {
        if (blobUrlPtr != IntPtr.Zero)
        {
            context.BlobUrl = Marshal.PtrToStringAnsi(blobUrlPtr);
        }
        context.Complete = true;
    }
}
```

**Updated LoadTextureWebGL coroutine (lines 156-263)**
```csharp
private IEnumerator LoadTextureWebGL()
{
    // Try Resources first
    Texture2D texture = TryToLoadTextureFromResources();
    if (texture != null) { /* ... */ }

    // Split path: /idbfs/hash/filename -> directory + fileName
    string fullPath = PathInStorage;
    int lastSlash = fullPath.LastIndexOf('/');
    string directory = fullPath.Substring(0, lastSlash);
    string fileName = fullPath.Substring(lastSlash + 1);

    // Check cache first (fast path)
    string blobUrl = ReadFileAsBlobUrl(fullPath);

    if (string.IsNullOrEmpty(blobUrl))
    {
        // Not cached - need to preload from IndexedDB
        int contextId = _nextContextId++;
        var context = new PreloadContext { Complete = false };
        _preloadContexts[contextId] = context;

        // Call async preload
        _balancyPreloadFileAsBlobUrl(
            directory,
            fileName,
            OnFilePreloaded,
            new IntPtr(contextId));

        // Wait for completion (with timeout)
        float timeout = 5f;
        float elapsed = 0f;
        while (!context.Complete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        blobUrl = context.BlobUrl;
        _preloadContexts.Remove(contextId);
    }

    // Load texture from blob URL
    using (var request = UnityWebRequestTexture.GetTexture(blobUrl))
    {
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            texture = DownloadHandlerTexture.GetContent(request);
            CreateSpriteFromTexture(texture);
        }
    }
}
```

## Key Technical Points

### 1. IndexedDB vs IDBFS

- **IndexedDB**: Browser's key-value storage API, persistent across sessions
- **IDBFS**: Emscripten's virtual filesystem that CAN persist to IndexedDB via `FS.syncfs()`
- **Critical difference**: They don't automatically sync - files saved to IndexedDB aren't visible to `FS.readFile()` without explicit sync

Our solution bypasses IDBFS entirely for sprite loading.

### 2. BalancyIndexedDBFileHelper

JavaScript helper already present in Unity project at:
`/Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/WebView/Resources/WebGL/IndexedDBFileHelper.js.txt`

Key methods:
- `initIndexedDB()`: Opens IndexedDB connection
- `saveFile(directory, fileName, data, isBinary)`: Saves file to IndexedDB
- `loadFile(directory, fileName)`: Loads file from IndexedDB (returns Promise)
- `fileExists(directory, fileName)`: Check if file exists

### 3. IL2CPP Marshaling Limitations

Unity's IL2CPP compiler does NOT support:
- ❌ Marshaling lambda expressions to native code
- ❌ Marshaling instance methods to native code
- ✅ Only static methods with [AOT.MonoPInvokeCallback] attribute

**Solution**: Use static callback + context dictionary
```csharp
// ❌ DOESN'T WORK in IL2CPP:
_balancyPreloadFileAsBlobUrl(dir, file, (url) => {
    myBlobUrl = url;
}, IntPtr.Zero);

// ✅ WORKS in IL2CPP:
[AOT.MonoPInvokeCallback(typeof(PreloadCallback))]
private static void OnFilePreloaded(IntPtr userData, IntPtr blobUrlPtr) {
    int contextId = userData.ToInt32();
    var context = _preloadContexts[contextId];
    context.BlobUrl = Marshal.PtrToStringAnsi(blobUrlPtr);
}
```

### 4. Blob URLs

Browser feature for creating temporary URLs to binary data:
```javascript
var blob = new Blob([arrayBuffer], { type: 'image/png' });
var blobUrl = URL.createObjectURL(blob);
// Returns: "blob:http://localhost:8000/uuid-here"
```

Benefits:
- ✅ Unity's UnityWebRequest can load from blob URLs
- ✅ Works synchronously after creation
- ✅ Cached for reuse
- ⚠️ Should call `URL.revokeObjectURL()` when done (memory cleanup)

### 5. Async Handling Pattern

JavaScript IndexedDB operations are Promise-based (async), but Unity needs synchronous access:

```csharp
// Pattern: Poll in coroutine
IEnumerator LoadAsync() {
    var context = new Context { Complete = false };

    // Start async operation
    StartAsyncOperation(staticCallback, contextId);

    // Poll until complete
    while (!context.Complete && elapsed < timeout) {
        yield return null; // Wait one frame
        elapsed += Time.deltaTime;
    }

    // Use result
    UseResult(context.Result);
}
```

## Path Format

Example path: `/idbfs/692f0edafac0cf5046c65be006146c2c/30b197f2-783d-11f0-bdb3-1fec53a055ba_Cache/Files/1206_1762525716885.png`

Split into:
- **Directory**: `/idbfs/692f0edafac0cf5046c65be006146c2c`
- **FileName**: `30b197f2-783d-11f0-bdb3-1fec53a055ba_Cache/Files/1206_1762525716885.png`

IndexedDB stores with key = directory + "/" + fileName

## Performance Considerations

### Caching Strategy

1. **First load**: Slow path (~50-200ms)
   - Load from IndexedDB
   - Create blob URL
   - Cache blob URL
   - Load texture via UnityWebRequest

2. **Subsequent loads**: Fast path (~1-5ms)
   - Check cache
   - Return cached blob URL immediately
   - Load texture via UnityWebRequest

### Memory Management

Blob URLs are NOT automatically garbage collected. Consider:
```csharp
// When clearing sprite from memory:
internal static void ClearFromMemory(string id)
{
    if (AllObjects.TryGetValue(id, out var oneObject))
    {
        if (oneObject is OneObjectSprite sprite)
        {
            // TODO: Revoke blob URL to free memory
            // Need to add jslib function:
            // _balancyRevokeBlobUrl(fullPath)

            Object.Destroy(sprite.Sprite.texture);
            Object.Destroy(sprite.Sprite);
        }
        AllObjects.Remove(id);
    }
}
```

## Debugging

Enable detailed logging in jslib:
```javascript
console.log('[BalancyWebView Plugin] Preloading file:', fullPath);
console.log('[BalancyWebView Plugin] File loaded from IndexedDB, size:', data.byteLength);
console.log('[BalancyWebView Plugin] Blob URL created:', blobUrl);
```

Enable detailed logging in C#:
```csharp
Debug.Log("**==>> [WebGL] Full path: " + fullPath);
Debug.Log("**==>> [WebGL] Directory: " + directory);
Debug.Log("**==>> [WebGL] FileName: " + fileName);
Debug.Log("**==>> [WebGL] Preloading file from IndexedDB...");
Debug.Log("**==>> [WebGL] Preload complete, blob URL: " + blobUrl);
```

## Testing

1. Clear browser cache and IndexedDB
2. Launch Unity WebGL build
3. Trigger sprite loading
4. Verify in console:
   - "Saved file to IndexedDB" logs
   - "Preloading file from IndexedDB" logs
   - "Blob URL created" logs
   - "Texture loaded successfully" logs
5. Check IndexedDB in browser DevTools:
   - Application > IndexedDB > BalancyFileStorage > files
   - Should see file records with data

## Future Improvements

1. **Batch preloading**: Preload multiple sprites at once
2. **Memory management**: Implement blob URL revocation
3. **Cache persistence**: Consider caching blob URLs across sessions
4. **Error recovery**: Better handling of IndexedDB failures
5. **Progress reporting**: Report loading progress for large sprites

## Troubleshooting

### Symptom: "'FS' was not exported. add it to EXPORTED_RUNTIME_METHODS"
**Cause**: Code trying to access `Module.FS` or `FS` directly
**Fix**:
- This error occurs when code tries to access Emscripten's FS module, which Unity doesn't export
- Our solution completely removes FS dependency by using IndexedDB + blob URL caching
- If you see this error, check that you're using the updated `_balancySendMessage` that uses `window._balancyBlobUrlCache` instead of FS access
- Ensure sprites are preloaded via `DataObjectsManager.LoadTextureWebGL()` before sending to WebView

### Symptom: "BalancyIndexedDBFileHelper not available"
**Cause**: IndexedDBFileHelper.js.txt not loaded
**Fix**: Ensure file is in Resources/WebGL and properly injected by C#

### Symptom: "File not found in IndexedDB"
**Cause**: File wasn't saved or wrong path format
**Fix**:
- Check save logs
- Verify directory/fileName split is correct
- Check IndexedDB in browser DevTools

### Symptom: "IL2CPP does not support marshaling delegates"
**Cause**: Using lambda or instance method for callback
**Fix**: Use static method with [AOT.MonoPInvokeCallback]

### Symptom: "Image not in cache" warning in WebView messages
**Cause**: Sprite not preloaded before being sent to WebView
**Fix**:
- Ensure `DataObjectsManager.GetSprite()` is called for all sprites before opening WebView
- This populates `window._balancyBlobUrlCache` which WebView messages use
- The sprite loading system should preload all required sprites automatically

### Symptom: Timeout waiting for preload
**Cause**: Callback never called or wrong context ID
**Fix**:
- Check JavaScript console for errors
- Verify callback signature matches jslib
- Increase timeout for debugging

## References

- [Emscripten IDBFS Documentation](https://emscripten.org/docs/api_reference/Filesystem-API.html#filesystem-api-idbfs)
- [IndexedDB API](https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API)
- [Unity IL2CPP P/Invoke](https://docs.unity3d.com/Manual/IL2CPP-OptimizingBuildTimes.html)
- [URL.createObjectURL()](https://developer.mozilla.org/en-US/docs/Web/API/URL/createObjectURL)
