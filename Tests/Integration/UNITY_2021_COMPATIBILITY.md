# Unity 2021 compatibility check — 2026-09-21

## Initial 2021.3.58f1 attempt (superseded by the 2021.3.45f2 results below)

Installed editor: **2021.3.58f1**, Apple Silicon, Android and iOS modules installed.
Starting a separate copy of the current demo project with this editor shows:
“This build of Unity 2021 is part of an Extended LTS release, which requires either
 a valid Unity Industry or Unity Enterprise license.” The editor stops before import.
No license settings were changed. The original Unity project was not downgraded.
No Unity Android player, Unity iOS export, final Xcode link, or demo Play Mode run
has been validated with this editor. Do not treat the checks below as substitutes.

## Reproduced and fixed

1. **Editor API:** the 1.9.2 `BalancyIOSLinkSmokeBuild.cs` fails with CS0117 / CS0103
   for `simulatorSdkArchitecture` / `AppleMobileArchitectureSimulator`, using the
   installed 2021.3.58f1 Roslyn compiler and reference assemblies. The guarded file
   compiles successfully.
2. **D8:** the 1.9.2 AAR fails with `java.lang.NullPointerException` in
   `:app:desugarDebugFileDependencies`. Reproduction uses Unity's Gradle **7.5.1**,
   Unity's template AGP **7.4.2**, and Temurin **Java 11** for Apple Silicon.
   The fixed AAR builds successfully with those tools. Producer javac is pinned
   to 17 with Java 8 source/target; a class version check alone missed the javac 21
   MethodParameters metadata incompatibility.
3. **Additional Android compile error:** `AndroidJNI.GetJavaVM` does not exist in
   the 2021.3.58f1 reference assembly. C# now obtains the VM through
   `com.balancy.core.NativeRuntime.getJavaVM`, backed by a real JNI call to
   `libBalancyCore.so`. Direct AAssetManager reads remain; no StreamingAssets copy
   fallback was introduced. The Java class has consumer ProGuard keep rules.

## Validation

- Production Balancy.WebView, Balancy, Balancy.UI, Balancy.CheatPanel compiled
  against Unity 2021.3.58f1 APIs with Editor, Android and iOS defines.
- Balancy.Editor compiled with Editor defines: **13 assembly/target combinations**.
- Android C++ Release libraries rebuilt: arm64-v8a, armeabi-v7a, x86_64.
- Fixed AAR plus real C++ libraries: **79/79 native harness checks passed**,
  first using standalone old D8 3.3.75, then using the APK produced by AGP 7.4.2.
  This includes VM retrieval on two threads, native WebView lifecycle, 20 bridge
  View cycles, image decoding, caching, DOM cleanup and retired-view collection.
- The harness mocks UnityPlayer and resource responses; it does not test Unity
  IL2CPP or prove end-to-end packaged-file reads from the C++ helper.
- The Gradle fixture uses compileSdk/build-tools 34 and targetSdk 31. Initial
  compileSdk 35 also hit the old AGP AAPT2 resource-table incompatibility; lowering
  only the fixture compileSdk isolates the SDK D8 failure. This is not validation
  of modern store target requirements.
- Unity's bundled Java 11 is Intel-only and cannot run on this host as configured;
  Temurin Java 11 for Apple Silicon was downloaded from Adoptium to a temporary
  folder. The Unity installation was not modified.

## Re-run API checks

```sh
python3 Tests/Integration/validate-unity2021-api.py \
  --unity-contents /path/to/2021.3.58f1/Unity.app/Contents \
  --output /tmp/balancy-unity2021-api
```

This is an API compile check using references from the installed 2021 template,
not Unity's asset importer or player build pipeline.

For the Android harness add `--aar WebView/Plugins/Android/balancywebview.aar`
and `--core-libs Plugins/Android` to `Tests/WebView/native/run-android.py`.
`--d8-jar /path/to/r8.jar` selects an older D8 for regression checks.

## Release requirements / remaining checks

Ship the C# change, AAR and all three updated Android C++ libraries together.
Older C++ binaries do not export the new JNI method. No version/tag/push was made.

The initial remaining work was to use an eligible editor, adapt the demo-only
Unity 6 package dependencies in the isolated copy, and validate actual players.
The follow-up results below supersede that initial blocker. The package
currently declares Unity 2022.3; that declaration has not been lowered on the
strength of partial checks.

Local logs and test project: `/tmp/balancy-unity2021-check/`.
The copied project now contains the fixes; baseline failure logs are preserved.

## Follow-up: actual Unity 2021.3.45f2 editor and players

The Personal license works with **2021.3.45f2**. Its Intel-only Package Manager
and Java initially failed on this Apple Silicon host. Rosetta 2 was installed
with the user's explicit authorization to accept Apple's license agreement.
The editor and its bundled Java 11 now launch successfully.

