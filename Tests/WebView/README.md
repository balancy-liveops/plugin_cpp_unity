# Persistent WebView regression checks

Branch: `codex/persistent-webview-fixes`. Ship with the matching TypeScript bridge/core changes; generated bridge and Unity WebGL resources are included in this SDK branch.

## Run

Use Unity Test Runner → EditMode → `Balancy.Tests.PersistentWebViewTests` for the eight state-transition tests. They test the actual `PersistentViewState` used by the component, with fake transport and time, including 100 open/close cycles.

The same fixture can run without starting the editor using installed Mono and the Unity project's NUnit package:

```sh
python3 Tests/WebView/validate.py
node --test Tests/WebView/webgl-plugin.test.cjs
node --test Tests/WebView/classic-bootstrap.test.cjs
```

`validate.py` also runs `Balancy.Tests.RuntimeBootstrapTests`, which keep `native/classic-bootstrap.js.txt` identical to the page loader `RuntimeBootstrap` builds. `classic-bootstrap.test.cjs` and the Android harness execute that file, so after changing the loader run `python3 Tests/WebView/validate.py --update-fixtures`, then both. Unity's bundled Mono works when no system Mono is installed: `--mcs <Editor>/Data/MonoBleedingEdge/bin/mcs --mono <Editor>/Data/MonoBleedingEdge/bin/mono`.

The classic loader also preserves the native legacy shim's `owner`, `sendMessageToUnity` and inbound receiver.
`onMessage` replacements made through either the old cached object or the new bridge remain visible. Modern responses
are handled first, and exceptions in the legacy receiver are logged without breaking modern request settlement.
The Node bootstrap tests cover these contracts, repeated injection, and reuse of an existing persistent bridge.
To additionally execute the generated loader with the shipped production bridge, provide a jsdom module path:

```sh
BALANCY_JSDOM_PATH=/path/to/node_modules/jsdom node --test Tests/WebView/classic-bootstrap-production.test.cjs
```

The production test is skipped without this optional dependency. It checks owner access during script installation,
legacy outbound/inbound messaging and real correlated request settlement, including a throwing legacy callback.
It uses a shell document to avoid backend-dependent view initialization; it does not replace an Android/Unity player test.

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

## Classic reveal gate — 2026-10-09

Android classic pages now open hidden and are revealed by the page's `BalancyIsReady`, an error, or a 30 s timeout. `validate.py` also runs `Balancy.Tests.ClassicRevealGateTests`, which drive the actual `ClassicRevealGate` with a fake clock.

The Android harness checks the native side on a real WebView. Unity's bundled SDK only has build-tools 36.0.0, so pass `--build-tools`:

```sh
U=/path/to/Editor/Data/PlaybackEngines/AndroidPlayer
python3 Tests/WebView/native/run-android.py --sdk $U/SDK --java-home $U/OpenJDK --build-tools 36.0.0 --output /tmp/balancy-android-tests --serial <device>
```

The harness first checks the generated classic loader on a normal page, then uses that same loader on a hidden
classic page. The page must stay hidden through page finish, send `BalancyIsReady` while hidden, and be revealed by
`showWebView()`. A final pass reveals a hidden page before it finishes loading, as the timeout can:
`onPageFinished` hides it again (alpha 0), and a second `showWebView()` after page finish shows it.
That is why Unity shows a view again when its page finishes after an early reveal.

82 checks passed on a REDMAGIC 9 Pro (Android 16, WebView 153), both built from source and with `--aar WebView/Plugins/Android/balancywebview.aar`. The `checks=` count in the result line also includes the timing line.

## Ready request routing regression — 2026-10-12

The classic ready request is dispatched before `onShown` can change its owner. The gate records the opening before
dispatch and only reveals that same opening afterward. If an application filter closes/reopens the view or recreates
the profile, its old ready and rejection response are discarded instead of reaching the replacement. A rejection
without a context change still reveals the same page. Close before reveal continues to cancel both opening callbacks;
the existing 30-second fallback, input behavior and persistent/iOS/WebGL paths are retained.

`ClassicReadyRequest` uses Unity's JSON parser and inspects the request's own action (or batch members), so nested
payload fields and requests tagged with a persistent `viewId` cannot reveal the page. Run `Balancy.Tests.ClassicRevealGateTests` in Unity EditMode for the actual
parser tests, including whitespace, malformed input, nested actions and request batches. Those editor tests are
excluded from the standalone Mono fixture, which runs the gate's state/routing tests without loading Unity.

```sh
python3 Tests/WebView/ready-routing.test.py
```

This additionally executes the actual routing methods extracted from `RenderViewsManager` and the component, with
fake native transport and known classifier outcomes. It checks owner selection, application rejection, close/reopen
from callbacks and duplicate ready delivery. It does not execute the C++ backend or emulate Unity's JSON parser.
