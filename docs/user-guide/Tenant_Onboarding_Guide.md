# Hướng dẫn Onboarding Tenant — SystemAdmin

> **Phiên bản:** R1.2 (Domain Reseller R1 + KhachLink Multi-Profile R1)
> **Ngày tạo:** 2026-08-18
> **Role yêu cầu:** `SystemAdmin` (cross-tenant)
> **Truy cập:** `https://app2.khachvip.online` → NavMenu → Admin section

Tài liệu này hướng dẫn SystemAdmin hoàn tất onboarding 1 tenant mới với KhachLink storefront (FullCommerce hoặc Reseller) — bao gồm 2 lựa chọn domain:
1. **Subdomain `*.khachvip.online`** (dùng wildcard cert, không cần mua domain riêng)
2. **Domain riêng tenant mua từ GoDaddy** (vd `shopa.com` — dùng Domain Reseller)

---

## 0. Quy trình tổng quan

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. TẠO TENANT (TenantManagement)                                    │
│    → Tenant record + tenant ID + onboarding seed (products/recipes)  │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────────────┐
│ 2. TẠO SHOPINSTANCE (ShopERP Instances)                             │
│    → Gán tenant vào 1 ShopERP VPS (SQLite per-tenant)                │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────────────┐
│ 3. CHỌN DOMAIN (2 lựa chọn)                                         │
│    ├─ A. Subdomain *.khachvip.online (wildcard cert)                │
│    │   → Tạo KhachLinkInstance qua /admin/khachlink-instances        │
│    │   → Tạo A record GoDaddy (subdomain → VPS IP)                   │
│    │   → SSL: wildcard cert đã có sẵn                                │
│    └─ B. Domain riêng (tenant mua từ GoDaddy)                        │
│        → Tạo TenantDomain record qua /admin/domains                  │
│        → Tạo A record GoDaddy API (apex → VPS IP)                    │
│        → Tạo KhachLinkInstance + link tới TenantDomain               │
│        → SSL: cron init-ssl-tenant-domains.sh (1h)                   │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────────────┐
│ 4. VERIFY                                                           │
│    → Truy cập https://<domain> → KhachLink loads đúng tenant context │
│    → Test: add-to-cart, checkout, order history                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 1. Bước 1 — Tạo Tenant (TenantManagement)

### 1.1 Truy cập trang quản lý tenant
1. Login ShopERP với tài khoản SystemAdmin.
2. NavMenu → **Tenant Management** → `/admin/tenants`.
3. Click **"+ Thêm tenant"**.

### 1.2 Điền thông tin tenant
| Field | Mô tả | Ví dụ |
|---|---|---|
| **Tenant Name** | Tên cửa hàng/doanh nghiệp | "Shop A Coffee" |
| **Business Type** | Loại HKD (F&B, Retail, Service...) | F&B |
| **Legal Form** | Hình thức pháp lý (HKD, Company...) | HKD |
| **Tax Code** | Mã số thuế (nếu có) | 0123456789 |
| **Contact Email** | Email chủ tenant | owner@shopa.com |
| **Contact Phone** | SĐT chủ tenant | 0901234567 |

4. Save → hệ thống tạo tenant record + tự onboarding seed (products/recipes theo Business Type).

### 1.3 Lưu Tenant ID
- Sau khi tạo, tenant có **Tenant ID** (UUID). Ghi lại ID này — cần cho bước 3.
- VD: `12345678-1234-1234-1234-123456789abc`

---

## 2. Bước 2 — Gán Tenant vào ShopInstance

### 2.1 Truy cập ShopERP Instances
1. NavMenu → **ShopERP Instances** → `/admin/shop-instances`.
2. Kiểm tra ShopInstance hiện có (mỗi VPS = 1 ShopInstance).

### 2.2 Gán tenant vào ShopInstance
1. Click **Edit** trên ShopInstance muốn gán (thường là ShopInstance default nếu chỉ 1 VPS).
2. Add tenant ID (từ bước 1.3) vào danh sách tenants của ShopInstance.
3. Save.