### Isolated demo adaptation

Only `/tmp/balancy-unity2021-check/project` and `project-ios` were adapted:

- Removed Unity 6-only built-in adaptiveperformance, physicscore2d and
  vectorgraphics modules; removed unused AI Navigation, Collab and development
  feature dependencies.
- Set Addressables 1.21.21, Timeline 1.6.5, UGUI 1.0.0, TMP 3.0.6 and Test Framework
  1.1.33. Unity Purchasing remains 5.1.2.
- Android fixture uses IL2CPP/ARM64, minSdk 26, targetSdk 34 and the 2021.3.45f2
  bundled JDK/SDK/NDK/Gradle. Application ID: `com.balancy.compat2021.tests`.
- The current demo scene `Assets/Game/NewTest.unity` was used.
- For runtime cycles only, automatic prepare in MainUI was disabled in the copy,
  and a temporary coroutine opened View 1421 three times normally, then called
  PrepareWebView and opened it three more times, closing between each opening.
  No purchase/reset actions were invoked.

The real project was not downgraded. Test-only fixture changes are not part of the
SDK package or release changes.

### Completed checks

- Real editor import succeeds with the compatible demo dependencies.
- Restoring the 1.9.2 Editor helper in the copy reproduces the client's exact
  CS0117/CS0103 errors inside Unity. Restoring the fix clears them.
- Unity EditMode: **162/162 passed** (`editmode.xml`).
- Unity PlayMode Section tests: **7/7 passed** (`playmode.xml`).
- Demo runtime in Unity Editor: **6/6 View 1421 open/close cycles passed**,
  3 normal and 3 persistent (`editor-runtime.log`). This asserts shown callbacks
  and cycle completion, not a pixel-perfect visual comparison.
- **Android IL2CPP APK built successfully** with bundled Java 11 / Gradle 7.5.1 /
  AGP 7.4.2. Original APK started on an ARM64 emulator and reached local CMS/profile
  readiness and cloud update without JNI/library-load exceptions.
- **iOS device**: Unity export and final unsigned Xcode Debug build/link succeeded.
- **iOS Simulator x86_64**: Unity export and final unsigned Xcode Debug build/link
  succeeded. The binary could not be installed into the ARM64 iOS 26.3 simulator
  (architecture mismatch). Unity 2021's `libiPhone-lib.dylib` is x86_64 only.

Both iOS builds used the installed Xcode/iOS 27 SDK. They do not establish runtime
behavior on physical devices or the minimum supported iOS version. The package's
Unity minimum declaration remains unchanged pending the release decision.

### Android runtime follow-up: unresolved first-launch failure

The instrumented Android player passed **6/6 View 1421 open/close cycles** on a
warm restart (three normal, three persistent). The returned HTML path was
`/android_asset/Balancy/.../1421_1789743934681/index.html`, confirming direct
packaged asset access. See `android-runtime-retry.log`.

However, the first launch failed to resolve View 1421. Clearing only the disposable
test application's data reproduced it with the same APK:

- 12:38:07.747: local OnDataUpdated, CMS/Profile ready.
- 12:38:07.748: test calls GetObjectView(1421).
- 12:38:09.514: callback produces no usable path.
- 12:38:10.327: cloud OnDataUpdated finishes.

See `android-runtime-clean-full.log`. This is not a 40-second fixture timeout.
Catalog replacement is a leading hypothesis: DataLoader resets its preload
generation, and DataObjectsManager rejects responses belonging to the previous
catalog. The log alone does not prove the exact branch taken. No speculative
production change was made to this behavior. This remains an open runtime issue;
the compatibility build results must not be presented as full release approval.

An additional attempt to boot an x86_64 iOS 17 simulator stalled during simulator
startup. It was shut down. Neither simulator build was runtime-validated.

The subsequent clean-install **offline** run also failed to resolve 1421
(`android-runtime-clean-offline.log`). Network access was disabled only in the
disposable emulator and restored afterward. Therefore cloud catalog replacement
is not a sufficient explanation. APK inspection confirms packaged index.html and
manifest.json for 1421, 1218, 1217, 1624, 1419 and 1756, plus ID-prefixed assets
for every declared image/font dependency; corresponding View ZIP files are absent.
The preload path checks/downloads the ZIP before examining extracted HTML. Exact
catalog versions and the failing dependency still need tracing; presence of an
ID-prefixed asset alone does not establish a matching catalog version.

### Maintainer clarification

The maintainer confirmed that the development backend still lacks the known
manifest fix and expects the offline View 1421 failure. No client-side workaround
was introduced during the Unity compatibility work. Online warm-run cycles passed;
the initial online empty-path observation is retained above as an observed result,
without claiming that its exact cause was established.
