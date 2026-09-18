# WebView: native fixes, preparation latency, and regression coverage

Date: 2026-09-16. Branch: `codex/persistent-webview-fixes`.

## Measured optimization

During UI preparation, batching now gathers the current JavaScript turn and its microtasks, then dispatches without the extra 16 ms display-frame wait. Normal runtime batching remains 16 ms; count/byte budgets and large-response isolation are unchanged. Nested preparations share the mode. Aborting a view releases it immediately, even if an image decode never settles.

Synthetic bridge benchmark: 60 uncached localization keys per view, 1 ms mock host response, 5 warmup + 35 measured windows:

| Metric | Before | After |
|---|---:|---:|
| Median preparation | 24.28 ms | 7.86 ms |
| p95 | 31.81 ms | 11.26 ms |
| Requests per view | 60 | 60 |
| Packets per view | 1 | 1 |

This isolates bridge scheduling in JSDOM. It is not a claim of 3x faster total customer window startup. Reproduce with `node packages/bridge/scripts/benchmark-preparation.cjs` in the TypeScript repository.

All native platforms now show immediately when both configured presentation values are zero, avoiding unnecessary timer/animation scheduling. Nonzero custom animations remain supported.

Image diagnostics now collect computed styles only for the four retained samples, instead of querying every image and discarding most results. The regression test verifies 4 style queries for 50 images. Sample callbacks are evaluated immediately and never retained.

New bounded timing stages `bridgeQueue.action.N` and `bridgeRoundTrip.action.N` separate queueing from response wait. Action 10 is localization; 11 is image URL. Round trip includes transport, native queue/work, aggregate-batch waiting, and response delivery; it does not isolate C++ CPU time. Parallel spans overlap and must not be summed as wall time. Cancellation is counted separately from actual replies.

## Bugs fixed

- Android repeated initialize added extra containers to the Activity. Initialization is now idempotent; replacing an Activity detaches the previous container.
- Android queued injection/message closures could use the next WebView after replacement. They now retain the intended target identity and reject retired targets.
- Android JS interfaces reject messages from retired views; close removes the interface, delegates, and pending page loading before destruction.
- Android null owners and JSON containing backslashes, quotes, newlines, or Unicode are handled safely.
- iOS and macOS ignore late script messages from closed contexts. macOS close clears delegates, view hierarchy and retained WebKit references.
- iOS native serialization accepts numeric/null JS messages instead of raising an Objective-C exception for a non-container JSON root.
- macOS stress testing reproduced EXC_BAD_ACCESS in AppKit window transform cleanup. ARC-managed windows now explicitly disable automatic release-on-close and redundant AppKit window animations. The full stress fixture subsequently passed three consecutive runs.
- Late sprite URL/decode completion can no longer overwrite a newer `setImage` for the same element.
- Unity WebGL obsolete preparation/injection failures cannot fail the replacement shell.

Android baseline reproduced three failing checks before the fixes: repeated-container growth, delayed zero-animation show, and stale injection entering the replacement. See raw baseline/after results under `native/validation/`.

## Validation

| Layer | Result |
|---|---|
| Bridge unit/integration | 88 tests passed, typecheck passed |
| TypeScript core | 119 tests passed, typecheck/build passed |
| Unity Editor EditMode | 33 tests passed (includes 16 state tests) |
| WebGL plugin | 4 tests passed |
| Android 12/API 31 | 66 native/integration checks passed |
| Android 16/API 36 | 66 native/integration checks passed |
| iOS 17.0 | 59 native/integration checks passed |
| iOS 26.3 | 61 native/integration checks passed, including added scalar-message cases |
| macOS | 181 checks per run; three consecutive runs passed |

Native check counts include repeated assertions across lifecycle cycles; they are not counts of unique test scenarios. Mobile harnesses use the actual plugin and built bridge, with a mock resource host. They exercise local image paths across changing View base URLs, UTF-8 transport, code/cache reuse, and clearing.

Android AAR and universal macOS WebView dylib rebuilt; iOS device source compilation checked. Bridge and Unity WebGL adapter rebuilt and synchronized into SDK resources and local StreamingAssets. C++ core binary was not changed.

## Memory evidence

- Chromium: 520 persistent cycles; no retained view probes after forced GC. DOM documents/nodes/listeners stayed 3/15/7. Heap after persistent cycles was approximately 2.24 → 2.29 MB, with a plateau. Additional iframe tests ended around 2.60 MB.
- Android: retired native WebViews became unreachable after GC; container count stayed fixed across replacements.
- iOS/macOS: weak references to closed controllers cleared. iOS child-controller/view counts returned to baseline. Embedded macOS timers, CGContext, pixel buffers and offscreen windows were released.

These are positive regression checks, not proof of zero leaks in every client script, physical-device GPU driver, or separate renderer process. Physical Unity IL2CPP runs with the customer's heavy views remain the next validation layer.

## Remaining work

- C++ script compilation API still cannot distinguish an empty script-free project from an unavailable downloaded bundle. Local OnDataUpdated skips downloads; cloud completion can still report a failed script download. A success/revision status would close this gap.
- Use the new queue/round-trip metrics in customer logs to investigate remaining localization latency and batch head-of-line waiting.
- View preloading without instantiation and optional manifest localization/animation metadata remain separate design work. They are not required for these improvements.
