# Loyalty Points Integrity — Detail Coding Plan (v1.0)

**Created:** 2026-09-20
**Status:** ✅ BATCH 1 COMPLETE + DEPLOYED + RV PRODUCTION PASS (2026-09-20, `ebbc1f5a` + `6023ad0`) — Batch 2-5 pending
**Mode:** IMPLEMENT (Batch 1 xong) → Batch 2 (PG ledger authority) kế tiếp
**Branch:** `main`
**Liên quan:** `docs/plans/loyalty-consistency-fix-plan.md` (COMPLETE, Alliance routing), `docs/plans/loyalty-alliance-master-plan.md`, `docs/AI/tasks/loyalty_points_visibility_master_plan.md` (Phase 3 VND APPROVED)

---

## 1. MỤC TIÊU (user yêu cầu — "triệt để")

1. **Điểm thưởng được cộng thực sự vào khách hàng** sau đơn hoàn thành (fix RC1: Silo mất sync PG→SQLite).
2. **Tính chính xác**: 1 công thức duy nhất cho award / banner / checkout-estimate (fix RC2).
3. **Không double award** (fix RC3 — guard tập trung, per-order, không per-DB).
4. **Phân biệt rõ điểm đến từ tenant nào** — mọi entry có tenant attribution, cả 2 mode.
5. **Hợp lệ theo ngân sách**: tenant đăng ký budget (vd 1000 điểm/tháng) thì KHÔNG thể tặng quá — enforce mọi path.
6. **Đúng luật theo mode**:
   - **Silo**: khách chỉ tiêu được điểm tại tenant đã tặng.
   - **Alliance**: khách tiêu được ở mọi tenant member (shared pool) — kèm attribution ai chịu chi phí; khi tiêu **ưu tiên điểm của tenant hiện tại trước** (D2 APPROVED), thiếu mới dùng tenant khác.

---

## 2. VẤN ĐỀ (EVIDENCE)

### 2.1 RC1 — Silo award trên Gateway ghi PG, khách đọc SQLite → không thấy điểm (BLOCKER)

| # | Bằng chứng | File:line |
|---|---|---|
| 1 | Đơn KhachLink hoàn thành tại **Gateway** → `ProcessLoyaltyPointsAsync` → `LoyaltyRewardsService.AddPointsAsync` → repo dùng `IVanAnDbContext` = **PG** | `DeliveryWorkflowService.cs:75-94`, `OrderWorkflowService.cs:656`, `LoyaltyRewardsRepository.cs:8` |
| 2 | Khách xem số dư: KhachLink → Gateway `/api/loyalty/my` → **forward ShopERP** → SQLite | `LoyaltyController.cs:46-71` (Gateway), `ShopERP/Controllers/LoyaltyController.cs:47-79` |
| 3 | Sync PG→SQLite chỉ có `LoyaltySyncSubscriber` subscribe **`vanan.cloud.loyalty.changed.>`** — payload `{customerDeviceId, pointBalance, updatedAt}` — publish **chỉ bởi AllianceWalletService** (Alliance mode) | `LoyaltySyncSubscriber.cs:53`, `AllianceWalletService.cs:433-448` |
| 4 | Silo award publish **`loyalty.points.changed`** payload `{customerId, pointsChange, newBalance, reason, isAdd, timestamp}` — consumer duy nhất là **PushNotificationBackgroundService** (push, không sync DB) | `LoyaltyRewardsService.cs:310-338`, `PushNotificationBackgroundService.cs:69` |
| 5 | Outbox `LoyaltyPointsChanged` → NatsSyncWorker → subject `vanan.cloud.loyalty.points.changed` — **không khớp** `vanan.cloud.loyalty.changed.>` | `NatsSyncWorker.cs:137-145` |

→ Sai cả subject lẫn payload. Điểm PG không bao giờ tới SQLite. **"Được tính nhưng không cộng vào".**

### 2.2 RC2 — Tính không chính xác (3 lệch công thức)

