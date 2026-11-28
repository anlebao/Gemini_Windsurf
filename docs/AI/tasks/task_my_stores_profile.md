# TASK CARD: "Cửa hàng của tôi" — Profile list tenant đã mua + điểm thưởng per-tenant

> **DONE — `374acb16`** (approved + implemented 2026-11-28 · TDD 12/12 · guard ALL PASSED)
> Created: 2026-11-28 · Mode: IMPLEMENT (sau approve) · TDD · Sweep-first (đã xong trong review)
> Nguồn: review session 2026-11-28 — khách mua ở nhiều tenant muốn thấy list
> tenant + số điểm còn lại từng tenant để chủ động đổi điểm.
> Rule: **KHÔNG chạm Domain/DB/migration — không cần Gate 5** (pure PG read-path +
> UI). UI Platform components only (`VanAnCard`/`VanAnButton`).

## OBJECTIVE

1. `GET /api/customers/my-stores` (Gateway): trả list tenant khách đã tương tác
   (mua hàng / có điểm) kèm `pointBalance` per-tenant — union 3 nguồn PG:
   Orders ∪ LoyaltyRewards (Silo) ∪ AllianceWallet breakdown.
2. `Profile.razor`: zone "Cửa hàng của tôi" — mỗi row: logo + tên + N đơn/lần
   cuối + badge điểm + nút "Vào cửa hàng" → `/store/{slug}` (same-origin,
   đúng Fix-1 semantics).
3. Reuse tối đa: `IAllianceWalletService` breakdown · `ITenantSearchService`
   batch map · `my-identity` forward pattern · `X-Customer-Device-Id` header
   pattern (guest — giống `CustomerOrdersController`).

## CURRENT STATE — SWEEP (verified 2026-11-28)

### Nguồn dữ liệu đã có trong Gateway PG (cross-tenant, mọi VPS)

| Nguồn | Field | Verify |
|---|---|---|
| `Orders` | `CustomerDeviceId` (string, Guid format D) + `TenantId` + `TotalAmount` + `CreatedAt` | Gateway order-creator → PG; guest order cũng ghi deviceId (`OrderService.cs:1115`) |
| `Customers` | `DeviceId` + `TenantId` | PG stubs do `EnsureCustomerStubAsync` tạo khi có loyalty write — vẫn ghi DeviceId (`LoyaltyPointLedgerService.cs:562`) |
| `LoyaltyRewards` | `(CustomerId, TenantId) → PointBalance` | **Silo balance trong PG** — mọi write qua `LoyaltyPointLedgerService` trên Gateway; ShopERP `LoyaltyRewardsServiceHttpProxy` chỉ forward (`Program.cs:204`) |
| `AllianceWallet` + `AllianceTransactions` | `IAllianceWalletService.GetWalletByDeviceIdAsync` + `GetTenantBalancesAsync` | alliance per-tenant points — đã dùng trong `GET /api/loyalty/wallet` |
| `Tenants` + `Settings` | `Name`, `Settings.Slug`, `Settings.LogoUrl` | `ITenantSearchService.GetStoreInfoAsync` (anti-drift rule W3-2d) |

### Gap

- `/api/loyalty/my` chỉ trả **1 tenant** (token → customerId → ResolveCustomerTenant).
- `/api/loyalty/wallet` breakdown chỉ **Alliance mode** — Silo không có tương đương.
- `Profile.razor` không có tenant list — chỉ quick-access links.
- Chưa có endpoint "my stores" nào (đã grep `my-stores|my-tenants` — 0 hit).

### Code reuse đã định danh

| Piece | Reuse cách |
|---|---|
| `my-identity` forward | copy pattern `LoyaltyController.GetWallet` (`LoyaltyController.cs:241-262`) |
| `X-Customer-Device-Id` header | copy pattern `CustomerOrdersController.GetMyOrders` (`CustomerOrdersController.cs:28-35`) — guest path |
| `ITenantSearchService` | += `GetStoreInfoMapAsync(ids)` batch (mirror `GetFeatureFlagMapAsync`/`GetKhachLinkDomainMapAsync`) — anti-drift: mọi tenant-read qua service này |
| UI mẫu | `AllianceWallet.razor` breakdown markup + `VanAnCard`/`VanAnButton` |

### Fact nền đã verify

- `customer_device_id` trong localStorage đã tồn tại (OrderHistory gửi header).
- `Core.Tests` reference trực tiếp `VanAn.Gateway.csproj` (csproj:62) → test
  được `VanAn.Gateway.Services` bằng in-memory Sqlite (pattern
  `InstanceFeatureGateTests`).
- `Order.CustomerDeviceId` lưu `Guid.ToString()` format "D" → so sánh
  `deviceId.ToString()` chính xác.

## IMPACT-NGUỢC GUARDS (không được vỡ)

- **Read-only** — không write path mới; `IgnoreQueryFilters` là by-design
  (cross-tenant read, cùng pattern wallet/ATRS-9) + `AsNoTracking`.
