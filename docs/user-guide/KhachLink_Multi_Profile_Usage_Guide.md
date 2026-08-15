# Hướng dẫn sử dụng — KhachLink Multi-Profile (5 Loại KhachLink)

> **Tài liệu vận hành & sử dụng** cho System Admin, Tenant Owner, và các role khác.
> **Phiên bản:** R1 (Multi-Profile Core + Type 1 Directory + Type 4 FullCommerce + Multi-domain)
> **Ngày tạo:** 2026-08-15
> **Tài liệu nguồn:** `docs/AI/tasks/khachlink_multi_profile/master_plan.md` + `release_strategy.md`
> **Feature flag:** `KhachLink:MultiProfileEnabled` (default OFF — zero regression tới deployment hiện tại)

---

## 0. Tóm tắt nhanh

KhachLink Multi-Profile cho phép **cùng 1 KhachLink container** phục vụ nhiều "loại" trang khách hàng khác nhau, phân biệt theo **domain** (`*.khachvip.online`). Mỗi instance = 1 cấu hình (profile + nav flags + owner tenant) được lưu trong PostgreSQL (Gateway source of truth). KhachLink runtime fetch cấu hình qua `GET /api/v1/khachlink-instances/by-domain/{domain}` khi tải trang, không cần restart container khi thêm instance mới.

| Profile | Loại | Mô tả | Owner | Release |
|---|---|---|---|---|
| `FullCommerce` (0) | Type 4 | TMĐT đầy đủ cho 1 tenant, domain riêng | 1 tenant (OwnerTenantId) | **R1** |
| `Directory` (1) | Type 1 | Danh bạ shop/doanh nghiệp/sản phẩm — ẩn cart, rewards, redeem | Platform (null) | **R1** |
| `Reseller` (4) | Type 5 | Tenant trung gian bán lại sản phẩm/dịch vụ cho tenant con | 1 reseller tenant | R2 |
| `Logistics` (2) | Type 2 | Sàn shipper — reuse Community Commerce | Platform/tenant | R3 |
| `JobMarket` (3) | Type 3 | Sàn việc/dịch vụ — list "job" như Product (filter text) | Platform/tenant | R3 |

**Trạng thái release:**
- ✅ **R1** — Multi-Profile Core + Type 1 + Type 4 + Multi-domain (Sprints 1-6)
- ⏳ **R2** — Type 5 Reseller (Sprint 7)
- ⏳ **R3** — Type 2 Logistics + Type 3 JobMarket (Sprints 8-9)

> Nếu R2/R3 chưa merge, dropdown Profile trong admin UI sẽ **disable** các option Reseller/Logistics/JobMarket kèm tooltip "R2"/"R3".

---

## 1. Kiến trúc & nguyên tắc vận hành

### 1.1 Single deployment + multi-domain routing
- **KHÔNG** tạo 5 Docker image riêng. Cùng 1 KhachLink container phục vụ tất cả instance.
- nginx wildcard `*.khachvip.online` (trừ `api2`/`app2`/`www2`/`diemthuong2` đã có explicit block) → proxy tới KhachLink container.
- KhachLink runtime đọc `window.location.hostname` qua JS interop → gọi Gateway API → nhận cấu hình instance.
- **Thêm instance mới = tạo record + DNS + SSL expand → KHÔNG cần restart KhachLink container.**

### 1.2 Vị trí lưu trữ dữ liệu
| Dữ liệu | Nơi lưu | Ghi chú |
|---|---|---|
| `KhachLinkInstances` table | PostgreSQL (Gateway) | Source of truth. EF migration `AddKhachLinkInstances` |
| Nav flags (15 bool) | PostgreSQL (flattened columns) | Owned entity `KhachLinkNavFlags` |
| Products (cho Directory/JobMarket) | ShopERP SQLite (per-tenant) | Type 1/3 dùng existing `Product` entity, chỉ khác text content |
| Orders (FullCommerce/Reseller) | PostgreSQL (Gateway) → NATS → ShopERP SQLite | Option C routed async delivery |

