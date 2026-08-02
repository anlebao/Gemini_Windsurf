# Feature Task Card: Guest Checkout Form UI (Bucket A — W6 Deferred)

> **Status:** ANALYZE COMPLETE — pending user approval (Step 4)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Origin:** Deferred from W6/Bucket A — `omnichannel-order-lifecycle.spec.ts:53` (`test.skip`)
> **Date opened:** 2026-07-07

---

## 1. Use Case & Business Design

**Problem:** KhachLink's `/checkout` page auto-creates the order on first render (`OnAfterRenderAsync`) with NO guest information form. The deferred golden E2E test (`omnichannel-order-lifecycle.spec.ts:53`) expects a guest checkout form with name/phone/address inputs + "Đặt hàng" button — the form does not exist.

**Goal:** Add a guest information step to KhachLink's checkout flow. Customer fills name/phone/address → clicks "Đặt hàng" → order is created with customer info persisted to `Order.CustomerInfo` (owned value object, columns already exist in DB).

**Out of scope:** Loyalty points UI (already dead code per project_state.md L295), payment method selection (QR modal already works), authenticated customer flow.

---

## 2. Reverse Impact Analysis + TDD Plan

### UI Layer (KhachLink — `5_WebApps/KhachLink/`)
- **`Pages/Checkout.razor`** — REWRITE:
  - Remove auto-redirect / auto-create on `OnAfterRenderAsync` (currently creates order immediately).
  - Add guest form section: name/phone/address inputs + "Đặt hàng" submit button.
  - On submit → validate → call `POST /api/public/orders/checkout` with customer info → show order confirmation.
  - **UI Platform compliance fix:** current page uses raw Bootstrap (`btn`, `card`, `alert-warning`) — migrate to `VanAnButton`/`VanAnCard`/`VanAnAlert` to match neighboring KhachLink pages (Home.razor, Cart.razor, Scan.razor all use UI Platform).
  - **Pre-existing bug fix (incidental):** lines 195-201 contain duplicate trailing content (`string? PaymentUrl; decimal Amount; }`) — remove.
  - Use `CheckoutFlowState` (already registered, has `CustomerName/Phone/Address` fields — currently unused) to hold form state.
- **`Services/CheckoutFlowState.cs`** — already has fields. No change needed.

### Application Layer (Gateway — `2_Gateway/`)
- **`Controllers/PublicOrdersController.cs`** — extend `CheckoutOrderRequest` DTO with `CustomerName`/`CustomerPhone`/`CustomerAddress` (nullable strings) + pass to `CreateOrderCommand`.

### Service Layer (CoreHub — `3_CoreHub/`)
- **`Commands/CreateOrderCommand.cs`** — add `CustomerName`/`CustomerPhone`/`CustomerAddress` (nullable strings).
- **`Services/OrderService.cs`** — in `CreateOrderFromCommandAsync`, set `Order.CustomerInfo` from command fields via new `Order.SetCustomerInfo(...)` method.

### Domain Layer (`1_Shared/`)
- **`Domain.cs` → `Order` entity** — add `SetCustomerInfo(CustomerInfo info)` method (follows existing `SetCustomerDeviceId` pattern, line 1010). Sets `CustomerInfo` + calls `UpdateAudit()`.
  - **⚠️ DOMAIN MODIFICATION — requires user approval per governance IMPLEMENT rule.**
  - `CustomerInfo` value object already exists (`1_Shared/Domain/CustomerInfo.cs`) with `FullName`/`PhoneNumber`/`Email`/`Address`/`Notes`.

### Infrastructure Layer
- **`OrderConfiguration.cs`** — already maps `CustomerInfo` as `OwnsOne` (line 18). No migration needed — columns `CustomerInfo_FullName`, `CustomerInfo_PhoneNumber`, `CustomerInfo_Email`, `CustomerInfo_Address`, `CustomerInfo_Notes` already exist (InitialCreate migration L618-622).

### Tests
- **`6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs`** — no new DI registrations needed (CheckoutFlowState already registered L73). No change.
- **`6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts`** — remove `test.skip` on line 53 after implementation.
- **New unit test:** `Order.SetCustomerInfo` domain method test (in `6_Tests/VanAn.Core.Tests/Domain/`).
- **New integration test:** `POST /api/public/orders/checkout` with customer info → verify `Order.CustomerInfo` persisted.

### TDD Plan
1. Write failing domain test: `Order_SetCustomerInfo_SetsFieldsAndUpdatesAudit`
2. Implement `Order.SetCustomerInfo` → test passes
3. Write failing integration test: checkout with customer info → verify persisted
4. Extend command/DTO/controller/service → test passes
5. Rewrite `Checkout.razor` with guest form
6. Un-skip E2E test → run golden suite (post-implementation, per Playwright guard)

---

## 3. Detailed Coding Plan + Namespace Strategy

### Namespace Strategy
No new namespaces. All changes in existing namespaces:
- `VanAn.Shared.Domain` (Order, CustomerInfo)
- `VanAn.CoreHub.Commands` (CreateOrderCommand)
- `VanAn.CoreHub.Services` (OrderService)
- `VanAn.Gateway.Controllers` (PublicOrdersController, CheckoutOrderRequest)
- `VanAn.KhachLink.Pages` (Checkout.razor)
- `VanAn.KhachLink.Services` (CheckoutFlowState — already exists)

### Implementation Phases (per Phase Isolation rule)

**Phase 1 — Domain** (1 file)
- Add `Order.SetCustomerInfo(CustomerInfo info)` method to `1_Shared/Domain.cs`
- Add domain test
- Validate: `dotnet build` + domain test

**Phase 2 — Application/Service** (3 files)
- Extend `CreateOrderCommand` with customer fields
- Update `OrderService.CreateOrderFromCommandAsync` to set `CustomerInfo`
- Extend `PublicOrdersController.CheckoutOrderRequest` DTO + pass through
- Add integration test
- Validate: `dotnet build` + integration test

**Phase 3 — UI** (1 file + 1 test spec)
- Rewrite `Checkout.razor` with guest form (UI Platform components)
- Remove `test.skip` from `omnichannel-order-lifecycle.spec.ts:53`
- Validate: `dotnet build` + guard-check.ps1
- Playwright validation deferred to post-implementation per Playwright Guard

### Active Skills
- `accounting-ui-implementation` (UI form for order checkout)
- `domain-integrity-validation` (Domain method addition — guard check)
- `ui-platform-compliance-review` (Checkout.razor migration to UI Platform)

---

## 4. Review & Approval (PENDING USER)

**Items requiring explicit approval:**
1. **Domain modification:** Add `Order.SetCustomerInfo(CustomerInfo)` method to `1_Shared/Domain.cs`.
   - Justification: `CustomerInfo` value object + `Order.CustomerInfo` property already exist; only a setter method is missing. Follows existing `SetCustomerDeviceId` pattern. No new columns, no migration, no immutability concern (Order is mutable by design — `AccountingEntry` is the immutable one).
2. **UI Platform migration of `Checkout.razor`:** Rewrite raw Bootstrap → VanAn UI Platform components (incidental compliance fix while implementing the feature).
3. **E2E test un-skip:** Remove `test.skip` from `omnichannel-order-lifecycle.spec.ts:53`.

**Estimated files modified:** 6 production + 2 test (1 new domain test + 1 new integration test) + 1 E2E spec edit = 9 files.

---

## 5-7. IMPLEMENT (pending approval)

To be executed after user approval.
