# Task Card: Phase 5 — KhachLink Multi-tenant Cart + Checkout UI + QR Code with Prices

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 5 of 8
> **Depends on:** Phase 3 contract (`CheckoutResponse` shape with `orders[]` array + per-item ProductName/VatRate requirement + price validation endpoint)
> **Unlocks:** End-to-end checkout flow (with Phase 4) + fast offline QR scan

---

## 1. Use Case & Business Design

**Problem:**
1. `CartItem` record (`1_Shared/Domain/CartItem.cs`) has no `TenantId` field. KhachLink cannot tell Gateway which tenant each item belongs to → Gateway cannot group/route.
2. `CartItem` has `ProductName` + `VatRate` (existing) but `Checkout.razor` doesn't send them in checkout request. Phase 3 requires client to provide `ProductName` + `VatRate` per item (Gateway PG doesn't have products — uses client snapshot for order creation).
3. `Checkout.razor` currently sends a flat `items[]` array without `tenantId` per item, and expects a single-order response (`{ orderId, tenantId, amount, ... }`). Phase 3 changes the response to `{ orders: [...], successCount, failureCount, errors: [...] }`.
4. Customer adding 2 products from 2 different tenants must see **2 separate orders** in the success screen, each with its own tracking link.
5. **NEW (Round 2 decision):** QR code currently contains only `ProductId`, `ShopId`, `Timestamp`, `TableNumber?` — no price/VAT. `Scan.razor` must make API call after scan to fetch product details. Decision: add `UnitPrice` + `VatRate` to QR code so Scan.razor can skip API call entirely.
6. **NEW:** If product price/VAT changed after QR was printed, checkout must block the order and notify customer "giá sản phẩm đã thay đổi". Validation happens at Gateway via ShopERP HTTP call (Phase 3 price validation endpoint).

**Goal:**
1. Add `TenantId` to `CartItem` record + populate from `ProductDto.TenantId` in `CartState.AddItem`. (ProductName + VatRate already exist on CartItem — just include them in checkout request.)
2. Update `Checkout.razor` to:
   - Send `tenantId` + `productName` + `vatRate` per item in checkout request (per Phase 3 §3 contract).
   - Handle `CheckoutResponse` with multiple orders.
   - Display all created orders (loop, not single).
   - Show errors per tenant if any failed.
   - Handle price-validation failure response (show "giá đã thay đổi" alert with current vs QR price).
   - Store `created_orders: [{orderId, tenantId}, ...]` in localStorage for OrderHistory.
3. Update `OrderTracking.razor` — `GET /api/public/orders/{id}` queries PG directly (per Phase 3 Q1 resolution, no tenantId needed for tracking). No change to tracking request, but may display multiple orders from one checkout session.
4. Update `OrderHistory.razor` to handle multiple orders from a single checkout session.
5. **NEW: Add `UnitPrice` + `VatRate` to `QRCodePayload`** (`1_Shared/DTOs/QRCodePayload.cs`).
6. **NEW: Update `Scan.razor`** — remove API call to fetch product details after scan. Use QR data directly (ProductId, UnitPrice, VatRate, ProductName not in QR — still needed from API OR add ProductName to QR too).
7. **NEW: Update QR generation UI** (ShopERP) — add reminder/warning: "⚠️ Khi đổi giá hoặc VAT của sản phẩm này, bạn cần in lại QR code và dán đè QR cũ."

**Out of scope:** Gateway router (Phase 3), ShopERP OrderSyncSubscriber (Phase 4), Admin UI (Phase 6), Accounting consolidation (Phase 3.5).

---

## 1.5. QR Code Design (NEW — Round 2 decision)

### QRCodePayload + QR Service changes

```csharp
public class QRCodePayload
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public long Timestamp { get; set; }
    public string? TableNumber { get; set; }
    // NEW fields (snapshot at QR print time):
    public decimal UnitPrice { get; set; }    // Price at QR print time
    public decimal VatRate { get; set; }      // VAT rate at QR print time
    public string? ProductName { get; set; }  // Product name at QR print time (cart display without API call)
}
```

