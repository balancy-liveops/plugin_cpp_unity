# Persistent View ownership and platform parity — 2026-09-17

## Changes

- Managed instances are owned by the View even when detached or reparented outside its mount. Closing destroys their components and removes their DOM. Scriptless clones are tracked too. Raw prefab templates remain cached.
- Destruction unregisters objects before user callbacks, preventing recursive destruction from invoking `onDestroy` twice. Cleanup rejects new instantiation and visits remaining objects once instead of rescanning the whole registry per orphan.
- Standalone TypeScript now matches Unity's explicit prepare intent, deferred preparation until `onDataUpdated`, script-content comparison, and safe whole-context replacement. Hidden active views count as active. Clear ACK gates replacement; queued views and readiness callbacks survive it. Rolling scripts back cancels an unnecessary replacement. Public stop clears deferred intent even before initialization.
- Script source is injected once during preparation. Standalone load messages no longer carry base64 script bundles. Classic pages still receive their scripts.
- Fast preparation batching now covers root `init()` and classic component initialization too. Runtime batching remains 16 ms after readiness.
- `viewDisposed` performance logs expose `liveElementsAfterClear`, `liveInstancesAfterClear`, `pendingPreparationsAfterClear`, `pendingRequestsAfterClear` (expected zero), and `prefabTemplatesCached` (allowed nonzero).

## Memory evidence

Actual Chromium, 520 persistent cycles, each with a component, listeners, timer, tween, cancelled request and two prefab instances: one detached, one appended outside the View mount.

- 1 template load, 1 retained template, 1040 prefab component destructions.
- Zero retained probe objects after forced GC at every checkpoint. Probes reference DOM and components.
- DOM nodes/listeners stayed 18/7. Persistent heap after warmup: 2.32 → 2.37 MB over 20 → 520 cycles.
- Extra iframe/host tests also leave zero probes and the same DOM/listener counts. Heap becomes larger because the test loads additional SDK bundles into the parent page; it is not the same persistent-only workload.

TypeScript repository: `packages/bridge/scripts/check-memory.cjs --performance`; raw results in `packages/bridge/validation/prefab-ownership-browser-parity-2026-09-17.json`.

## Browser and WebGL checks

Actual iframe tests cover 40 standalone TypeScript opens with 4 deferred script-context replacements, and 45 Unity WebGL adapter opens across 3 shells. They verify single bundle execution, current revision, clear ACKs, registry/DOM cleanup, and GC reachability. This exercises the production iframe adapter, not a complete Unity-generated WebGL game or a real backend.

Unit suites: bridge 92, TypeScript core 128, WebGL jslib 4 passed. Persistent C# state fixture: 16 passed. No C# production code changed in this pass. An optional whole-SDK compiler check could not run with the installed Unity 2021 DotNetSdk layout; the previously validated C# implementation is unchanged.

## Native checks

Mobile harnesses use production native plugins plus the rebuilt bridge, local PNG decoding and mocked backend responses. Each now creates 40 prefab instances, including detached/reparented ones, and checks destruction, retained template cache, empty instance registry and DOM. Platform results are saved alongside the fixtures in `native/validation/*ownership*`.

Results: Android 12/API 31: 66 checks; Android 16/API 36: 66; iOS 17: 63; iOS 26.3: 61; macOS: 181, all passed. Counts include repeated cycle assertions, not unique scenarios. The iOS 17 run includes two extra navigation-readiness assertions added after the iOS 26 run.

An iOS test had assumed a message would return within 300 ms; under concurrent simulator load that assertion failed. It now waits for the actual expected message with a bounded five-second timeout. The iOS 17 cold run also exposed premature test injection before file navigation finished; the harness now waits for the requested document, and its repeat run passed.

Native source/binaries are unchanged; bridge and WebGL resources are rebuilt. Physical-device Unity IL2CPP, renderer-process/GPU memory and customer-specific scripts still need staging validation.

## Runtime batching experiment

`packages/bridge/scripts/benchmark-runtime-batching.cjs` compares 0/4/8/16 ms using synchronous mock replies. For same-turn requests all delays preserve one packet per burst. With requests spread across tasks, this run produced 80/40/23/20 packets respectively. Sequential requests cannot be combined at any delay.

A global zero delay is therefore a latency/transport-volume tradeoff, not a free optimization. Keep 16 ms for runtime until real interaction traces show a better choice. View preparation already uses zero-delay scheduling while preserving batching and budgets. This experiment does not measure native CPU cost.

## Customer script contract

SDK-created instances are now cleaned even without the expected parent. Arbitrary DOM outside the mount still belongs to the creator. Register `balancy.onViewDispose` cleanup for external nodes, observers, RAF loops, workers, sockets and global references. Capture `balancy.viewSignal` before awaits and check cancellation before creating later UI. An unreachable detached object is collectible; attaching it to `document.body` or storing it globally keeps it alive.

Examples and diagnostics are documented in the TypeScript bridge README. Next staging check: open every customer View once, then repeatedly alternate a fixed subset; inspect the cleanup counters and cold/warm timing separately. Use physical-device profilers for total native memory after the browser checks pass.
