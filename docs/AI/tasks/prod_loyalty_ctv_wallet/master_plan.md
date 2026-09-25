# Master Plan — Fix Production: Loyalty Award 5000đ Bug + CTV Wallet Identity Fragmentation

**Ngày:** 2026-09-25
**Mode:** ANALYZE → chờ review → IMPLEMENT
**Liên quan:** Issue #185 (loyalty), #187 (CTV wallet/rút tiền)
**Trạng thái:** RCA hoàn tất — chờ approve → IMPLEMENT code xong (tasks 1,2a,4,6,7,8) — tasks 3+5 (data prod) chờ review trước khi chạy

---

## 0. RCA Summary (đã verify bằng prod data)

### Bug A — Loyalty: preview 30 điểm, cộng thật 5000 điểm

| | Preview "+30" | Award "+5000" |
|---|---|---|
| Nơi chạy | Gateway `GET /api/loyalty/estimate` (PG) | ShopERP `OrderWorkflowService.ProcessLoyaltyPointsAsync` (SQLite) |
| Config | `LoyaltyGlobalConfigs` PG: rate=0.01, max=30 → 50k×0.01=500→clamp **30** | `ShopFeatureSettings` SQLite tenant `a5b6c7d8`: **rate=1.0, max=5000** → clamp **5000** |

3 lỗi xếp chồng:

