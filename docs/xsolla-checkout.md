# Xsolla checkout in Unity

This SDK branch adds an opt-in selector for paid `InitPurchase`, `InitPurchaseShop`, and both `InitPurchaseOffer` variants. Free purchases retain their existing path. Selecting the standard provider invokes the developer's existing hard-purchase callback.

## Setup

This branch ships native plugin libraries rebuilt from the matching `balancy_cpp` checkout, including the Xsolla exports. When updating the core, rebuild plugins with the existing macOS/iOS/Android/Unity WebGL scripts; Unity WebGL requires its Unity-compatible Emscripten toolchain. User edits in `Prefabs/BalancyLauncher.cs` are preserved separately from the integration commits.

Configure the Xsolla platform, API key and webhook secret on the payments server, enable sandbox checkout, and configure signed combined webhooks. No merchant secrets or bearer tokens belong in Unity configuration.

```csharp
// Update this flag from your verified host/storefront payment-program integration.
// Country alone does not establish permission to offer an alternative payment.
bool alternativePaymentAllowed = false;
Balancy.API.Xsolla.Configure(new Balancy.XsollaOptions {
    Enabled = true,
    CanOfferAlternativePayment = () => alternativePaymentAllowed,
    Country = "US", // ISO country needed to create checkout; not an eligibility filter.
    Language = "en",
    // NativeLabel = "Apple App Store", // Optional; otherwise derived from the app platform.
    PollIntervalMs = 2000,
    TimeoutMs = 240000
});
```

`CanOfferAlternativePayment` defaults to false for every host, including Unity WebGL. A missing, false, or throwing callback preserves the standard provider without preparing an Xsolla request or opening the selector. Set it true only when the host/storefront integration has verified that the payment program is available; profile country conditions and `Country` do not provide that verification.

The published system package must contain a currently active `Payments.AlternativePaymentRule` document, derived from `SmartObjects.ConditionalTemplate`. Condition and Priority are inherited, not duplicated. Provider is Xsolla (50); `MinPriceUsd`, All/IncludeOnly product selection, base `IncludedProducts`, and higher-priority `ExcludedProducts` are checked in the shared core. It selects one current document by the normal condition/priority mechanism on each paid init, including the initial value and after profile/config updates. If that selected document is absent or ineligible, the standard provider runs directly; lower-priority rules are not searched for a replacement. Export=false takes effect after deployment and config delivery, not when a draft checkbox changes. Pending orders keep their frozen snapshot and remain recoverable when a campaign ends.

`Bonus` is an ordinary reference to a polymorphic `Payments.PurchaseBonus` component. `PurchaseBonusFixedReward` contains an injected Reward; `PurchaseBonusPerSpend` contains Item and fractional ItemsPerUsd. The shared core/server calculation rounds to the nearest integer with halves rounded up (away from zero for the nonnegative amounts): 0.2 × $4.99 = 0.998 → 1, and 0.7 × $5 = 3.5 → 4. Unknown bonus types and invalid data do not produce a promised bonus. Unity displays the verified quote counts unchanged; it does not independently calculate or grant bonuses.

`ChoiceView` is preserved as a typed nullable View asset field. This stage uses the system selector as the safe fallback; the merchant custom-view/controller binding remains deferred. No new merchant JavaScript controller API is provided in this stage.

Configure before SDK initialization or after readiness. Profile readiness and application resume trigger recovery. Recovery may also be invoked explicitly:

```csharp
await Balancy.API.Xsolla.RecoverPurchases();
// Disable the selector; existing server orders are retained.
Balancy.API.Xsolla.Configure(null);
```

The C++ core uses the current SDK auth session and environment for every player API request. Existing network endpoint overrides apply to local development; SDK options do not expose tokens or payment keys. The payments server selects sandbox or production checkout. Unity accepts only HTTPS Pay Station URLs at the exact official hosts `sandbox-secure.xsolla.com` and `secure.xsolla.com`, with the expected path and token. Keep sandbox enabled on the server while testing.

## Presentation and completion

The selector is rendered by the shared bridge. An already open View keeps its owner and content. A hidden persistent shell is shown temporarily and hidden again; a closed transient WebView receives a temporary empty host and is closed when presentation finishes. Persistent mode is never enabled implicitly.

Mobile/native Unity opens validated checkout with `Application.OpenURL`. Unity WebGL reserves a browser window synchronously during the selection click through a request-scoped parent opener, then navigates it when checkout is prepared. A blocked popup does not create an order. Retrying reuses the same reserved request/order identity.