### 1.3 Feature flag
```json
// 2_Gateway/appsettings.json
"KhachLink": {
  "MultiProfileEnabled": false   // default OFF
}
```
- **OFF** → endpoint `/by-domain/{domain}` trả 404 → KhachLink runtime fallback `NavFlags = new()` (all true = FullCommerce default) → **existing deployment không thay đổi**.
- **ON** → runtime fetch cấu hình theo domain → render theo profile preset.

### 1.4 Tenant context
- `OwnerTenantId = null` (Platform instance, ví dụ Directory) → tenant context = `LastInteractionService` (existing behavior — khách chọn tenant khi checkout).
- `OwnerTenantId != null` (tenant-owned instance, ví dụ FullCommerce/Reseller) → `KhachLinkLayout` set `TenantService.SetCurrentTenant(owner)` → toàn bộ trang gắn với tenant đó.

---

## 2. Hướng dẫn cho System Admin

> **Role yêu cầu:** `PlatformRole = SystemAdmin` (cross-tenant). Truy cập qua ShopERP (`app2.khachvip.online`).

### 2.1 Truy cập trang quản trị
1. Login ShopERP với tài khoản SystemAdmin.
2. NavMenu (admin section) → **KhachLink Instances** → `/admin/khachlink-instances`.
3. Trang hiển thị bảng: Label · Profile · CustomDomain · OwnerTenant (tên hoặc "Platform") · IsActive · Actions.

### 2.2 Tạo KhachLinkInstance mới

**Bước 1 — Tạo instance trong admin UI:**
1. Nút **"+ New"** → mở modal (dùng UI Platform components).
2. Điền:
   - **Label** (bắt buộc, max 200 ký tự) — tên dễ nhận biết, ví dụ "Danh bạ Vạn An".
   - **Profile** (dropdown) — chỉ `FullCommerce` + `Directory` enabled trong R1. Các option khác disabled kèm tooltip "R2"/"R3".
   - **CustomDomain** (bắt buộc, max 255, unique) — ví dụ `danhba.khachvip.online`. Hệ thống tự normalize lowercase. Validate format + trùng.
   - **OwnerTenant** (dropdown, nullable) — "Platform (no tenant)" cho Type 1 Directory; chọn tenant cụ thể cho Type 4 FullCommerce / Type 5 Reseller.
   - **Nav flags checkbox grid** (15 toggle) — tự load theo Profile preset. SystemAdmin có thể **override từng flag** riêng. Nút **"Apply Profile Preset"** để reset về preset.
3. Save → gọi `POST /api/v1/khachlink-instances` → trả 201.

**Bước 2 — Cấu hình DNS:**
- Thêm A record `<subdomain>.khachvip.online` → IP của gateway VPS.
- Ví dụ: `danhba.khachvip.online A 34.x.x.x`.

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

> **Không cần restart KhachLink container** — runtime fetch config qua API mỗi lần tải trang (cache localStorage TTL 5 phút, key `khachlink_instance_config`).

### 2.3 Chỉnh sửa instance
1. Click **Edit** trên row tương ứng.
2. Đổi Profile / Nav flags / Label / OwnerTenant. **CustomDomain không nên đổi** sau khi đã cấp SSL (trừ khi có plan migrate domain).
3. Save → `PUT /api/v1/khachlink-instances/{id}`.
4. Nếu đổi Profile → Nav flags tự reset về preset (trừ khi SystemAdmin override trước khi save).

### 2.4 Deactivate (soft delete)
1. Click **Deactivate** → confirm.
2. `DELETE /api/v1/khachlink-instances/{id}` → set `IsActive=false` (KHÔNG hard delete — giữ audit trail).
3. Domain tương ứng → runtime fetch trả 404 → fallback FullCommerce default.
4. **DNS + SSL cert vẫn còn** — cần cleanup thủ công nếu muốn thu hồi hoàn toàn (xem §6).