1. **Wrong tenant in award path** — `OrderWorkflowService.cs:559-584` đọc settings theo `customer.TenantId` thay vì `order.TenantId`. Đơn guest `01a0d671` (tenant `0001`) resolve customer bằng DeviceId → `c7b2dc24` (tenant `a5b6`) → dùng settings `a5b6`, đồng thời **bypass `AwardOnAllOrders=false` của tenant `0001`**.
2. **Config drift SQLite↔PG** — `a5b6c7d8` có row trong SQLite (`rate=1.0, max=5000`) nhưng KHÔNG có row trong PG `ShopFeatureSettings` → estimate (PG) dùng global config, award (SQLite) dùng config bẩn. Sync event `shop.feature.settings.changed` (#185-3) đã tồn tại nhưng row này miss (tạo trước event, hoặc event lost).
3. **Global config cũng drift giữa 2 scope** — award path ở ShopERP scope query `LoyaltyGlobalConfigs` nhưng entity bị **Ignore** trong ShopERPDbContext → exception → fallback appsettings `rate=0.1` (10%). PG global thật = `0.01, max=30`. Tức là ngay cả khi tenant không có row, ShopERP-award vẫn khác Gateway-estimate (0.1 unclamped vs 0.01 max 30).

### Bug B — CTV "Thi Le 01": ví không cập nhật

6 customer rows trùng tên, 3 devices:

| CustomerId | DeviceId | Vai trò |
|---|---|---|
| `616c70e6` | `380a5425` | stub cũ, tenant 0001 |
| `02ebb9ac` | `F4BCD6CA` | stub — chỉ có SQLite, chưa sync PG |
| `c9d10165` | `23d6a24c` | **tài khoản OTP thật** (phone encrypted) |
| `573fafef` | `F4BCD6CA` | stub |
| `7b7f3895` | `F4BCD6CA` | stub |
| `c7b2dc24` | `F4BCD6CA` | **Salesman role + 3 referrals + ví 2.340đ** |

- Backend pipeline ĐÚNG: `SalesReferrals` → `CoolingPeriodJob` (hourly, 24h) → `WalletTransactions` type=Commission đã credit 2×1.170đ hôm nay 08:30 cho `c7b2dc24`. Referral `7eaaa316` (1.500đ, tạo 9/24 14:20) Pending — hết cooling ~14:20 UTC hôm nay, by-design.
- **Root cause:** role/referral/ví gắn vào stub `c7b2dc24`; login OTP resolve về `c9d10165` → wallet = 0đ, role check 403.
- `CustomerMergeService.MergeDeviceStubsIntoLoginAsync` chỉ chuyển loyalty points + soft-delete stub — KHÔNG migrate `CommunityRoles`/`SalesReferrals`/`WalletTransactions`/`WithdrawalRequests`. Nếu merge chạy với stub là salesman → ví/hoa hồng bị orphan trên row đã xóa.
- Bug phụ: 1 device `F4BCD6CA` sinh 4 stubs — `GetByDeviceIdAsync` (`CustomerRepository.cs:24-29`) không IgnoreQueryFilters → global tenant filter của `Customers` chặn cross-tenant lookup → tạo stub trùng.
- Withdraw submit path code ổn; min rút 500.000đ vs balance 2.340đ → user thấy lỗi, dễ nhầm là "UI lỗi". `Wallet.razor` swallow lỗi `GetWalletAsync` (không hiện error → render ví 0đ).

---

## 1. Fix Plan

### Task 1 — Award path: dùng `order.TenantId` (code fix, file duy nhất)

**File:** `3_CoreHub/Services/OrderWorkflowService.cs`

Đổi block 559-584: `customer.TenantId` → `order.TenantId` cho cả settings lookup, `Loyalty_Program_Enabled` check, và log.

```csharp
// BEFORE (line 559):
if (_shopFeatureSettingsService != null && customer.TenantId.Value != Guid.Empty)
{
    var tenantSettings = await _shopFeatureSettingsService.GetSettingsAsync(customer.TenantId);
    ...
    _logger.LogWarning(ex, "... tenant {TenantId} ...", customer.TenantId);
}

// AFTER:
if (_shopFeatureSettingsService != null && order.TenantId.Value != Guid.Empty)
{
    var tenantSettings = await _shopFeatureSettingsService.GetSettingsAsync(order.TenantId);
    ...
    // logs → order.TenantId
}
```

**Lưu ý:** line 725 `AddPointsAsync(customer.Id, customer.TenantId.Value, ...)` — giữ `customer.TenantId` vì đó là ledger của customer (đúng semantics), chỉ đổi phần *formula resolution*.

### Task 2 — Unified global config cho ShopERP scope (chọn 1)

Vấn đề: `OrderWorkflowService.cs:534-557` query `LoyaltyGlobalConfigs` — entity Ignored trong ShopERPDbContext → ShopERP-award fallback appsettings 0.1, Gateway-estimate dùng PG 0.01/max30.

**User đã chọn: 2a** — replicate `LoyaltyGlobalConfigs` PG→SQLite qua sync event. Chi tiết implement:

1. **Un-ignore trong ShopERPDbContext:** xóa `_ = modelBuilder.Ignore<LoyaltyGlobalConfig>();` (line ~272). `LoyaltyGlobalConfigConfiguration` (CoreHub) auto-apply qua `ApplyConfigurationsFromAssembly` → SQLite migration tạo table `LoyaltyGlobalConfigs`. Entity là global (`TenantId.Empty`), không implement `IMustHaveTenant` → không bị tenant filter.
2. **Publisher (Gateway):** `LoyaltyConfigController.UpdateGlobalConfig` (line ~94, sau `SaveChangesAsync`) publish NATS `vanan.cloud.loyalty.config.changed` — inject `INatsEventPublisher` (đã registered `Program.cs:1306`). Payload: `{ pointsRate, minPointsPerOrder, maxPointsPerOrder, mode, maxWalletPoints, updatedAt }`. Không cần Outbox (config change hiếm, admin trigger; thêm log warn nếu publish fail).
3. **Subscriber (ShopERP):** extend `LoyaltySyncSubscriber` — thêm subscription `vanan.cloud.loyalty.config.changed` → upsert row duy nhất trong SQLite `LoyaltyGlobalConfigs`. Idempotent (apply nguyên giá trị, không merge).
4. **Backfill lần đầu:** sau deploy, trigger 1 PUT `/api/platform/loyalty/config` với giá trị hiện tại (hoặc script INSERT vào SQLite trực tiếp) để SQLite có row ngay.

### Task 3 — Data cleanup config

```sql
-- ShopERP SQLite (vanan-shoperp-1, /app/keys/vanan_shoperp.db):
UPDATE ShopFeatureSettings
SET Loyalty_PointsRate = <giá trị owner duyệt>,
    Loyalty_MaxPointsPerOrder = <giá trị owner duyệt>
WHERE TenantId = 'A5B6C7D8-1234-5678-9ABC-DEF012345678';

-- Tenant 7C021960: rate=0 → loyalty tắt thực tế nhưng Program_Enabled=1 — hỏi owner có ý định không.
```

**Khuyến nghị:** không UPDATE tay — gọi `UpdateSettingsAsync` (qua admin UI hoặc script) để phát `shop.feature.settings.changed` → PG được backfill tự động, khỏi drift tiếp. Verify sau: `SELECT * FROM "ShopFeatureSettings" WHERE "TenantId"='a5b6c7d8-...'` trong PG phải có row.

**User đã chốt (2026-09-25):** tenant `a5b6` rate = **0.001 (1 điểm/1000đ)** — giống tenant `0001`.

**⚠️ MaxPointsPerOrder pitfall:** `tenantSettings.Loyalty_MaxPointsPerOrder.HasValue` → null thì kế thừa global max=30 → đơn 50k sẽ ra 30 điểm (clamp), KHÔNG phải 50. Để "50.000đ → 50 điểm" đúng kỳ vọng, set `Loyalty_MaxPointsPerOrder = 10000` (cap cao, không ảnh hưởng đơn thường) cho a5b6. Đồng thời lưu ý tenant `0001` đang có max=10 → đơn 50k chỉ được 10 điểm — nếu owner muốn cũng phải sửa.

**Values cho a5b6:** `Loyalty_PointsRate=0.001, Loyalty_MinPointsPerOrder=1, Loyalty_MaxPointsPerOrder=10000, Loyalty_AwardOnAllOrders=1, Loyalty_Program_Enabled=1`

### Task 4 — Regression tests

**File:** `6_Tests/VanAn.Core.Tests/Services/` (tìm test file OrderWorkflowService hiện có, thêm cases):

- `ProcessLoyaltyPoints_UsesOrderTenant_NotCustomerTenant`: order tenant A (settings awardOnAll=false), customer tenant B (awardOnAll=true, rate=1.0) → assert KHÔNG award / award theo config A.
- `ProcessLoyaltyPoints_GuestCrossTenant`: order.CustomerId=null, CustomerDeviceId trỏ customer tenant khác → settings của order tenant.
- Mock `IShopFeatureSettingsService` trả settings khác nhau theo tenant → assert đúng tenant được query.

### Task 5 — CTV identity: verification trước, merge có điều kiện

**REVISED 2026-09-25 sau khi user confirm CTV login = social:**

- Social login (`SocialAuthController.GoogleCallback:93`) resolve customer bằng **Email** trong `GetAllActiveAsync()` (SQLite, chỉ active rows).
- Tất cả 6 rows đều `IdentityLevel=1 (Social)`. SQLite: 4 rows đã `IsDeleted=1` (đã merge trước đó). Active: `c9d10165` (KHÔNG có email → **không thể** là social-login target) + `c7b2dc24` (có email).
- **Kết luận:** social login của Thi Le 01 gần như chắc chắn resolve về **`c7b2dc24`** — chính là row đang giữ salesman role + referrals + ví (2.340đ đã credit 08:30 hôm nay). `c9d10165` là account phone-only riêng (KHÔNG merge vào đó — sẽ orphan ngược lại!).
- **Complaint "ví không cập nhật"** nhiều khả năng = check trước giờ CoolingPeriodJob trả (commission +1.170×2 chỉ credit 9/25 08:30 UTC) + UI nuốt lỗi. "Rút tiền UI lỗi" = min 500.000đ vs balance 2.340đ (by design) hoặc modal bug đã fix 24/9.

**Kế hoạch thực tế:**
1. Verify trước: nhờ CTV mở `/community/wallet` → phải thấy 2.340đ + 2 tx commission. Nếu thấy → không cần data merge.
2. Nếu vẫn 0đ → lấy customerId từ session (profile page / token) → so với `c7b2dc24` → lúc đó mới merge.
3. Cleanup stubs (SAU KHI verify): soft-delete trong cả SQLite + PG các stub `616c70e6`, `02ebb9ac`(SQLite-only), `573fafef`, `7b7f3895` — c7b2dc24 GIỮ LẠI (canonical). `c9d10165` chỉ soft-delete nếu xác nhận là stub/test account của cùng người.
4. **Lưu ý sync gap:** PG `Customers` vẫn có 5 rows active (soft-delete ở SQLite chưa sync lên PG) → nếu dọn, phải dọn cả 2 DB.

```sql
-- Chỉ chạy SAU khi verify canonical = c7b2dc24:
-- PG: UPDATE "Customers" SET "IsDeleted"=true WHERE "Id" IN ('616c70e6-...','573fafef-...','7b7f3895-...');
-- (c9d10165 giữ nguyên — phone account riêng, không phải stub device)
```

### Task 6 — Extend `CustomerMergeService` (code fix — ngăn tái phát)

**File:** `3_CoreHub/Services/CustomerMergeService.cs`

Trong `MergeDeviceStubsIntoLoginAsync`, trước khi `stub.SoftDelete()`:

```csharp
// Migrate community data từ stub → login customer:
await _dbContext.CommunityRoles
    .Where(r => r.CustomerId == stub.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.CustomerId, loginCustomerId));
await _dbContext.SalesReferrals
    .Where(r => r.SalesmanId == stub.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.SalesmanId, loginCustomerId));
await _dbContext.WalletTransactions
    .Where(w => w.OwnerId == stub.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(w => w.OwnerId, loginCustomerId));
await _dbContext.WithdrawalRequests
    .Where(w => w.OwnerId == stub.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(w => w.OwnerId, loginCustomerId));
```

**Đã verify (2026-09-25):** `CommunityRoles`/`SalesReferrals`/`WalletTransactions`/`WithdrawalRequests` bị `Ignore()` trong `ShopERPDbContext` (line 253-265) — PG-only. NHƯNG ShopERP DI có sẵn `VanAnDbContext` (PG) — `SocialAuthController:23` inject trực tiếp. → Merge service inject thêm `VanAnDbContext? pgContext` và migrate 4 bảng này trên PG, còn Customers/LoyaltyRewards vẫn trên `_dbContext` (SQLite). Trên Gateway `IVanAnDbContext == VanAnDbContext` (cùng PG) → guard `ReferenceEquals` để không chạy 2 lần.

**Lưu ý cũ (đã giải quyết):** service chạy trong ShopERP scope (SQLite) — SalesReferrals/WalletTransactions/CommunityRoles là **PG-only tables**.

### Task 7 — Fix stub duplication (cross-tenant DeviceId lookup)

**File:** `3_CoreHub/Infrastructure/Repositories/CustomerRepository.cs:24-29`

```csharp
public async Task<Customer?> GetByDeviceIdAsync(Guid deviceId)
{
    return await _context.Customers
        .IgnoreQueryFilters()  // deviceId identity là cross-tenant — stub phải reuse toàn hệ thống
        .Where(c => c.DeviceId == deviceId && !c.IsDeleted)
        .OrderByDescending(c => c.IdentityLevel)  // ưu tiên Verified > Social > Guest
        .FirstOrDefaultAsync();
}
```

**Trade-off cần review:** IgnoreQueryFilters trên Customers là mở cross-tenant read — chấp nhận được cho *device-identity resolution* (device vật lý không thuộc tenant), nhưng phải đảm bảo caller không dùng kết quả để ghi data vào sai tenant. Award path dùng nó để resolve customer — sau Task 1 đã dùng `order.TenantId` cho formula nên safe.

### Task 8 — Wallet UI: surface lỗi thay vì render 0đ (optional, nhỏ)

**File:** `5_WebApps/KhachLink/Pages/Wallet.razor`

`LoadWalletAsync` hiện `if (result.Success)` else im lặng → thêm `_loadError` banner khi API fail/401 → user biết là lỗi thay vì "ví không cập nhật".

---

## 1b. Tiến độ IMPLEMENT (2026-09-25, sau khi user approve)

| Task | Trạng thái | Chi tiết |
|---|---|---|
| 1 — order.TenantId | ✅ DONE | `OrderWorkflowService.cs:559-591` — settings lookup + Program_Enabled + logs đều dùng `order.TenantId` |
| 2a — replicate LoyaltyGlobalConfigs PG→SQLite | ✅ DONE | ① ShopERPDbContext bỏ `Ignore<LoyaltyGlobalConfig>` + migration `20260925130531_AddLoyaltyGlobalConfigMirror` ② `LoyaltyConfigController` publish `vanan.cloud.loyalty.config.changed` ③ `LoyaltySyncSubscriber.SyncGlobalConfigAsync` upsert idempotent |
| 4 — regression tests | ✅ DONE | `OrderWorkflowTenantFormulaTests.cs` (2 tests) — **red-green verified**: revert về `customer.TenantId` → cả 2 FAIL; fix → PASS. Chú ý: `BaseEntity : IMustHaveTenant` → Order cũng bị tenant filter — test dùng decorator `CrossTenantOrderRepository` (IgnoreQueryFilters cho order load) + ambient = customer tenant |
| 6 — extend CustomerMergeService | ✅ DONE | Thêm `VanAnDbContext? pgContext` (optional — ShopERP DI có sẵn PG context; Gateway `_dbContext as VanAnDbContext` tự dùng). Migrate 4 bảng PG bằng `ExecuteUpdateAsync` + `IgnoreQueryFilters` (chỉ re-point owner FK, giữ nguyên TenantId — không rò rỉ cross-tenant). Test mới `Merge_GuestStub_MigratesCommunityData` PASS |
| 7 — GetByDeviceIdAsync | ✅ DONE (đổi cách) | **KHÔNG dùng IgnoreQueryFilters** (rủi ro cross-tenant read) — dùng `OrderByDescending(IdentityLevel).ThenByDescending(CreatedAt)` deterministic: ưu tiên Verified > Social > Guest, mới nhất. Root cause stub trùng = merge xóa stub → checkout guest sau tạo stub mới; ordering fix giúp resolve đúng account thật khi nhiều row cùng device |
| 8 — Wallet.razor | ✅ DONE | `LoadWalletAsync` else-branch hiện `alert-danger` với `ErrorMessage` thay vì im lặng render ví 0đ |

**Deviation đã ghi nhận so với plan gốc:**
- Task 6: plan đề xuất migrate trên `_dbContext` — thực tế community tables PG-only trong ShopERP scope → migrate qua `pgContext` (PG), và thêm `IgnoreQueryFilters` (nghiệp vụ: stub/order cross-tenant, chỉ đổi owner FK).
- Task 7: plan đề xuất `IgnoreQueryFilters()` — thực tế chọn ordering deterministic (an toàn hơn, không mở cross-tenant read). Nếu sau này vẫn sinh stub trùng, cân nhắc lại.
- Test cross-tenant phát hiện `Order` kế thừa `BaseEntity : IMustHaveTenant` → bị global tenant filter (điều này có thể ảnh hưởng prod: order load qua `GetByIdWithIncludesAsync` chỉ thấy order cùng tenant ambient — cần kiểm tra khi deploy).

**Validation đã chạy (2026-09-25):**
- `dotnet build` CoreHub/Gateway/ShopERP/KhachLink: ✅ 0 error
- `dotnet test VanAn.Core.Tests` (full): ✅ **1816 passed / 0 failed / 20 skipped**
- `dotnet test VanAn.ShopERP.Tests`: ✅ 99 passed
- Red-green: regression tests FAIL với code cũ, PASS với fix ✅
- `dotnet ef migrations add AddLoyaltyGlobalConfigMirror`: ✅ (lưu ý: EF design-time host báo DI warning `IJwtTokenService` scoped-in-singleton — pre-existing, không phải từ change này; runtime ShopERP vẫn chạy bình thường)

## 2. Thứ tự thực hiện đề xuất

| # | Task | Loại | Risk |
|---|---|---|---|
| 1 | Task 1 (order.TenantId) | Code | Thấp |
| 2 | Task 4 (tests) | Test | — |
| 3 | Task 3 (config cleanup qua UpdateSettingsAsync) | Data | Thấp — cần owner chốt rate |
| 4 | Task 5 (merge Thi Le 01) | Data | Trung — cần verify login identity trước |
| 5 | Task 7 (GetByDeviceIdAsync) | Code | Trung — cross-tenant read |
| 6 | Task 6 (extend merge service) | Code | Cần verify DbSet availability |
| 7 | Task 2 (global config unify) | Code+Data | Tùy option |
| 8 | Task 8 (UI error surface) | UI | Thấp |

## 3. Verification (RV) sau deploy

1. Tạo đơn test tenant `0001` bằng device của customer thuộc tenant khác → award theo config `0001` (rate 0.001, max 10 → đơn 50k = 10 điểm do clamp max; và awardOnAll=false → 0 nếu không tracking code).
2. Đơn tenant `a5b6` 50.000đ → estimate == award (sau khi config sạch).
3. Thi Le 01 login → `/community/wallet` thấy balance 2.340đ + 2 tx commission; referral pending 1.500đ → sau 14:20 UTC thấy +1.500đ.
4. `dotnet build VanAn.sln` + `dotnet test` (Core.Tests) pass.

## 4. Open Questions — ĐÃ CHỐT (2026-09-25)

1. Tenant `a5b6` rate = **0.001** (1điểm/1000đ) ✅
2. Thi Le 01 login = **social** → canonical = `c7b2dc24` (row active duy nhất có email). Task 5 revise: verify ví trước, merge có điều kiện. ✅
3. Task 2 = **2a** (replicate LoyaltyGlobalConfigs PG→SQLite) ✅
4. Chỉ xử lý **Thi Le 01** — không audit diện rộng ✅

**Remaining verify (runtime):** CTV mở `/community/wallet` — nếu thấy 2.340đ thì complaint là do cooling-period timing; nếu 0đ → lấy customerId từ session rồi xử lý tiếp.
