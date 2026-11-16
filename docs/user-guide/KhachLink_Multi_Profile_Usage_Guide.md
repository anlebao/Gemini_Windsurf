# Hướng dẫn sử dụng — KhachLink Multi-Profile (6 Loại KhachLink)

> **Tài liệu vận hành & sử dụng** cho System Admin, Tenant Owner, và các role khác.
> **Phiên bản:** R1.2 (Multi-Profile Core + Type 1 Directory + Type 4 FullCommerce + Type 6 Hoptacxa + Multi-domain + Dynamic CORS + Style/Icon override)
> **Ngày tạo:** 2026-08-15 — **Cập nhật:** 2026-11-14 (thêm profile Hoptacxa, style/icon override, activate/audit, sửa nav flags preset + cache TTL theo code hiện tại)
> **Tài liệu nguồn:** `docs/AI/tasks/khachlink_multi_profile/master_plan.md` + `release_strategy.md` + `docs/AI/tasks/dynamic_cors/master_plan.md` + `docs/AI/plans/entry-points-retention-master-plan.md`
> **Feature flag:** `KhachLink:MultiProfileEnabled` (default OFF trong config — **production hiện đang ON**, đã có instance live)

---

## 0. Tóm tắt nhanh

KhachLink Multi-Profile cho phép **cùng 1 KhachLink container** phục vụ nhiều "loại" trang khách hàng khác nhau, phân biệt theo **domain** (`*.khachvip.online` + external domain như `timlathay.com`, `commienphi.timlathay.com`, `hoptacxa.timlathay.com`). Mỗi instance = 1 cấu hình (profile + nav flags + owner tenant + style/icon override) được lưu trong PostgreSQL (Gateway source of truth). KhachLink runtime fetch cấu hình qua `GET /api/v1/khachlink-instances/by-domain/{domain}` khi tải trang, không cần restart container khi thêm instance mới. **CORS cũng động** — Gateway tự pick up `CustomDomain` từ registry mỗi 5 phút, không cần edit docker-compose hay restart Gateway (xem §1.5).

| Profile | Loại | Mô tả | Owner | Trạng thái |
|---|---|---|---|---|
| `FullCommerce` (0) | Type 4 | TMĐT đầy đủ cho 1 tenant, domain riêng | 1 tenant (OwnerTenantId) | ✅ Live (`diemthuong2.khachvip.online` seed) |
| `Directory` (1) | Type 1 | Danh bạ shop/doanh nghiệp/sản phẩm — ẩn cart, orders, rewards | Platform (null) | ✅ Live (`timlathay.com`) |
| `Reseller` (4) | Type 5 | Tenant trung gian bán lại sản phẩm/dịch vụ cho tenant con | 1 reseller tenant | ✅ Preset implemented, selectable trong admin UI |
| `Logistics` (2) | Type 2 | Sàn shipper — reuse Community Commerce | Platform/tenant | ⏳ R3 — option enabled nhưng chưa có preset riêng phía server |
| `JobMarket` (3) | Type 3 | Sàn việc/dịch vụ — list "job" như Product (filter text) | Platform/tenant | ⏳ R3 — option enabled; `/jobs` chưa ship (ShowJobs là dead flag) |
| `Hoptacxa` (5) | Type 6 | Portal hợp tác xã — danh bạ NHIỀU HTX + member shops, browse-only | Platform (null) | ✅ Live (`hoptacxa.timlathay.com`, `commienphi.timlathay.com`) |

> **Dropdown Profile trong admin UI:** cả 6 option đều **selectable** (không còn disable). Logistics/JobMarket vẫn gắn nhãn "— R3" trong option text.

---

## 1. Kiến trúc & nguyên tắc vận hành

### 1.1 Single deployment + multi-domain routing
- **KHÔNG** tạo Docker image riêng per profile. Cùng 1 KhachLink container phục vụ tất cả instance.
- nginx wildcard `*.khachvip.online` (trừ `api2`/`app2`/`www2`/`diemthuong2` đã có explicit block) → proxy tới KhachLink container. External domain (vd `timlathay.com`) cần DNS + nginx block riêng.
- KhachLink runtime đọc `window.location.hostname` qua JS interop → gọi Gateway API → nhận cấu hình instance.
- **Thêm instance mới = tạo record + DNS + SSL expand → KHÔNG cần restart KhachLink container, KHÔNG cần restart Gateway, KHÔNG cần edit docker-compose** (CORS tự pick up trong 5 phút — xem §1.5).