- Identity = deviceId bearer — chỉ trả data của CHÍNH device đó; ưu tiên
  token→`my-identity` (validated); header `X-Customer-Device-Id` chỉ là
  fallback (cùng trust level `customerorders` hiện hữu — KHÔNG tăng exposure).
- Fail-open per-source: alliance lookup / orders / rewards lỗi → trả phần còn
  lại + log warning (không 500 vì 1 nguồn).
- Tenant set cap **50** (order by LastOrderAt desc, nulls last).
- Order `Status == "cancelled"` → loại khỏi stats; tenant chỉ có đơn cancelled
  thì vẫn liệt kê nếu có điểm.
- Anonymous GET giữ nguyên contract: không token + không header → **401**
  (không leak gì cả).
- Endpoint mới — không đụng `/api/loyalty/my`, `/api/loyalty/wallet`,
  `/api/customerorders` (contract cũ giữ nguyên).
- UI: section ẨN hoàn toàn khi list rỗng (guest chưa mua không thấy gì).
- Domain layer — KHÔNG đụng.

## IMPLEMENTATION CHECKLIST

### Phase A — `MyStoresService` + `ITenantSearchService.GetStoreInfoMapAsync` (TDD — test trước)

- [x] `ITenantSearchService` += `Task<Dictionary<Guid, TenantStoreDto>>
  GetStoreInfoMapAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken)`:
  single query `Tenants.Where(t => ids.Contains(t.Id)).Include(Settings)` →
  map `TenantStoreDto` (reuse logic `GetStoreInfoAsync` — gồm logo presign
  nếu đang có). Id không match → vắng trong dict (caller fallback name
  "Cửa hàng {id8}").
- [x] `2_Gateway/Services/MyStoresService.cs` (namespace `VanAn.Gateway.Services`,
  mirror `InstanceFeatureGate` style — primary ctor + XML doc):
  `Task<IReadOnlyList<MyStoreDto>> GetMyStoresAsync(Guid deviceId, CancellationToken)`.
  - `Guid.Empty` → empty list.
  - Orders: `Orders.IgnoreQueryFilters().AsNoTracking()
    .Where(o => o.CustomerDeviceId == deviceId.ToString() && !o.IsDeleted)`
    group by TenantId → `OrderCount` (đếm non-cancelled), `LastOrderAt` (max
    CreatedAt — tính cả cancelled), `TotalSpent` (sum TotalAmount non-cancelled).
  - Silo: `Customers.IgnoreQueryFilters().Where(c => c.DeviceId == deviceId
    && !c.IsDeleted).Select(c => c.Id)` → `LoyaltyRewards.IgnoreQueryFilters()
    .Where(r => customerIds.Contains(r.CustomerId))` group by TenantId →
    sum PointBalance.
  - Alliance: `IAllianceWalletService.GetWalletByDeviceIdAsync(deviceId)`
    → `GetTenantBalancesAsync(wallet.Id)` → dict tenantId→netPoints
    (clamp ≥0 — mirror wallet endpoint `Math.Max(0, b.NetPoints)`).
  - Merge rule: `pointBalance = alliancePts ?? siloPts ?? 0`; `Mode =
    alliancePts != null ? "Alliance" : "Silo"`. Union key = TenantId.
  - Enrich: `GetStoreInfoMapAsync(unionIds)` → name/slug/logo; tenant thiếu
    → fallback `Name = $"Cửa hàng {id8}"`.
  - Sort: `LastOrderAt desc` (nulls last) → `PointBalance desc`; `Take(50)`.
- [x] DI `Program.cs`: `AddScoped<IMyStoresService, MyStoresService>()` —
  đặt cạnh block loyalty (~line 1740).

**Gate A (TDD):** `6_Tests/VanAn.Core.Tests/Services/MyStoresServiceTests.cs`
— in-memory Sqlite + `VanAnDbContext` (mirror `InstanceFeatureGateTests`
fixture; mock `IAllianceWalletService` + `ITenantSearchService` bằng stub class
trong test file). ≥8 tests:

1. Orders 2 tenant → 2 rows đúng `OrderCount/LastOrderAt/TotalSpent`.
2. Cancelled order loại khỏi count/spent.
3. Customer 2 tenant (DeviceId chung) + LoyaltyRewards → silo pts đúng tenant.
4. Alliance wallet breakdown → tenant có `Mode="Alliance"` + điểm từ wallet
   (không lấy silo row).
5. Union: tenant có điểm nhưng 0 đơn → vẫn listed (orderCount=0).
6. Không data → `[]`. Guid.Empty → `[]`.
7. Tenant name/slug từ map; tenant thiếu → fallback `Cửa hàng {id8}`.
8. Alliance service throw → vẫn trả orders+silo (fail-open).
9. Sort: gần nhất trước; cap 50.

### Phase B — `CustomerStoresController` (Gateway)