> **Lưu ý:** 1 tenant chỉ thuộc 1 ShopInstance. Đừng gán 1 tenant vào 2 ShopInstance — sẽ gây order routing sai.

---

## 3. Bước 3 — Chọn Domain (A: Subdomain hoặc B: Domain riêng)

### 3A. Lựa chọn A — Subdomain `*.khachvip.online` (nhanh, không cần mua domain)

#### 3A.1 Tạo KhachLinkInstance
1. NavMenu → **KhachLink Instances** → `/admin/khachlink-instances`.
2. Click **"+ Thêm instance"**.
3. Điền form:
   - **Label**: `Shop A KhachLink` (tên dễ nhận biết)
   - **Profile**: `FullCommerce (Type 4)` (hoặc `Reseller (Type 5)` nếu R2 đã merge)
   - **Custom Domain**: `shopa.khachvip.online` (subdomain mới — chưa trùng instance khác)
   - **Owner Tenant**: chọn `Shop A Coffee` (từ dropdown — tenant tạo ở bước 1)
   - **Nav Flags**: giữ preset (FullCommerce: all true trừ ShowJobs) hoặc override từng flag.
4. Save → hệ thống tạo instance + tự động thêm domain vào CORS allowlist (≤ 5 phút).

#### 3A.2 Tạo DNS A record (GoDaddy)
**Cách 1 — GoDaddy UI:**
1. Login GoDaddy → Domain Manager → `khachvip.online` → DNS.
2. Add record:
   - Type: `A`
   - Name: `shopa` (subdomain — không nhập full `shopa.khachvip.online`)
   - Value: `136.85.94.119` (Gateway VPS IP)
   - TTL: `600` (10 phút — propagate nhanh)
3. Save.

**Cách 2 — Gateway API (tự động, cần SystemAdmin JWT):**
```bash
# Tạo A record qua Gateway → GoDaddy API
curl -X PUT "https://api2.khachvip.online/api/v1/domains/khachvip.online/a-record" \
  -H "Authorization: Bearer <SYSTEMADMIN_JWT>" \
  -H "Content-Type: application/json" \
  -d '{"name":"shopa","ipAddress":"136.85.94.119","ttl":600}'
```

#### 3A.3 SSL — KHÔNG cần làm gì
- Wildcard cert `*.khachvip.online` đã có sẵn (Sprint 5, `afe84723`).
- Subdomain mới tự động được cert cover → HTTPS hoạt động ngay khi DNS propagate.

#### 3A.4 Verify
```bash
# Đợi DNS propagate (5-30 phút)
dig shopa.khachvip.online +short
# → 136.85.94.119

# Test HTTPS
curl -sI https://shopa.khachvip.online | head -3
# → HTTP/2 200

# Test by-domain API
curl -s https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/shopa.khachvip.online | jq .ownerTenantId
# → "<tenant ID của Shop A>"
```

Mở trình duyệt → `https://shopa.khachvip.online` → KhachLink loads với tenant context = Shop A Coffee.

---

### 3B. Lựa chọn B — Domain riêng tenant mua từ GoDaddy (vd `shopa.com`)

#### 3B.1 Tenant mua domain (ngoài hệ thống)
1. Tenant tự mua domain tại GoDaddy: https://www.godaddy.com (hoặc Mắt Bão, Namecheap...).
2. Tenant cung cấp cho SystemAdmin:
   - **Domain name**: `shopa.com`
   - **Registrant email**: `owner@shopa.com` (dùng cho renewal alerts)
   - **EPP code** (nếu cần transfer — KHÔNG cần cho R1, chỉ khi tenant muốn Vạn An manage domain)
3. SystemAdmin lưu thông tin này — sẽ nhập vào bước 3B.2.