### 1.2 Vị trí lưu trữ dữ liệu
| Dữ liệu | Nơi lưu | Ghi chú |
|---|---|---|
| `KhachLinkInstances` table | PostgreSQL (Gateway) | Source of truth. EF migration `AddKhachLinkInstances` |
| Nav flags (15 bool) | PostgreSQL (flattened columns) | Owned entity `KhachLinkNavFlags`. **Admin UI chỉ hiển thị 14 toggle** — `ShowJobs` đang ẩn (dead flag, `/jobs` chưa ship) |
| Style overrides (`Theme`, `LogoUrl`, `NavColor`, `HeaderColor`, `FooterColor`) | PostgreSQL (`KhachLinkInstances` columns, nullable) | Issue #143 — null = kế thừa tenant ShopConfig |
| PWA icon per-instance | PostgreSQL (`IconData` byte[] ≤ 2MB) + serve qua `GET /api/public/khachlink-instances/icons/{id}` | 2026-10-15 — override; null = icon mặc định theo Profile |
| Products (cho Directory/JobMarket) | ShopERP SQLite (per-tenant) | Type 1/3 dùng existing `Product` entity, chỉ khác text content |
| Orders (FullCommerce/Reseller) | PostgreSQL (Gateway) → NATS → ShopERP SQLite | Option C routed async delivery |
| **CORS allowed origins** | PostgreSQL (`KhachLinkInstance.CustomDomain`) + `appsettings` static | Dynamic — xem §1.5 |

### 1.3 Feature flag
```json
// 2_Gateway/appsettings.json
"KhachLink": {
  "MultiProfileEnabled": false   // default OFF trong repo config
}
```
- **OFF** → endpoint `/by-domain/{domain}` trả 404 → KhachLink runtime fallback `NavFlags = new()` → **existing deployment không thay đổi**.
- **ON** → runtime fetch cấu hình theo domain → render theo profile preset.
- **Trạng thái production:** flag đang **ON** (qua env `KHACHLINK_MULTIPROFILE_ENABLED=true` trên gateway VPS). Admin page `/admin/khachlink-instances` hiển thị trạng thái flag hiện tại ở đầu trang.

### 1.4 Tenant context
- `OwnerTenantId = null` (Platform instance — Directory, Hoptacxa) → tenant context = `LastInteractionService` (existing behavior — khách chọn tenant khi checkout).
- `OwnerTenantId != null` (tenant-owned instance — FullCommerce/Reseller) → `KhachLinkLayout` set `TenantService.SetCurrentTenant(owner)` → toàn bộ trang gắn với tenant đó.
- `Hoptacxa` là portal **multi-HTX**: `OwnerTenantId=null`, danh bạ nhiều hợp tác xã + member shops qua `/api/cooperatives` — commerce diễn ra ở member storefronts, KHÔNG phải storefront 1 tenant.

### 1.5 Dynamic CORS (R1.1 — merged PR #133)
Gateway CORS policy không còn hardcode trong docker-compose. Thay vào đó:
```
PostgreSQL (KhachLinkInstance.CustomDomain WHERE IsActive=true)
    ↓  DynamicCorsCacheHostedService (BackgroundService, 5 min refresh)
IMemoryCache (origin snapshot)
    ↓  DynamicCorsService (Singleton, sync read-only)
CORS middleware (SetIsOriginAllowed callback)
```

**Hành vi:**
- **Static origins** (`api2`/`app2`/`www2`/`diemthuong2`) — bake trong `2_Gateway/appsettings.Production.json` (`Cors:StaticOrigins`). Gateway start được ngay cả khi PG down.
- **Dynamic origins** — mỗi 5 phút, `DynamicCorsCacheHostedService` query `GetActiveCustomDomainsAsync()` (lightweight: `SELECT "CustomDomain" WHERE "IsActive" = true`) → cập nhật `IMemoryCache`.
- **CORS callback** đọc `IMemoryCache` (sync, không DB call) → thêm `Access-Control-Allow-Origin` nếu origin match.
- **Thêm domain mới qua admin UI → CORS hoạt động trong ≤ 5 phút, KHÔNG restart Gateway, KHÔNG edit docker-compose.**
- **Deactivate instance → domain đó rời CORS allowlist trong ≤ 5 phút** (TTL acceptable — CORS không phải security boundary).
- **`AllowCredentials()` KHÔNG dùng** — KhachLink WASM dùng JWT Bearer, không cookie (giảm CSRF risk).
- **`CanonicalizeDomain()`** — constructor `KhachLinkInstance` tự strip scheme/path/port/slash + validate hostname format. Admin nhập `https://shopA.khachvip.online:443/path` → lưu `shopa.khachvip.online`.

**Đã xóa khỏi docker-compose:** `Cors__AllowedOrigins__*` env vars (`docker-compose.prod.yml`, `docker-compose.gateway.yml`). Không còn cần thiết.

**RV 8/8 PASS (2026-08-17):** `timlathay.com` (registry), `diemthuong2` (static+registry), `app2`/`www2` (static), `evil.com`/`random-test-123` (unknown → no header), OPTIONS preflight 204, add new domain → CORS works sau 5 min không restart.

---

## 2. Hướng dẫn cho System Admin

> **Role yêu cầu:** `PlatformRole = SystemAdmin` (cross-tenant). Truy cập qua ShopERP (`app2.khachvip.online`).