An available Xsolla purchase first reserves its exact reward/context and selected rule/version in C++ and saves the profile checkpoint. Before opening the selector, Unity requests an authenticated quote for that reservation and waits for its durable storage. The readonly selector shows the verified USD amount and localized bonus names/counts. Quote failure keeps an explicit standard-provider choice available without showing an unverified bonus. Checkout sends the reserved request identity, country and language; the native core injects the frozen product/context/quote identity. Main and bonus rewards share the existing verified grant ledger, so retries retain the original promised offer. The server determines payment success. If Xsolla preparation is unavailable, the selector disables Xsolla with an explanation and keeps the standard provider and cancellation available; choosing the standard provider remains explicit. The wrapper checks request and order correlation, calls the dedicated verified C++ completion route, waits for durability, then acknowledges the server grant. Success UI and definitive callbacks require `acknowledged: true` with the exact grant ID. Transport, conflict, reconciliation, and malformed acknowledgment errors leave the applied ledger intact and the purchase recoverable; retry confirms that same grant before reporting once. C++ verifies its coherent inventory/grant checkpoint on native platforms; Unity WebGL additionally waits for `FS.syncfs(false)` before checkout and acknowledgment. A storage error leaves the order recoverable and unacknowledged.

Closing the selector after submitting checkout dismisses its presentation and polling, without canceling the provider transaction or launching the standard provider. The original init callback reports a pending result; resume/explicit recovery processes a later paid order. Changing session or profile prevents a delayed response from granting or reporting for another player.

The selector keeps its success state visible until **Done**. A purchased offer closing its game View releases that background owner without hiding the payment overlay; Done then restores the hidden shell or closes a transient renderer. External renderer closure during checkout dismisses presentation while paid tracking continues. Closing the pending selector deliberately stops that watch; recovery remains available.

Successful live completion preserves existing `OnHardPurchased*` callbacks. A live retry after a persistence error reports once after successful persistence. Authenticated unacknowledged recovery can report a verified `applied:false` grant after a prior checkpoint or acknowledgment failure. An already acknowledged historical receipt does not create a new report. Reporting is deduplicated per active SDK scope, not across devices, processes, or separately initialized SDK instances. Two instances with the same replicated applied ledger may each emit their local `OnHardPurchased*` completion notification after acknowledgment. These wrapper notifications do not apply inventory or invoke built-in revenue analytics; consumers aggregating purchases or revenue must deduplicate by the stable `PaymentInfo.OrderId`. Original init callbacks are never revived by recovery. Callback delivery itself is not guaranteed across a process crash. Refunds requiring correction remain a reconciliation case; the SDK does not subtract already spent rewards automatically.

`RecoverPurchases()` processes independent paid orders before bounded deferred readback and isolates individual order failures. If one or more attempted orders fail, it rejects with `InvalidOperationException("xsolla_recovery_incomplete")`; the inner `AggregateException` contains their individual errors. The top-level error message no longer equals a single underlying provider error. Automatic recovery logs this summary; applications can inspect the inner errors for diagnostics. SDK stop or session changes still abort recovery immediately.

## Verification

- `scripts/check-xsolla-adapter.sh`: shipped C# adapter with deterministic native/transport/View boundary fixtures; uses .NET 9 and keeps build output outside Unity Assets.
- `node scripts/checks/xsolla-webgl-storage.cjs`: WebGL popup ownership, storage completion and fault handling.
- Unity EditMode: `Balancy.Tests.XsollaCheckoutTests`; regression suites `PurchaseLifecycleTests` and `PersistentWebViewTests`.

The shipped plugin artifacts are built from the matching core source. The automated C# and EditMode tests verify adapter contracts, lifecycle, URL validation, and storage barriers with fixtures; built artifacts do not establish on-device payment behavior. These tests make no Xsolla payment. An actual paid sandbox checkout on each target device and public signed webhook delivery still require end-to-end verification in the corresponding environment.


The TypeScript Node adapter enables durable Xsolla payment snapshots on POSIX (macOS/Linux), syncing the file and directory entries before acknowledgement. Its Node Windows Xsolla path is unavailable because it cannot guarantee rename durability on power loss; ordinary cache usage still works. This Node adapter limitation does not disable native Unity payments on Windows.

A chooser held open past the verified quote expiry cannot submit checkout. Unity rechecks expiry before the attempt and after its asynchronous storage flush, closes the unused popup, discards and flushes the reservation, and reports `Offer expired; start the purchase again`. Start a new purchase to obtain a fresh quote. Existing submitted orders and recovery continue using their frozen snapshot independently of quote expiry.