| # | Lệch | Bằng chứng |
|---|---|---|
| 1 | Checkout estimate: `(int)(cartState.TotalAmount × rate)` — **không clamp min/max, không trừ discount, không gồm shipping** | `Checkout.razor:475-478` vs award `OrderWorkflowService.cs:557-562` |
| 2 | Banner tracking `ComputePointsAwardedAsync` chỉ là **recompute estimate** (comment tự ghi nhận) — drift khi config đổi sau khi order xong | `PublicOrdersController.cs:471-475, 488-493` |
| 3 | ShopERP (POS) **ignore `LoyaltyGlobalConfig`** trong EF model → query ném exception → fallback appsettings (rate 0.1, min 10, max null) — **bỏ qua config admin trên PG** → award POS khác banner | `ShopERPDbContext.cs:270`, `OrderWorkflowService.cs:495-518` |

### 2.3 RC3 — Double-award guard per-DB

Guard `OrderWorkflowService.cs:455-476` check history theo **từng DB** (PG vs SQLite độc lập) → đơn hoàn thành cả 2 phía có thể award 2 lần.

### 2.4 Phân biệt tenant + luật mode (phát hiện sâu — yêu cầu mới)

| # | Vấn đề | Bằng chứng |
|---|---|---|
| 1 | **Silo mất tenant attribution**: `LoyaltyRewards` unique `(TenantId, CustomerId)` nhưng mọi lookup chỉ filter CustomerId (IgnoreQueryFilters) → khách mua ở tenant A rồi B: điểm B **cộng vào row của A**; redeem ở B **trừ điểm của A** | `LoyaltyRewardsConfiguration.cs:32`, `LoyaltyRewardsRepository.cs:12-18`, `LoyaltyRewardsService.cs:38-54, 72-95` |
| 2 | **Budget không enforceable**: flag `ValcnV2_LoyaltyBudget` **default OFF**; `SetBudgetCaps` không có API/UI gọi; counters chỉ tăng khi flag ON | `FeatureFlagService.cs:25`, `Domain.cs:2429`, `LoyaltyConfigController.cs:137-174` |
| 3 | **Alliance không có sub-balance per tenant**: wallet 1 pool; breakdown = net từ tx log với REDEEM mis-attribute vào tenant tiêu | `AllianceWallet.cs:2456-2495`, `LoyaltyController.cs:176-183` (Gateway wallet breakdown) |
| 4 | **Double-write nguy hiểm**: cùng khách có thể có row SQLite (POS) + row PG (Gateway) → nếu sync đơn giản "ghi đè balance" sẽ **nuốt điểm POS** | `LoyaltySyncSubscriber.cs:153-160` |

---

## 3. KIẾN TRÚC ĐÍCH (Single Source of Truth)

```
                    ┌─────────────────────────────────────────────┐
                    │  GATEWAY (PG) — LOYALTY LEDGER (authority)  │
                    │                                             │
                    │  LoyaltyRewards (Silo, per (Customer,Tenant))│
                    │  AllianceWallet + AllianceTransaction (+     │
                    │    SourceTenantId REDEEM attribution)        │
                    │  LoyaltyTenantConfig (budget caps + counters)│
                    │  LoyaltyIssuanceRecord (per-order, PG)       │
                    └──────▲──────────────────────────▲───────────┘
      award/spend/refund   │ proxy (X-Internal-Api-Key)│  direct (in-proc)
      (ShopERP POS)  ┌─────┴──────┐            ┌───────┴────────┐
                     │ ShopERP    │            │ OrderWorkflow/ │
                     │ HTTP proxy │            │ Redemption/    │
                     └─────▲──────┘            │ Mission (GW)   │
                           │ NATS mirror (PG→SQLite)             │
                     ┌─────┴──────┐            └────────────────┘
                     │ SQLite     │  LoyaltyRewards = READ MIRROR │
                     │ (ShopERP)  │  (offline-first display)      │
                     └────────────┘
```

**Nguyên tắc:**
1. **Mọi WRITE loyalty (award/spend/refund/reversal) đi qua Gateway PG** — một nguồn quyền lực duy nhất.
2. ShopERP POS gọi qua **internal HTTP proxy** (pattern có sẵn: `LoyaltyBudgetServiceHttpProxy`, `AllianceWalletServiceHttpProxy`, header `X-Internal-Api-Key`).
3. **SQLite `LoyaltyRewards` = mirror đọc** cho offline-first; sync qua NATS `vanan.cloud.loyalty.changed.{deviceId}` (payload mở rộng), **merge idempotent, không ghi đè ngược**.
4. **Tenant attribution bắt buộc ở mọi entry** (Silo: row per (customer,tenant); Alliance: TransactionTenantId + SourceTenantId khi tiêu).
5. **Budget enforce ở mọi award path** (đăng ký qua API admin; check + counters atomic; reversal decrement).
6. **1 công thức điểm duy nhất** (`LoyaltyPointsCalculator`): award = banner = estimate.
7. Guard chống double-award theo **orderId trên ledger** (không per-DB).