### 2.1 Truy cập trang quản trị
1. Login ShopERP với tài khoản SystemAdmin.
2. NavMenu (admin section) → **KhachLink Instances** → `/admin/khachlink-instances`.
3. Trang hiển thị:
   - Banner trạng thái feature flag `KhachLink:MultiProfileEnabled` (ON/OFF).
   - Bảng: Nhãn · Profile · Custom Domain · Owner Tenant (tên hoặc "Platform (no tenant)") · Trạng thái (Hoạt động/Tạm dừng) · Actions.
   - Actions mỗi row: **Sửa** · **📝 Lịch sử** (audit history) · **Vô hiệu hoá** hoặc **Kích hoạt** (tuỳ trạng thái).

### 2.2 Tạo KhachLinkInstance mới

**Bước 1 — Tạo instance trong admin UI:**
1. Nút **"+ Thêm instance"** → mở modal (UI Platform components).
2. Điền:
   - **Label** (bắt buộc) — tên dễ nhận biết, ví dụ "Danh bạ Vạn An".
   - **Profile** (dropdown) — 6 option selectable: `FullCommerce`, `Directory`, `Logistics (— R3)`, `JobMarket (— R3)`, `Reseller`, `Hợp tác xã (Type 6)`.
   - **CustomDomain** (bắt buộc, unique) — ví dụ `danhba.khachvip.online` hoặc external `timlathay.com`. Hệ thống tự **canonicalize** qua `CanonicalizeDomain()` (strip scheme/path/port/slash, lowercase). Validate format + trùng. **Lưu ý:** UI placeholder ghi "bỏ trống nếu là instance mặc định" nhưng Domain layer **bắt buộc non-empty** — để trống sẽ báo lỗi 400.
     - **Domain này cũng tự được thêm vào CORS allowlist trong ≤ 5 phút** (xem §1.5) — không cần edit docker-compose.
     - **Auto-link TenantDomain:** nếu CustomDomain trùng một `TenantDomain` record chưa link, hệ thống tự động link `KhachLinkInstanceId` + tạo A record tại registrar (best-effort, fail chỉ log warning).
   - **OwnerTenant** (dropdown, nullable) — "Platform (no tenant)" cho Type 1 Directory / Type 6 Hoptacxa; chọn tenant cụ thể cho Type 4 FullCommerce / Type 5 Reseller.
   - **Nav flags checkbox grid (14 toggle)** — tự load theo Profile preset khi đổi Profile. SystemAdmin có thể **override từng flag** riêng. Nút **"Apply Profile Preset"** để reset về preset. (`ShowJobs` không hiển thị — dead flag.)
3. Save → gọi `POST /api/v1/khachlink-instances` → trả 201.

**Bước 2 — Cấu hình DNS:**
- Thêm A record `<subdomain>.khachvip.online` → IP của gateway VPS.
- Ví dụ: `danhba.khachvip.online A 34.x.x.x`.
- (Wildcard DNS có thể đã cover subdomain — verify trước khi thêm record mới.)

**Bước 3 — Expand SSL certificate (trên gateway VPS):**
```bash
sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh
```
- Script đọc tất cả `CustomDomain` từ `KhachLinkInstances` table (qua `docker exec vanan-postgres psql` hoặc Gateway API).
- Chạy `certbot certonly --webroot --expand -d www2.khachvip.online -d api2.khachvip.online -d app2.khachvip.online -d diemthuong2.khachvip.online -d <subdomain_mới>...`
- Restart nginx: `docker compose -f docker-compose.gateway.yml restart nginx`.
- **Lưu ý:** Let's Encrypt giới hạn 100 SAN/cert. Đủ cho MVP. Khi gần đầy, tách cert thứ 2.

**Bước 4 — Verify:**
```bash
curl -sI https://<subdomain>.khachvip.online | head -5    # HTTP/2 200
```
Mở trình duyệt → trang load theo profile preset (xem §3 để kiểm tra nav items).

> **Không cần restart KhachLink container** — runtime fetch config qua API mỗi lần tải trang (cache localStorage TTL **1 phút**, key `khachlink_instance_config_v3` — xem §6.5).

### 2.3 Chỉnh sửa instance
1. Click **Sửa** trên row tương ứng.
2. **Chỉnh được:** Profile · Nav flags · **Style overrides** (Theme, LogoUrl, màu Nav/Header/Footer) · **Icon app PWA** (upload PNG ≤ 2MB hoặc "Dùng mặc định theo loại").
3. **KHÔNG chỉnh được** (immutable sau khi tạo): Label · CustomDomain · OwnerTenant — các field này bị disable trong form.
4. Nếu đổi Profile hoặc bất kỳ nav flag nào → hệ thống hiện **confirm dialog** liệt kê diff (nav items thêm/bớt) trước khi save.
5. Save → `PUT /api/v1/khachlink-instances/{id}` → trả 204.
6. **Style override:** để trống = kế thừa tenant ShopConfig. Platform instance (OwnerTenant=null) **bắt buộc chọn Theme** để có giao diện.

### 2.4 Deactivate / Activate (soft delete)
1. Click **Vô hiệu hoá** → confirm.
2. `DELETE /api/v1/khachlink-instances/{id}` → set `IsActive=false` (KHÔNG hard delete — giữ audit trail) → trả 204.
3. Domain tương ứng → runtime fetch trả 404 → fallback FullCommerce default.
4. **Kích hoạt lại:** click **Kích hoạt** → `POST /api/v1/khachlink-instances/{id}/activate` → `IsActive=true`.
5. **DNS + SSL cert vẫn còn** — cần cleanup thủ công nếu muốn thu hồi hoàn toàn (xem §6).

