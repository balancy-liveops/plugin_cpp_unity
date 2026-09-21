# Native WebView regression harnesses

These run the production Java/Objective-C++ implementations on real Android WebView / WKWebView, without requiring Unity or a backend. The mobile apps also inject the built production bridge and open/clear 20 views containing localization and a local PNG. Host resource responses are mocked; this is not a Unity IL2CPP or C++ backend integration test.

Fixtures deliberately use `.java.txt` and `.mm.txt` extensions so Unity cannot accidentally compile test Activities or `main()` into a client build.

## Android

Boot an emulator, then run from the Balancy SDK root:

```sh
python3 Tests/WebView/native/run-android.py --sdk "$ANDROID_SDK_ROOT" --java-home "$JAVA_HOME" --output /tmp/balancy-android-test --serial emulator-5554
```

To test the shipped binary rather than recompiling the plugin source, add
`--aar WebView/Plugins/Android/balancywebview.aar` and use a fresh output directory.
This also rejects an AAR containing class files newer than Java 8 (major version 52),
checking the bytecode target. This alone does not guarantee compatibility with older D8 versions.
The harness itself still uses SDK build-tools 35; it does not replace a full build
in the client's Unity/AGP environment.

Uses SDK platform/build-tools 35 and a temporary locally signed APK. The runner uninstalls only `com.balancy.webview.tests` to avoid conflicting test signing keys. It tests repeated initialization, hidden preparation, immediate show, cancellation of delayed show, null/escaped legacy owner, double close, delayed injection targeting a retired view, 30 native replacements, real bridge image decoding under changing base URLs, cache reuse, DOM clearing, and weak-reference collection of retired WebViews.

## iOS Simulator

```sh
python3 Tests/WebView/native/run-ios.py --output /tmp/balancy-ios-test --device BOOTED_SIMULATOR_UDID
```

Requires Xcode and an arm64 simulator. Tests production WKWebView creation, immediate/hidden presentation, delayed-show cancellation, Unicode messaging, scalar message serialization, stale messages/navigation after close, 20 controller replacements, ARC release, and the same production-bridge/image/cache scenario. Installs only `com.balancy.webview.ios-tests`.

## macOS

```sh
xcrun clang++ -std=c++17 -x objective-c++ -fobjc-arc -framework Cocoa -framework WebKit -framework Metal -framework QuartzCore -framework CoreGraphics Tests/WebView/native/macos/main.mm.txt -o /tmp/balancy-native-macos-tests
/tmp/balancy-native-macos-tests
```

Uses small actual AppKit windows (briefly visible). Twenty normal window cycles plus five embedded cycles cover ARC window ownership, immediate show, hide/close, delegate and handler cleanup, late messages, timers, graphics contexts, pixel buffers, and weak controller release.

## Results and limits

See `validation/` and `../NATIVE_OPTIMIZATION_REPORT.md`. Android 12/API 31 and Android 16/API 36, iOS 17.0 and iOS 26.3, and macOS were exercised locally. These tests do not establish GPU-driver behavior on physical Android devices or total memory use of separate WebKit/Chromium renderer processes.

The 2026-09-17 fixture also instantiates two cached prefabs per View (one detached and one reparented outside the mount). Across 20 opens it requires 40 `onDestroy` calls, one template load, no remaining managed instances and an empty View DOM. `prefab-fixture.js.txt` is a test asset copied only into the standalone harness app.

## Emergency exit regression (2026-09-18)

Persistent hide and emergency close now remove the transient native button and cancel its timer. A new View starts with the button hidden; the emergency feature remains enabled by default and still requires a triple tap/click. Disabling it prevents the button from appearing. Tests cover initial state, exit/reopen, ordinary hide/reopen, disable and reenable. macOS: 215 checks passed; Android: 75 checks passed; iOS 26: 77 checks passed. iOS 17 compiled, but its simulator stalled at app launch, so this run does not validate iOS 17. TypeScript core: 130 tests passed, including iframe emergency-exit reuse and reenable tests.

