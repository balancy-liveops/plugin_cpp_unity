# Native WebView regression harnesses

These run the production Java/Objective-C++ implementations on real Android WebView / WKWebView, without requiring Unity or a backend. The mobile apps also inject the built production bridge and open/clear 20 views containing localization and a local PNG. Host resource responses are mocked; this is not a Unity IL2CPP or C++ backend integration test.

Fixtures deliberately use `.java.txt` and `.mm.txt` extensions so Unity cannot accidentally compile test Activities or `main()` into a client build.

## Android

Boot an emulator, then run from the Balancy SDK root:

```sh
python3 Tests/WebView/native/run-android.py --sdk "$ANDROID_SDK_ROOT" --java-home "$JAVA_HOME" --output /tmp/balancy-android-test --serial emulator-5554
```

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
