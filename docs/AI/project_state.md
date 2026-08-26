# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong gom vào archive.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 10 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — Blazor SSR (Directory) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM/SSR (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.
**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `5_WebApps/Directory` (Blazor SSR, Directory-profile tenants) — `UI.Platform` (Shared components) — `6_Tests`.
**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

**VPS Access (GCP — for RV + manual deploy):**
- GCP project: `vanan-prod` (gcloud SDK at `C:\Users\lebao\AppData\Local\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd`)
- SSH command pattern: `gcloud compute ssh <INSTANCE_NAME> --zone <ZONE> --project vanan-prod`
- Instances (4): `vanan-gateway` (asia-southeast1-a, 136.85.94.119) · `vanan-shop-a` (asia-southeast1-b, 34.177.89.248) · `vanan-khachlink` (asia-southeast1-c, 136.85.111.51) · `vanan-khachlink-20260815-timlathay-com` (asia-southeast1-c, 136.85.78.51)
- CD: `cd-multivps.yml` (push to `main`) deploys to all 3 VPS + smoke tests. Legacy `cd.yml` (push to `oracle-prod`) — SSH broken since 2026-08-06, use multi-VPS CD only.

---

## 2. Current Objective

**CRAWL-TO-ONBOARD TENANT PIPELINE — PHASES 1-5 COMPLETE + DEPLOYED. PHASE 6 NEXT.** �
- **Plan structure (reviewed + restructured 2026-08-25):**
  - **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md` (114 dòng — stable, 12 locked decisions, 8 corrections from review)
  - **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md` (154 dòng — codebase findings @ commit `73f77f14`, line refs will stale)
  - **Task cards:** `docs/AI/tasks/crawl-onboarding/task_phase{1-8}_*.md` (8 files, 59-92 dòng each)
  - **Legacy plan (deprecated):** `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` (600 dòng — keep for audit, do NOT use)
- **Mục tiêu:** Pipeline crawl business listings (trangvangvietnam.com HTML + doanhnghiep.vn/xinvoice.vn REST API) → tạo **Pending tenant** (profile read-only, SĐT mask theo ND13/2023) → owner **Claim** qua GPKD upload → **SysAdmin approve** → tenant Active + admin user + permission groups + published slug.
- **12 design decisions (locked):** see master plan §3.
- **8 corrections from review (applied to master plan + task cards):**
  - C1: FK dùng `Guid`/`Guid?` (Single-Identity Pattern — HARD STOP), NOT `TenantId` value object
  - C2: ShopERP SQLite migration needed for 2 Tenants columns (legacy plan said "NO" — SAI, `ShopERPDbContext.cs:55` has `DbSet<Tenant>`)
  - C3: Crawler worker port 5010 (NOT 5003 — ShopERP conflict)
  - C4: `UpdateSlug()` guard UNCHANGED (`Status == Inactive` only) — Pending bypasses via factory param
  - H1: `Pending=5` (not `=0`)
  - H2: `CreateUnverified` 4 params (with pendingSlug), bypass UpdateSlug
  - H3: 12 With methods (not 14) + thread CrawledPhone + preserve LegalForm/BusinessField/CharterCapital
  - H4: `Verify()` also guards `PotentialDuplicateOf == null`
  - H5: Duplicate — first canonical, rest mark dup of first (not chain)
  - H6: No `MaskedPhone` field — Pending profile HIDE SĐT section entirely (M3 — `Phone` null from Gateway, không mask)
  - **H7 (NEW Option A approved 2026-08-25):** Active tenant sync PG→SQLite qua NATS để đảm bảo tenant identity nhất quán (tránh accounting split — order hôm nay gắn tenantId X PG, mai gắn tenantId Y SQLite → số liệu sai). `VerifyAsync` publish `TenantVerifiedEvent` + `UpdateProfileAsync` publish `TenantProfileUpdatedEvent` (5 events total, không phải 4) → outbox → NATS `vanan.cloud.tenant.verified`/`tenant.profile.updated` → NEW `TenantSyncSubscriber` ở ShopERP upsert SQLite row (cùng Guid tenantId). Pending KHÔNG sync. Follow `OrderSyncSubscriber` pattern.
- **Legal findings (M3 resolved 2026-08-25):** Luật 91/2025/QH15 + ND356/2025/NĐ-CP (effective 01/01/2026, thay thế ND13/2023) — Điều 19 không có exemption "dữ liệu đã công khai"; ND356 Điều 3(7) SĐT = dữ liệu cá nhân cơ bản. **User-approved:** crawl SĐT + store `CrawledPhone` (internal), **HIDE SĐT section trên Pending profile** (tránh "công khai" per Điều 16). Sau Verify, `ContactPhone` = owner-provided (consent). BỎ SMS notify. Residual risk: storage = processing chưa consent — user chấp nhận + xóa CrawledPhone sau Verify (data minimization) + đánh giá định kỳ per Điều 19(2).
- **Open questions (resolve before relevant phase):**
  - M2 ✅ RESOLVED (2026-08-26): doanhnghiep.vn API verified — `GET /api/v1/search?q={name}&limit={N}` + `GET /api/v1/companies/{mst}` (17 fields, no phone). xinvoice.vn requires API key — deferred.
  - M3 ✅ RESOLVED (2026-08-25): see Legal findings above.
  - M5 ✅ RESOLVED: rate limit "claim-submit" 3/24h FixedWindow in `2_Gateway/Program.cs`.
  - O1 ⏳ PENDING (Phase 6): KhachLink image upload service — check if R2StorageController or existing upload service exists for GPKD image upload.
  - O2 ✅ RESOLVED: HMAC middleware exists but passive (empty ProtectedPaths). Crawler uses JWT service account auth (simpler for MVP).
  - O3 ⏳ PENDING (Phase 8): `VanAnDbContextTestFactory` update for new entities.
  - O4 ✅ RESOLVED: Option A — TenantSyncSubscriber implemented (Phase 4).