The macOS harness also verifies that a shell prepared with an earlier size adopts the current Unity Game View dimensions before it is shown and preserves them while becoming visible. It also routes the native title-bar close button through the SDK lifecycle and verifies that the same persistent shell can be shown again.

The macOS universal library and Unity WebGL resources were rebuilt. Android AAR classes were compiled with `javac --release 8` against API 35 and replaced in the existing AAR, preserving its manifest and metadata (the offline Gradle build lacked AGP 8.6.0). Only production plugin classes are packaged.

## iOS resource response regression (2026-09-20)

The iOS fixture now returns physical `file://` URLs, matching C++ FileHelper responses, rather than pre-converted `balancy-local://` URLs. Each of 20 persistent View cycles decodes both a downloaded Documents image and a bundled image. The native transport converts response URLs using the configured persistent/StreamingAssets roots; HTTP/data URLs and unrelated message payloads stay unchanged. iOS 26.3 Simulator: 77 checks passed. This does not replace an IL2CPP build on a physical iPhone.

Run `python3 Tests/WebView/ios-local-resource-urls.test.py` on macOS for 16 Foundation checks of the production response mapper (single/batch responses, packaged paths, escaping, unchanged payloads and unconfigured roots).

## Android page-finish bridge regression (2026-09-21)

The Android fixture loads the production bridge from a `<script src>` in the shell,
matching Unity's persistent shell, and checks that `onPageFinished` preserves its
receiver. Previously the fixture injected the bridge after page completion and
missed the native fallback overwriting `_receiveMessageFromUnity`. The updated
fixture fails on the old Java implementation and passes 76 checks with the fix,
including 20 image/dependency preparation and clear cycles. This is a native
protocol regression test, not a full reproduction of a client's View scripts.

## Older Android build-tool compatibility (2026-09-21)

Build the production AAR with the Gradle Java 17 toolchain and Java 8 source/target.
Do not remove the compiler pin: javac 21 can emit unnamed `MethodParameters`
metadata even when targeting Java 8; R8/D8 3.3.75 crashes parsing the released
1.9.2 AAR. The rebuilt AAR passes that same D8.

The runner accepts `--d8-jar /path/to/r8.jar` to test an older consumer compiler:

```sh
python3 Tests/WebView/native/run-android.py --sdk "$ANDROID_SDK_ROOT" --java-home "$JAVA_HOME" --output /tmp/balancy-old-d8-test --aar WebView/Plugins/Android/balancywebview.aar --d8-jar /path/to/r8-3.3.75.jar --serial emulator-5554
```

R8 3.3.75 is available from Google's Maven repository at
`https://dl.google.com/dl/android/maven2/com/android/tools/r8/3.3.75/r8-3.3.75.jar`.
Use Java 17 for this regression run. Local result: 76/76 checks passed with the
rebuilt binary and old D8. This reproduces the reported D8 failure family; it is
not a full build with the client's exact Unity 2021.3.58/Gradle setup.

`BalancyIOSLinkSmokeBuild` also guards simulator architecture APIs with
`UNITY_2022_3_OR_NEWER`; older editors preserve their default architecture.
Compilation and player builds were subsequently validated with Unity 2021.3.45f2;
see the compatibility report below for results and runtime limits.

## Unity 2021 JNI initialization regression

Use `--aar WebView/Plugins/Android/balancywebview.aar --core-libs Plugins/Android`
to package the actual core libraries and test JNI VM retrieval on two threads.
The new Java entry point replaces the unavailable Unity 2021 `AndroidJNI.GetJavaVM`
API. The AAR and rebuilt core binaries must ship together. 79/79 checks passed on
the emulator, also in an APK built with Gradle 7.5.1 / AGP 7.4.2 / Java 11.
See `../../Integration/UNITY_2021_COMPATIBILITY.md` for the Unity build results, licensing
constraints and the limits of these checks.