### 2.5 Toggle feature flag
```bash
# Trên gateway VPS — env var (CD pipeline-friendly, đã có trong docker-compose.gateway.yml)
KHACHLINK_MULTIPROFILE_ENABLED=true   # trong .env.gateway

# Cách 2: appsettings.json trong container
docker exec vanan-gateway sh -c "sed -i 's/\"MultiProfileEnabled\": false/\"MultiProfileEnabled\": true/' appsettings.json"
docker compose -f docker-compose.gateway.yml restart gateway
```
> **Lưu ý CORS (R1.1):** `Cors__AllowedOrigins__*` env vars đã bị xóa khỏi `docker-compose.prod.yml` + `docker-compose.gateway.yml`. CORS giờ động từ `KhachLinkInstance` registry (xem §1.5). **KHÔNG thêm env var CORS mới** — thêm domain qua admin UI là đủ. Static origins (`api2`/`app2`/`www2`/`diemthuong2`) nằm trong `Cors:StaticOrigins` của `appsettings.Production.json`.

**Test ON/OFF:**
```bash
# Flag OFF → 404
curl -s -o /dev/null -w "%{http_code}\n" https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online
# 404

# Flag ON → 200 + JSON
curl -s https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online | jq
```

### 2.6 Nav flags reference (15 flag — admin UI hiển thị 14)

> `ShowJobs` tồn tại trong DTO/entity nhưng **ẩn khỏi admin UI** (dead flag — `/jobs` chưa ship, chờ milestone M1 của entry-points retention). Giá trị trong bảng dưới là **preset hiệu lực qua admin UI** (`GetPresetFlags`).
>
> ⚠️ **Server fallback caveat:** `KhachLinkNavFlags.ForProfile()` chỉ có case riêng cho `Directory`, `Reseller`, `Hoptacxa`. `Logistics`/`JobMarket` rơi vào default = **all-true** nếu tạo qua API mà không gửi `NavFlagsOverride`. **Luôn tạo/sửa qua admin UI** để preset đúng.

| Flag | Mô tả | FullCommerce | Directory | Hoptacxa | Reseller | Logistics (R3) | JobMarket (R3) |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| ShowHome | Trang chủ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowStores | Cửa hàng / Danh bạ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowProfile | Tài khoản | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowCart | Giỏ hàng | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowOrders | Đơn hàng | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ |
| ShowLoyaltyHistory | Lịch sử tích điểm | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowMissions | Nhiệm vụ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowRewards | Đổi điểm | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowAllianceWallet | Ví liên minh | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowCampaigns | Khuyến mãi | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| ShowScan | Quét QR | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowQrClaim | QR gửi xe | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| ShowCommunity | Community tabs | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ |
| ShowJobs *(ẩn khỏi admin)* | Sàn việc (/jobs) | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| ShowStaffDashboard | Dashboard nhân viên | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |

> **Gate kép:** `ShowAllianceWallet` = true vẫn chưa đủ — tenant phải ở **Loyalty Alliance mode** thì ví mới hiện (W3-2 FR-V5). Nav item hiện khi **flag = true AND role check pass** (cả hai phải đúng).

### 2.7 API endpoints (tham khảo)

| Endpoint | Method | Auth | Mục đích |
|---|---|---|---|
| `/api/v1/khachlink-instances` | GET | SystemAdmin | List all |
| `/api/v1/khachlink-instances/{id}` | GET | SystemAdmin | Get by ID |
| `/api/v1/khachlink-instances/by-domain/{domain}` | GET | AllowAnonymous | Public lookup (KhachLink runtime dùng) — 404 nếu flag OFF |
| `/api/v1/khachlink-instances` | POST | SystemAdmin | Create (201) |
| `/api/v1/khachlink-instances/{id}` | PUT | SystemAdmin | Update (Profile + NavFlags + style) → 204 |
| `/api/v1/khachlink-instances/{id}` | DELETE | SystemAdmin | Deactivate/soft delete (204) |
| `/api/v1/khachlink-instances/{id}/activate` | POST | SystemAdmin | Activate lại instance đã deactivate (204) |
| `/api/v1/khachlink-instances/{id}/icon` | PUT | SystemAdmin | Upload icon PWA (base64 ≤ 2MB) → 204 |
| `/api/v1/khachlink-instances/{id}/icon` | DELETE | SystemAdmin | Xóa icon override → về default theo Profile (204) |
| `/api/public/khachlink-instances/icons/{instanceId}` | GET | AllowAnonymous | Serve icon bytes (override hoặc default theo Profile) |
| `/api/public/khachlink-instances/icons/default` | GET | AllowAnonymous | Icon generic fallback (unknown domain) |
| `/admin/khachlink-instances/{id}/audit` | page | SystemAdmin | Audit history UI (không phải API endpoint) |

---

## 3. Hướng dẫn theo loại instance (demo flow)