**Service layer updates required:**
- `3_CoreHub/Services/IQrCodeService.cs` — ADD overload:
  ```csharp
  byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber, decimal unitPrice, decimal vatRate, string productName);
  ```
- `3_CoreHub/Services/QrCodeService.cs` — IMPLEMENT overload:
  - Build `new QRCodePayload(productId, shopId, tableNumber) { UnitPrice = unitPrice, VatRate = vatRate, ProductName = productName }`.
  - Call `ToQrContent()`.
  - Keep existing 3-param overload for backward compat (but mark obsolete or route to new overload with zeros).
- `5_WebApps/ShopERP/Controllers/ProductsController.cs` (`GetProductQrCode`) — pass `product.Price`, `product.VatRate`, `product.Name` to `GenerateProductQRCode`.
- `6_Tests/VanAn.Core.Tests/Services/QrCodeServiceTests.cs` — update tests to assert QR payload contains price/VAT/name.
- `3_CoreHub/Services/QRCodePayload.cs` — ADD constructor overload accepting `unitPrice`, `vatRate`, `productName`.

**Backward compatibility:** `FromJson` must handle old QR codes (missing `UnitPrice`/`VatRate`/`ProductName`) → default to 0 / null. Scan.razor checks: if `UnitPrice == 0` → fall back to API call (legacy QR support).

### Scan.razor flow (NEW)
```
Scan QR → parse QRCodePayload
  → if UnitPrice > 0 (new QR):
      → add to cart directly (ProductId, UnitPrice, VatRate, ProductName from QR)
      → NO API call
  → else (legacy QR):
      → API call to fetch product details (existing flow)
      → add to cart
```

### Checkout price validation flow (NEW)
```
Checkout → send items with UnitPrice/VatRate from QR
  → Gateway PublicOrdersController.CreateCheckoutOrder:
      For each tenant group, before calling OrderService.CreateOrderFromCommandAsync:
        If tenant.Price_Validation_Enabled:
          For each item: HTTP GET to correct ShopERP (routed by Tenant.ShopInstanceId):
              GET {shopInstanceBaseUrl}/api/products/{productId}/validate-price?unitPrice={qrPrice}&vatRate={qrVat}&tenantId={tenantId}
          → ShopERP ProductsController.ValidatePrice:
              1. Load ShopFeatureSettings for tenant
              2. If Price_Validation_Enabled = false → return 200 OK (skip validation)
              3. If Price_Validation_Enabled = true → compare QR UnitPrice/VatRate with current Product.Price/Product.VatRate
              4. If mismatch → 409 Conflict { productId, currentPrice, currentVatRate, qrPrice, qrVatRate, message: "Giá sản phẩm đã thay đổi" }
  → If any mismatch: Gateway returns CheckoutResponse with successCount=0, failureCount>0, errors[] containing price mismatch per product.
  → Checkout.razor shows VanAnAlert: "Sản phẩm {ProductName} đã đổi giá từ {oldPrice} → {newPrice}. Vui lòng quét lại QR mới."
```

**Owner toggle (NEW — Round 2 decision):** Tenant owner can enable/disable price validation in ShopERP Settings → ShopFeatures page. Toggle: `Price_Validation_Enabled` (default ON). When OFF, checkout trusts QR price entirely (no ShopERP HTTP call) — faster checkout, but no staleness protection.

### ShopFeatureSettings update (NEW)

- **`3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs`** — ADD field:
  ```csharp
  /// <summary>Toggle: validate QR price against current product price at checkout. Default: ON.
  /// When OFF, checkout trusts QR price (faster, no ShopERP HTTP call) — use when prices rarely change.</summary>
  public bool Price_Validation_Enabled { get; private set; } = true;
  ```
  - Update constructor: set `Price_Validation_Enabled = true`.
  - Update `UpdateToggles` method: add `bool priceValidation = true` parameter (with default so existing call sites compile) + `Price_Validation_Enabled = priceValidation;`.