### 2.5 Toggle feature flag (chỉ R1)
```bash
# Trên gateway VPS — sửa appsettings.json trong container hoặc env var
# Cách 1: env var (CD pipeline-friendly)
KHACHLINK_MULTIPROFILE_ENABLED=true

# Cách 2: appsettings.json
docker exec vanan-gateway sh -c "sed -i 's/\"MultiProfileEnabled\": false/\"MultiProfileEnabled\": true/' appsettings.json"
docker compose -f docker-compose.gateway.yml restart gateway
```
**Test ON/OFF:**
```bash
# Flag OFF → 404
curl -s -o /dev/null -w "%{http_code}\n" https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online
# 404

# Flag ON → 200 + JSON
# (sau khi toggle)
curl -s https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online | jq
```

### 2.6 Nav flags reference (15 toggle)

| Flag | Mô tả | FullCommerce | Directory | Logistics (R3) | JobMarket (R3) | Reseller (R2) |
|---|---|:---:|:---:|:---:|:---:|:---:|
| ShowHome | Trang chủ | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowStores | Cửa hàng / Danh bạ | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowProfile | Tài khoản | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShowCart | Giỏ hàng | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowOrders | Đơn hàng | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowLoyaltyHistory | Lịch sử tích điểm | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowMissions | Nhiệm vụ | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowRewards | Đổi điểm | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowAllianceWallet | Ví liên minh | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowCampaigns | Khuyến mãi | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowScan | Quét QR | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowQrClaim | QR gửi xe | ✅ | ❌ | ❌ | ❌ | ✅ |
| ShowCommunity | Community tabs | ✅ | ❌ | ✅ | ❌ | ✅ |
| ShowJobs | Sàn việc (/jobs) | ✅ | ❌ | ❌ | ✅ | ✅ |
| ShowStaffDashboard | Dashboard nhân viên | ✅ | ❌ | ❌ | ❌ | ✅ |

> **Quy tắc render:** Nav item hiện khi **flag = true AND role check pass** (cả hai phải đúng). Ví dụ `ShowCommunity` = true nhưng user không phải shipper/salesman/shop owner → vẫn ẩn.

### 2.7 API endpoints (tham khảo)

| Endpoint | Method | Auth | Mục đích |
|---|---|---|---|
| `/api/v1/khachlink-instances` | GET | SystemAdmin | List all |
| `/api/v1/khachlink-instances/{id}` | GET | SystemAdmin | Get by ID |
| `/api/v1/khachlink-instances/by-domain/{domain}` | GET | AllowAnonymous | Public lookup (KhachLink runtime dùng) — 404 nếu flag OFF |
| `/api/v1/khachlink-instances` | POST | SystemAdmin | Create (201) |
| `/api/v1/khachlink-instances/{id}` | PUT | SystemAdmin | Update |
| `/api/v1/khachlink-instances/{id}` | DELETE | SystemAdmin | Deactivate (204) |

---

## 3. Hướng dẫn theo loại instance (demo flow)

### 3.1 Type 1 — Directory (`danhba.khachvip.online`)
**Mục đích:** Danh bạ shop/doanh nghiệp/sản phẩm — không có cart/rewards/redeem.

**Tạo:**
1. Admin UI → New → Profile=`Directory`, CustomDomain=`danhba.khachvip.online`, OwnerTenant=`Platform (no tenant)`.
2. Nav flags auto-set: ShowHome/Stores/Profile=true, rest=false.
3. DNS + SSL expand.

**Verify:**
- Mở `https://danhba.khachvip.online` → nav chỉ có **Trang chủ · Cửa hàng · Tài khoản**.
- Header icons (cart, rewards, missions, loyalty history) ẩn.
- `/stores` render danh sách shop — khách browse nhưng không add to cart.
- Checkout không khả dụng (theo thiết kế).

### 3.2 Type 4 — FullCommerce (`shopA.khachvip.online`)
**Mục đích:** TMĐT đầy đủ cho 1 tenant cụ thể, domain riêng.

