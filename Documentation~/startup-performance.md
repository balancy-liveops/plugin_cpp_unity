# Startup performance measurements

These measurements track the Android startup work that motivated the asynchronous
initialization changes. Keep the earlier columns when adding another iteration so
release notes can show the full progression.

Test environment: Unity 6000.5.1f1, `sdk_gphone64_arm64` emulator, clean install,
development instrumentation enabled. Network timings are inherently variable, so
the callback-handler and injected-payload measurements are the strongest comparison.

| Metric | SDK 1.8.2 baseline | Latest before fix | Iteration 1 | Iteration 2 | Iteration 3 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Android StreamingAssets preparation | 602 ms median | 726 ms | 763 ms | 715 ms median | **99 ms median (native lazy mount)** |
| Session start to first `OnDataUpdated` | 0.955 s median | 1.410 s | 2.712 s | 2.164 s median | **2.147 s median** |
| `InitCloudVersion` to cloud `OnDataUpdated` | 3.627 s median | 12.064 s | 1.824 s | 0.861 s median | **0.840 s median** |
| Cloud `OnDataUpdated` handler | not instrumented | 97.1 ms dispatch batch | 39.0 ms | 11.5 ms median | **8.2 ms median** |
| Largest logged native main-thread dispatch after first callback | not instrumented | 97.1 ms | 41.8 ms | 28.3 ms | **42.3 ms max** |
| Native JavaScript injection | about 1.98M script chars, compiled twice | 2,501,715 chars plus 471,437-char bridge | 2,510,326 chars plus 471,437-char bridge | 1,139-char shell + 1,660-char bootstrap | **unchanged** |
| Combined script CDN request when matching file is packaged | not supported | yes | yes | no | **no** |
| Asset preload relative to cloud callback | blocks update path | blocks update path | asynchronous, 6.783 s | asynchronous, 5.450 s median | **asynchronous, 5.086 s median** |
| Crash or ANR in measured run | customer reported | none in reference run | none | none | **none** |

The 1.8.2 cloud-flow samples were 2.242 s, 3.627 s, and 26.303 s; the table uses
the median because the last sample was a network outlier. Iteration 2 also uses the
median of three clean installs; ranges were 549-716 ms for packaged-file preload,
1.792-2.351 s to the first callback, 0.795-0.905 s for cloud sync, and 6.5-16.5 ms
inside the cloud callback handler. Iteration 2 resolves the
combined script by its branch-scoped versioned path even when `IsCMSUpdated` is false.
The WebView reads both the current bridge and combined script from files, so neither
multi-megabyte source is copied through C# or a native JavaScript injection.

Iteration 2 Android source logs:
`Builds/startup-pipeline-iteration2-final-run2.log`, `run3.log`, and `run4.log` in
the integration project.

Iteration 3 replaces the Android manifest walk and C#/JNI copy of every text asset
with an NDK `AAssetManager` provider. Packaged files are opened synchronously and
lazily by C++; persistent resource updates shadow packaged files. Across two clean
installs and one warm start, file preparation was 311.7 ms, 99.1 ms, and 93.7 ms
(the first process launch is the cold outlier). The native provider itself was ready
after 42.1 ms, 16.1 ms, and 16.7 ms. The unchanged persistent-WebView preparation
still dominates the first callback in this test project. A final clean validation after
the JNI lifetime fix completed file preparation in 78.8 ms and reached both callbacks
without a crash or ANR.

Iteration 3 Android source logs:
`Builds/android-native-assets-run1.log`, `android-native-assets-run2-warm.log`, and
`android-native-assets-run3-clean.log` in the integration project.