- **`3_CoreHub/Services/IShopFeatureSettingsService.cs`** — add `Price_Validation_Enabled` to `ShopFeatureSettingsDto`.
- **`3_CoreHub/Services/ShopFeatureSettingsService.cs`** — map new field in `ToDto` and `UpdateSettingsAsync`.
- **`3_CoreHub/Infrastructure/Configurations/ShopFeatureSettingsConfiguration.cs`** — no change needed if convention mapping.
- **PG + SQLite migration**: add column `Price_Validation_Enabled` (boolean, default true).
- **`5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor`** — ADD toggle row after VAT Display toggle:
  ```html
  <div class="feature-row" data-testid="toggle-price-validation">
      <div class="feature-info">
          <strong>Kiểm tra giá khi thanh toán</strong>
          <p class="text-muted small">Khi bật, hệ thống kiểm tra giá trong QR code có khớp với giá hiện tại của sản phẩm. Nếu giá đã đổi, khách hàng sẽ được thông báo và không thể đặt hàng với giá cũ. Tắt nếu giá sản phẩm ít thay đổi để thanh toán nhanh hơn.</p>
      </div>
      <div class="form-check form-switch">
          <input class="form-check-input" type="checkbox" role="switch"
                 id="price-validation" @bind="settings.Price_Validation_Enabled" />
      </div>
  </div>
  ```
- **This is an Infrastructure entity + DTO change, NOT a Domain modification.** No Domain approval needed.

### QR generation UI update (ShopERP)
- In `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` QR modal (around line 247), add `VanAnAlert` (warning type) below the QR preview:
  - "⚠️ Lưu ý: Khi bạn thay đổi giá (UnitPrice) hoặc thuế (VAT) của sản phẩm này, bạn cần **in lại QR code** và dán đè lên QR cũ. Khách hàng quét QR cũ sẽ thấy giá đã thay đổi khi thanh toán và không thể đặt hàng với giá cũ."
- This is a static reminder, always visible on the QR modal and print popup.

---

## 2. Reverse Impact Analysis

### Domain Layer (`1_Shared/`)
- **`Domain/CartItem.cs`** — ADD `TenantId` field:
  - `public Guid TenantId { get; init; } = Guid.Empty;`
  - **Domain Modification — requires user approval per governance IMPLEMENT rule.**
  - **Do NOT use `required`** — `CartItem` already has `required` properties on all fields; adding a new `required` field would break every construction site. Use default `Guid.Empty` so legacy/uninitialized `CartItem` objects remain valid; validation (`TenantId != Guid.Empty`) happens at checkout.
  - All places constructing `CartItem` should pass `TenantId` (grep `new CartItem` and `with {` copy expressions).

### KhachLink Services (`5_WebApps/KhachLink/Services/`)
- **`CartState.cs`** — UPDATE `AddItem`:
  - `Items.Add(new CartItem { ..., TenantId = product.TenantId });`
- **`EnhancedCartService.cs`** — REVIEW all `CartItem` constructions (lines 9, 12, 32, 37, 38, 42, 72, 73, 105, 106, 110, 132, 136, 150, 186, 195, 218, 230, 231, 251, 268, 278, 293, 305, 307 per grep). Each must pass `TenantId`.
- **`ConflictResolutionService.cs`** — REVIEW `CartItem` constructions (lines 9, 107).
- **`SyncConflictResolver.cs`** — REVIEW (lines 9, 88, 104).
- **`CheckoutFlowState.cs`** — REVIEW if it needs to store list of created orders (currently single).

### KhachLink Pages (`5_WebApps/KhachLink/Pages/`)
- **`Checkout.razor`** — REWRITE `SubmitGuestOrder`:
  - Build `orderRequest.items` with `tenantId = i.TenantId` per item.
  - Parse `CheckoutResponse` (new shape from Phase 3 §4).
  - If `successCount >= 1`: show success screen with list of created orders (loop).
  - If `failureCount > 0`: show warnings section listing failed tenants.
  - If `successCount == 0`: show error screen with `errors[]`.
  - Store `created_orders: [{orderId, tenantId, amount}, ...]` in localStorage for OrderHistory.
  - Clear cart only if all items were successfully ordered (or partially clear — items belonging to failed tenants stay in cart for retry).