**Tạo:**
1. Admin UI → New → Profile=`FullCommerce`, CustomDomain=`shopA.khachvip.online`, OwnerTenant=`<tenant A>`.
2. Nav flags auto-set: all 15 true.
3. DNS + SSL expand.

**Verify:**
- Mở `https://shopA.khachvip.online` → tất cả icons hiện (cart, rewards, missions, loyalty history, profile, scan, qr-claim...).
- Tenant context = tenant A (toàn bộ sản phẩm/đơn hàng thuộc tenant A).
- Customer order → order snapshot `CommerceMode.Marketplace` (existing flow).

### 3.3 Type 5 — Reseller (R2 — `reseller.khachvip.online`)
**Mục đích:** Tenant trung gian bán sản phẩm/dịch vụ cho tenant con.

**Tạo (sau R2 merge):**
1. Admin UI → New → Profile=`Reseller`, CustomDomain=`reseller.khachvip.online`, OwnerTenant=`<reseller tenant>`.
2. Nav flags auto-set: all 15 true.
3. DNS + SSL expand.

**Verify:**
- Mở `https://reseller.khachvip.online` → all icons + tenant context = reseller.
- Customer order → order snapshot `CommerceMode.Reseller` (existing flow qua `TenantSettings.CommerceModeOverride` hoặc `GlobalCommerceMode`).
- Products = existing `Product` entity (chỉ khác text content) — không cần entity mới.

### 3.4 Type 2 — Logistics (R3 — `ship.khachvip.online`)
**Mục đích:** Sàn shipper — reuse Community Commerce (Sprint 4-7 cũ).

**Tạo (sau R3 merge):**
1. Admin UI → New → Profile=`Logistics`, CustomDomain=`ship.khachvip.online`, OwnerTenant=Platform hoặc tenant.
2. Nav flags auto-set: ShowHome/Stores/Profile/Community=true, rest=false.

**Verify:**
- Mở `https://ship.khachvip.online` → nav có **Trang chủ · Cửa hàng · Community · Tài khoản**.
- Login as shipper → community tabs hiện (role AND flag — cả hai phải true): `/community/nearby-orders`, `/community/active-deliveries`, `/community/wallet`.
- Cart/rewards/scan ẩn.

### 3.5 Type 3 — JobMarket (R3 — `vieclam.khachvip.online`)
**Mục đích:** Sàn việc/dịch vụ — list "job" như Product (filter text).

**Tạo (sau R3 merge):**
1. Admin UI → New → Profile=`JobMarket`, CustomDomain=`vieclam.khachvip.online`, OwnerTenant=Platform hoặc tenant.
2. Nav flags auto-set: ShowHome/Stores/Profile/Jobs=true, rest=false.

**Verify:**
- Mở `https://vieclam.khachvip.online` → nav có **Trang chủ · Cửa hàng · Sàn việc · Tài khoản**.
- `/jobs` page → list products có text "job"/"việc"/"dịch vụ"/"service" trong name (case-insensitive contains).
- Cart/rewards/community ẩn.

---

## 4. Hướng dẫn cho Tenant Owner (role tenant-scoped)

> Tenant Owner là người quản trị 1 tenant cụ thể (HKD). **KHÔNG** có quyền tạo KhachLinkInstance — đó là việc của SystemAdmin.

**Quyền Tenant Owner liên quan:**
- Yêu cầu SystemAdmin cấp instance FullCommerce/Reseller với `OwnerTenantId` = tenant mình.
- Sau khi cấp, tenant owner quản lý sản phẩm/đơn hàng/rewards **bình thường qua ShopERP** (`app2.khachvip.online`) — không cần thao tác gì trên KhachLink instance.
- Đổi nội dung hiển thị trên KhachLink instance = đổi Products/Campaigns/Missions trong ShopERP (existing flow).

**Lưu ý:** Tenant Owner **không thấy** trang `/admin/khachlink-instances` (yêu cầu `PlatformRole=SystemAdmin`).

---

## 5. Hướng dẫn cho các role khác