### 3.1 Type 1 — Directory (`timlathay.com` — đang live)
**Mục đích:** Danh bạ shop/doanh nghiệp/sản phẩm — không có cart/orders/rewards/redeem.

**Tạo:**
1. Admin UI → "+ Thêm instance" → Profile=`Directory`, CustomDomain=`timlathay.com`, OwnerTenant=`Platform (no tenant)`.
2. Nav flags auto-set: ShowHome/Stores/Campaigns/Profile=true, rest=false.
3. DNS + SSL expand.

**Verify:**
- Mở `https://timlathay.com` → nav có **Trang chủ · Cửa hàng · Khuyến mãi · Tài khoản**.
- Header icons (cart, rewards, missions, loyalty history) ẩn.
- `/stores` render danh sách shop — khách browse nhưng không add to cart.
- Checkout không khả dụng (theo thiết kế).

### 3.2 Type 4 — FullCommerce (`diemthuong2.khachvip.online` — seed)
**Mục đích:** TMĐT đầy đủ cho 1 tenant cụ thể, domain riêng.

**Tạo:**
1. Admin UI → "+ Thêm instance" → Profile=`FullCommerce`, CustomDomain=`shopA.khachvip.online`, OwnerTenant=`<tenant A>`.
2. Nav flags auto-set: tất cả true **trừ ShowJobs** (dead flag).
3. DNS + SSL expand.

**Verify:**
- Mở `https://shopA.khachvip.online` → tất cả icons hiện (cart, rewards, missions, loyalty history, profile, scan, qr-claim...).
- Tenant context = tenant A (toàn bộ sản phẩm/đơn hàng thuộc tenant A).
- Customer order → order snapshot `CommerceMode.Marketplace` (existing flow).

### 3.3 Type 5 — Reseller (`reseller.khachvip.online`)
**Mục đích:** Tenant trung gian bán sản phẩm/dịch vụ cho tenant con.

**Tạo:**
1. Admin UI → "+ Thêm instance" → Profile=`Reseller`, CustomDomain=`reseller.khachvip.online`, OwnerTenant=`<reseller tenant>`.
2. Nav flags auto-set: all true (trừ ShowJobs).
3. DNS + SSL expand.

**Verify:**
- Mở `https://reseller.khachvip.online` → all icons + tenant context = reseller.
- Customer order → order snapshot `CommerceMode.Reseller` (existing flow qua `TenantSettings.CommerceModeOverride` hoặc `GlobalCommerceMode`).
- Products = existing `Product` entity (chỉ khác text content) — không cần entity mới.

### 3.4 Type 2 — Logistics (R3 — `ship.khachvip.online`)
**Mục đích:** Sàn shipper — reuse Community Commerce (Sprint 4-7 cũ).

**Tạo (preset qua admin UI):**
1. Admin UI → "+ Thêm instance" → Profile=`Logistics`, CustomDomain=`ship.khachvip.online`, OwnerTenant=Platform hoặc tenant.
2. Nav flags auto-set (admin preset): ShowHome/Stores/Orders/Community/Profile=true, rest=false.
3. DNS + SSL expand.

> ⚠️ `KhachLinkNavFlags.ForProfile(Logistics)` chưa có case riêng → nếu tạo qua API raw không gửi `NavFlagsOverride`, server fallback all-true. Dùng admin UI.

**Verify:**
- Mở `https://ship.khachvip.online` → nav có **Trang chủ · Cửa hàng · Đơn hàng · Community · Tài khoản**.
- Login as shipper → community tabs hiện (role AND flag — cả hai phải true): `/community/nearby-orders`, `/community/active-deliveries`, `/community/wallet`.
- Cart/rewards/scan ẩn.

### 3.5 Type 3 — JobMarket (R3 — `vieclam.khachvip.online`)
**Mục đích:** Sàn việc/dịch vụ — list "job" như Product (filter text).

**Tạo (preset qua admin UI):**
1. Admin UI → "+ Thêm instance" → Profile=`JobMarket`, CustomDomain=`vieclam.khachvip.online`, OwnerTenant=Platform hoặc tenant.
2. Nav flags auto-set (admin preset): ShowHome/Stores/Profile=true (+ShowJobs=true trong preset, nhưng flag đang ẩn/không hiệu lực vì `/jobs` chưa ship).
3. DNS + SSL expand.

> ⚠️ Cùng caveat server fallback như Logistics — dùng admin UI.

**Verify:**
- Mở `https://vieclam.khachvip.online` → nav có **Trang chủ · Cửa hàng · Tài khoản** (trang `/jobs` chưa tồn tại — chờ M1).
- Cart/rewards/community ẩn.

### 3.6 Type 6 — Hoptacxa (`hoptacxa.timlathay.com`, `commienphi.timlathay.com` — đang live)
**Mục đích:** Portal hợp tác xã — browse-only danh bạ NHIỀU HTX + member shops. `OwnerTenantId=null` (platform portal). Commerce diễn ra ở member storefronts.