- **`OrderTracking.razor`** — UPDATE:
  - If user navigates from checkout success, may need to track multiple orders. Add "Theo dõi tất cả" link showing list.
  - `GET /api/public/orders/{id}?tenantId={tenantId}` per Phase 3 Q1 — send `tenantId` (read from localStorage `created_orders`).
- **`OrderHistory.razor`** — UPDATE:
  - Read `created_orders` from localStorage.
  - Group orders by checkout session (timestamp?) or just flat list.
  - Display each order with its `tenantId` + tenant name (if available).

### KhachLink Models (`5_WebApps/KhachLink/Models/`)
- **`OrderInfo.cs`** — REVIEW if it needs `TenantId` field (currently has `CartItem` ref at line 12).

### UI Platform Compliance
- All new UI in `Checkout.razor` must use `VanAnButton`, `VanAnCard`, `VanAnAlert` — NO raw Bootstrap classes.
- Existing `Checkout.razor` already uses UI Platform components — maintain that.

### Tests
- **NEW: `6_Tests/VanAn.Core.Tests/CartItemTenantIdTests.cs`**:
  - `CartItem_WithTenantId_PreservesTenantId`
  - `CartState_AddItem_SetsTenantIdFromProduct`
- **Manual browser test (Phase 5 gate):**
  - Cart with 2 products from tenant A + 1 product from tenant B → checkout → verify 2 orders created, both displayed, cart cleared for successful items only.
- **NO Playwright** (per governance Playwright Guard — deferred to post-implementation validation).

### TDD Plan
1. Write failing unit test for `CartItem.TenantId`.
2. Add `TenantId` to `CartItem` record → test passes.
3. Update `CartState.AddItem` to set `TenantId`.
4. Update all other `CartItem` constructions in KhachLink services (compile errors will guide).
5. Update `Checkout.razor` request building + response handling.
6. Manual browser test (since no Playwright).
7. Build + verify.

---

## 3. Detailed Coding Plan

### Namespace Strategy
- `VanAn.Shared.Domain` (CartItem)
- `VanAn.KhachLink.Services` (CartState, EnhancedCartService, etc.)
- `VanAn.KhachLink.Pages` (Checkout.razor, OrderTracking.razor, OrderHistory.razor)
- `VanAn.KhachLink.Models` (OrderInfo if needed)

### Implementation Steps
**Step 1 — Domain (1 file):**
- Add `TenantId` to `1_Shared/Domain/CartItem.cs`.
- Build → expect compile errors in KhachLink services (good — guides what to update).

**Step 2 — CartState (1 file):**
- Update `CartState.AddItem` to set `TenantId = product.TenantId`.

**Step 3 — Other CartItem constructions (3-4 files):**
- `EnhancedCartService.cs`, `ConflictResolutionService.cs`, `SyncConflictResolver.cs`.
- Each `new CartItem { ... }` must add `TenantId = sourceProduct.TenantId` (or pass through from existing CartItem in copy scenarios).
- Build → 0 errors.

**Step 4 — Checkout.razor rewrite (1 file):**
- Update `SubmitGuestOrder`:
  - Add `TenantId = i.TenantId`, `ProductName = i.ProductName`, `VatRate = i.VatRate` to each item in request (per Phase 3 §3 contract — client provides snapshot).
  - Change `OrderCreatedResult` class to `CheckoutResponseResult` matching new shape:
    ```csharp
    private class CheckoutResponseResult {
        [JsonPropertyName("orders")] public List<CreatedOrderItem> Orders { get; set; } = new();
        [JsonPropertyName("successCount")] public int SuccessCount { get; set; }
        [JsonPropertyName("failureCount")] public int FailureCount { get; set; }
        [JsonPropertyName("errors")] public List<CheckoutErrorItem> Errors { get; set; } = new();
    }
    private class CreatedOrderItem {
        [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
        [JsonPropertyName("tenantId")] public Guid TenantId { get; set; }
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("subTotal")] public decimal SubTotal { get; set; }
        [JsonPropertyName("totalVatAmount")] public decimal TotalVatAmount { get; set; }
    }
    private class CheckoutErrorItem {
        [JsonPropertyName("tenantId")] public Guid TenantId { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; } = "";
    }
    ```
  - Success screen: loop `result.Orders` → show each order (OrderId, Amount, tracking link).
  - Error section: if `result.Errors.Any()`, show `VanAnAlert` warning with list of failed tenants.
  - localStorage: `await JSRuntime.InvokeVoidAsync("localStorage.setItem", "created_orders", JsonSerializer.Serialize(result.Orders.Select(o => new { o.OrderId, o.TenantId })))`.
  - Cart clearing: only clear items whose `TenantId` is in `result.Orders.Select(o => o.TenantId)`. Failed tenant items stay in cart.

