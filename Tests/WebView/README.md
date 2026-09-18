# Persistent WebView regression checks

Branch: `codex/persistent-webview-fixes`. Ship with the matching TypeScript bridge/core changes; generated bridge and Unity WebGL resources are included in this SDK branch.

## Run

Use Unity Test Runner → EditMode → `Balancy.Tests.PersistentWebViewTests` for the eight state-transition tests. They test the actual `PersistentViewState` used by the component, with fake transport and time, including 100 open/close cycles.

The same fixture can run without starting the editor using installed Mono and the Unity project's NUnit package:

```sh
python3 Tests/WebView/validate.py
node --test Tests/WebView/webgl-plugin.test.cjs
```

Optional compiler checks use an existing Unity project's generated `Balancy.csproj` and `Balancy.WebView.csproj` references:

```sh
python3 Tests/WebView/validate.py --project /path/to/unity-project --unity-editor /path/to/Unity.app
```

The script compiles both assemblies and the WebView assembly's iOS, Android, WebGL, macOS and Windows conditional branches. This is a compiler check, not an IL2CPP/player build. WebGL tests execute the actual `.jslib` in a JavaScript VM and cover shared initialization, failure/retry, persistent routing and duplicate exports.

## Native plugin rebuild

Requires Xcode and Android SDK/OpenJDK (Unity's bundled tools work):

```sh
export JAVA_HOME=/path/to/AndroidPlayer/OpenJDK
python3 Tests/WebView/build-native.py --android-jar /path/to/SDK/platforms/android-36/android.jar --output /tmp/balancy-native-build --update
```

Use a new output directory each time. Omit `--update` to keep build products outside the SDK. The script compiles Java to replace `classes.jar` while preserving the existing AAR's manifest and metadata; builds/ad-hoc signs a universal Intel/Apple Silicon macOS dylib; syntax-checks iOS Objective-C++. This keeps source and shipped binary changes aligned. iOS remains source-built in the customer player build.

## Results and remaining validation

- 8 NUnit state tests, 3 WebGL plugin tests passed.
- Both C# assemblies and all five WebView platform branches compiled against Unity 6000.5.1f1 references.
- Android Java compiled with Unity OpenJDK and API 36; macOS universal dylib rebuilt; iOS syntax check passed with Xcode iPhoneOS SDK 26.2. Existing platform deprecation warnings remain.
- Matching TypeScript checkout: 37 bridge and 114 core tests passed, including DOM lifecycle fixtures and repeated cycles.

No iOS/Android device, browser player or Unity Editor test-runner execution was performed. The standalone NUnit fixture does not load Unity or native plugins. Validate cold/warm open, A→close→B, error/retry, language/content/profile changes, native fade scheduling during rapid hide/show and customer legacy HTML on each actual target before release. Windows native remains unsupported.

The public API still supports one active view; Prepare now has an optional failure callback. Root readiness waits for asynchronous initialization, while native fade completion remains separate. SDK scripts can register custom cleanup through `balancy.onViewDispose` and use `balancy.viewSignal`; arbitrary third-party asynchronous code still needs its own cancellation/disposal. Module-script HTML requires the full-page path. See `packages/bridge/PERSISTENT_WEBVIEW.md` in the TypeScript SDK for the full protocol and compatibility contract.

## Second pass — 2026-09-15

Native delayed show callbacks are cancelled/generation-guarded and stale navigation callbacks are ignored. Renderer termination resets persistent state even after shell readiness; the new NUnit scenario verifies re-preparation. Android/macOS binaries were rebuilt. The matching TypeScript branch also fixes iframe navigation latency, blob URL ownership and JS disposal; see `packages/bridge/PERFORMANCE_AND_MEMORY.md`. Its standalone headless Chrome GC fixture covers 520 persistent cycles; this does not replace native player profiling.