**Tạo:**
1. Admin UI → "+ Thêm instance" → Profile=`Hoptacxa`, CustomDomain=`<sub>.timlathay.com` (hoặc `*.khachvip.online`), OwnerTenant=`Platform (no tenant)`.
2. Nav flags auto-set: ShowHome/Stores/Campaigns/Community/Profile=true, rest=false.
3. DNS + SSL expand (`certbot --expand` thêm SAN).

**Verify:**
- Mở domain → nav có **Trang chủ · Cửa hàng · Khuyến mãi · Community · Tài khoản**; cart/orders/rewards/scan ẩn.
- Manifest PWA hiển thị tên "Hợp tác xã".
- Data HTX/member shops qua `GET /api/cooperatives` + `/api/cooperatives/by-domain/{domain}` (public, rate-limit 60rpm, nginx cache 5m).
- Tham khảo: `docs/AI/plans/entry-points-retention-master-plan.md`, `docs/specs/SRS_EntryPoints_Retention_Upgrade_v1.0.md`, `docs/AI/tasks/entry_points_retention/`.

---

## 4. Hướng dẫn cho Tenant Owner (role tenant-scoped)

> Tenant Owner là người quản trị 1 tenant cụ thể (HKD). **KHÔNG** có quyền tạo KhachLinkInstance — đó là việc của SystemAdmin.

**Quyền Tenant Owner liên quan:**
- Yêu cầu SystemAdmin cấp instance FullCommerce/Reseller với `OwnerTenantId` = tenant mình.
- Sau khi cấp, tenant owner quản lý sản phẩm/đơn hàng/rewards **bình thường qua ShopERP** (`app2.khachvip.online`) — không cần thao tác gì trên KhachLink instance.
- Đổi nội dung hiển thị trên KhachLink instance = đổi Products/Campaigns/Missions trong ShopERP (existing flow).
- **Style override** (theme/màu/logo) là việc của SystemAdmin trên trang instance — tenant owner không tự chỉnh.

**Lưu ý:** Tenant Owner **không thấy** trang `/admin/khachlink-instances` (yêu cầu `PlatformRole=SystemAdmin`).

---

## 5. Hướng dẫn cho các role khác

### 5.1 Customer (khách hàng)
- Truy cập domain KhachLink instance (`https://timlathay.com`, `https://diemthuong2.khachvip.online`...).
- Nav items hiển thị theo profile preset + role (guest/registered).
- PWA icon trên điện thoại theo instance (icon riêng nếu admin upload, không thì mặc định theo Profile).
- Không cần thao tác gì với cấu hình instance — trải nghiệm tự động theo domain.

### 5.2 Shipper / Salesman / Shop Owner (Community roles)
- Login trên instance có `ShowCommunity=true` (FullCommerce/Reseller/Logistics/Hoptacxa) AND role match → community tabs hiện.
- Quyền community = existing Community Commerce flow — không thay đổi.

### 5.3 Staff (Dashboard nhân viên)
- Login trên instance có `ShowStaffDashboard=true` AND `_isStaff=true` → Dashboard nav item hiện.
- Existing staff dashboard flow — không thay đổi.