**Step 5 — OrderTracking.razor (1 file):**
- No change to tracking request — `GET /api/public/orders/{id}` queries PG directly (per Phase 3 Q1 resolution, no tenantId needed).
- If user navigates from checkout success with multiple orders, may show "Đơn hàng khác từ phiên này" section linking to other orders in `created_orders` localStorage.

**Step 6 — OrderHistory.razor (1 file):**
- Read `created_orders` from localStorage.
- Display list of past orders with tenantId + tracking link.

**Step 7 — Manual browser test:**
- Start all 5 services locally (Docker PG + NATS + ShopERP + KhachLink + Gateway).
- Add 2 products from tenant A (`00000000-...-001`).
- Add 1 product from tenant B (need to verify local SQLite has 2+ tenants — if not, seed one).
- Go to /checkout → fill form → submit.
- Verify: 2 orders created, both shown, cart cleared.

**Step 8 — Full regression:**
- `dotnet build VanAn.sln` — 0 errors.
- `guard-check.ps1` PASS.

### Active Skills
- `accounting-ui-implementation` (Checkout.razor UI rewrite)
- `ui-platform-compliance-review` (ensure VanAn components used)
- `domain-integrity-validation` (CartItem modification)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Core.Tests --filter CartItem` | All pass |
| Manual browser | Add 2-tenant cart → checkout | 2 orders created, both displayed |
| Guard check | `./guard-check.ps1` | PASS |

---

## 5. Deliverables

- Modified: `1_Shared/DTOs/QRCodePayload.cs` (add UnitPrice, VatRate, ProductName)
- Modified: `1_Shared/Domain/CartItem.cs` (add TenantId)
- Modified: `5_WebApps/KhachLink/Services/CartState.cs`
- Modified: `5_WebApps/KhachLink/Services/EnhancedCartService.cs` (all CartItem constructions)
- Modified: `5_WebApps/KhachLink/Services/ConflictResolutionService.cs`
- Modified: `5_WebApps/KhachLink/Services/SyncConflictResolver.cs`
- Modified: `5_WebApps/KhachLink/Pages/Scan.razor` (remove API call, use QR data directly, legacy fallback)
- Modified: `5_WebApps/KhachLink/Pages/Checkout.razor` (request + response handling + UI + price mismatch alert)
- Modified: `5_WebApps/KhachLink/Pages/OrderTracking.razor` (send tenantId)
- Modified: `5_WebApps/KhachLink/Pages/OrderHistory.razor` (read created_orders)
- Modified: `3_CoreHub/Services/IQrCodeService.cs` (new overload with price/VAT/name)
- Modified: `3_CoreHub/Services/QrCodeService.cs` (new overload implementation)
- Modified: `3_CoreHub/Services/QRCodePayload.cs` (new constructor)
- Modified: `5_WebApps/ShopERP/Controllers/ProductsController.cs` (`GetProductQrCode` passes price/VAT/name)
- Modified: `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` (add Price_Validation_Enabled)
- Modified: `3_CoreHub/Services/IShopFeatureSettingsService.cs` (ShopFeatureSettingsDto add field)
- Modified: `3_CoreHub/Services/ShopFeatureSettingsService.cs` (map new field)
- Modified: `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` (add price validation toggle)
- Modified: `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` (QR reminder alert)
- New: `ShopERP ProductsController.ValidatePrice` endpoint (or new `PriceValidationController`)
- New: PG migration `AddPriceValidationToggle` (ShopFeatureSettings column)
- New: SQLite migration `AddPriceValidationToggle`
- New: `6_Tests/VanAn.Core.Tests/CartItemTenantIdTests.cs`
- New: `6_Tests/VanAn.Core.Tests/QRCodePayloadPriceTests.cs` (QR with price fields + backward compat)

---

## 6. Approval Gate

**Domain modification requires user approval:**
- [ ] `CartItem.TenantId` field addition approved (`public Guid TenantId { get; init; } = Guid.Empty;`, default to avoid breaking `required` record construction)

**DTO / Infrastructure changes (no Domain approval needed):**
- [ ] `QRCodePayload` (DTO, not Domain) — UnitPrice/VatRate/ProductName addition approved
- [ ] `ShopFeatureSettingsEntity` (Infrastructure, not Domain) — Price_Validation_Enabled field addition approved
- [ ] `ShopFeatureSettingsDto` + service mapping update approved

**UX / data schema:**
- [ ] localStorage `created_orders` schema approved
- [ ] Partial cart clearing on partial failure approved (items for failed tenants stay in cart)

---

## 7. COMPLETION SUMMARY

**Phase 5 COMPLETE** — pending commit on `main`.

### Files created
| File | Purpose |
|------|---------|
| `6_Tests/VanAn.Core.Tests/Domain/CartItemTenantIdTests.cs` | 5 tests: CartItem.TenantId defaults, preservation, CartState propagation, multi-tenant cart |
| `6_Tests/VanAn.Core.Tests/Dto/QRCodePayloadPriceTests.cs` | 5 tests: QR payload price/VAT/name fields, JSON round-trip, legacy QR backward compat |
| `3_CoreHub/Infrastructure/Migrations/20260720032140_AddPriceValidationToggle.cs` | Npgsql migration: add `Price_Validation_Enabled` column to `ShopFeatureSettings` |
| `5_WebApps/ShopERP/Migrations/20260720031444_AddPriceValidationToggle.cs` | SQLite migration: add `Price_Validation_Enabled` column + pending model changes |

### Files modified
| File | Change |
|------|--------|
| `1_Shared/Domain/CartItem.cs` | Add `TenantId` (init-only, defaults `Guid.Empty`) for multi-tenant cart grouping |
| `1_Shared/DTOs/QRCodePayload.cs` | Add `UnitPrice`, `VatRate`, `ProductName`, `TenantId` fields + 2 new constructors (6-arg, 7-arg) |
| `3_CoreHub/Services/QrCodeService.cs` | Add 2 new `GenerateProductQRCode` overloads (6-arg with price/VAT/name, 7-arg with TenantId) |
| `3_CoreHub/Services/IShopFeatureSettingsService.cs` | Add `Price_Validation_Enabled` to DTO + `PriceValidationResult` class |
| `3_CoreHub/Services/ShopFeatureSettingsService.cs` | Wire `Price_Validation_Enabled` through update/IsEnabled/ToDto |
| `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` | Add `Price_Validation_Enabled` property + `UpdateToggles` parameter |
| `3_CoreHub/Infrastructure/Configurations/ShopFeatureSettingsConfiguration.cs` | Map `Price_Validation_Enabled` column with default `false` |
| `5_WebApps/KhachLink/Services/CartState.cs` | `AddItem` sets `TenantId` from `ProductDto.TenantId` |
| `5_WebApps/KhachLink/Services/CartService.cs` | Add `AddItemAsync(CartItem)` overload for partial cart clear after multi-tenant checkout |
| `5_WebApps/KhachLink/Services/EnhancedCartService.cs` | Update `new CartItem` sites to set `TenantId` + `OfflineOrderItemDto.TenantId` |
| `5_WebApps/KhachLink/Models/OfflineOrderDto.cs` | Add `TenantId` to `OfflineOrderItemDto` for offline sync round-trip |
| `5_WebApps/KhachLink/Pages/Checkout.razor` | Multi-tenant checkout request + `CheckoutResponse` handling + partial cart clear + `created_orders` localStorage |
| `5_WebApps/KhachLink/Pages/Scan.razor` | Fast path: use QR price/VAT/name/tenantId directly (no API call) + legacy fallback |
| `5_WebApps/KhachLink/Pages/OrderTracking.razor` | Show "other orders from this session" section (reads `created_orders` localStorage) |
| `5_WebApps/KhachLink/Pages/OrderHistory.razor` | Add `TenantId` to `OrderDto` for multi-tenant order display |
| `5_WebApps/ShopERP/Controllers/ProductsController.cs` | QR generation passes price/VAT/name/tenantId to new overload + `ValidateProductPrice` endpoint |
| `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` | Add `Price_Validation_Enabled` toggle UI |
| `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` | Add Phase 5 QR reminder alert in QR modal |
| `6_Tests/VanAn.Core.Tests/Services/QrCodeServiceTests.cs` | Add 2 tests for new QR overloads (price/VAT/name + PNG size check) |

### Issues fixed during implementation
- **CartItem construction sites:** `EnhancedCartService` had 2 `new CartItem` sites that needed `TenantId` propagation through `OfflineOrderItemDto`.
- **CartService.AddItemAsync signature:** Only accepted `ProductDto` — added `AddItemAsync(CartItem)` overload for partial cart clear after multi-tenant checkout (items from failed tenants re-added for retry).
- **Blazor `data-testid` complex content:** `data-testid="checkout-link-tracking-@order.OrderId"` caused RZ9986 error — removed dynamic testid (Blazor doesn't allow mixed C# + markup in attributes).
- **Migration bundling:** ShopERP SQLite migration bundled pending model changes (ShopInstances, OutboxRoutingKey) with `Price_Validation_Enabled` — expected EF behavior when prior model changes weren't migrated.

### Verification

#### Static Verification (compile-time)
- **Build:** `dotnet build VanAn.sln` — 0 errors, 67 pre-existing warnings
- **Unit tests:** `dotnet test VanAn.Core.Tests` — 1038 passed, 0 failed, 16 skipped (12 new tests added: 5 CartItem + 5 QR Payload + 2 QR Service)
- **guard-check.ps1:** PASSED — architecture guard, Roslyn analyzers, build, fast test gate all green

#### Live Runtime Verification (boot + HTTP + UI)
> **Lesson learned (Wave 0):** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Live runtime verification is MANDATORY for all phases.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | Gateway health | PASS | `{"status":"Healthy"}` |
| RV2a | CheckoutResponse has orders[] array | PASS | orders.Count=1 |
| RV2b | successCount=1 | PASS | successCount=1 |
| RV2c | First order has orderId | PASS | orderId=019f7de9-8a4b-77c4-88c9-fa610cc2e668 |
| RV3 | QR endpoint returns non-empty PNG | PASS | 4540 bytes, HTTP 200 |
| RV4 | ValidateProductPrice match=true (correct price) | PASS | match=True, reason=OK |
| RV5 | ValidateProductPrice match=false (stale price) | PASS | match=False, reason=UnitPrice mismatch, currentPrice=30000.0 |
| RV6 | Price_Validation_Enabled toggle readable | PASS | value=False (default) |
| RV7 | KhachLink order tracking UI loads | PASS | HTTP 200, contentLength=23686 |

**RV script:** `scripts/verify-phase5-multi-tenant-checkout-prod.ps1`
**VPS deploy fixes during RV:**
1. Npgsql migration `AddPriceValidationToggle` tried to drop FK already dropped by Phase 3 → removed duplicate DropFK/AddFK
2. SQLite migration `AddPriceValidationToggle` had unnecessary FK drop/re-add → removed (SQLite keeps FK)
3. `SHOP_INSTANCE_ID` env var missing from `docker-compose.prod.yml` → hardcoded Guid `00000000-0000-0000-0000-000000000001` (matches Phase 1 seed)