> **R1 scope:** Tenant tự mua + renew domain. Vạn An chỉ quản lý DNS A record + SSL.
> **R2 (sắp tới):** Vạn An reseller domain qua GoDaddy API — tenant mua qua Vạn An storefront, auto everything.

#### 3B.2 Tạo TenantDomain record (tracking)
1. NavMenu → **Tenant Domains** → `/admin/domains`.
2. Click **"+ Thêm domain"** (hoặc nút tương ứng).
3. Điền form:
   - **Domain**: `shopa.com` (full domain, không có scheme/path)
   - **Owner Tenant**: chọn `Shop A Coffee`
   - **Registrant Email**: `owner@shopa.com`
   - **Registrar**: `GoDaddy` (default)
   - **Ngày hết hạn**: nhập ngày tenant báo (vd `2027-08-17`)
4. Save → hệ thống tạo TenantDomain record (status = `Pending`).

#### 3B.3 Tạo A record (GoDaddy API — tự động)
**Cách 1 — Qua Gateway admin UI:**
1. Trên trang `/admin/domains`, click **DNS** trên row của `shopa.com`.
2. Modal hiển thị DNS records hiện tại (trống nếu domain mới).
3. Click **"Add A record"** (hoặc nút tương ứng):
   - Name: `@` (apex domain — `shopa.com`)
   - IP: `136.85.94.119` (Gateway VPS IP — auto-fill default)
   - TTL: `600`
4. Save → Gateway gọi GoDaddy API → A record tạo trên GoDaddy DNS.

**Cách 2 — Qua API trực tiếp:**
```bash
curl -X PUT "https://api2.khachvip.online/api/v1/domains/shopa.com/a-record" \
  -H "Authorization: Bearer <SYSTEMADMIN_JWT>" \
  -H "Content-Type: application/json" \
  -d '{"name":"@","ipAddress":"136.85.94.119","ttl":600}'
```

Verify A record đã tạo:
```bash
curl -s "https://api2.khachvip.online/api/v1/domains/shopa.com/dns-records" \
  -H "Authorization: Bearer <SYSTEMADMIN_JWT>" | jq '.[] | select(.type=="A" and .name=="@")'
# → [{"data":"136.85.94.119","name":"@","ttl":600,"type":"A"}]
```

> **Lưu ý:** Domain phải có NS = GoDaddy default (`ns69/ns70.domaincontrol.com`) để GoDaddy API quản lý DNS.
> Nếu tenant đã đổi NS sang Cloudflare/other → A record phải tạo ở DNS provider đó (manual).

#### 3B.4 Tạo KhachLinkInstance + link TenantDomain
1. NavMenu → **KhachLink Instances** → `/admin/khachlink-instances`.
2. Click **"+ Thêm instance"**.
3. Điền form:
   - **Label**: `Shop A KhachLink`
   - **Profile**: `FullCommerce (Type 4)` (hoặc `Reseller` nếu R2)
   - **Custom Domain**: `shopa.com` (phải khớp với TenantDomain ở bước 3B.2)
   - **Owner Tenant**: chọn `Shop A Coffee`
   - **Nav Flags**: giữ preset hoặc override.
4. Save → hệ thống **tự động detect** TenantDomain matching + **auto-link** + **auto-create A record** (nếu chưa có).

> **Auto-link logic:** Khi tạo KhachLinkInstance với CustomDomain = `shopa.com`, hệ thống tìm TenantDomain có Domain = `shopa.com`. Nếu tìm thấy + chưa link → tự động link + tạo A record apex → VPS IP.

#### 3B.5 SSL — cron tự động (chờ ≤ 1 giờ)
- Cron `init-ssl-tenant-domains.sh` chạy mỗi 1 giờ trên Gateway VPS.
- Script query `TenantDomains` table → tìm active domains có link KLI → request Let's Encrypt cert qua webroot challenge.
- **KHÔNG cần SSH VPS** — cron tự chạy.