- [x] `2_Gateway/Controllers/CustomerStoresController.cs` `[ApiController]
  [Route("api/customers")] [AllowAnonymous]` — `GET my-stores`:
  - deviceId resolve: (a) `X-Customer-Token` → forward `GET
    /api/loyalty/my-identity` (shoperp client) → `deviceId` (copy block từ
    `GetWallet`); (b) không token → header `X-Customer-Device-Id` (Guid.TryParse).
  - Cả 2 vắng/invalid → `401 { error }`.
  - `Ok(await _myStores.GetMyStoresAsync(deviceId, ct))`.
  - try/catch → 500 `{ error }` (pattern controller hiện hữu).
- [x] DTO `MyStoreDto` trong file service: `TenantId, Name, Slug?, LogoUrl?,
  OrderCount, TotalSpent, LastOrderAt?, PointBalance, Mode`.

**Gate B:** controller test nhẹ trong `MyStoresServiceTests` hoặc file riêng —
`MockHttpClientFactory` (pattern `DeviceRegistrationControllerTests`): token
hợp lệ → identity resolve → 200 list; không gì → 401; header-only guest → 200.

### Phase C — KhachLink UI

- [x] `Services/Http/MyStoresHttpService.cs` (mirror `AllianceWalletHttpService`):
  `GetMyStoresAsync(token, deviceId)` → `MyStoreDto[]` — gửi CẢ
  `X-Customer-Token` + `X-Customer-Device-Id`; non-200 → empty + log.
- [x] `Profile.razor` zone mới sau "Việc cần làm", trước "Truy cập nhanh":
  `VanAnCard` "Cửa hàng của tôi" — render khi `_myStores.Count > 0`.
  Row (mirror breakdown markup `AllianceWallet.razor:89-98`): logo/`bi-shop`
  + `fw-semibold` name + sub `N đơn · dd/MM/yyyy` + `badge bg-primary`
  "N điểm" + `VanAnButton` Outline sm "Vào cửa hàng" →
  `Slug != null ? /store/{slug} : /store/by-id/{tenantId}`.
  `data-testid="my-store-{tenantId}"` / `my-store-points-{tenantId}`.
- [x] Load trong `OnAfterRenderAsync(firstRender)` — đọc `customer_token` +
  `customer_device_id` (đã có sẵn pattern); không cần đăng nhập (guest có
  deviceId vẫn hiện nếu server trả rows).
- [x] `_navFlags.ShowStores` gate section? **Không** — giống Fix-3: quan hệ
  đã tồn tại không bị discovery kill-switch giấu. Gate `ShowLoyaltyHistory`?
  Điểm = dữ liệu loyalty → section ẩn điểm-badge khi `ShowLoyaltyHistory=false`
  (vẫn hiện list tenant + nút vào store).

### Phase D — Ship

- [x] `dotnet build` affected projects (Gateway + KhachLink + Core.Tests) 0 err
- [x] `dotnet test` filter MyStores + InstanceFeatureGate (regression) PASS
- [x] `guard-check.ps1` ALL PASSED
- [x] E2E spec `6_Testing/e2e-tests/my-stores.spec.ts` (Gate 4): profile →
  section hiện khi có data · row có tên tenant + badge điểm + link `/store/`.
- [x] RV probe `6_Testing/rv-scripts/my-stores-probe.mjs`: L1 `GET
  /api/customers/my-stores` với `X-Customer-Device-Id` của device RV có sẵn
  (dùng deviceId từ `store-context-rv-sweep` run — nó tạo order? nếu không:
  tạo deviceId giả → expect `[]` 200 chứ không 401) + assert shape DTO.
- [x] Commit → push → CD → re-run probe prod.
- [x] Update card + `project_state.md` (Python — KHÔNG PowerShell cho .md tiếng Việt).

## OUT OF SCOPE

- Visit-only tenants (chưa mua) — visit chỉ ở localStorage client; cover cần
  Fix-2 server-side visit tracking (task card riêng, đã deferred).
- Pagination/infinite scroll — cap 50 đủ cho W này.
- "Đổi điểm" deep-link vào redemption flow từng tenant — nút chính là "Vào cửa
  hàng"; redeem luật Silo diễn ra tại checkout tenant đó (không đổi).
- Cross-device merge (cùng SĐT khác thiết bị) — identity tier wave khác.
- Sắp xếp/lọc UI, search trong list.

## DECISIONS (đã chốt trong review — user duyệt card = duyệt luôn)

- D1: nguồn sự thật = **Orders PG** (đã mua), không dùng localStorage
  `khachlink_last_store` (chỉ last-1) hay `created_orders` (device-local).
  Caveat đã thông báo: visit-only tenant không hiện — đúng yêu cầu "đã mua".
- D2: identity = `deviceId` (union key cross-tenant), resolve token-first +
  header fallback — không cấp quyền mới so với `customerorders`.
- D3: service ở **Gateway** (`VanAn.Gateway.Services`) vì cần
  `ITenantSearchService` (không kéo xuống CoreHub được) — test trực tiếp từ
  Core.Tests (đã reference Gateway).
- D4: Silo points từ `LoyaltyRewards` PG (source of truth ledger), Alliance
  points từ wallet breakdown — merge rule alliance-wins.
- D5: section không bị `ShowStores` kill-switch giấu (cùng lập luận Fix-3);
  badge điểm gate theo `ShowLoyaltyHistory`.