---

## 4. PHASES & TASKS

### PHASE 1 — PG Ledger = single source of truth (award/spend/refund qua Gateway)

#### T1.1 Domain — thêm cột attribution cho AllianceTransaction
**File:** `1_Shared/Domain.cs` (`AllianceTransaction`, ~line 2502)
- Thêm `Guid? SourceTenantId` — tenant sở hữu điểm bị tiêu (Alliance REDEEM, FIFO).
- Thêm `void SetSourceTenant(Guid tenantId)` (set 1 lần trước khi lưu; append-only log).
- **DECISION D2**: FIFO consume (mặc định) — điểm được tiêu theo thứ tự earned cũ nhất trước.

#### T1.2 CoreHub — `LoyaltyPointLedgerService` (NEW, PG-only, Gateway + internal API dùng chung)
**File:** `3_CoreHub/Services/LoyaltyPointLedgerService.cs` (NEW) + `ILoyaltyPointLedgerService.cs` (NEW)

```csharp
public interface ILoyaltyPointLedgerService
{
    // Award: budget check → mode routing → write ledger → issuance record → publish sync event
    Task<LedgerResult> AwardAsync(AwardRequest req);          // req: customerId, tenantId, points, reason, sourceOrderId, idempotencyKey
    // Spend (Silo: chỉ trừ row (customer,tenant); Alliance: wallet FIFO + SourceTenantId)
    Task<LedgerResult> SpendAsync(SpendRequest req);          // req: customerId, tenantId, points, reason, idempotencyKey
    Task<LedgerResult> RefundAsync(RefundRequest req);        // voucher cancel → hoàn điểm (Silo row / Alliance wallet)
    Task<int> RevertOrderAsync(Guid orderId, TenantId tenantId, string reason); // Phase 4 reversal (2c) dùng chung
    Task<int?> GetAwardedPointsAsync(Guid orderId, Guid tenantId); // banner đọc THỰC TẾ (fix RC2.2)
    Task<int> GetBalanceAsync(Guid customerId, Guid tenantId);     // read (Silo row / Alliance wallet)
}
```

**Logic AwardAsync (thay thế `ProcessLoyaltyPointsAsync` — refactor, không tạo path song song):**
1. Ensure customer tồn tại ở PG (upsert stub nếu thiếu — pattern `BackfillCustomersToPg` / `OrderSyncSubscriber`).
2. **Budget check LUÔN CHẠY** (không phụ thuộc flag — flag chỉ là emergency off): `CheckAndAdjustPointsAsync` (PerOrderRateCap / Monthly / Daily / PerCustomerDaily qua `LoyaltyIssuanceRecord` PG).
3. Nếu `adjusted <= 0` → return skip (order vẫn complete).
4. Mode routing (dùng chung code với `ProcessLoyaltyPointsAsync` hiện tại):
   - **Silo**: `LoyaltyRewards` row tại **(customerId, tenantId)** (tạo mới nếu thiếu — unique index có sẵn) → `AddPoints`.
   - **Alliance** (member): `AllianceWallet.AddPointsAsync(deviceGuid, tenantId, ...)` — **thêm budget check defense-in-depth trong wallet** (T1.5).
5. Tạo `LoyaltyIssuanceRecord` **PG** (per-order, cho budget per-customer-daily + reversal).
6. `RecordIssuanceAsync` (counters atomic `ExecuteUpdateAsync`).
7. Publish sync event qua `LoyaltyBalanceSyncPublisher` (T2.1).

