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
