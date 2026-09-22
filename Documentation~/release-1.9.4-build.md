# SDK 1.9.4: Jenkins build handoff

This branch contains the source baseline for the upcoming 1.9.4 package.
It is not a completed player-tested release. Existing native binaries inherited
from main must be replaced by CI builds of the selected final C++ source.
Do not publish a Unity release tag until the complete artifact set passes validation.

## Source selection

- Unity package: this `dev` revision (includes Unity 2021 compatibility,
  task lifecycle integration and event removal callbacks).
- C++ core: `balancy_cpp` tag `v1.9.4`; resolve it once to a full commit SHA
  before starting platform jobs. This includes Android JNI commit `bfbab87b`,
  extensible tasks and event removal lifetime fixes.
- TypeScript/bridge: `plugin_cpp_typescript` main revision containing
  `6fe11f3` (event removal callbacks), or a subsequently approved revision.
  Record the full selected SHA before compilation.
- Output package version: `1.9.4`.

Keep source revisions and package version as separate build inputs. Do not move
published tags to label different binaries. Save source SHA, compiler/SDK versions,
artifact SHA-256 and build logs together.

## Required artifacts

Rebuild Android Core for arm64-v8a, armeabi-v7a and x86_64; iOS device arm64 and
simulator arm64/x86_64 XCFramework; macOS universal Core; Windows x86_64 Core;
Unity WebGL Core; Android WebView AAR; macOS WebView plugin; TypeScript WASM and
its web/node JS glue; and generated WebView bridge/WebGL bundles.

The package root is the directory containing this package.json. In a standalone
Git clone, copy into `Plugins/` and `WebView/` directly. The local demo project's
`Assets/Balancy` prefix is not part of the GitHub package layout.

Android AAR must be produced with the checked-in JDK 17 toolchain and Java 8
target, include `com.balancy.core.NativeRuntime` and its consumer keep rule.
All three Android Core libraries must define the dynamic JNI export
`Java_com_balancy_core_NativeRuntime_getJavaVM`.

Keep the complete `Plugins/iOS/BalancyCore.xcframework`, both simulator architectures,
and `Info.plist`. Windows interop expects `Plugins/Windows/x86_64/libBalancyCore.dll`.

## Validation and destination

After replacing every native artifact, run from the package root:

```sh
python3 .github/scripts/check_task_exports.py --plugins Plugins
```

This checks actual symbol/export tables for the task ABI on every shipping slice.
Also check the Android JNI export, AAR Java 8/D8 compatibility, Unity Android/iOS/
WebGL linking, and runtime startup/View lifecycle on the supported targets.
Symbol checks and successful linking are not device runtime validation.

Commit CI artifacts to `dev`. Unity `main` and Unity release tags are intentionally
not published by this preparation step; promotion is a later explicit action.