**Kiểm tra SSL status:**
```bash
# Đợi DNS propagate + cron chạy (≤ 1 giờ)
dig shopa.com +short
# → 136.85.94.119

# Test HTTPS (sau khi cron issue cert)
curl -sI https://shopa.com | head -3
# → HTTP/2 200 (SSL OK)
```

> **Troubleshooting:** Nếu sau 1 giờ HTTPS vẫn fail:
> 1. Check DNS: `dig shopa.com +short` → phải trả VPS IP
> 2. Check cert: `curl -vI https://shopa.com 2>&1 | grep -i ssl` → nếu "unable to find certificate" → cron chưa chạy hoặc challenge fail
> 3. Manual trigger (cần SSH VPS): `sudo bash /opt/vanan/scripts/init-ssl-tenant-domains.sh`
> 4. Check nginx: domain riêng cần explicit server block trong `nginx/templates/vanan.multivps.conf.template` (thêm theo pattern `@@EXT_DOMAIN_START:shopa.com@@` — xem `timlathay.com` làm mẫu)

#### 3B.6 Verify
```bash
# 1. by-domain API
curl -s https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/shopa.com | jq .ownerTenantId
# → "<tenant ID của Shop A>"

# 2. HTTPS
curl -sI https://shopa.com | head -3
# → HTTP/2 200

# 3. DNS records
curl -s "https://api2.khachvip.online/api/v1/domains/shopa.com/dns-records" \
  -H "Authorization: Bearer <SYSTEMADMIN_JWT>" | jq '.[] | select(.type=="A")'
# → apex A record → VPS IP
```

Mở trình duyệt → `https://shopa.com` → KhachLink loads với tenant context = Shop A Coffee.

---

## 4. So sánh 2 lựa chọn domain

| Tiêu chí | A: Subdomain `*.khachvip.online` | B: Domain riêng từ GoDaddy |
|---|---|---|
| **Chi phí** | $0 (Vạn An bear cost renew `khachvip.online`) | Tenant tự trả (~$12-23/năm cho `.com`) |
| **Thời gian setup** | 5-30 phút (chỉ đợi DNS propagate) | 1-2 giờ (DNS + chờ cron SSL ≤ 1h) |
| **SSL** | Wildcard cert có sẵn (0 bước) | Cron Let's Encrypt (≤ 1h) |
| **Branding** | `shopa.khachvip.online` (subdomain) | `shopa.com` (domain riêng — chuyên nghiệp hơn) |
| **DNS management** | GoDaddy API (auto qua admin UI) | GoDaddy API (auto qua admin UI) |
| **Renewal** | Vạn An quản lý | Tenant tự renew (SystemAdmin theo dõi expiry) |
| **Phù hợp** | Tenant nhỏ, thử nghiệm, không quan trọng branding | Tenant lớn, cần branding riêng, e-commerce nghiêm túc |

---

## 5. FullCommerce vs Reseller — khác biệt

| Tiêu chí | FullCommerce (Type 4) | Reseller (Type 5) |
|---|---|---|
| **Mục đích** | Cửa hàng online của 1 tenant | Tenant trung gian bán lại cho tenant con |
| **Profile** | `FullCommerce = 0` | `Reseller = 4` |
| **OwnerTenantId** | Tenant owner (bắt buộc != null) | Reseller tenant (bắt buộc != null) |
| **Nav flags preset** | All true (trừ ShowJobs) | All true (trừ ShowJobs) — giống FullCommerce |
| **Commerce mode** | `Marketplace` (default) | `Reseller` (qua `TenantSettings.CommerceModeOverride`) |
| **Products** | Tenant owner's products | Reseller tenant's products + commission from sub-tenants |
| **Order flow** | Customer → tenant owner | Customer → reseller → sub-tenant (commission split) |
| **Release** | ✅ R1 (available now) | ⏳ R2 (Sprint 7 — pending) |

> **R2 status:** Nếu dropdown Profile không có option `Reseller` → R2 chưa merge. Dùng FullCommerce tạm thời, upgrade lên Reseller sau khi R2 deploy.