### 5.4 Guard (Security — QR Verify)
- Guard QR Verify (Issue #126) hoạt động trên instance có `ShowScan=true` + `ShowQrClaim=true` (mặc định FullCommerce/Reseller).
- Directory/Hoptacxa/Logistics/JobMarket ẩn theo thiết kế (không có scan/claim).

---

## 6. Vận hành & bảo trì

### 6.1 Thêm instance mới (checklist)
- [ ] SystemAdmin → `/admin/khachlink-instances` → "+ Thêm instance" → điền Label/Profile/CustomDomain/OwnerTenant/Nav flags
- [ ] DNS A record `<subdomain>.khachvip.online` → gateway VPS IP (external domain: DNS + nginx block riêng; wildcard DNS có thể đã cover)
- [ ] SSH gateway VPS → `sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh`
- [ ] Restart nginx: `docker compose -f docker-compose.gateway.yml restart nginx`
- [ ] Verify: `curl -sI https://<subdomain>.khachvip.online` → 200
- [ ] Mở trình duyệt → kiểm tra nav items theo profile
- [ ] (Tuỳ chọn) Upload icon PWA + style override trong Edit modal
- [ ] **CORS tự động** — đợi ≤ 5 phút (hoặc verify bằng `curl -sI -H "Origin: https://<subdomain>.khachvip.online" https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/<subdomain>.khachvip.online | grep -i access-control-allow-origin`). **KHÔNG cần edit docker-compose, KHÔNG cần restart Gateway.**

### 6.2 Thu hồi instance (retire)
1. Admin UI → Vô hiệu hoá (soft delete — `IsActive=false`).
2. Domain → fallback FullCommerce default (runtime fetch trả 404).
3. **CORS:** domain rời allowlist trong ≤ 5 phút (TTL cache — CORS không phải security boundary, nếu cần thu hồi ngay xem `IDynamicCorsService.InvalidateCache()` trong `docs/AI/tasks/dynamic_cors/`).
4. **Cleanup DNS + SSL (thủ công):**
   - Xóa A record DNS.
   - SSL cert: KHÔNG xóa SAN riêng (Let's Encrypt không hỗ trợ remove 1 SAN). Đợi cert renew tự nhiên (60 ngày) hoặc re-issue cert mới không chứa domain đã thu hồi nếu cần.
5. Hard delete record (nếu muốn dọn DB): `DELETE FROM "KhachLinkInstances" WHERE "Id" = '<guid>';` — **chỉ sau khi đã backup**.

### 6.3 Rollback khẩn cấp
| Tình huống | Cách | Thời gian |
|---|---|---|
| Multi-profile gây regression toàn bộ | Toggle `KhachLink:MultiProfileEnabled=false` → tất cả instance render FullCommerce default | < 1 phút |
| 1 instance lỗi | Vô hiệu hoá instance đó trong admin UI | < 1 phút |
| 1 profile preset gây lỗi | Sửa Nav flags override từng instance trong admin UI (hoặc flag OFF toàn cục) | < 1 phút |
| Lỗi code nghiêm trọng | `git revert <release-commit>` trên main | < 10 phút |

### 6.4 Giám sát
- `KhachLinkInstances` table: `SELECT "Label", "Profile", "CustomDomain", "IsActive" FROM "KhachLinkInstances" WHERE "IsActive" = true;`
- Audit history per instance: `/admin/khachlink-instances/{id}/audit` (ai đổi gì, khi nào).
- Gateway logs: filter `KhachLinkInstanceController` — kiểm tra 404 ratio trên `/by-domain/` (404 cao = domain misconfigured hoặc flag OFF).
- nginx access log: filter `*.khachvip.online` + `*.timlathay.com` (trừ api2/app2/www2/diemthuong2) — kiểm tra 502/503/504.

### 6.5 Cache behavior
- KhachLink runtime cache config trong `localStorage`:
  - Key config: `khachlink_instance_config_v3` (JSON)
  - Key timestamp: `khachlink_instance_config_v3_ts`
  - **TTL: 1 phút** (giảm từ 5 phút trong fix #134 để deactivate có hiệu lực nhanh hơn).
- Sau khi sửa instance trong admin UI, khách cần **clear localStorage** hoặc đợi **1 phút** để thấy thay đổi.
- Dev tip: DevTools → Application → Local Storage → xóa 2 key `_v3` + `_v3_ts` → reload.
- NavMenu flash: by-domain API resolve async sau first paint → nav có thể render default flags vài giây rồi re-render theo preset (pre-existing, đã biết).

---

## 7. Troubleshooting

| Triệu chứng | Nguyên nhân khả thi | Cách xử lý |
|---|---|---|
| Domain mới trả 404 trên `/by-domain/` | Feature flag OFF | Toggle `KhachLink:MultiProfileEnabled=true` |
| Domain mới trả 404 (flag ON) | Instance `IsActive=false` hoặc CustomDomain sai | Admin UI → kiểm tra IsActive + CustomDomain (lowercase) |
| Domain mới SSL error | Chưa chạy `init-ssl-khachlink-instances.sh` | SSH gateway VPS → chạy script → restart nginx |
| Domain mới 502 Bad Gateway | DNS chưa trỏ / nginx chưa có wildcard block | Kiểm DNS A record + nginx config `nginx -t` |
| Nav items không đổi sau khi sửa | localStorage cache (TTL **1 phút**, key `_v3`) | Clear localStorage hoặc đợi 1 phút |
| Tất cả domain render FullCommerce (không phân biệt) | Flag OFF hoặc runtime fetch fail → fallback default | Kiểm flag + Gateway logs + Network tab |
| Tạo instance báo lỗi "CustomDomain cannot be empty" | UI placeholder gây nhầm — Domain **bắt buộc** non-empty | Nhập domain hợp lệ (hostname thuần) |
| Nav flags sai sau khi tạo qua API raw (Logistics/JobMarket) | `ForProfile()` chưa có case → fallback all-true | Tạo/sửa qua admin UI (gửi NavFlagsOverride), hoặc bổ sung case trong `KhachLinkNavFlags.ForProfile()` |
| `ShowJobs` không thấy trong admin UI | Cố ý ẩn — dead flag, `/jobs` chưa ship (W3-2) | Không cần xử lý; bật lại khi M1 land |
| `FK constraint violation` khi tạo instance | (Không áp dụng — `KhachLinkInstance` Single-Identity, `TenantId=Guid.Empty` sentinel) | Kiểm exclusion list trong `ApplyMultiTenancyFilters` |
| `relation "__EFMigrationsHistory" does not exist` | Query migration history sai case (Pattern #9) | Dùng quoted PascalCase: `SELECT "MigrationId" FROM "__EFMigrationsHistory"` |
| **CORS error** trên domain mới (`Access-Control-Allow-Origin` missing) | Domain chưa vào CORS allowlist (cache 5 min chưa refresh) | Đợi ≤ 5 phút, hoặc verify `IsActive=true` + `CustomDomain` canonicalize đúng. **KHÔNG thêm env var `Cors__AllowedOrigins__*`** — đã deprecated (xem §1.5) |
| **CORS error** trên domain đã có trong registry | `IsActive=false` (deactivated) hoặc `CustomDomain` sai format | Admin UI → kiểm tra IsActive + CustomDomain (phải là hostname thuần, không scheme/path/port) |
| **CORS error** trên static domain (`api2`/`app2`/`www2`/`diemthuong2`) | `Cors:StaticOrigins` trong `appsettings.Production.json` bị sửa/xóa | Kiểm `2_Gateway/appsettings.Production.json` → `Cors:StaticOrigins` array |
| **Gateway không start** (PG down) | (Không xảy ra) — static origins từ appsettings, dynamic cache pre-warm fail gracefully | Gateway start được ngay cả khi PG down; dynamic origins sẽ pick up khi PG lên |
| PWA icon không đổi trên điện thoại | Icon cache của browser/PWA + instance config cache 1 phút | Clear site data / đợi 1 phút; kiểm `IconUpdatedAt` trong audit |

---

## 8. Quy tắc tuân thủ (compliance)

- **Domain integrity:** `KhachLinkInstance` trong `1_Shared/Domain/Aggregates/KhachLinkAggregate/` — pure, no EF Core attrs. EF config riêng trong `3_CoreHub/Infrastructure/Configurations/`.
- **Single-Identity Pattern:** `Id = PK only`, không có `KhachLinkInstanceId` VO. `TenantId = Guid.Empty` (platform sentinel) — excluded khỏi multi-tenancy query filter.
- **Option C:** KhachLink HTTP-only — `KhachLinkInstanceHttpService` gọi Gateway API, KHÔNG inject `IVanAnDbContext` trực tiếp.
- **UI Platform:** Admin page dùng UI Platform components (Gate 5) — không tự viết HTML/CSS.
- **Feature flag default OFF trong repo config** — production bật qua env var; rollback = set `KHACHLINK_MULTIPROFILE_ENABLED=false` + restart gateway.
- **AccountingEntry:** Không liên quan — immutable, không touch.
- **Pattern #10 (Gateway charset):** Nếu controller forward content, strip charset từ `Request.ContentType` trước khi pass cho `StringContent`/`MediaTypeHeaderValue`.
- **Dynamic CORS (R1.1):** CORS allowlist động từ `KhachLinkInstance.CustomDomain` (registry) + `Cors:StaticOrigins` (appsettings). **KHÔNG thêm env var `Cors__AllowedOrigins__*`** — đã xóa khỏi docker-compose. `CanonicalizeDomain()` validate hostname ở Domain constructor. `AllowCredentials()` KHÔNG dùng (JWT Bearer, không cookie). Cache TTL 5 phút — CORS không phải security boundary.
- **Instance cache (KhachLink runtime):** localStorage key `khachlink_instance_config_v3`, TTL 1 phút — KHÔNG đổi key/TTL mà không bump version suffix.
- **Hoptacxa portal:** `OwnerTenantId=null`, multi-HTX qua `Member.MemberTenantId` — KHÔNG gắn 1 tenant, KHÔNG tạo entity Cooperative riêng (reuse Membership Infrastructure).

---

## 9. Tham khảo

| Tài liệu | Đường dẫn |
|---|---|
| Master plan | `docs/AI/tasks/khachlink_multi_profile/master_plan.md` |
| Release strategy | `docs/AI/tasks/khachlink_multi_profile/release_strategy.md` |
| Sprint task cards | `docs/AI/tasks/khachlink_multi_profile/sprint{1-9}_*.md` |
| **Dynamic CORS master plan** | `docs/AI/tasks/dynamic_cors/master_plan.md` |
| **Dynamic CORS Sprint 1 task card** | `docs/AI/tasks/dynamic_cors/sprint1_dynamic_cors_task_card.md` |
| **Dynamic CORS PR** | https://github.com/anlebao/Gemini_Windsurf/pull/133 (squash `d9545d5e`) |
| **Entry Points Retention (Hoptacxa) plan** | `docs/AI/plans/entry-points-retention-master-plan.md` |
| **Entry Points Retention SRS** | `docs/specs/SRS_EntryPoints_Retention_Upgrade_v1.0.md` |
| **Entry Points task cards** | `docs/AI/tasks/entry_points_retention/` |
| **Instance icon plan** | `docs/AI/plans/khachlink-instance-icon-master-plan.md` |
| Hoptacxa user guide | `docs/HopTacXa/Portal_Hoptacxa_HuongDanSuDung.md` |
| Multi-VPS Deployment Guide | `docs/operations/Multi_VPS_Deployment_Guide.md` (§6 — "Thêm KhachLinkInstance mới") |
| ShopInstance Capacity Handbook | `docs/operations/ShopInstance_Capacity_Handbook.md` |
| Guard QR Verify (ref pattern) | `docs/AI/tasks/guard_qr_verify/` |
| nginx template | `nginx/templates/vanan.multivps.conf.template` |
| SSL script | `scripts/init-ssl-khachlink-instances.sh` |
| Reference entity | `1_Shared/Domain/ShopInstance.cs` (platform-level routing pattern) |
| Governance | `.devin/rules/governance.md` |
| Project state | `docs/AI/project_state.md` |
