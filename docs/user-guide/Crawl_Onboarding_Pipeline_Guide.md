# Hướng dẫn sử dụng Crawl-to-Onboard Pipeline

> **Phiên bản:** R1.0 (Phase 1-8 complete)
> **Ngày tạo:** 2026-08-26
> **Module:** Crawl-to-Onboard Tenant Pipeline
> **Trạng thái:** Đã triển khai (Deployed) + Xác minh thời gian chạy (RV PASS)

Tài liệu này hướng dẫn 3 nhóm người dùng sử dụng module Crawl-to-Onboard Pipeline — từ crawl business listings tự động → tạo Pending tenant → owner Claim qua GPKD → SysAdmin approve → tenant Active.

---

## Mục lục

1. [Tổng quan quy trình](#1-tổng-quan-quy-trình)
2. [Hướng dẫn cho Owner Tenant (Claim ownership)](#2-hướng-dẫn-cho-owner-tenant)
3. [Hướng dẫn cho SystemAdmin (Quản lý Pending + Claims + Duplicates)](#3-hướng-dẫn-cho-systemadmin)
4. [Hướng dẫn cho Developer / Deployment Staff](#4-hướng-dẫn-cho-developer--deployment-staff)
5. [Sơ đồ kiến trúc + Data flow](#5-sơ-đồ-kiến-trúc--data-flow)
6. [API Reference](#6-api-reference)
7. [Cấu hình (Configuration)](#7-cấu-hình-configuration)
8. [Khắc phục sự cố (Troubleshooting)](#8-khắc-phục-sự-cố-troubleshooting)
9. [Câu hỏi thường gặp (FAQ)](#9-câu-hỏi-thường-gặp-faq)

---

## 1. Tổng quan quy trình

### 1.1. Mục đích

Pipeline tự động hóa việc tìm kiếm + onboarding tenant mới từ các nguồn business listing công khai (trangvangvietnam.com, doanhnghiep.vn). Thay vì SysAdmin phải nhập tay thông tin tenant, hệ thống:

1. **Crawl** danh sách doanh nghiệp từ nguồn công khai
2. Tạo **Pending tenant** (profile read-only, ẩn SĐT theo Luật 91/2025 + ND356/2025)
3. Owner doanh nghiệp **Claim** ownership bằng cách upload Giấy phép kinh doanh (GPKD)
4. SysAdmin **Review + Approve** claim → tenant chuyển sang Active + tạo admin user + permission groups

### 1.2. Sơ đồ quy trình

```
┌─────────────────────────────────────────────────────────────────────────┐
│ 1. CRAWL (SysAdmin trigger hoặc scheduled)                               │
│    SysAdmin → /admin/crawl-trigger → POST /api/v1/crawl/trigger (202)   │
│    → Crawler worker crawl trangvangvietnam.com / doanhnghiep.vn          │
│    → POST /api/v1/crawl/batch (max 500 listings/batch)                   │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────────┐
│ 2. PENDING TENANT CREATED                                                │
│    → TenantStatus.Pending (Status=5)                                     │
│    → Slug: pending-{MST}-{random} (vd: pending-0106463914-a1b2c3)        │
│    → Settings.CrawledPhone stored internal (KHÔNG hiển thị)              │
│    → Settings.ContactPhone = null (ẩn section SĐT trên profile)          │
│    → CrawlSource audit record (SourceSite + SourceUrl + RawJson)         │
│    → Duplicate check: cùng MST → mark PotentialDuplicateOf (H5)          │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────────┐
│ 3. OWNER CLAIM (Owner doanh nghiệp)                                      │
│    Owner truy cập https://{domain}/store/{slug}                         │
│    → Thấy banner "Doanh nghiệp chưa xác minh" + nút "Nhận quyền"        │
│    → Click → /store/{slug}/claim form:                                   │
│      - Họ tên + SĐT + Email (optional)                                   │
│      - MST (cross-check vs Settings.TaxCode)                             │
│      - Upload ảnh GPKD (Cloudinary, rate-limited 10/giờ/IP)              │
│    → POST /api/v1/tenants/{tenantId}/claims (AllowAnonymous, 3/24h)     │
│    → TenantClaimRequest record (Status=Submitted)                        │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────────┐
│ 4. SYSADMIN REVIEW + APPROVE                                             │
│    SysAdmin → /admin/claims → Claims Queue                               │
│    → Xem GPKD image + cross-check MST                                    │
│    → Approve: nhập owner credentials (username + password + slug)        │
│      → POST /api/v1/claims/{id}/approve                                  │
│      → VerifyAsync: tạo Owner user + 4 permission groups + Activate      │
│      → TenantStatus.Active + slug update (vd: cafe-abc)                  │
│      → ContactPhone = owner-provided (consent per M3)                    │
│      → Publish TenantVerifiedEvent → NATS → ShopERP SQLite sync          │
│    → OR Reject: nhập lý do                                               │
│      → POST /api/v1/claims/{id}/reject                                   │
│      → TenantClaimRequest.Status=Rejected                                │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────────┐
│ 5. TENANT ACTIVE + OWNER LOGIN                                           │
│    Owner login ShopERP với credentials từ SysAdmin                       │
│    → Full commerce features (products, orders, accounting)               │
│    → KhachLink storefront hiển thị đầy đủ (SĐT, commerce UI)             │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.3. Vai trò (Roles)

| Role | Quyền | Truy cập |
|---|---|---|
| **Owner (chủ doanh nghiệp)** | Claim ownership Pending tenant, login sau khi Active | KhachLink `/store/{slug}/claim`, ShopERP login |
| **SystemAdmin** | Trigger crawl, review claims, approve/reject, resolve duplicates, verify pending | ShopERP `/admin/*` (app2.khachvip.online) |
| **Anonymous (khách)** | Xem Pending tenant profile (ẩn SĐT + commerce UI) | KhachLink `/store/{slug}` |

### 1.4. Tuân thủ pháp lý (Legal compliance)

- **Luật 91/2025/QH15 + ND356/2025/NĐ-CP** (effective 01/01/2026): SĐT = dữ liệu cá nhân cơ bản
- **Crawl SĐT**: lưu `CrawledPhone` internal (KHÔNG hiển thị trên Pending profile)
- **Pending profile**: Ẩn hoàn toàn section SĐT (Phone=null từ Gateway) — tránh "công khai" per Điều 16
- **Sau Verify**: `ContactPhone` = owner-provided (consent) — KHÔNG copy từ CrawledPhone
- **Data minimization**: CrawledPhone nên xóa sau Verify (đánh giá định kỳ per Điều 19(2))
- **Rate limit**: Claim submit 3 req/IP/24h, Image upload 10/giờ/IP

---

## 2. Hướng dẫn cho Owner Tenant

### 2.1. Nhận quyền sở hữu (Claim ownership) cho doanh nghiệp Pending

#### Bước 1: Truy cập storefront

Mở trình duyệt → nhập URL storefront của doanh nghiệp:
```
https://{domain}/store/{slug}
```
- `{domain}`: domain KhachLink (vd: `diemthuong2.khachvip.online`)
- `{slug}`: slug dạng `pending-{MST}-{random}` (vd: `pending-0106463914-a1b2c3`)

#### Bước 2: Nhận diện Pending tenant

Trang storefront hiển thị:
- **Banner vàng**: "Doanh nghiệp chưa xác minh — Nếu bạn là chủ doanh nghiệp, hãy nhận quyền sở hữu"
- **Nút "Nhận quyền"**: link đến `/store/{slug}/claim`
- **Section SĐT ẨN**: không hiển thị số điện thoại (theo Luật 91/2025)
- **Commerce UI ẨN**: không hiển thị sản phẩm, giỏ hàng, đặt hàng

#### Bước 3: Điền form Claim

Click "Nhận quyền" → trang `/store/{slug}/claim`:

| Trường | Yêu cầu | Ghi chú |
|---|---|---|
| Họ và tên | Bắt buộc | Tên chủ doanh nghiệp |
| Số điện thoại | Bắt buộc | SĐT liên lạc (sau Verify = ContactPhone) |
| Email | Tùy chọn | Email liên lạc |
| Mã số thuế (MST) | Bắt buộc | Cross-check vs MST trong hệ thống — phải khớp |
| Ảnh GPKD | Bắt buộc | Upload ảnh Giấy phép kinh doanh (jpg/png/webp, max 5MB) |

#### Bước 4: Upload ảnh GPKD

1. Click "Chọn file" → chọn ảnh GPKD (jpg/png/webp, max 5MB)
2. Hệ thống upload lên Cloudinary (anonymous, rate-limited 10/giờ/IP)
3. Ảnh preview hiển thị sau khi upload thành công
4. Nếu lỗi 429 (Too Many Requests): đợi 1 giờ rồi thử lại

#### Bước 5: Submit Claim

1. Kiểm tra thông tin → click "Gửi yêu cầu"
2. Thành công: "Yêu cầu đã gửi. SysAdmin sẽ liên hệ trong 24-48h."
3. Lỗi 409 (Conflict): đã có claim đang chờ → liên hệ SysAdmin
4. Lỗi 429 (Too Many Requests): giới hạn 3 claim/24h/IP → đợi ngày hôm sau

#### Bước 6: Nhận credentials sau khi Approve

- SysAdmin approve → owner nhận credentials (username + password) qua SĐT/email
- Login tại `https://app2.khachvip.online` → sử dụng đầy đủ ShopERP features

### 2.2. Lưu ý quan trọng

- **MST phải khớp**: MST trong form Claim phải trùng với MST crawled từ nguồn công khai
- **GPKD rõ nét**: ảnh mờ/nhòe có thể bị reject — SysAdmin sẽ kiểm tra
- **Rate limit**: 3 claim/24h/IP — không spam submit
- **SĐT consent**: SĐT trong form Claim = SĐT hiển thị trên storefront sau Active (consent per Luật 91/2025)

---

## 3. Hướng dẫn cho SystemAdmin

### 3.1. Truy cập

```
URL: https://app2.khachvip.online
Login: sysadmin@vanan.vn / [password từ Seed:SysAdminPassword]
NavMenu → Hệ thống group:
  - Quản lý Tenant (TenantManagement)
  - Hàng đợi Claim (ClaimsQueue)
  - Kích hoạt Crawl (CrawlTrigger)
```

### 3.2. Trigger Crawl (Kích hoạt crawl)

#### Mục đích
Crawl business listings từ trangvangvietnam.com / doanhnghiep.vn → tạo Pending tenants tự động.

#### Steps

1. NavMenu → **Hệ thống** → **Kích hoạt Crawl** (`/admin/crawl-trigger`)
2. Điền form:

| Trường | Mô tả | Ví dụ |
|---|---|---|
| Nguồn (Source) | Site crawl | `trangvangvietnam` hoặc `doanhnghiep` |
| Ngành (Industry) | Lọc theo ngành | `F&B`, `Retail`, `Service` |
| Tỉnh/Thành (Province) | Lọc theo tỉnh | `Hà Nội`, `Hồ Chí Minh` |
| Số lượng (MaxResults) | Max listings crawl | `100` (default), max 500/batch |

3. Click **"Kích hoạt"** → hệ thống trả về 202 Accepted
4. Crawler worker chạy async (background) → crawl listings → POST `/api/v1/crawl/batch`
5. Mỗi listing → tạo Pending tenant (nếu MST chưa tồn tại) hoặc mark duplicate (nếu MST đã có)

#### Kiểm tra kết quả

1. NavMenu → **Hệ thống** → **Quản lý Tenant** → tab **Pending**
2. Danh sách Pending tenants mới hiển thị (Status=Pending, slug `pending-{MST}-*`)
3. Tab **Trùng lặp** hiển thị tenants bị mark duplicate (cùng MST)

### 3.3. Quản lý Pending Tenants

#### Tab "Pending" (`/admin/tenants` → tab Pending)

Danh sách tenants Status=Pending với:
- Tên doanh nghiệp + MST + địa chỉ
- Nguồn crawl (SourceSite + SourceUrl)
- Ngày tạo

#### Verify Pending Tenant (bypass claim)

Dùng khi cần kích hoạt tenant trực tiếp (không qua Claim form):

1. Click **"Verify"** trên tenant row → modal mở:
2. Nhập:

| Trường | Mô tả |
|---|---|
| Owner Username | Username cho owner (vd: `owner@abc.vn`) |
| Owner Password | Password (auto-generated, có thể override) |
| Owner Display Name | Tên hiển thị (vd: `Nguyễn Văn A`) |
| Owner Phone | SĐT owner (consent — sẽ là ContactPhone) |
| Slug | Slug mới (vd: `cafe-abc` — thay `pending-{MST}-*`) |

3. Click **"Xác minh"** → hệ thống:
   - Tạo Owner user + 4 permission groups (Owner, StoreKeeper, Guard, Staff)
   - Tenant Status → Active
   - Slug update → published slug
   - Publish TenantVerifiedEvent → NATS → ShopERP SQLite sync
4. Credentials hiển thị **MỘT LẦN** — copy + gửi cho owner

#### Resolve Duplicate (Giải quyết trùng lặp)

Khi 2+ tenants có cùng MST (duplicate):

1. Tab **Trùng lặp** → danh sách tenants có `PotentialDuplicateOf != null`
2. Click **"Giải quyết"** → modal:
   - **Keep**: tenant sẽ Verify (chính canonical)
   - **Deactivate**: tenant sẽ Inactive (bị trùng)
   - **Reason**: lý do (vd: "MST trùng, giữ tenant tạo trước")
3. Click **"Xác nhận"** → tenant duplicate → Inactive, tenant canonical → có thể Verify

### 3.4. Review + Approve Claims

#### Claims Queue (`/admin/claims`)

Danh sách claims Status=Submitted với:
- Tên doanh nghiệp + MST + claimant info
- Ảnh GPKD (link Cloudinary — click để xem)
- Cross-check MST link (tra cứu MST trên Tổng cục Thuế)

#### Approve Claim

1. Click **"Xem GPKD"** → kiểm tra ảnh GPKD rõ nét + thông tin khớp
2. Click **"Cross-check MST"** → tra cứu MST trên Tổng cục Thuế (external link)
3. Nếu OK → click **"Approve"** → modal:

| Trường | Mô tả |
|---|---|
| Owner Username | Username cho owner |
| Owner Password | Password (auto-generated) |
| Owner Display Name | Tên hiển thị |
| Owner Phone | SĐT (default = claimant phone) |
| Slug | Slug mới (auto-suggest từ tên doanh nghiệp) |

4. Click **"Approve"** → hệ thống:
   - VerifyAsync: tạo Owner user + permission groups + Activate tenant
   - Claim Status → Approved + ReviewedAt + ReviewedByUserId
   - Credentials hiển thị **MỘT LẦN** — copy + gửi cho owner
5. Nếu lỗi 409: tenant đã Active (đã verify trước đó) → reject claim

#### Reject Claim

1. Click **"Reject"** → modal:
   - **Lý do từ chối** (vd: "GPKD không rõ nét", "MST không khớp")
2. Click **"Reject"** → Claim Status → Rejected + RejectionReason
3. Owner có thể submit claim lại (nếu trong rate limit)

### 3.5. Best practices

- **Kiểm tra GPKD cẩn thận**: ảnh rõ nét, thông tin khớp với MST
- **Cross-check MST**: luôn tra cứu MST trên Tổng cục Thuế trước approve
- **Slug clean**: dùng slug dạng `{ten-doanh-nghiep}` (vd: `cafe-abc`, không dấu, không space)
- **Credentials bảo mật**: gửi credentials qua kênh bảo mật (SMS/email riêng), không hiển thị công khai
- **Duplicate check**: nếu tab Trùng lặp có entries → resolve trước khi approve claim
- **Crawl batch nhỏ**: max 100-200 listings/batch để tránh rate limit từ nguồn crawl

---

## 4. Hướng dẫn cho Developer / Deployment Staff

### 4.1. Kiến trúc tổng thể

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         KIẾN TRÚC MODULE                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────────┐  │
│  │  KhachLink   │    │   Gateway    │    │      CoreHub             │  │
│  │  (WASM)      │───▶│  (YARP+API)  │───▶│  (Services, in-process)  │  │
│  │              │    │              │    │                          │  │
│  │ - Store.razor│    │ Controllers: │    │ Services:                │  │
│  │ - Claim.razor│    │ - ImageUpload│    │ - TenantOnboarding       │  │
│  │ - ImageUpload│    │ - Crawl      │    │ - TenantClaim            │  │
│  │   Service    │    │ - TenantClaim│    │ - DuplicateDetection     │  │
│  │ - ClaimHttp  │    │ - TenantPend │    │                          │  │
│  │   Service    │    │              │    │ DbContext (PG):          │  │
│  └──────────────┘    └──────┬───────┘    │ - Tenants                │  │
│                             │            │ - TenantClaimRequests    │  │
│                             │            │ - CrawlSources           │  │
│                             │            └──────────────────────────┘  │
│                             │                                           │
│                     ┌───────▼────────┐                                  │
│                     │  PostgreSQL    │                                  │
│                     │  (Gateway VPS) │                                  │
│                     └────────────────┘                                  │
│                                                                         │
│  ┌──────────────┐              ┌──────────────────────────┐            │
│  │   Crawler    │              │       ShopERP            │            │
│  │   Worker     │──HTTP──────▶│  (Blazor Server)         │            │
│  │   (7_Tooling)│              │                          │            │
│  │              │              │ - TenantManagement.razor │            │
│  │ - RestApi    │              │ - ClaimsQueue.razor      │            │
│  │   Adapter    │              │ - CrawlTrigger.razor     │            │
│  │ - HtmlAdapter│              │ - TenantClaimApiClient   │            │
│  │              │              │ - TenantSyncSubscriber   │            │
│  │ Port 5010    │              │                          │            │
│  └──────────────┘              │ SQLite (per-tenant):     │            │
│                                │ - Tenants (synced via    │            │
│                                │   NATS TenantVerified)   │            │
│                                └──────────────────────────┘            │
│                                                                         │
│                     ┌────────────────┐                                  │
│                     │     NATS       │                                  │
│                     │  (Message bus) │                                  │
│                     │                │                                  │
│                     │ Subjects:      │                                  │
│                     │ - tenant.      │                                  │
│                     │   verified     │                                  │
│                     │ - tenant.      │                                  │
│                     │   profile.     │                                  │
│                     │   updated      │                                  │
│                     └────────────────┘                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.2. Cấu trúc code

```
1_Shared/Domain/
├── Aggregates/TenantAggregate/
│   ├── Tenant.cs                    # +CreateUnverified, +Verify, +PotentialDuplicateOf
│   ├── TenantSettings.cs            # +CrawledPhone field, +WithCrawledPhone method
│   ├── TenantStatus.cs              # +Pending=5
│   ├── TenantClaimRequest.cs        # NEW aggregate (claim lifecycle)
│   └── CrawlSource.cs               # NEW audit entity
└── Events/
    └── TenantEvents.cs              # 5 events (Pending, Verified, ClaimRequested, ClaimApproved, ProfileUpdated)

2_Gateway/Controllers/
├── ImageUploadController.cs         # NEW — anonymous Cloudinary upload
├── CrawlController.cs               # Phase 4 — batch + trigger + sources
├── TenantClaimController.cs         # Phase 4 — submit + list + approve + reject
└── TenantPendingController.cs       # Phase 4 — pending list + verify + duplicates

3_CoreHub/Services/
├── TenantOnboardingService.cs       # +OnboardUnverifiedAsync +VerifyAsync
├── TenantClaimService.cs            # NEW — claim lifecycle
├── DuplicateDetectionService.cs     # NEW — duplicate detection + resolution
├── Claims/ClaimDtos.cs              # NEW DTOs
└── Onboarding/CrawlDtos.cs          # NEW DTOs

5_WebApps/KhachLink/
├── Pages/Store.razor                # +Pending banner + Claim button
├── Pages/Claim.razor                # NEW — claim form
├── Services/Http/ClaimHttpService.cs     # NEW
└── Services/Http/ImageUploadService.cs   # NEW

5_WebApps/ShopERP/
├── Components/Pages/Admin/
│   ├── TenantManagement.razor       # +3 tabs (All/Pending/Duplicates) + Verify/Resolve modals
│   ├── ClaimsQueue.razor            # NEW — claims queue
│   └── CrawlTrigger.razor           # NEW — crawl trigger form
├── Services/TenantClaimApiClient.cs # NEW — Gateway API client
└── Services/TenantSyncSubscriber.cs # Phase 4 — NATS → SQLite sync

7_Tooling/VanAn.Crawler/            # Phase 5 — crawler worker
├── RestApiAdapter.cs               # doanhnghiep.vn REST API
├── TrangVangHtmlAdapter.cs         # trangvangvietnam.com HTML scrape
└── Program.cs                      # Worker service, port 5010

6_Tests/VanAn.Core.Tests/
├── Domain/TenantPendingTests.cs          # 20 domain tests
├── Services/Onboarding/OnboardUnverifiedTests.cs  # 10 service tests
└── Services/Claims/TenantClaimServiceTests.cs     # 13 service tests
```

### 4.3. Database schema

#### PostgreSQL (Gateway — source of truth)

```sql
-- Tenants table (existing + 2 new columns)
Tenants:
  + PotentialDuplicateOf    UUID NULL  -- Guid? (correction C1, no FK constraint)
  + Settings_CrawledPhone   VARCHAR(50) NULL  -- internal, NOT displayed

-- NEW: TenantClaimRequests table
TenantClaimRequests:
  Id                UUID PK
  TenantId          UUID FK -> Tenants.Id (FK Restrict)
  ClaimantName      VARCHAR NOT NULL
  ClaimantPhone     VARCHAR NOT NULL
  ClaimantEmail     VARCHAR NULL
  GpkdImageUrl      VARCHAR NOT NULL  -- Cloudinary URL
  TaxCodeSubmitted  VARCHAR NOT NULL  -- cross-check vs Settings.TaxCode
  Status            INT NOT NULL      -- 0=Submitted, 1=Approved, 2=Rejected
  SubmittedAt       TIMESTAMP NOT NULL
  ReviewedByUserId  UUID NULL
  ReviewedAt        TIMESTAMP NULL
  RejectionReason   VARCHAR NULL
  -- BaseEntity: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted
  -- Indexes: IX_TenantClaimRequests_TenantId, IX_TenantClaimRequests_Status

-- NEW: CrawlSources table (audit trail)
CrawlSources:
  Id          UUID PK
  TenantId    UUID FK -> Tenants.Id (FK Cascade)
  SourceSite  VARCHAR NOT NULL  -- "trangvangvietnam", "doanhnghiep"
  SourceUrl   VARCHAR NOT NULL  -- original listing URL
  RawJson     TEXT NULL         -- full crawled data JSON
  CrawledAt   TIMESTAMP NOT NULL
  -- BaseEntity fields
  -- Index: IX_CrawlSources_TenantId
```

#### SQLite (ShopERP — per-tenant, synced via NATS)

```sql
-- Tenants table (ONLY 2 new columns — TenantClaimRequests/CrawlSources NOT in SQLite)
Tenants:
  + PotentialDuplicateOf    TEXT NULL  -- Guid? as TEXT (SQLite)
  + Settings_CrawledPhone   TEXT NULL
```

### 4.4. Migrations

```bash
# CoreHub PG migration (Phase 2)
cd 3_CoreHub
dotnet ef migrations add AddCrawlOnboarding --output-dir Migrations
# Auto-generated: 2 new tables + 2 new Tenants columns + 3 indexes

# ShopERP SQLite migration (Phase 2 — hand-written, correction C2)
cd 5_WebApps/ShopERP
# 20260825225206_AddCrawlOnboardingTenantsColumns.cs
# ONLY 2 Tenants columns (TenantClaimRequests/CrawlSources NOT in SQLite)
```

### 4.5. Build + Deploy

#### Build local

```bash
dotnet build VanAn.sln
# 0 errors expected

dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj
# 1480+ passed (20 domain + 23 service tests for crawl-onboard)
```

#### Deploy (CD Multi-VPS)

```bash
git push origin main
# Pre-push hook: full CI pipeline (build + unit tests + startup + arch + integration)
# CD Multi-VPS: deploys to 3 VPS (gateway + shop-a + khachlink)
#   - vanan-gateway (asia-southeast1-a) — Gateway + PG + NATS + nginx
#   - vanan-shop-a (asia-southeast1-b) — ShopERP + SQLite
#   - vanan-khachlink (asia-southeast1-c) — KhachLink WASM + nginx
```

#### Manual deploy (if CD fails)

```bash
# SSH to gateway VPS
gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod
cd /opt/vanan
git pull origin main
docker compose -f docker-compose.prod.yml up -d --build gateway nginx

# SSH to shop VPS
gcloud compute ssh vanan-shop-a --zone asia-southeast1-b --project vanan-prod
cd /opt/vanan
git pull origin main
docker compose -f docker-compose.prod.yml up -d --build shoperp

# SSH to khachlink VPS
gcloud compute ssh vanan-khachlink --zone asia-southeast1-c --project vanan-prod
cd /opt/vanan
git pull origin main
docker compose -f docker-compose.prod.yml up -d --build khachlink nginx
```

### 4.6. Cấu hình (Configuration)

#### Gateway (`2_Gateway/appsettings.json`)

```json
{
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  },
  "Crawler": {
    "BaseUrl": "http://crawler:5010",
    "ApiKey": "crawler-jwt-service-account"
  },
  "Authentication": {
    "JwtSettings": {
      "SecretKey": "your-jwt-secret"
    }
  }
}
```

#### Crawler Worker (`7_Tooling/VanAn.Crawler/appsettings.json`)

```json
{
  "Gateway": {
    "BaseUrl": "http://gateway:80",
    "ApiKey": "crawler-jwt-service-account"
  },
  "CrawlSources": {
    "TrangVangVietnam": {
      "BaseUrl": "https://trangvangvietnam.com",
      "RateLimitMs": 3000,
      "MaxPages": 50
    },
    "DoanhNghiep": {
      "BaseUrl": "https://doanhnghiep.vn",
      "ApiKey": "your-api-key"
    }
  }
}
```

#### Rate limits (`2_Gateway/Program.cs`)

```csharp
// Claim submit: 3 req/IP/24h (FixedWindow)
options.AddFixedWindowLimiter("claim-submit", opt =>
{
    opt.PermitLimit = 3;
    opt.Window = TimeSpan.FromHours(24);
});

// Image upload: 10 req/IP/hour (FixedWindow)
options.AddFixedWindowLimiter("image-upload", opt =>
{
    opt.PermitLimit = 10;
    opt.Window = TimeSpan.FromHours(1);
});
```

### 4.7. Testing

```bash
# Domain tests (20 tests)
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj \
  --filter "FullyQualifiedName~TenantPendingTests"

# Service tests (23 tests)
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj \
  --filter "FullyQualifiedName~OnboardUnverifiedTests|FullyQualifiedName~TenantClaimServiceTests"

# Full test suite
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj
# Expected: 1480+ passed, 0 failed, 20 skipped
```

### 4.8. Runtime Verification (RV)

```bash
# Layer 1: API checks
# Get SystemAdmin JWT
gcloud compute ssh vanan-shop-a --zone asia-southeast1-b --project vanan-prod \
  --command="bash /tmp/rv_login.sh"

# Run API checks
gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod \
  --command="bash /tmp/rv_phase678_api.sh '<JWT_TOKEN>'"

# Layer 5: DB inspection
gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod \
  --command="printf 'SELECT column_name, data_type FROM information_schema.columns WHERE table_name = \"TenantClaimRequests\" ORDER BY ordinal_position;\n' | docker exec -i vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -t"
```

---

## 5. Sơ đồ kiến trúc + Data flow

### 5.1. Data flow (Option C — PG source of truth + routed async delivery)

```
KhachLink (WASM)  →  Gateway (API)  →  CoreHub (Services)  →  PostgreSQL
     │                    │                    │
     │                    │                    └─ TenantOnboardingService
     │                    │                    └─ TenantClaimService
     │                    │                    └─ DuplicateDetectionService
     │                    │
     │                    ├─ ImageUploadController (anonymous, Cloudinary)
     │                    ├─ CrawlController (SystemAdmin)
     │                    ├─ TenantClaimController (anonymous + SystemAdmin)
     │                    └─ TenantPendingController (SystemAdmin)
     │
     └─ Store.razor (Pending banner + Claim button)
     └─ Claim.razor (claim form + GPKD upload)

Crawler Worker  →  Gateway (POST /api/v1/crawl/batch)
     │
     ├─ RestApiAdapter (doanhnghiep.vn)
     └─ TrangVangHtmlAdapter (trangvangvietnam.com)

ShopERP (Blazor Server)
     │
     ├─ TenantManagement.razor (3 tabs: All/Pending/Duplicates)
     ├─ ClaimsQueue.razor (claims queue)
     ├─ CrawlTrigger.razor (crawl trigger form)
     ├─ TenantClaimApiClient (wraps Gateway API)
     └─ TenantSyncSubscriber (NATS → SQLite sync)

NATS (message bus)
     ├─ vanan.cloud.tenant.verified    → TenantSyncSubscriber → SQLite upsert
     └─ vanan.cloud.tenant.profile.updated → TenantSyncSubscriber → SQLite upsert
```

### 5.2. Event flow (Verify tenant)

```
1. SysAdmin → POST /api/v1/claims/{id}/approve
2. TenantClaimService.ApproveClaimAsync
   → TenantOnboardingService.VerifyAsync
     → CreateUser (Owner role)
     → CreatePermissionGroups (4 groups)
     → Tenant.Verify() → Status=Active
     → Tenant.UpdateSlug(publishedSlug)
     → OutboxMessage(TenantVerifiedEvent)
   → Claim.Status = Approved
3. OutboxProcessor → NATS publish "vanan.cloud.tenant.verified"
4. ShopERP TenantSyncSubscriber → SQLite Tenant upsert (same Guid tenantId)
5. Response: VerifyResult (ownerUserId, permissionGroupsCreated, publishedSlug)
```

---

## 6. API Reference

### 6.1. Anonymous endpoints

| Method | Path | Mô tả | Rate limit |
|---|---|---|---|
| `GET` | `/api/v1/tenants/by-slug/{slug}` | Get tenant by slug (Pending: Phone=null, IsPending=true, ClaimUrl) | — |
| `POST` | `/api/v1/tenants/{tenantId}/claims` | Submit claim (GPKD upload) | 3/24h/IP |
| `POST` | `/api/v1/images/upload` | Upload image to Cloudinary (multipart/form-data) | 10/giờ/IP |

### 6.2. SystemAdmin endpoints (JWT Bearer)

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/api/v1/tenants/pending` | List Pending tenants |
| `POST` | `/api/v1/tenants/{id}/verify` | Verify Pending tenant (bypass claim) |
| `GET` | `/api/v1/tenants/duplicates` | List duplicate tenants |
| `POST` | `/api/v1/tenants/duplicates/resolve` | Resolve duplicate (keep + deactivate) |
| `GET` | `/api/v1/claims` | List submitted claims |
| `GET` | `/api/v1/claims/{id}` | Get claim detail |
| `POST` | `/api/v1/claims/{id}/approve` | Approve claim (returns credentials ONCE) |
| `POST` | `/api/v1/claims/{id}/reject` | Reject claim |
| `POST` | `/api/v1/crawl/trigger` | Trigger crawl (202 Accepted) |
| `POST` | `/api/v1/crawl/batch` | Batch crawl (max 500 listings) |
| `GET` | `/api/v1/crawl/sources/{tenantId}` | Get crawl audit trail |

### 6.3. Request/Response examples

#### Submit Claim

```http
POST /api/v1/tenants/{tenantId}/claims
Content-Type: application/json

{
  "claimantName": "Nguyễn Văn A",
  "claimantPhone": "0901234567",
  "claimantEmail": "owner@abc.vn",
  "gpkdImageUrl": "https://res.cloudinary.com/.../gpkd.jpg",
  "taxCodeSubmitted": "0106463914"
}

Response: 201 Created
{
  "claimId": "uuid",
  "status": "Submitted"
}
```

#### Approve Claim

```http
POST /api/v1/claims/{claimId}/approve
Authorization: Bearer <SystemAdmin JWT>
Content-Type: application/json

{
  "ownerUsername": "owner@abc.vn",
  "ownerPassword": "Password123!",
  "ownerDisplayName": "Nguyễn Văn A",
  "ownerPhone": "0901234567",
  "slug": "cafe-abc"
}

Response: 200 OK
{
  "tenantId": "uuid",
  "ownerUserId": "uuid",
  "permissionGroupsCreated": 4,
  "publishedSlug": "cafe-abc"
}
```

#### Trigger Crawl

```http
POST /api/v1/crawl/trigger
Authorization: Bearer <SystemAdmin JWT>
Content-Type: application/json

{
  "sourceSite": "trangvangvietnam",
  "industry": "F&B",
  "province": "Hồ Chí Minh",
  "maxResults": 100
}

Response: 202 Accepted
{
  "message": "Crawl trigger forwarded to crawler worker.",
  "request": { "source": "trangvangvietnam", "maxResults": 100 }
}
```

---

## 7. Cấu hình (Configuration)

### 7.1. Environment variables (Docker)

| Variable | Service | Mô tả |
|---|---|---|
| `Cloudinary__CloudName` | Gateway | Cloudinary cloud name |
| `Cloudinary__ApiKey` | Gateway | Cloudinary API key |
| `Cloudinary__ApiSecret` | Gateway | Cloudinary API secret |
| `Crawler__BaseUrl` | Gateway | Crawler worker URL (http://crawler:5010) |
| `Crawler__ApiKey` | Gateway | Crawler JWT service account key |
| `Seed__SysAdminPassword` | ShopERP | SystemAdmin password (override default) |
| `ConnectionStrings__DefaultConnection` | Gateway | PostgreSQL connection string |

### 7.2. Rate limits

| Policy | Limit | Window | Endpoint |
|---|---|---|---|
| `claim-submit` | 3 req | 24h | POST /api/v1/tenants/{id}/claims |
| `image-upload` | 10 req | 1h | POST /api/v1/images/upload |
| `api` | 200 burst | — | All /api/ endpoints (global) |

---

## 8. Khắc phục sự cố (Troubleshooting)

### 8.1. Owner: Claim submit lỗi

| Lỗi | Nguyên nhân | Khắc phục |
|---|---|---|
| 404 Not Found | Tenant không tồn tại hoặc đã Active | Kiểm tra slug trên KhachLink |
| 409 Conflict | Đã có claim Submitted cho tenant này | Đợi SysAdmin review claim cũ |
| 429 Too Many Requests | Giới hạn 3 claim/24h/IP | Đợi 24h rồi thử lại |
| 400 Bad Request | MST không khớp | Kiểm tra MST trên GPKD vs MST hệ thống |

### 8.2. Owner: Image upload lỗi

| Lỗi | Nguyên nhân | Khắc phục |
|---|---|---|
| 400 Bad Request | File rỗng hoặc sai format | Dùng jpg/png/webp, max 5MB |
| 429 Too Many Requests | Giới hạn 10 upload/giờ/IP | Đợi 1 giờ |
| 500 Server Error | Cloudinary config sai | Liên hệ admin |

### 8.3. SystemAdmin: Approve claim lỗi

| Lỗi | Nguyên nhân | Khắc phục |
|---|---|---|
| 409 Conflict | Tenant đã Active (verified trước đó) | Reject claim |
| 400 Bad Request | Slug đã tồn tại | Đổi slug khác |
| 500 Server Error | User creation fail (email trùng) | Đổi owner username/email |

### 8.4. SystemAdmin: Crawl trigger không tạo tenant

| Triệu chứng | Nguyên nhân | Khắc phục |
|---|---|---|
| 202 nhưng không có Pending tenant | Crawler worker không chạy | Kiểm tra crawler container: `docker ps` |
| | MST đã tồn tại (skip duplicate) | Check tab Trùng lặp |
| | Crawl source trả 0 kết quả | Thay đổi keywords/province |

### 8.5. Developer: Build lỗi

| Lỗi | Fix |
|---|---|
| `CS0246: TenantId not found` | Add `using VanAn.Shared.Domain.Aggregates.TenantAggregate;` |
| `CS0104: VerifyResult ambiguous` | Add `using VerifyResult = VanAn.CoreHub.Services.Onboarding.VerifyResult;` |
| EF Core LINQ translation fail (Pattern #8) | Use `t.Id == new TenantId(guid)` NOT `t.Id.Value == guid` |

### 8.6. Developer: DuplicateDetectionService 400 error

```
System.InvalidOperationException: The LINQ expression '__canonicalIds_0' could not be translated
```

**Root cause**: `canonicalIds.Contains(t.Id.Value)` — `t.Id` is `TenantId` value object, `.Value` in `Where` fails LINQ translation (Pattern #8).

**Fix**: Convert to `List<TenantId>` + use `Contains(t.Id)`:
```csharp
var canonicalIds = duplicates
    .Select(t => new TenantId(t.PotentialDuplicateOf!.Value))
    .Distinct()
    .ToList();
var canonicalTenants = await dbContext.Tenants
    .IgnoreQueryFilters()
    .Where(t => canonicalIds.Contains(t.Id))  // NOT t.Id.Value
    .ToDictionaryAsync(t => t.Id.Value, t => t.Name, ct);
```

---

## 9. Câu hỏi thường gặp (FAQ)

### Q1: Tại sao SĐT không hiển thị trên Pending tenant?

**A**: Theo Luật 91/2025/QH15 + ND356/2025/NĐ-CP (effective 01/01/2026), SĐT là dữ liệu cá nhân cơ bản. Việc "công khai" SĐT crawled trên storefront vi phạm Điều 16. Pipeline HIDE hoàn toàn section SĐT trên Pending profile (Phone=null từ Gateway). Sau khi owner Claim + SysAdmin Approve, SĐT owner-provided (consent) mới hiển thị.

### Q2: Owner có thể claim tenant đã Active không?

**A**: Không. Claim chỉ dành cho tenant Status=Pending. Tenant Active đã có owner → không cần claim.

### Q3: Hai tenant có cùng MST — xử lý thế nào?

**A**: Pipeline tự động mark tenant thứ 2 là duplicate của tenant thứ 1 (correction H5 — first canonical). SysAdmin resolve duplicate: Verify tenant "keep" + Deactivate tenant "duplicate". KHÔNG merge data.

### Q4: Credentials sau Approve hiển thị bao lâu?

**A**: Credentials (username + password) hiển thị **MỘT LẦN** trong modal Approve. SysAdmin phải copy + gửi cho owner ngay. Nếu mất, cần reset password qua UserManagement.

### Q5: Crawler crawl bao nhiêu listings mỗi batch?

**A**: Max 500 listings/batch (POST /api/v1/crawl/batch). Mỗi listing có MST → check duplicate → tạo Pending tenant (nếu MST chưa có) hoặc skip (nếu MST đã tồn tại).

### Q6: Tenant Pending có sync sang ShopERP SQLite không?

**A**: Không. Chỉ tenant Active (sau Verify) mới sync PG→SQLite qua NATS (`TenantVerifiedEvent` → `TenantSyncSubscriber`). Pending tenant KHÔNG sync (Option A — H7).

### Q7: Image upload lưu ở đâu?

**A**: Cloudinary (cloud-based image storage). Gateway nhận multipart upload → upload lên Cloudinary → trả về URL. URL lưu trong `TenantClaimRequests.GpkdImageUrl`.

### Q8: Làm sao xóa CrawledPhone sau Verify (data minimization)?

**A**: Hiện tại CrawledPhone vẫn lưu sau Verify (đánh giá định kỳ per Điều 19(2)). Tech debt: thêm cleanup job xóa CrawledPhone sau Verify + sau thời gian retention (vd: 90 ngày).

---

## Phụ lục: Tham chiếu

- **Master plan**: `docs/AI/plans/crawl-onboarding-master-plan.md` (114 dòng — 12 locked decisions, 8 corrections)
- **Task cards**: `docs/AI/tasks/crawl-onboarding/task_phase{1-8}_*.md` (8 files)
- **PRs**: #162 (Phase 1-4), #163 (Phase 5), #164 (Phase 6+7+8)
- **Domain entities**: `1_Shared/Domain/Aggregates/TenantAggregate/`
- **Services**: `3_CoreHub/Services/` (TenantOnboardingService, TenantClaimService, DuplicateDetectionService)
- **Tests**: `6_Tests/VanAn.Core.Tests/Domain/TenantPendingTests.cs` + `Services/Onboarding/OnboardUnverifiedTests.cs` + `Services/Claims/TenantClaimServiceTests.cs`

---

> **Tài liệu này được tạo bởi Devin AI — Crawl-to-Onboard Pipeline Phase 6+7+8 (2026-08-26)**
> **Cập nhật khi:** có thay đổi API, thêm crawl source, hoặc thay đổi legal compliance.
