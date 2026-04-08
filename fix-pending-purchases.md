# Fix: Stale OfferInstanceId Causes Null Offer on Purchase Completion

## Problem

When `FinalizedHardPurchase` runs for a `PurchaseType.Offer`, it calls `FindOfferInfo(productInfo.OfferInstanceId)` which returns null. The `OfferInstanceId` stored in the `PendingPurchase` is from a previous session and doesn't match any active offer in the current session.

### Root cause chain

1. A previous purchase attempt created a `PendingPurchase` entry with `OfferInstanceId = "old-id"` and `Status = WaitingForStore`.
2. The app was stopped/crashed before the store responded.
3. Commit `9bc2a93` intentionally stopped deleting `WaitingForStore` entries on startup — the store might deliver the purchase on `OnPurchasesFetched`, and deleting the record would cause the user to lose their paid purchase.
4. In the new session, a new offer instance is created with a different `OfferInstanceId = "new-id"` (instance IDs are per-session).
5. User purchases the same product again — a new `PendingPurchase` is appended with `OfferInstanceId = "new-id"`.
6. Store completes — `ProcessPendingOrder` — `GetPendingPurchaseByProductId(productId, WaitingForStore)` — `List.Find()` returns the **first** match — picks the stale entry with `"old-id"`.
7. `FinalizedHardPurchase` — `FindOfferInfo("old-id")` — null — purchase fails silently.

### Why the existing `WaitingForStore` preservation is correct

Preserving `WaitingForStore` entries across sessions is necessary. If the user paid but the app crashed before the store callback, the store will re-deliver the purchase on next launch via `OnPurchasesFetched` → `ProcessPendingOrder`. Deleting the pending record means there's nothing to match and the purchase is silently lost.

### The actual bugs

1. **`List.Find()` returns first match, not most recent** — `GetPendingPurchaseByProductId` should return the most recent entry, not the oldest stale one.
2. **Duplicate entries are allowed** — the duplicate guard in `AddPendingPurchase` was commented out. When a new purchase for the same product is initiated, a duplicate is added without removing the stale entry.
3. **`FinalizedHardPurchase` has no fallback** — if the offer is gone (deactivated or stale instance ID), the purchase fails entirely even though all necessary data (`StoreItemUnnyId`, `OfferUnnyId`) is available to complete it.

## Changes

### PendingPurchaseManager.cs (BalancyPayments)

**Replace stale `WaitingForStore` entries in `AddPendingPurchase`**: When a new purchase is initiated for the same `ProductId`, any existing `WaitingForStore` entries for that product are removed before adding the new one. This is safe because the store doesn't care which pending record exists — only that one exists with the matching `ProductId`. The new entry has the correct `OfferInstanceId` for the current session. This differs from the old approach (which deleted ALL `WaitingForStore` on startup) — it only removes entries for the same product and only when a new purchase is being initiated.

**Use `FindLast` instead of `Find`**: Both `GetPendingPurchaseByProductId` overloads now use `FindLast` as a safety net. If duplicates somehow still exist, the most recently added entry (with the current session's `OfferInstanceId`) is returned instead of the oldest stale one.

### API.cs (Balancy Runtime)

**Add StoreItem fallback in `FinalizedHardPurchase`**: When `FindOfferInfo` returns null for `PurchaseType.Offer`, the code now falls back to `HardPurchaseStoreItem` via `productInfo.GetStoreItem()`. The `StoreItemUnnyId` stored in the pending purchase resolves independently of the offer lifecycle, so the user gets what they paid for. The same fallback is applied to `PurchaseType.OfferGroup`.

**Remove debug logs**: Removed `Debug.LogWarning("1>> callback=...")` and the offer dump loop that were added during investigation.

### Actions.cs (Balancy Runtime)

**Remove debug logs**: Removed `Debug.LogError("CREATE 1 >> ...")` and `Debug.LogError("CREATE 2 >> ...")` from the `BalancyProductInfo` constructors.

## Files modified

| File | Repository | Changes |
|------|-----------|---------|
| `PendingPurchaseManager.cs` | `plugin_cpp_unity/Assets/BalancyPayments/` | Replace stale entries + FindLast |
| `Runtime/API.cs` | `plugin_cpp_unity/Assets/Balancy/` | StoreItem fallback + debug log cleanup |
| `Runtime/Actions.cs` | `plugin_cpp_unity/Assets/Balancy/` | Debug log cleanup |