---

## 6. Checklist onboarding (print-friendly)

### FullCommerce + Subdomain
- [ ] 1. Tạo tenant (TenantManagement)
- [ ] 2. Gán tenant vào ShopInstance
- [ ] 3A.1. Tạo KhachLinkInstance (Profile=FullCommerce, Domain=shopa.khachvip.online, OwnerTenant=Shop A)
- [ ] 3A.2. Tạo DNS A record (GoDaddy UI hoặc API: shopa.khachvip.online → 136.85.94.119)
- [ ] 3A.3. SSL — bỏ qua (wildcard cert có sẵn)
- [ ] 3A.4. Verify: `curl -sI https://shopa.khachvip.online` → 200
- [ ] 3A.4. Verify: mở trình duyệt → KhachLink loads đúng tenant

### FullCommerce + Domain riêng (GoDaddy)
- [ ] 1. Tạo tenant (TenantManagement)
- [ ] 2. Gán tenant vào ShopInstance
- [ ] 3B.1. Tenant mua domain `shopa.com` tại GoDaddy
- [ ] 3B.2. Tạo TenantDomain record (/admin/domains — Domain=shopa.com, OwnerTenant=Shop A)
- [ ] 3B.3. Tạo A record (GoDaddy API qua admin UI: @ → 136.85.94.119)
- [ ] 3B.4. Tạo KhachLinkInstance (Profile=FullCommerce, Domain=shopa.com, OwnerTenant=Shop A) → auto-link TenantDomain
- [ ] 3B.5. Chờ cron SSL (≤ 1 giờ) — hoặc manual trigger trên VPS
- [ ] 3B.6. Verify: `curl -sI https://shopa.com` → 200
- [ ] 3B.6. Verify: mở trình duyệt → KhachLink loads đúng tenant

### Reseller + Subdomain (sau R2 merge)
- [ ] 1. Tạo reseller tenant (TenantManagement)
- [ ] 2. Gán tenant vào ShopInstance
- [ ] 3A.1. Tạo KhachLinkInstance (Profile=Reseller, Domain=reseller.khachvip.online, OwnerTenant=Reseller Tenant)
- [ ] 3A.2. Tạo DNS A record (reseller.khachvip.online → 136.85.94.119)
- [ ] 3A.3. SSL — bỏ qua (wildcard cert có sẵn)
- [ ] 3A.4. Verify + test commission flow

### Reseller + Domain riêng (sau R2 merge)
- [ ] 1. Tạo reseller tenant (TenantManagement)
- [ ] 2. Gán tenant vào ShopInstance
- [ ] 3B.1. Reseller mua domain `reseller.com` tại GoDaddy
- [ ] 3B.2. Tạo TenantDomain record (/admin/domains)
- [ ] 3B.3. Tạo A record (GoDaddy API)
- [ ] 3B.4. Tạo KhachLinkInstance (Profile=Reseller, Domain=reseller.com) → auto-link
- [ ] 3B.5. Chờ cron SSL (≤ 1 giờ)
- [ ] 3B.6. Verify + test commission flow

---

## 7. Troubleshooting

### 7.1 KhachLink loads nhưng sai tenant (FullCommerce)
**Triệu chứng:** Mở `https://shopa.khachvip.online` → trang hiển thị sản phẩm của tenant khác.

**Nguyên nhân:** `OwnerTenantId` trong KhachLinkInstance sai hoặc null.

**Fix:**
1. Vào `/admin/khachlink-instances` → Edit instance.
2. Kiểm tra `Owner Tenant` — phải là `Shop A Coffee` (không phải "Platform (no tenant)").
3. Nếu sai → Deactivate instance + tạo instance mới (OwnerTenantId không thể edit sau khi tạo).

### 7.2 HTTPS fail (domain riêng)
**Triệu chứng:** `curl -sI https://shopa.com` → `ERR_SSL_PROTOCOL_ERROR` hoặc cert mismatch.