### 5.1 Customer (khách hàng)
- Truy cập domain KhachLink instance (`https://shopA.khachvip.online`).
- Nav items hiển thị theo profile preset + role (guest/registered).
- Không cần thao tác gì với cấu hình instance — trải nghiệm tự động theo domain.

### 5.2 Shipper / Salesman / Shop Owner (Community roles — R3 Logistics)
- Login trên instance Logistics → community tabs hiện (nếu `ShowCommunity=true` AND role match).
- Quyền community = existing Community Commerce flow (Sprint 4-7 cũ) — không thay đổi.

### 5.3 Staff (Dashboard nhân viên)
- Login trên instance có `ShowStaffDashboard=true` AND `_isStaff=true` → Dashboard nav item hiện.
- Existing staff dashboard flow — không thay đổi.

### 5.4 Guard (Security — QR Verify)
- Guard QR Verify (Issue #126) hoạt động trên instance có `ShowScan=true` + `ShowQrClaim=true` (mặc định FullCommerce/Reseller).
- Directory/Logistics/JobMarket ẩn theo thiết kế (không có scan/claim).

---

## 6. Vận hành & bảo trì

### 6.1 Thêm instance mới (checklist)
- [ ] SystemAdmin → `/admin/khachlink-instances` → New → điền Label/Profile/CustomDomain/OwnerTenant/Nav flags
- [ ] DNS A record `<subdomain>.khachvip.online` → gateway VPS IP
- [ ] SSH gateway VPS → `sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh`
- [ ] Restart nginx: `docker compose -f docker-compose.gateway.yml restart nginx`
- [ ] Verify: `curl -sI https://<subdomain>.khachvip.online` → 200
- [ ] Mở trình duyệt → kiểm tra nav items theo profile

### 6.2 Thu hồi instance (retire)
1. Admin UI → Deactivate (soft delete — `IsActive=false`).
2. Domain → fallback FullCommerce default (runtime fetch trả 404).
3. **Cleanup DNS + SSL (thủ công):**
   - Xóa A record DNS.
   - SSL cert: KHÔNG xóa SAN riêng (Let's Encrypt không hỗ trợ remove 1 SAN). Đợi cert renew tự nhiên (60 ngày) hoặc re-issue cert mới không chứa domain đã thu hồi nếu cần.
4. Hard delete record (nếu muốn dọn DB): `DELETE FROM "KhachLinkInstances" WHERE "Id" = '<guid>';` — **chỉ sau khi đã backup**.

### 6.3 Rollback khẩn cấp
| Tình huống | Cách | Thời gian |
|---|---|---|
| Multi-profile gây regression toàn bộ | Toggle `KhachLink:MultiProfileEnabled=false` → tất cả instance render FullCommerce default | < 1 phút |
| 1 instance lỗi | Deactivate instance đó trong admin UI | < 1 phút |
| R2/R3 profile gây lỗi | Disable option trong admin UI (hoặc flag OFF) | < 1 phút |
| Lỗi code nghiêm trọng | `git revert <release-commit>` trên main | < 10 phút |

### 6.4 Giám sát
- `KhachLinkInstances` table: `SELECT "Label", "Profile", "CustomDomain", "IsActive" FROM "KhachLinkInstances" WHERE "IsActive" = true;`
- Gateway logs: filter `KhachLinkInstanceController` — kiểm tra 404 ratio trên `/by-domain/` (404 cao = domain misconfigured hoặc flag OFF).
- nginx access log: filter `*.khachvip.online` (trừ api2/app2/www2/diemthuong2) — kiểm tra 502/503/504.

### 6.5 Cache behavior
- KhachLink runtime cache config trong `localStorage` (key `khachlink_instance_config`, TTL 5 phút).
- Sau khi sửa instance trong admin UI, khách cần **clear localStorage** hoặc đợi 5 phút để thấy thay đổi.
- Dev tip: DevTools → Application → Local Storage → xóa key → reload.

---

## 7. Troubleshooting

| Triệu chứng | Nguyên nhân khả thi | Cách xử lý |
|---|---|---|
| Domain mới trả 404 trên `/by-domain/` | Feature flag OFF | Toggle `KhachLink:MultiProfileEnabled=true` |
| Domain mới trả 404 (flag ON) | Instance `IsActive=false` hoặc CustomDomain sai | Admin UI → kiểm tra IsActive + CustomDomain (lowercase) |
| Domain mới SSL error | Chưa chạy `init-ssl-khachlink-instances.sh` | SSH gateway VPS → chạy script → restart nginx |
| Domain mới 502 Bad Gateway | DNS chưa trỏ / nginx chưa có wildcard block | Kiểmm DNS A record + nginx config `nginx -t` |
| Nav items không đổi sau khi sửa | localStorage cache (TTL 5 phút) | Clear localStorage hoặc đợi 5 phút |
| Tất cả domain render FullCommerce (không phân biệt) | Flag OFF hoặc runtime fetch fail → fallback default | Kiểmm flag + Gateway logs + Network tab |
| Reseller/Logistics/JobMarket option disabled | R2/R3 chưa merge | Đợi release hoặc kiểmm `KhachLinkNavFlags.ForProfile()` đã có case chưa |
| `FK constraint violation` khi tạo instance | (Không áp dụng — `KhachLinkInstance` Single-Identity, `TenantId=Guid.Empty` sentinel) | Kiểmm exclusion list trong `ApplyMultiTenancyFilters` |
| `relation "__EFMigrationsHistory" does not exist` | Query migration history sai case (Pattern #9) | Dùng quoted PascalCase: `SELECT "MigrationId" FROM "__EFMigrationsHistory"` |

---

## 8. Quy tắc tuân thủ (compliance)

- **Domain integrity:** `KhachLinkInstance` trong `1_Shared/Domain/` — pure, no EF Core attrs. EF config riêng trong `3_CoreHub/Infrastructure/Configurations/`.
- **Single-Identity Pattern:** `Id = PK only`, không có `KhachLinkInstanceId` VO. `TenantId = Guid.Empty` (platform sentinel) — excluded khỏi multi-tenancy query filter.
- **Option C:** KhachLink HTTP-only — `KhachLinkInstanceHttpService` gọi Gateway API, KHÔNG inject `IVanAnDbContext` trực tiếp.
- **UI Platform:** Admin page dùng UI Platform components (Gate 5) — không tự viết HTML/CSS.
- **Feature flag default OFF:** Zero regression cho existing deployment (`diemthuong2.khachvip.online` seed instance FullCommerce).
- **AccountingEntry:** Không liên quan — immutable, không touch.
- **Pattern #10 (Gateway charset):** Nếu controller forward content, strip charset từ `Request.ContentType` trước khi pass cho `StringContent`/`MediaTypeHeaderValue`.

---

## 9. Tham khảo

| Tài liệu | Đường dẫn |
|---|---|
| Master plan | `docs/AI/tasks/khachlink_multi_profile/master_plan.md` |
| Release strategy | `docs/AI/tasks/khachlink_multi_profile/release_strategy.md` |
| Sprint task cards | `docs/AI/tasks/khachlink_multi_profile/sprint{1-9}_*.md` |
| Multi-VPS Deployment Guide | `docs/operations/Multi_VPS_Deployment_Guide.md` (§6 — "Thêm KhachLinkInstance mới") |
| ShopInstance Capacity Handbook | `docs/operations/ShopInstance_Capacity_Handbook.md` |
| Guard QR Verify (ref pattern) | `docs/AI/tasks/guard_qr_verify/` |
| nginx template | `nginx/templates/vanan.multivps.conf.template` |
| SSL script | `scripts/init-ssl-khachlink-instances.sh` |
| Reference entity | `1_Shared/Domain/ShopInstance.cs` (platform-level routing pattern) |
| Governance | `.devin/rules/governance.md` |
| Project state | `docs/AI/project_state.md` |