- **8 phases status:**
  - ✅ Phase 1 — Domain + Events (commit `684cc8f8`, PR #162)
  - ✅ Phase 2 — EF Config + Migration (commit `498a5f86`, PR #162)
  - ✅ Phase 3 — Services (commit `1069dbfd`, PR #162)
  - ✅ Phase 4 — API Gateway + TenantSyncSubscriber (commit `dcd7c5ec`, PR #162, RV PASS)
  - ✅ Phase 5 — Crawler worker (commit `8ba372f3`, PR #163, CD deployed)
  - ⏳ Phase 6 — UI KhachLink (NEXT — Pending tenant store page + claim form)
  - ⏳ Phase 7 — UI ShopERP Admin (Pending queue + verify + duplicates)
  - ⏳ Phase 8 — Tests + RV
- **Phase 6 context for new session:**
  - Task card: `docs/AI/tasks/crawl-onboarding/task_phase6_ui_khachlink.md`
  - O1 to resolve: check R2StorageController or existing image upload in KhachLink for GPKD upload
  - Gateway endpoints already live: `GET /api/tenants/by-slug/{slug}` (returns `IsPending` + `ClaimUrl` + `Phone=null` for Pending), `POST /api/v1/tenants/{tenantId}/claims` (AllowAnonymous + rate-limited 3/24h)
  - KhachLink is Blazor WASM — uses HTTP via Gateway only (NO DbContext)
  - UI Platform components required (VanAnButton, VanAnCard, etc.)
  - Branch: create `feature/crawl-onboard-phase6-ui-khachlink` from `main`

---

**ISSUE #103 — IMPERSONATION + DATA ISOLATION — COMPLETE + DEPLOYED + RV FULL PASS.** ✅
- **`c42c4cbe` — Issue #103 (impersonate button not working):** Switched from HttpClient POST to Razor Pages (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) for proper HTTP context handling (Set-Cookie + redirect). Dual role (SystemAdmin + Owner) + `impersonating` marker claim. Global banner in `MainLayout.razor` with exit button. `NavMenu.razor` hides "Hệ thống" menu when impersonating. `AdminLayout.razor` renders Owner menu. `TenantManagement.razor` uses NavigateTo. 23 integration tests PASS (18 original + 5 new Razor Page flow). Pushed, CI PASS, CD deployed. RV on `app2.khachvip.online` ALL PASS.
- **`73f77f14` — Issue #103 data isolation (follow-up):** Root cause: impersonation copied ALL claims + added Owner role but did NOT remove SystemAdmin role → user had BOTH roles → `IsInRole("SystemAdmin")=true` → 9 pages showed cross-tenant data (Orders "ALL tenants" dropdown with `IgnoreQueryFilters()`, Accounting/EInvoice EMPTY default, UserManagement tenant selector, ShopFeatures tenant selector). Fix: strip SystemAdmin role during impersonation (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims in `Impersonate.cshtml.cs` + `AdminController.cs`). Re-add SystemAdmin role on exit (`ExitImpersonate.cshtml.cs` + `AdminController.ExitImpersonation`). `[AllowAnonymous]` on `exit-impersonation` API endpoint (impersonating user has Owner role only — class-level `[Authorize(Policy="SystemAdmin")]` would block exit). `wasImpersonating` guard added. 2 new tests (AM-S24: `/admin/tenants` denied after impersonation; AM-S25: accessible after exit). 25/25 tests PASS. Pushed, CI PASS (1411+17+273+39), CD deployed. RV 9/9 PASS on `app2.khachvip.online`.

---

**DIRECTORY SSR — COMPLETE + DEPLOYED + RV FULL PASS.** ✅
- `main` @ `c34a428a` (7 commits). New `5_WebApps/Directory` Blazor SSR .NET 8 app for Directory-profile tenants (timlathay.com). Load: ~10s (22.8MB WASM) → **0.04s cached / 0.56s first**. nginx `map $is_directory` + variable `proxy_pass` with Docker DNS resolver → SSR container (port 8080). 4 runtime fixes (nginx DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum). RV D3-D8 all PASS: 10 stores render, map works, Commerce unaffected, 56MiB memory. **No remaining actions.**

---

**POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS (3 commits on `main`).** ✅
- **`e7848be9` — Issue #157 (timlathay.com Home page 3 bugs):** (1) nginx `/_framework/` proxy for Directory domains (was 404 HTML → MIME text/html → browser refused blazor.web.js); (2) removed initial tenant list load on page entry (added `_hasSearched` flag); (3) `@bind:event="oninput"` for voice search input persistence.
- **`1eeb4615` — WebSocket + Leaflet markers:** nginx WebSocket upgrade headers (`proxy_http_version 1.1` + `Upgrade`/`Connection` + `proxy_read_timeout 86400s`) for `/_blazor` interactive render mode. Leaflet default marker icons → unpkg CDN (wwwroot/lib/leaflet ships no `images/` subfolder).
- **`6c9182da` — KhachLink Commerce WASM fixes (diemthuong2.khachvip.online):** (1) `ThemeType` JSON enum deserialization — added `JsonStringEnumConverter` to `ShopConfigHttpService` + `TenantProfileHttpService` (Gateway returns `"theme":"Classic"` string, System.Text.Json default expects numbers → JsonException → DefaultShopConfig fallback losing tenant branding); (2) missing `/icons/shortcuts/search.png` + `categories.png` → copied `icon-96x96.png`; (3) ServiceWorker script evaluation failed — merged TWO duplicate `activate` event listeners (v18 missing `event.waitUntil()` → race condition), bumped cache v18 → v19-merge-activate.
- **All 3 commits pushed to `origin/main`, CI PASS, CD deployed.** Pending RV on production.

---

**ISSUE #161 — ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX (commit `5c5a07c5`).** ✅
- **Bug 1:** Revenue/Expense entry forms fail validation dù nhập đủ. Root cause: Blazor `@bind` drop events khi nhập nhanh → `formData.Values` rỗng → `ValidateForm()` fail. Fix: JS interop đọc DOM BEFORE validation + khởi tạo `formData.Values` với defaults + set default Value cho account/category selects.
- **Bug 2:** Transaction history "Ngày" sai (hiện creation time thay vì user-entered date). Root cause: `AccountingEntry` constructor hardcode `TransactionDate = DateTime.UtcNow`. Fix (3-layer): Domain thêm optional `transactionDate` param (backward compatible) → Service pass-through → UI pass user-entered date.
- **Pushed to `origin/main`, CI PASS (1411+17+266+39), CD deployed.** Comment posted on #161.

---

**ISSUE #156 — APPLY GROUP + COLLAPSIBLE NAV TO ALL MENUS (commit `4ee64719`).** ✅
- Áp dụng pattern group-theo-nghiệp-vụ + collapsible (details/summary) từ AdminLayout cho tất cả nav menu:
  - **ShopERP NavMenu.razor:** Convert từ flat AuthorizeView sang VanANavigation với role-based grouped items (Vận hành, Sản phẩm, Kế Toán, Hóa Đơn, CRM, Quản trị, Hệ thống, v.v.)
  - **AccountingLayout.razor:** Group thành Nhập Bút Toán + Báo Cáo
  - **EInvoiceLayout.razor:** Group thành Hóa Đơn + Cấu Hình + Giám Sát
  - **KhachLink NavMenu.razor:** Group desktop sidebar thành Mua sắm + Tích điểm + Tiện ích + Cộng tác viên (mobile bottom bar giữ nguyên)
- **Pushed to `origin/main`, CI PASS (1411+17+266+39), CD deployed.** Comment posted on #156.

---

**FINANCIAL INTELLIGENCE MVP-2 — MERGED TO MAIN (PR #152 `dc8338ed`) + 6 post-merge bug fixes.** ✅
- **Status corrected 2026-08-25:** Previously noted as "pending push + PR" but actually already merged via PR #152 + follow-up commits `e9598115`, `de786420`, `57c15d5c`, `d74ae9d6`, `efd3fa01`, `4593af60` (BusinessProfile save/load fix, ShopInstance BaseUrl seed, JWT Bearer auth for ShopERP, 5 bugs fix, auth policy correction, admin guide expansion).
- All 5 phases + 61 tests + post-merge fixes IN main. No remaining actions for MVP-2.

**CRAWL-TO-ONBOARD TENANT PIPELINE — PHASE 4 (API GATEWAY + TENANTSYNCSUBSCRIBER) COMPLETE.** 🟢
- **Branch:** `feature/crawl-onboard-tenant-pipeline` @ `dcd7c5ec` (Phase 1 `684cc8f8` + Phase 2 `498a5f86` + Phase 3 `1069dbfd` + Phase 4 `dcd7c5ec`)
- **Plan structure:** master plan + research snapshot + 8 task cards (committed `7e8afec7`)
- **Pre-flight complete 2026-08-25** (`7e9a0b4e`):
  - ✅ Branch created (Strategy B refined — main already has MVP-2 merged, no separate merge needed)
  - ✅ M4 verified: all research line refs accurate @ `7e8afec7`
  - ✅ M5 resolved: `AddRateLimiter` exists in `2_Gateway/Program.cs:103-137`, add policy `claim-submit` (3/24h FixedWindow)
  - ✅ O1 resolved: KhachLink has NO image upload service; `IImageStorageService` + `CloudinaryImageStorageService` in CoreHub → add Gateway endpoint `POST /api/v1/images/upload` for KhachLink HTTP upload
  - ✅ O2 resolved: `HmacApiKeyLookupAdapter.cs` exists in Gateway — crawler auth via HMAC API key
  - ✅ O3 resolved: `VanAnDbContextTestFactory` uses `EnsureCreated` — new DbSets auto-created, NO factory change
  - ⏳ M2 deferred to before Phase 5 (curl doanhnghiep.vn/xinvoice.vn API schema)
- **Phase 1 — Domain + Events COMPLETE 2026-08-25** (`684cc8f8`):
  - ✅ `TenantStatus.Pending=5` added (correction H1)
  - ✅ `TenantSettings.CrawledPhone` field + ctor param 17 + 13th `WithCrawledPhone` method + all 12 existing With methods thread CrawledPhone (M3)
  - ✅ `Tenant.CreateUnverified(id, name, settings, pendingSlug)` factory (4 params — correction H2, bypass UpdateSlug)
  - ✅ `Tenant.Verify()` method (guards `Status==Pending && PotentialDuplicateOf==null` — correction H4)
  - ✅ `Tenant.PotentialDuplicateOf` (Guid? — correction C1) + `MarkPotentialDuplicateOf(Guid)` + `IsPending()`
  - ✅ `UpdateSlug()` guard UNCHANGED (correction C4)
  - ✅ 5 events + `TenantSettingsSnapshot` record (H7 Option A)
  - ✅ `TenantClaimRequest.cs` aggregate + `CrawlSource.cs` audit entity (FK via BaseEntity.TenantId — Single-Identity)
  - ✅ `dotnet build 1_Shared/VanAn.Shared.csproj` — 0 errors
- **Phase 2 — EF Config + Migration COMPLETE 2026-08-25:**
  - ✅ `TenantConfiguration.cs`: add `Settings_CrawledPhone` (varchar(50)) + `PotentialDuplicateOf` (Guid?, no FK constraint — correction C1) mappings
  - ✅ `TenantClaimRequestConfiguration.cs` created: map `TenantClaimRequests` table (PG-only), FK Restrict delete, indexes IX_TenantClaimRequests_TenantId + IX_TenantClaimRequests_Status
  - ✅ `CrawlSourceConfiguration.cs` created: map `CrawlSources` table (PG-only), FK Cascade delete, index IX_CrawlSources_TenantId, RawJson as unbounded text
  - ✅ `IVanAnDbContext` + `VanAnDbContext`: add `DbSet<TenantClaimRequest>` + `DbSet<CrawlSource>` (PG-only)
  - ✅ `ShopERPDbContext`: add DbSet declarations (interface contract) + `Ignore<TenantClaimRequest>()` + `Ignore<CrawlSource>()` in OnModelCreating (PG-only entities, not in SQLite)
  - ✅ CoreHub PG migration `20260825224745_AddCrawlOnboarding.cs` generated: 2 new tables + 2 new Tenants columns + 3 indexes, Down migration clean
  - ✅ ShopERP SQLite migration `20260825225206_AddCrawlOnboardingTenantsColumns.cs` hand-written (correction C2): only 2 Tenants columns (PotentialDuplicateOf + Settings_CrawledPhone), TenantClaimRequests/CrawlSources NOT in SQLite (PG-only)
  - ✅ `dotnet build VanAn.sln` — 0 errors
  - ⚠️ Pre-existing drift noted: TenantDomains table missing from SQLite migrations (separate tech debt, not addressed here)
- **Phase 3 — Services COMPLETE 2026-08-25:**
  - ✅ `CrawlDtos.cs` created: `CrawlListingDto`, `VerifyTenantRequest` (M3: OwnerPhone from claim form, NOT CrawledPhone), `VerifyResult`
  - ✅ `ITenantOnboardingService` extended: +`OnboardUnverifiedAsync` +`VerifyAsync`
  - ✅ `TenantOnboardingService` extended: `OnboardUnverifiedAsync` (Pending only, no user/groups, duplicate check H5, CrawlSource audit) + `VerifyAsync` (user+groups+Activate+ContactPhone from owner form M3+slug update+Option A outbox publish TenantVerifiedEvent)
  - ✅ `ITenantClaimService` + `TenantClaimService` + `ClaimDtos.cs` created: Submit/Approve/Reject/List/Get claim lifecycle. ApproveClaimAsync reuses VerifyAsync (DRY)
  - ✅ `IDuplicateDetectionService` + `DuplicateDetectionService` created: MarkDuplicateIfTaxCodeExistsAsync (H5 first canonical), ListPotentialDuplicatesAsync, ResolveDuplicateAsync (no merge)
  - ✅ `TenantManagementService.UpdateProfileAsync` modified: +publish OutboxMessage `TenantProfileUpdatedEvent` (Option A — H7, NATS sync sang SQLite)
  - ✅ DI registration in `2_Gateway/Program.cs`: +`ITenantClaimService` +`IDuplicateDetectionService` (Gateway-only, PG — NOT in ShopERP DI per Option C)
  - ✅ Test fixes: `TenantManagementServiceTests` + `TenantOnboardingServiceTests` constructor calls updated (null outboxRepository param)
  - ✅ `dotnet build VanAn.sln` — 0 errors
- **Phase 4 — API Gateway + TenantSyncSubscriber COMPLETE 2026-08-25:**
  - ✅ `TenantStoreController.GetBySlug` modified: Pending → Phone=null + Email=null (M3 HIDE SĐT section per Luật 91/2025 Điều 16) + IsPending=true + ClaimUrl. Suspended/Inactive/Converted → 404. Added IsPending + ClaimUrl fields to TenantStoreDto.
  - ✅ `CrawlController.cs` created: POST /api/v1/crawl/batch (max 500, skip existing MST, return BatchCrawlResult) + GET /api/v1/crawl/sources/{tenantId} (audit trail) + POST /api/v1/crawl/trigger (202 Accepted, YARP forward to crawler port 5010 deferred to Phase 5 appsettings)
  - ✅ `TenantClaimController.cs` created: POST /api/v1/tenants/{id}/claims [AllowAnonymous]+[EnableRateLimiting("claim-submit")] + GET /api/v1/claims [SystemAdmin] + GET /{id} + POST /{id}/approve (returns credentials ONCE) + POST /{id}/reject
  - ✅ `TenantPendingController.cs` created: GET /api/v1/tenants/pending + POST /{id}/verify (direct bypass claim) + GET /api/v1/tenants/duplicates + POST /duplicates/resolve
  - ✅ Rate limit policy `claim-submit` added to `2_Gateway/Program.cs`: 3 req/IP/24h FixedWindow (M5 resolved)
  - ✅ `TenantSyncSubscriber.cs` created in `5_WebApps/ShopERP/Services/` (Option A): subscribes NATS `vanan.cloud.tenant.verified` + `vanan.cloud.tenant.profile.updated` → upsert Tenant row SQLite (cùng Guid tenantId). Pending events NOT synced. Idempotent (upsert, not insert). Follows OrderSyncSubscriber pattern (retry with exponential backoff).
  - ✅ `TenantSyncSubscriber` registered in `5_WebApps/ShopERP/Program.cs` as HostedService
  - ✅ `dotnet build VanAn.sln` — 0 errors
- **Awaiting user approval to start Phase 5 (Crawler worker — new 7_Tooling/VanAn.Crawler.csproj).**

- **Branch:** `main` @ `73f77f14` (Issue #103 impersonation + data isolation + Directory SSR + post-deploy fixes #157 + #161 accounting validation/date fix + #156 nav group/collapsible all menus). **Build full sln:** 0 errors · **CI:** 1411 unit + 17 unit + 273 integration + 39 arch ALL PASS · **.NET SDK:** 8.0.422
- **Directory SSR:** ✅ COMPLETE — timlathay.com live (0.04s load, 10 stores, 56MiB). Issue #157 fixed (3 bugs). WebSocket + Leaflet markers fixed. See Section 2.
- **KhachLink Commerce WASM:** ✅ ThemeType enum + shortcut icons + SW duplicate activate fixed (commit `6c9182da`). Pending RV on `diemthuong2.khachvip.online`.
- **Issue #161 (Accounting):** ✅ Fixed + deployed. Revenue/Expense validation (JS interop DOM read before validate + default formData.Values) + TransactionDate 3-layer fix (Domain optional param → Service pass-through → UI pass user date). Pending RV.
- **Issue #156 (Nav group/collapsible):** ✅ Fixed + deployed. All 4 nav menus converted to grouped/collapsible pattern (ShopERP NavMenu, AccountingLayout, EInvoiceLayout, KhachLink NavMenu). Pending RV.
- **Issue #103 (Impersonation + data isolation):** ✅ Fixed + deployed + RV PASS. Two commits: `c42c4cbe` (Razor Page flow + dual role + banner) + `73f77f14` (strip SystemAdmin role during impersonation → 9 pages auto-fix to tenant-scoped data). 25/25 access matrix tests PASS. RV 9/9 PASS on `app2.khachvip.online`.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS), pending push + PR + CD + RV.
- **Infrastructure (all deployed + RV PASS):** GCP 3 VPS · nginx 5-layer rate limit · Cloudflare R2 (guard photos + auto-cleanup 30d) · Dynamic CORS from KhachLinkInstance registry · KhachLink Multi-Profile R1 enabled · Domain Reseller R1 (GoDaddy API) · Guard QR Verify (Issue #126) · OCR Hub R1 (PaddleOCR client-side) · Plate-as-metadata (PlateNumber optional).
- **Crawl-to-Onboard Tenant Pipeline:** 🟡 Plan reviewed + restructured 2026-08-25. Master plan `docs/AI/plans/crawl-onboarding-master-plan.md` (114 dòng) + research snapshot `docs/AI/plans/crawl-onboarding-research.md` (154 dòng) + 8 task cards `docs/AI/tasks/crawl-onboarding/task_phase{1-8}_*.md`. 12 design decisions locked + 8 corrections from review applied (C1-C4, H1-H6). Legacy 600-line plan deprecated. Awaiting Phase 1 start (Gate 5 protected).
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**Crawl-to-Onboard Tenant Pipeline (active — plan reviewed + restructured, awaiting implementation):**
1. Phase 1 — Domain + Events (task card: `docs/AI/tasks/crawl-onboarding/task_phase1_domain_events.md`): Add `TenantStatus.Pending=5`, `Tenant.CreateUnverified(id, name, settings, pendingSlug)` (4 params), `Tenant.Verify()` (guards `Status==Pending && PotentialDuplicateOf==null`), `Guid? PotentialDuplicateOf` (NOT `TenantId?` — Single-Identity Pattern), `CrawledPhone` field (12 With methods + preserve LegalForm/BusinessField/CharterCapital), **5 events** (`TenantPendingEvent`, `TenantVerifiedEvent`, `TenantClaimRequestedEvent`, `TenantClaimApprovedEvent`, **`TenantProfileUpdatedEvent`** — H7 Option A for NATS sync), 2 new aggregates (`TenantClaimRequest`, `CrawlSource` — FK `Guid` not `TenantId`). Gate 5 protected — user-approved. **DO NOT tighten `UpdateSlug()` guard** (C4). **M3:** CrawledPhone stored internal, NOT displayed on Pending profile.
2. Phase 2 — EF Config + Migration (task card: `task_phase2_ef_migration.md`): Map `TenantClaimRequests` + `CrawlSources` (PG-only) + `Tenants.PotentialDuplicateOf` + `Tenants.Settings_CrawledPhone` (BOTH CoreHub PG AND ShopERP SQLite — correction C2). DO NOT change `Status` default.
3. Phase 3 — Services (task card: `task_phase3_services.md`): Split `OnboardUnverifiedAsync` (Pending only) + `VerifyAsync` (user + groups + Activate + ContactPhone from owner-claim form + **publish outbox `TenantVerifiedEvent`** — Option A). New `ITenantClaimService` + `IDuplicateDetectionService` (first canonical, rest mark dup of first — correction H5). **Modify `TenantManagementService.UpdateProfileAsync` — publish outbox `TenantProfileUpdatedEvent`** (Option A). **M3:** VerifyAsync does NOT copy CrawledPhone→ContactPhone (legacy "unmask" dropped). ContactPhone from owner Claim form.
4. Phase 4 — API Gateway + TenantSyncSubscriber (task card: `task_phase4_api.md`): 3 new Gateway controllers + modify `TenantStoreController.GetBySlug` (Pending: `Phone=null` HIDE section — M3, NO `MaskedPhone` field — correction H6) + rate limit + YARP forward to crawler port **5010** (correction C3). **NEW `TenantSyncSubscriber` ở `5_WebApps/ShopERP/Services/`** (Option A) — subscribe `vanan.cloud.tenant.verified` + `vanan.cloud.tenant.profile.updated` → upsert Tenant row SQLite (cùng Guid tenantId). Pending events KHÔNG subscribe.
5. Phase 5 — Crawler worker (task card: `task_phase5_crawler.md`): New `7_Tooling/VanAn.Crawler.csproj` (governance exception). Hybrid `RestApiAdapter` (config-driven) + `TrangVangHtmlAdapter` (AngleSharp). HTTP to Gateway, no DbContext. Port 5010. **Verify M2 (API schema) + O2 (API key auth) before start.**
6. Phase 6 — UI KhachLink (task card: `task_phase6_ui_khachlink.md`): Pending banner on `Store.razor` + new `Claim.razor` + `ClaimHttpService`. NO `MaskedPhone` field. UI Platform components only.
7. Phase 7 — UI ShopERP Admin (task card: `task_phase7_ui_shoperp.md`): Pending tab + Duplicates tab in `TenantManagement.razor` + `ClaimsQueue.razor` + `CrawlTrigger.razor`. UI Platform only.
8. Phase 8 — Tests + RV (task card: `task_phase8_tests_rv.md`): Domain tests, service tests, integration tests, crawler tests. 5-layer RV. Playwright Gate 3 lifted after build pass.

**Resolve open questions before relevant phase:**
- ~~M3~~ RESOLVED 2026-08-25 (user-approved): Crawl SĐT + store CrawledPhone internal, HIDE SĐT section trên Pending profile (tránh "công khai" per Luật 91/2025 Điều 16). Sau Verify, ContactPhone = owner-provided (consent). BỎ SMS notify. Residual risk: storage = processing chưa consent — user chấp nhận + xóa CrawledPhone sau Verify (data minimization).
- ~~O4~~ RESOLVED 2026-08-25 (Option A approved): Active tenant sync PG→SQLite qua NATS (TenantVerifiedEvent + TenantProfileUpdatedEvent → TenantSyncSubscriber) — đảm bảo tenant identity nhất quán, tránh accounting split. Pending KHÔNG sync.
- M5 (rate limit impl) — before Phase 4
- M2 (verify doanhnghiep.vn + xinvoice.vn API schema) — before Phase 5
- O2 (Gateway API key auth for crawler) — before Phase 5
- O1 (KhachLink image upload service exists?) — before Phase 6
- O3 (VanAnDbContextTestFactory update) — before Phase 8

**Post-deploy RV (pending — commits `e7848be9`, `1eeb4615`, `6c9182da`, `5c5a07c5`, `4ee64719` deployed via CD; #103 RV already PASS):**
1. RV `timlathay.com`: (a) `/_framework/blazor.web.js` 200 + `application/javascript` MIME; (b) Home page empty until search (no tenant list on entry); (c) voice search input persists; (d) `/_blazor` WebSocket connects (no console error); (e) StoreFinder map markers render (no 404 for marker-icon.png); (f) close issue #157
2. RV `diemthuong2.khachvip.online`: (a) no `ThemeType` JsonException in console; (b) tenant branding loads from Gateway (theme, colors); (c) `/icons/shortcuts/search.png` + `categories.png` 200; (d) ServiceWorker `v19-merge-activate` activated (no "script evaluation failed"); (e) PWA shortcuts work
3. RV Issue #161 (app2.khachvip.online accounting): (a) Revenue entry submits successfully with all fields filled; (b) Expense entry submits successfully; (c) Transaction history "Ngày" column shows user-entered date (not creation time); (d) close issue #161
4. RV Issue #156 (all nav menus): (a) ShopERP main sidebar groups collapse/expand; (b) Accounting sidebar groups work; (c) EInvoice sidebar groups work; (d) KhachLink desktop sidebar groups work; (e) close issue #156

**Financial Intelligence MVP-2 (active):**
1. Push branch `feature/financial-intelligence-mvp2` (await user approval)
2. `gh pr create` → merge → CD Multi-VPS deploy
3. RV L1-L5

**KhachLink Multi-Profile R2/R3 (deferred):**
- R2 Sprint 7: Reseller profile preset + SystemAdmin UI + tests
- R3 Sprint 8-9: Logistics + JobMarket profiles

**Issue closure (pending manual RV):**
- Issue #130 (Guard QR creation) — 5 fixes applied, pending VPS RV + close
- Issue #126 (Guard QR Verify) — all 3 releases merged, pending manual RV + close

**Deferred / monitoring:**
- R2 (S4 EasyOCR) — deferred until VPS upgrade (4GB RAM) + tenant demand
- GCP Data Seeding — seed production data (fresh DB only 3 test tenants)
- #99-3 Phase B — Alliance VND Normalization (awaiting user approval)
- Hybrid Strategy Bước 2 — trigger when CPU > 70% / Memory > 80%
- Post-Sprint 7 flaky tests — 4 EInvoiceOrchestratorTests (skipped via CI filter)
- v3.0 deferred — INV-009, payment provider (VNPay/Momo), Ops Cost, Tier Distribution
- nginx deferred task cards — per-user rate limit, Blazor API aggregation, API classification

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| Loyalty Alliance = Option B (HTTP proxy + cache + idempotency) | Multi-VPS ready, ShopERP does NOT connect to PG directly |
| nginx 5-layer rate limit | Separate API/page/auth/WebSocket/static quotas — prevents 503 on fast navigation |
| Directory SSR = separate container, nginx map-based routing | Directory-profile tenants get <1s SSR load; Commerce domains keep WASM |

**Deployment Modes:** SaaS (`docker-compose.prod.yml` — all on 1 VPS) ‖ Edge (`docker-compose.edge.yml` — Server A: ShopERP+SQLite+NATS, Server B: Gateway+PG+KhachLink).

---

## 6. History Log (compressed — see archive + git log)

* [2026-08-25] **ISSUE #103 — IMPERSONATION + DATA ISOLATION.** 2 commits on `main` @ `73f77f14`. `c42c4cbe`: Razor Page flow (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) replacing HttpClient POST — proper HTTP context for Set-Cookie + redirect. Dual role (SystemAdmin + Owner) + `impersonating` marker. Global banner in `MainLayout.razor`. `73f77f14`: data isolation follow-up — strip SystemAdmin role during impersonation (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims) → 9 pages auto-fix to tenant-scoped data. Re-add SystemAdmin on exit. `[AllowAnonymous]` on `exit-impersonation` API + `wasImpersonating` guard. 25/25 access matrix tests PASS. Pushed, CI PASS, CD deployed. RV 9/9 PASS on `app2.khachvip.online`.
* [2026-08-23] **ISSUE #156 — NAV GROUP + COLLAPSIBLE ALL MENUS.** `4ee64719`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên). Pushed, CI PASS, CD deployed. Comment on #156.
* [2026-08-23] **ISSUE #161 — ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** `5c5a07c5`. Bug 1: JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on selects. Bug 2: 3-layer TransactionDate fix (Domain optional param → Service pass-through → UI pass user date). 5 test files updated for Moq. Pushed, CI PASS, CD deployed. Comment on #161.
* [2026-08-23] **POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS (3 commits).** `e7848be9` (issue #157: nginx `/_framework/` proxy + Home page no initial load + voice search `@bind:event="oninput"`). `1eeb4615` (WebSocket `/_blazor` upgrade headers + Leaflet marker icons → CDN). `6c9182da` (KhachLink Commerce: `ThemeType` enum `JsonStringEnumConverter` + missing shortcut icons + SW duplicate `activate` merge v19). All pushed, CI PASS, CD deployed. Pending RV.
* [2026-08-23] **DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app for Directory-profile KhachLink tenants (timlathay.com). Load: ~10s (22.8MB WASM) → 0.04s (cached) / 0.56s (first). 4 runtime fixes. CD 4 runs SUCCESS. RV D3-D8 all PASS.
* [2026-08-21] **FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE** on `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
* [2026-08-20] **PLATE-AS-METADATA REFACTOR + R2 PHOTO CLEANUP + QR/OCR FIXES — COMPLETE + DEPLOYED + RV PASS.** PlateNumber optional (154faf19). R2 Cleanup Service (60972c7c + a98e6f7e auth fix + e7911e23). QR white screen root cause (9f8495e9 — vendored qrcode.js corrupt → official v1.4.4). OCR 2-row plate (b07ec9cb).
* [2026-08-19] **OCR HUB R1 COMPLETE + MERGED + DEPLOYED.** QR Wallet 2-tab merge + OCR config infra + PaddleOCR ONNX client-side. #150 JSON case fix + #142 voice search auto-submit.
* [2026-08-17] **DYNAMIC CORS SPRINT 1 COMPLETE + MERGED (PR #133).** DynamicCorsService from KhachLinkInstance registry. RV 8/8 PASS.
* **Older (2026-08-15 and before):** KhachLink Multi-Profile R1, Issue #130, Guard QR Verify #126, Domain Reseller R1, Sprint A+B, GitHub Issues #114/#123/#124/#125, VALCN v2.0, Gateway Refactor, TT 99 compliance, Loyalty Alliance, Community Commerce, Multi-VPS Option C. See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` | Crawl-to-Onboard Tenant Pipeline plan (8 phases, 12 design decisions, research complete) |
| `docs/AI/tasks/directory_ssr/` | Directory SSR master plan + task card + detail coding plan (COMPLETE) |
| `docs/AI/tasks/task_financial_intelligence_mvp2.md` | Financial Intelligence MVP-2 task card (5 phases complete, pending PR) |
| `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` | Financial Intelligence SRS |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink WASM/SSR (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
                       ↓
              [in-process CoreHub]
                       ↓
                  PostgreSQL (central)

=== Edge Mode (docker-compose.edge.yml) ===
Server A (Edge):              Server B (Central):
  ShopERP → SQLite              Gateway → PostgreSQL
  NATS sync worker              [in-process CoreHub]
       ↓ NATS ↓
  ---------------→ Gateway
                   KhachLink → Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E.
**Roles:** `UserRole` (tenant-scoped) + `PlatformRole` (cross-tenant: SystemAdmin).

---

## 9. AI Health Check

- **Assumptions:** 0
- **Verified Facts:** Branch=`main` @ `73f77f14`. Crawl-to-Onboard Pipeline: plan written + 12 design decisions locked + 8 phases documented + research verified against codebase (Domain/Services/API/KhachLink/Outbox/Migrations/Tests/Solution structure). Older work: 7 recent commits on `main` (issue #103 impersonation+isolation, #157, WebSocket/Leaflet, KhachLink enum/icons/SW, #161 accounting validation+date, #156 nav group). Build 0 errors. 1411 unit + 17 unit + 273 integration + 39 arch ALL PASS. #103 RV 9/9 PASS. Pending production RV for 5 older commits.
- **Open Questions:** 4 (will resolve during implementation: Cloudinary upload service, API key auth, TestFactory update, CrawlSource location — all documented in plan file)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (25+), Open Questions (4) ≥ 3 → BUT all 4 are implementation-time questions with documented resolution paths in plan file, not blockers for starting Phase 1.

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-08-25 — CRAWL-TO-ONBOARD TENANT PIPELINE — PLAN COMPLETE.** New objective. Plan file: `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` (600 dòng). Pipeline: crawl trangvangvietnam.com (HTML) + doanhnghiep.vn/xinvoice.vn (REST API) → Pending tenant (read-only, SĐT mask ND13/2023) → Owner claim (GPKD upload) → SysAdmin approve → Active tenant + admin user. 12 design decisions locked (governance exceptions: new `7_Tooling/VanAn.Crawler.csproj` + Domain modification). 8 phases: Domain → Migration → Services → API → Crawler → UI KhachLink → UI Admin → Tests+RV. Research verified against codebase. Legal: trangvangvietnam ToS cấm scraping quy mô lớn → batch nhỏ + polite 3-5s; doanhnghiep.vn API = preferred legal source. Awaiting implementation start.
* **2026-08-25 — ISSUE #103 DATA ISOLATION (FOLLOW-UP).** Commit `73f77f14` on `main`. Root cause: impersonation copied ALL claims + added Owner role but did NOT remove SystemAdmin role → user had BOTH roles → `IsInRole("SystemAdmin")=true` → 9 pages showed cross-tenant data (Orders "ALL tenants" dropdown with `IgnoreQueryFilters()`, Accounting/EInvoice EMPTY default, UserManagement/ShopFeatures tenant selector). Fix: strip SystemAdmin role during impersonation in `Impersonate.cshtml.cs` + `AdminController.cs` (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims). Re-add SystemAdmin role on exit in `ExitImpersonate.cshtml.cs` + `AdminController.ExitImpersonation`. `[AllowAnonymous]` on `exit-impersonation` API endpoint + `wasImpersonating` guard. 2 new tests (AM-S24: `/admin/tenants` denied after impersonation; AM-S25: accessible after exit). 25/25 tests PASS. Pushed, CI PASS (1411+17+273+39), CD deployed. RV 9/9 PASS on `app2.khachvip.online` (login → impersonate → /admin/tenants 302 denied → /orders 200 tenant-scoped → exit → /admin/tenants 200 restored → API backward compat 200+200).
* **2026-08-25 — ISSUE #103 IMPERSONATE BUTTON NOT WORKING.** Commit `c42c4cbe` on `main`. Switched from HttpClient POST to Razor Pages (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) for proper HTTP context handling (Set-Cookie + redirect). Dual role (SystemAdmin + Owner) + `impersonating` marker claim. Global banner in `MainLayout.razor` with exit button. `NavMenu.razor` hides "Hệ thống" menu when impersonating. `AdminLayout.razor` renders Owner menu. `TenantManagement.razor` uses NavigateTo. 23 integration tests PASS (18 original + 5 new Razor Page flow). Pushed, CI PASS, CD deployed. RV ALL PASS.
* **2026-08-23 — ISSUE #156 NAV GROUP + COLLAPSIBLE ALL MENUS.** Commit `4ee64719` on `main`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based grouped), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu desktop sidebar (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên, mobile bottom bar giữ nguyên). Pushed, CI PASS (1411+17+266+39), CD deployed. Comment on #156.
* **2026-08-23 — ISSUE #161 ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** Commit `5c5a07c5` on `main`. Bug 1: Revenue/Expense validation fail — JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on account/category selects. Bug 2: TransactionDate wrong — 3-layer fix (Domain `AccountingEntry` constructor + `CreateRevenue`/`CreateExpense` factory methods add optional `transactionDate` param → `IAccountingService` + `AccountingEntryService` pass-through → `RevenueEntry.razor` + `ExpenseEntry.razor` pass user-entered date). 5 test files updated for Moq `It.IsAny<DateTime?>()`. Also fixed mojibake em dash in `AccountingEntryDto.cs`. Pushed, CI PASS, CD deployed. Comment on #161.
* **2026-08-23 — POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS.** 3 commits on `main` @ `6c9182da`. `e7848be9`: issue #157 (nginx `/_framework/` proxy for Directory domains + Home page no initial tenant load + voice search `@bind:event="oninput"`). `1eeb4615`: nginx WebSocket upgrade headers for `/_blazor` + Leaflet marker icons → unpkg CDN. `6c9182da`: KhachLink Commerce WASM — `ThemeType` enum `JsonStringEnumConverter` in `ShopConfigHttpService` + `TenantProfileHttpService` + missing shortcut icons + SW duplicate `activate` merge (v18 → v19-merge-activate). All pushed, CI PASS, CD deployed. Pending RV.
* **2026-08-23 — DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app (port 8080, 256MB). nginx map-based routing with Docker DNS resolver. 4 runtime fixes: nginx upstream DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum string conversion. CD 4 runs SUCCESS. RV D3-D8 all PASS: 0.04s cached load, 10 stores, Commerce unaffected, 56MiB. Local test PASS. Pre-push CI: 1411 unit + 266 integration + 39 arch ALL PASS.
* **2026-08-21 — FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE.** Branch `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
