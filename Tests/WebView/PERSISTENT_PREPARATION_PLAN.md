# Persistent preparation and script updates

## Implementation plan

1. Verify local/cloud DataIsReady ordering in C++.
2. Remember explicit client PrepareWebView intent, including calls before SDK initialization.
3. Read scripts on preparation and data notifications, not on every persistent view open.
4. Replace the entire idle shell when script contents change; defer while a view is active or clearing.
5. Inject scripts and bridge during preparation on native and Unity WebGL; loadView carries only the installed version.
6. Test readiness, repeated calls, queued views, stale ACKs, update coalescing, failure/retry, cleanup, and Unicode transport.
7. Build the Unity WebGL adapter, synchronize runtime assets, and validate Unity platform compilation.

## C++ findings

`UserData::initFromLocal` calls `initClientManagers(..., true)`: local initialization skips preload and then emits DataIsReady. Only cached scripts can be available at that point.

The cloud path waits for `DataLoader::updateDataObjectsFiles`, which calls `preloadAllScripts` before completion. However, download failure is logged and completion still fires. Therefore OnDataUpdated is a refresh boundary, not a proof that downloading succeeded.

The existing compile API returns an empty string both for missing combined code and for a project with no scripts. It provides no success/revision status. Do not treat an empty string alone as an error: that would break script-free views. A future native status API is needed to distinguish these cases without guessing. Native files are unchanged in this task.

## Runtime contract

Call `RenderViewsManager.PrepareWebView` explicitly. Calls before DataIsReady are remembered. The optional preparation callback reports shell readiness, not SDK data readiness. Stop clears intent and pending callbacks; the next SDK session requires opt-in again.

Controller processes script updates before invoking public OnDataUpdated subscribers, so an OpenView from a subscriber sees the latest pending version. Identical code preserves the shell. Changed code replaces the shell after the active view's clear acknowledgment, even if that view is merely hidden. An accepted but undispatched next view waits for the replacement shell. Updates during shell preparation coalesce and suppress obsolete ready notifications.

Ordinary Unity persistent loadView commands never contain script code or script Base64. Unity WebGL receives raw code/version during prepare and acknowledges the installed version. Classic windows retain their existing per-open script read.

Destroying a shell drops its JavaScript context and resource cache. Script-free CMS/resource updates continue to invalidate the corresponding caches without replacing the shell.

## Scope and validation

This change covers Unity native and Unity WebGL. The standalone TypeScript SDK's lifecycle remains unchanged. State transition tests verify reference/callback release; actual native memory release still requires device/Editor profiling across repeated shell replacement.

## Validation result (2026-09-16)

- Unity Editor EditMode: 33 passed (including 16 persistent-state tests).
- Standalone Mono state runner: the same 16 tests passed.
- Bridge: 81 passed.
- TypeScript core: 119 passed, including Unity adapter injection and obsolete-error tests.
- Unity WebGL plugin: 4 passed.
- Full SDK C# compilation and WebView iOS/Android/WebGL/macOS/Windows conditional compilation passed.
- Unity WebGL artifact rebuilt and copied to SDK resources and the local project's StreamingAssets.
- No native device runtime/memory profiling performed. C++ download failure vs script-free-project ambiguity remains as described above.