**Logic SpendAsync (thay thế Silo `SubtractPointsAsync` + Alliance `DeductPointsAsync` route):**
- **Silo**: đọc row **(customerId, redeemingTenantId)** → không có row hoặc `balance < points` → reject ("Không đủ điểm — điểm chỉ dùng được tại tenant đã tặng"). → **enforce luật Silo bằng cấu trúc**.
- **Alliance**: `AllianceWallet.DeductPointsAsync` + **FIFO attribution**: duyệt các tenant có EARN chưa tiêu (từ tx log) theo thứ tự thời gian, trừ dần; ghi `SourceTenantId` trên REDEEM entry. (T5.1 chi tiết.)

#### T1.3 Gateway — Internal Loyalty API (pattern: `InternalApiKeyAttribute`, X-Internal-Api-Key)
**File:** `2_Gateway/Controllers/InternalLoyaltyController.cs` (NEW, route `/api/internal/loyalty`)
- `POST /award`, `POST /spend`, `POST /refund`, `POST /revert-order`, `GET /balance`, `GET /awarded?orderId=`
- Idempotency: `X-Idempotency-Key` (pattern `AllianceWalletService` idempotency check).
- Auth: `[InternalApiKey]` (đã có) — KHÔNG expose ra ngoài.

#### T1.4 ShopERP — thay `LoyaltyRewardsService` bằng HTTP proxy (POS award/spend qua Gateway)
**File:** `5_WebApps/ShopERP/Services/LoyaltyRewardsServiceHttpProxy.cs` (NEW) + `Program.cs`
- Implements `ILoyaltyRewardsService`: `AddPointsAsync`/`SubtractPointsAsync`/`GetCustomerRewardsAsync` → call internal API.
- **Tenant-scoped**: `AddPointsAsync(customerId, points, reason)` → tenantId lấy từ `customer.TenantId` (cần read SQLite customer trước) — hoặc thêm param tenantId (T1.4a interface change, xem bên dưới).
- Register trong ShopERP `Program.cs` (thay `CoreHub.Services.LoyaltyRewardsService` ở line 200).
- Graceful: Gateway unreachable → log warning + **không fail order** (matching side-effect philosophy), điểm bù sau qua `LoyaltyBudget` counters (document).
- **T1.4a — interface change:** `ILoyaltyRewardsService.AddPointsAsync`/`SubtractPointsAsync` thêm `Guid tenantId` param — cập nhật toàn bộ call sites: `OrderWorkflowService` (đã có tenant), `MissionService`, `RedemptionService` (refund + silo spend), `LoyaltyController` (ShopERP), `BirthdayBonusJob`, `CustomerMergeService`.

#### T1.5 AllianceWalletService — defense-in-depth budget check
**File:** `3_CoreHub/Services/AllianceWalletService.cs`
- Inject `ILoyaltyBudgetService` (Gateway scope có sẵn): trong `AddPointsAsync` chạy `CheckAndAdjustPointsAsync` + `RecordIssuanceAsync` — bảo vệ mọi caller (Mission/OrderWorkflow/Internal API), không chỉ OrderWorkflow.

#### T1.6 OrderWorkflowService — refactor dùng ledger (bỏ duplicate logic)
**File:** `3_CoreHub/Services/OrderWorkflowService.cs`
- `ProcessLoyaltyPointsAsync` → gọi `_ledgerService.AwardAsync(...)` (giữ guard orderId: `GetAwardedPointsAsync` trước award — fix RC3 tập trung).
- Xoá: double-award guard history JSON per-DB, budget block cũ, mode routing Silo/Alliance, `CreateLoyaltyIssuanceRecordAsync` local.
- Giữ: `UpdateCustomerOrderStatsAsync`, campaign conversion, commission.

**Test Phase 1:**
- `LedgerServiceTests`: award Silo tạo row (customer,tenant); award Alliance → wallet + tx EARN có TransactionTenantId; spend Silo tại tenant không có điểm → reject; spend Silo tại đúng tenant → trừ đúng row; award 2 lần cùng orderId → guard chặn; idempotency key.
- `LoyaltyRewardsServiceHttpProxyTests`: happy path + Gateway down → không throw.
- Regression: toàn bộ `VanAn.Core.Tests` loyalty + order workflow suite.

---

### PHASE 2 — Đồng bộ & đọc (fix RC1: mirror PG→SQLite, merge an toàn)