**Nguyên nhân:** Cron SSL chưa chạy hoặc DNS chưa propagate.

**Fix:**
1. `dig shopa.com +short` → phải trả `136.85.94.119`. Nếu trống → DNS chưa propagate (chờ 30 phút) hoặc A record sai.
2. Nếu DNS OK → SSH Gateway VPS → chạy manual:
   ```bash
   sudo bash /opt/vanan/scripts/init-ssl-tenant-domains.sh
   ```
3. Nếu cert đã issue nhưng HTTPS vẫn fail → nginx chưa có server block cho domain này. Thêm explicit server block vào `nginx/templates/vanan.multivps.conf.template` (pattern `@@EXT_DOMAIN_START:shopa.com@@` — xem `timlathay.com` làm mẫu) + restart nginx.

### 7.3 Domain search trả "Lỗi tra cứu: 400 Bad Request"
**Triệu chứng:** `/admin/domains` → tra cứu domain → lỗi 400.

**Nguyên nhân:** `GODADDY_API_KEY` chưa set trong `.env.gateway` trên VPS.

**Fix:**
1. Check GitHub secret `GODADDY_API_KEY` đã set: `gh secret list | grep GODADDY`.
2. Trigger CD redeploy: push 1 commit nhỏ → CD sẽ ghi lại `.env.gateway` với API key.
3. Verify: `curl -s "https://api2.khachvip.online/api/v1/domains/health" -H "Authorization: Bearer <JWT>"` → `{"healthy":true}`.

### 7.4 Auto-link KHÔNG xảy ra khi tạo KhachLinkInstance
**Triệu chứng:** Tạo KhachLinkInstance với CustomDomain = TenantDomain.Domain nhưng TenantDomain.KhachLinkInstanceId vẫn null.

**Nguyên nhân:**
- TenantDomain chưa tồn tại (chưa tạo ở `/admin/domains` trước).
- Hoặc `DomainRegistrar:DefaultVpsIp` chưa set trong `.env.gateway`.

**Fix:**
1. Tạo TenantDomain trước (bước 3B.2) RỒI mới tạo KhachLinkInstance.
2. Check env var: `DOMAIN_REGISTRAR_DEFAULT_VPS_IP=136.85.94.119` trong `.env.gateway`.
3. Manual link: vào `/admin/domains` → click "Gắn KLI" trên row TenantDomain → chọn KhachLinkInstance + nhập VPS IP.

### 7.5 Tenant domain hết hạn (expiry)
**Triệu chứng:** Cron `MarkExpiredDomainsAsync` set status = `Expired` → KhachLink instance nên disable.

**Fix:**
1. Nhắc tenant renew domain tại GoDaddy TRƯỚC khi hết hạn 30 ngày.
2. Sau khi tenant renew → update `ExpiresAt` trong `/admin/domains` → status trở lại `Active`.
3. Nếu tenant không renew → Deactivate KhachLinkInstance + remove A record (DNS record tự expire khi domain expire).

---

## 8. Tham chiếu

| Tài liệu | Path |
|---|---|
| KhachLink Multi-Profile Usage Guide | `docs/user-guide/KhachLink_Multi_Profile_Usage_Guide.md` |
| Multi-VPS Deployment Guide | `docs/operations/Multi_VPS_Deployment_Guide.md` §6.1 |
| Domain Reseller R1 code | `1_Shared/Domain/Aggregates/DomainResellerAggregate/` + `3_CoreHub/Services/DomainRegistrar/` |
| GoDaddy API verified 2026-08-17 | Read + write + delete all PASS on production `khachvip.online` |
| GitHub secret | `GODADDY_API_KEY` (Personal Access Token) |
| Cron SSL script | `scripts/init-ssl-tenant-domains.sh` (1h interval) |
| Admin pages | `/admin/tenants` · `/admin/shop-instances` · `/admin/khachlink-instances` · `/admin/domains` |
