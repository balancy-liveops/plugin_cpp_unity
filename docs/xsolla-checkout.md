# Xsolla sandbox checkout in Unity

This SDK branch adds an opt-in selector for paid `InitPurchase`, `InitPurchaseShop`, and both `InitPurchaseOffer` variants. Free purchases retain their existing path. Selecting the standard provider invokes the developer's existing hard-purchase callback.

## Setup

Build native libraries from the matching `balancy_cpp` checkout before enabling this feature. Existing binaries do not contain the new Xsolla exports. Use the existing macOS/iOS/Android/Unity WebGL build scripts; Unity WebGL requires its Unity-compatible Emscripten toolchain. This change does not overwrite pre-existing modified plugin binaries.

Configure the Xsolla platform, API key and webhook secret on the payments server, enable sandbox checkout, and configure signed combined webhooks. No merchant secrets or bearer tokens belong in Unity configuration.

```csharp
Balancy.API.Xsolla.Configure(new Balancy.XsollaOptions {
    Enabled = true,
    Country = "US", // ISO country needed to create checkout; not an eligibility filter.
    Language = "en",
    // NativeLabel = "Apple App Store", // Optional; otherwise derived from the app platform.
    PollIntervalMs = 2000,
    TimeoutMs = 240000
});
```

Configure before SDK initialization or after readiness. Profile readiness and application resume trigger recovery. Recovery may also be invoked explicitly:

```csharp
await Balancy.API.Xsolla.RecoverPurchases();
// Disable the selector; existing server orders are retained.
Balancy.API.Xsolla.Configure(null);
```

The C++ core uses the current SDK auth session and environment for every player API request. Existing network endpoint overrides apply to local development; SDK options do not expose tokens or payment keys. The current implementation accepts sandbox Pay Station URLs only.

## Presentation and completion

The selector is rendered by the shared bridge. An already open View keeps its owner and content. A hidden persistent shell is shown temporarily and hidden again; a closed transient WebView receives a temporary empty host and is closed when presentation finishes. Persistent mode is never enabled implicitly.

Mobile/native Unity opens validated checkout with `Application.OpenURL`. Unity WebGL reserves a browser window synchronously during the selection click through a request-scoped parent opener, then navigates it when checkout is prepared. A blocked popup does not create an order. Retrying reuses the same reserved request/order identity.

Every purchase first reserves its exact reward/context in C++ and saves the profile checkpoint. The server determines payment success. The wrapper checks request and order correlation, calls the dedicated verified C++ completion route, waits for durability, then acknowledges the server grant. C++ verifies its coherent inventory/grant checkpoint on native platforms; Unity WebGL additionally waits for `FS.syncfs(false)` before checkout and acknowledgment. A storage error leaves the order recoverable and unacknowledged.

Closing the selector after submitting checkout dismisses its presentation and polling, without canceling the provider transaction or launching the standard provider. The original init callback reports a pending result; resume/explicit recovery processes a later paid order. Changing session or profile prevents a delayed response from granting or reporting for another player.

The selector keeps its success state visible until **Done**. A purchased offer closing its game View releases that background owner without hiding the payment overlay; Done then restores the hidden shell or closes a transient renderer. External renderer closure during checkout dismisses presentation while paid tracking continues. Closing the pending selector deliberately stops that watch; recovery remains available.

Successful live completion preserves existing `OnHardPurchased*` callbacks. A live retry after a persistence error reports once after successful persistence. Startup recovery does not repeat callbacks for grants that were already applied. Callback delivery itself is not guaranteed across a process crash. Refunds requiring correction remain a reconciliation case; the SDK does not subtract already spent rewards automatically.

## Verification

- `scripts/check-xsolla-adapter.sh`: shipped C# adapter with deterministic native/transport/View boundary fixtures; uses .NET 9 and keeps build output outside Unity Assets.
- `node scripts/checks/xsolla-webgl-storage.cjs`: WebGL popup ownership, storage completion and fault handling.
- Unity EditMode: `Balancy.Tests.XsollaCheckoutTests`; regression suites `PurchaseLifecycleTests` and `PersistentWebViewTests`.

These tests make no Xsolla payment. A real sandbox charge, public webhook delivery, and platform builds still need their corresponding environment and matching native libraries.