#### T2.1 `LoyaltyBalanceSyncPublisher` (NEW) — 1 event shape cho cả 2 mode
**File:** `3_CoreHub/Services/LoyaltyBalanceSyncPublisher.cs` (NEW)
- Publish subject **`vanan.cloud.loyalty.changed.{customerDeviceId}`** (khớp `LoyaltySyncSubscriber`).
- Payload (mở rộng, backward-compatible):
```json
{ "customerDeviceId": "...", "customerId": "...", "tenantId": "...",
  "pointBalance": 123, "updatedAt": "ISO", "type": "EARN|SPEND|ADJUST|REVERSAL",
  "points": 50, "reason": "..." }
```
- `LoyaltyRewardsService` (Silo) + `AllianceWalletService` (Alliance) đều gọi publisher này (thay 2 implement hiện tại).
- Outbox: event `LoyaltyBalanceChanged` (mới) + `RoutingKey = deviceId` → NatsSyncWorker subject `vanan.cloud.loyalty.changed.{deviceId}` (reliable).

#### T2.2 `LoyaltySyncSubscriber` — merge idempotent theo (tenantId, customerId), không ghi đè ngược
**File:** `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs`
- Ưu tiên match row SQLite theo **(tenantId, customerId)** từ payload; fallback device join (legacy).
- Không có row → **tạo row mới** (balance = payload) — trước đây skip.
- Append history entry idempotent (timestamp + points + reason) — có sẵn (BUG #9 fix), mở rộng type REVERSAL.
- **Chống stale overwrite**: chỉ cập nhật nếu `payload.updatedAt >= row.LastSyncedAt` (thêm cột/field tracking hoặc so với entry mới nhất).
- Đảm bảo KHÔNG overwrite điểm POS chưa sync lên PG: vì PG giờ là authority (Phase 1 chuyển mọi write lên PG), mirror overwrite là đúng.

#### T2.3 Backfill migration (một lần, có script/admin)
- Cộng dồn SQLite + PG cho từng (customer, tenant): PG row = max(0, pg) + max(0, sqlite) (cả 2 là điểm hợp lệ; row SQLite 0-balance vô hại); union history idempotent.
- Job: `BackfillLoyaltyToPgJob` (admin endpoint, pattern `BackfillCustomersToPg` trong `AdminController`).
- **Thứ tự bắt buộc**: chạy backfill TRƯỚC khi bật POS proxy write (T1.4) để tránh mất điểm POS.

#### T2.4 Read path — Silo fallback qua Gateway
**File:** `3_CoreHub/Services/LoyaltyReadRouter.cs`
- Silo: đọc SQLite mirror; nếu mirror không có row/khả nghi → fallback `GetBalanceAsync` qua Gateway internal (đảm bảo khách luôn thấy đúng).

**Test Phase 2:**
- `LoyaltySyncSubscriberTests` mở rộng: payload mới (customerId/tenantId) upsert đúng row; row chưa có → tạo; stale payload → skip; duplicate entry → skip.
- `LoyaltyBalanceSyncPublisherTests`: subject đúng `vanan.cloud.loyalty.changed.{deviceId}`; payload đủ field.
- E2E-ish integration: award Gateway (PG) → event → SQLite balance đổi (dùng NATS thật trong test infra nếu có; else mock publisher + test subscriber logic).

---

### PHASE 3 — Budget hợp lệ (đăng ký + enforce mọi path)

#### T3.1 Admin API — đăng ký budget per tenant
**File:** `2_Gateway/Controllers/LoyaltyConfigController.cs`
- `PUT /api/platform/loyalty/tenant/{tenantId}/config` request DTO mở rộng: `monthlyPointsBudget`, `dailyPointsBudget`, `perCustomerDailyLimit`, `perOrderRateCap` → `config.SetBudgetCaps(...)` (Domain đã có).
- Response DTO thêm 4 field.

#### T3.2 UI — `LoyaltyConfigAdmin.razor` (ShopERP)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyConfigAdmin.razor`
- Form: Monthly/Daily budget, Per-customer daily, Per-order rate cap (%) + hiển thị counters đã dùng (`PointsIssuedThisMonth/Today`) + nút Reset (gọi internal reset job).

#### T3.3 Enforcement — bỏ phụ thuộc flag (flag = emergency off, default **ON**)
**File:** `3_CoreHub/Services/FeatureFlagService.cs` + `OrderWorkflowService.cs` + `LoyaltyPointLedgerService.cs`
- `ValcnV2_LoyaltyBudget` default **true** (đổi `false` → `true`).
- `LoyaltyPointLedgerService.AwardAsync` gọi budget check **không điều kiện** (không `if flag`); flag chỉ dùng cho SystemAdmin tắt khẩn cấp.
- Per-customer daily: `LoyaltyIssuanceRecord` giờ **PG** (T1.2) → check chuẩn cho mọi path (kể cả POS proxy).

#### T3.4 Reversal decrement (đã có, verify)
**File:** `3_CoreHub/Services/RefundOrchestrationService.cs` (Step 2c)
- Verify: `DecrementIssuanceAsync` + `MarkReversed` chạy với ledger mới; Alliance reversal qua wallet REFUND.

**Test Phase 3:**
- `LoyaltyBudgetServiceTests` (mở rộng): monthly 1000 → 2 orders 600+600 → lần 2 còn 400; daily cap; per-customer daily (qua issuance record PG); rate cap; counter atomic (`ExecuteUpdate`); reversal decrement → award lại được.
- `LoyaltyConfigControllerTests`: PUT budget → config lưu; invalid (âm) → 400.

---

### PHASE 4 — 1 công thức duy nhất + Alliance VND (đã APPROVED)

#### T4.1 `LoyaltyPointsCalculator` (NEW, pure, testable)
**File:** `3_CoreHub/Services/LoyaltyPointsCalculator.cs` (NEW)
```csharp
public static class LoyaltyPointsCalculator
{
    // D1 (APPROVED 2026-09-20): base = NET revenue = SubTotal − DiscountAmount (bỏ VAT + phí ship)
    // Silo: base × rate  |  Alliance: base / VndPerPoint (Option A APPROVED — loyalty_points_visibility Phase 3)
    public static int Calculate(decimal netRevenue, PointsFormula formula); // rate, min, max, mode, vndPerPoint
    public static decimal NetRevenue(decimal subTotal, decimal discountAmount); // clamp ≥ 0
}
```
- Resolution thứ tự config (không đổi): tenant settings (`ShopFeatureSettings`) → PG `LoyaltyGlobalConfig` (int % → /100) → appsettings.
- Clamp min/max — 1 nơi duy nhất.
- **D1 (APPROVED)**: base = net revenue (`SubTotal − DiscountAmount`). Lưu ý: `Order.SubTotal` KHÔNG giảm theo discount trong entity (discount chỉ trừ ở `TotalAmount`) → base phải tính `SubTotal − DiscountAmount`, không phải `SubTotal`.

#### T4.2 Banner đọc ĐIỂM THỰC TẾ (bỏ recompute)
**File:** `2_Gateway/Controllers/PublicOrdersController.cs`
- `ComputePointsAwardedAsync` → thay bằng `GetAwardedPointsAsync(orderId, tenantId)` (ledger/AllianceTransaction `SourceOrderId`): completed/delivered → số THẬT; null nếu chưa award. Không còn drift config.

#### T4.3 Checkout estimate qua server (bỏ formula client)
**File:** `5_WebApps/KhachLink/Pages/Checkout.razor` + Gateway endpoint mới
- Gateway: `GET /api/loyalty/estimate?tenantId={t}&subTotal={s}&discountAmount={d}` → `LoyaltyPointsCalculator` (base = `s − d`, cùng clamp) — **D1: net revenue**.
- KhachLink gọi endpoint này thay vì tự tính `(int)(orderTotal*rate)`; trước khi tạo order gửi `subTotal − discountAmount` từ cart; sau khi tạo order thành công gọi lại với `order.SubTotal − order.DiscountAmount` thực tế.

**Test Phase 4:**
- `LoyaltyPointsCalculatorTests` (D1 — net revenue): SubTotal=100,000, Discount=10,000 (VAT 8% + shipping 15,000 bỏ qua) → base=90,000; Silo rate 10% → 9,000 điểm; clamp min 10; max 500; Alliance 90,000 / 1000 = 90; rate 0 → fallback; VndPerPoint 0 → default 1000; NetRevenue clamp ≥ 0 khi discount > subtotal.
- `PublicOrdersControllerTests`: banner trả số thực từ ledger; order chưa award → null.

---

### PHASE 5 — Alliance attribution khi tiêu điểm (FIFO + settlement)

#### T5.1 Consume attribution trên AllianceWallet — ưu tiên tenant hiện tại (D2 APPROVED)
**File:** `3_CoreHub/Services/AllianceWalletService.cs`
- `DeductPointsAsync` (REDEEM): trước khi trừ pool, xác định nguồn theo thứ tự:
  1. Đọc tx log: per-tenant `netEarn = SUM(EARN) − SUM(REDEEM.SourceTenantId == tenant)` (có T1.1).
  2. **Ưu tiên tenant hiện tại** (redeeming tenant): trừ hết `netEarn` của tenant này trước.
  3. Thiếu → các tenant khác theo **FIFO** (EARN sớm nhất còn dư trừ trước).
  4. Ghi **1 REDEEM entry cho mỗi nguồn** (append-only, khớp pattern hiện tại): mỗi entry `TransactionTenantId = tenant đang tiêu`, `SourceTenantId = tenant sở hữu điểm`, `Points = −số trừ từ nguồn đó`. Tổng points = yêu cầu.
- `GetWalletByDeviceIdAsync` breakdown (Gateway `/api/loyalty/wallet`): dùng `netEarn` per tenant (đã trừ đúng SourceTenantId) — attribution chính xác.
- Ví dụ: earn A=100 (t1), B=50 (t2); redeem 80 **tại B** → REDEEM −50 (B→B) + REDEEM −30 (A→B); balance 70; breakdown A=70, B=0.

#### T5.2 Settlement cơ bản (report)
- **File:** `2_Gateway/Controllers/LoyaltyController.cs` (hoặc `LoyaltyConfigController`) — `GET /api/platform/loyalty/settlement?tenantId=`
  - Trả: `pointsEarnedAtTenant`, `pointsConsumedAtTenant` (REDEEM có SourceTenantId=tenant khác → "chi hộ"), `pointsRedeemedByCustomersAtTenant` (REDEEM TransactionTenantId=tenant).
  - Dữ liệu cho SystemAdmin quyết định bù trừ giữa các tenant (feature report, không tự động trừ tiền).

**Test Phase 5:**
- `AllianceWalletAttributionTests` (D2): earn A=100 (t1), B=50 (t2); redeem 80 **tại B** → REDEEM −50 (B→B) + REDEEM −30 (A→B); breakdown A=70, B=0; redeem tại A 60 → trừ A trước (60) → breakdown A=10, B=0; redeem > tổng → reject; idempotency giữ nguyên.

---

## 5. DANH SÁCH FILE

| # | File | Phase | Loại |
|---|---|---|---|
| 1 | `1_Shared/Domain.cs` (AllianceTransaction.SourceTenantId) | 1 | MODIFY + migration PG |
| 2 | `3_CoreHub/Services/LoyaltyPointLedgerService.cs` (+ interface) | 1 | NEW |
| 3 | `2_Gateway/Controllers/InternalLoyaltyController.cs` | 1 | NEW |
| 4 | `5_WebApps/ShopERP/Services/LoyaltyRewardsServiceHttpProxy.cs` | 1 | NEW |
| 5 | `1_Shared/Services/ILoyaltyRewardsService.cs` (+ call sites) | 1 | MODIFY |
| 6 | `3_CoreHub/Services/AllianceWalletService.cs` | 1+5 | MODIFY |
| 7 | `3_CoreHub/Services/OrderWorkflowService.cs` | 1 | MODIFY (refactor) |
| 8 | `5_WebApps/ShopERP/Program.cs` | 1 | MODIFY |
| 9 | `3_CoreHub/Services/LoyaltyBalanceSyncPublisher.cs` | 2 | NEW |
| 10 | `3_CoreHub/Services/LoyaltyRewardsService.cs` | 2 | MODIFY (dùng publisher chung) |
| 11 | `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs` | 2 | MODIFY |
| 12 | `2_Gateway/Controllers/AdminController.cs` (backfill job) | 2 | MODIFY |
| 13 | `3_CoreHub/Services/LoyaltyReadRouter.cs` | 2 | MODIFY |
| 14 | `2_Gateway/Controllers/LoyaltyConfigController.cs` | 3 | MODIFY |
| 15 | `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyConfigAdmin.razor` | 3 | MODIFY |
| 16 | `3_CoreHub/Services/FeatureFlagService.cs` | 3 | MODIFY |
| 17 | `3_CoreHub/Services/LoyaltyPointsCalculator.cs` | 4 | NEW |
| 18 | `2_Gateway/Controllers/PublicOrdersController.cs` | 4 | MODIFY |
| 19 | `2_Gateway/Controllers/LoyaltyController.cs` (+ estimate) | 4 | MODIFY |
| 20 | `5_WebApps/KhachLink/Pages/Checkout.razor` | 4 | MODIFY |
| 21 | `2_Gateway/Controllers/LoyaltyController.cs` / `LoyaltyConfigController.cs` (settlement) | 5 | MODIFY |
| 22 | Migrations (PG): `SourceTenantId`, (nếu cần) `LastSyncedAt` mirror | 1+2 | NEW |

---

## 6. THỨ TỰ THỰC THI & ROLLOUT (an toàn)

| Batch | Nội dung | Risk | Activate |
|---|---|---|---|
| **Batch 1** | Phase 2 (sync fix) — KHÔNG đổi authority, chỉ đảm bảo PG award sync xuống SQLite đúng + merge an toàn | LOW | Ngay — fix "không cộng vào" cho đơn Gateway-completed |
| **Batch 2** | Phase 1 (ledger + internal API + proxy POS) + backfill T2.3 | HIGH (đổi write path) | Sau backfill, có flag tạm thời để quay đầu |
| **Batch 3** | Phase 3 (budget) + Phase 4 (calculator/banner/estimate) | MEDIUM | Ngay (budget: tenant nào có config mới bị cap) |
| **Batch 4** | Phase 5 (FIFO attribution + settlement report) | MEDIUM | Ngay (chỉ Alliance) |

> **Quan trọng:** Batch 2 phải sau Batch 1 (mirror đã merge an toàn) và sau backfill. Giữ flag `LoyaltyLedgerV2` (mới, default ON sau backfill) để rollback nhanh.

## 7. VERIFY CHECKLIST (RV)

| # | Test | Pass criteria |
|---|---|---|
| V1 | Khách đặt hàng KhachLink → completed (Gateway) | Số dư SQLite tăng đúng; `/api/loyalty/my` hiện đúng; log Gateway "Awarded X points" |
| V2 | POS order hoàn thành (ShopERP) | Proxy award → PG row đúng tenant; mirror sync; khách thấy điểm |
| V3 | Khách mua tenant A rồi B (Silo) | 2 row riêng; redeem tại B chỉ dùng điểm B; redeem tại B không có điểm → reject |
| V4 | Tenant budget 1000/tháng | Order thứ 2 vượt → clamp đúng; counter tăng; hết budget → skip + log |
| V5 | Banner tracking | Số = điểm THỰC tế (ledger), không recompute |
| V6 | Checkout estimate | = calculator cùng input (server endpoint) |
| V7 | Alliance earn A + B → redeem tại B | Wallet giảm; breakdown per tenant đúng (FIFO); settlement report ra số đúng |
| V8 | Order cancel (flag refund ON) | Reversal: điểm trừ đúng row/wallet + counter giảm + issuance record reversed |
| V9 | Build + guard + test | `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors + Core.Tests pass |
| V10 | Regression | Loyalty + OrderWorkflow + Redemption + Community suite xanh |

---

## 8. RỦI RO & DECISIONS

| # | Decision | Trạng thái |
|---|---|---|
| **D1** | Base tính điểm = **net revenue** (`SubTotal − DiscountAmount`, bỏ VAT + phí ship) | ✅ APPROVED 2026-09-20 |
| **D2** | Alliance consume: **ưu tiên tenant hiện tại**, thiếu mới FIFO tenant khác | ✅ APPROVED 2026-09-20 |
| **D3** | Budget: **luôn enforce khi tenant có caps**; flag `ValcnV2_LoyaltyBudget` default ON (emergency off) | ✅ APPROVED 2026-09-20 |
| **D4** | Banner: **đọc điểm THỰC TẾ từ ledger** (bỏ recompute) | ✅ APPROVED 2026-09-20 |
| **D5** | POS khi Gateway down: skip award + log (không fail order), counters không tăng; bù sau bằng data repair | ⏳ Mặc định theo đề xuất (chưa cần quyết) |

---
