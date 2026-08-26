# MASTER PLAN: Crawl-to-Onboard Tenant Pipeline

> Created: 2026-08-25
> Source plan (legacy, 600 dòng): `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` (deprecated — keep for audit trail only)
> Status: PENDING USER APPROVAL TO START PHASE 1
> Branch (proposed): `feature/crawl-onboard-tenant-pipeline`

## 1. MỤC TIÊU

Pipeline crawl business listings (trangvangvietnam.com HTML + doanhnghiep.vn/xinvoice.vn REST API) → tạo **Pending tenant** (profile read-only, SĐT mask theo ND13/2023) → owner **Claim** qua GPKD upload → **SysAdmin approve** → tenant Active + admin user + permission groups + published slug.

## 2. SCOPE (8 phases)

| Phase | Tên | Layer | Gate | Task card |
|---|---|---|---|---|
| 1 | Domain + Events | 1_Shared | **Gate 5 (Domain)** — user-approved | `task_phase1_domain_events.md` |
| 2 | EF Config + Migration | 3_CoreHub + 5_WebApps/ShopERP | — | `task_phase2_ef_migration.md` |
| 3 | Services (Onboarding split + Claim + Duplicate) | 3_CoreHub | — | `task_phase3_services.md` |
| 4 | API Gateway + TenantSyncSubscriber | 2_Gateway + 5_WebApps/ShopERP | — | `task_phase4_api.md` |
| 5 | Crawler worker (.csproj mới) | 7_Tooling | **Governance exception** (NO new .csproj rule) — user-approved | `task_phase5_crawler.md` |
| 6 | UI KhachLink | 5_WebApps/KhachLink | UI Platform compliance | `task_phase6_ui_khachlink.md` |
| 7 | UI ShopERP Admin | 5_WebApps/ShopERP | UI Platform compliance | `task_phase7_ui_shoperp.md` |
| 8 | Tests + RV | 6_Tests + production | Playwright Gate 3 | `task_phase8_tests_rv.md` |

**Dependency chain:** 1 → 2 → 3 → 4 → (5 ∥ 6 ∥ 7) → 8. Phases 5/6/7 có thể song song sau khi 4 done.

## 3. DESIGN DECISIONS (LOCKED — user-approved 2026-08-25)

| # | Decision | Ghi chú / Correction |
|---|---|---|
| 1 | Crawler = new `7_Tooling/VanAn.Crawler.csproj` | Governance exception approved. Document trong commit + AGENTS.md |
| 2 | Domain modification (Gate 5 exception) — add `TenantStatus.Pending=5` + `CreateUnverified()` + `Verify()` | **Correction H1:** dùng `=5` (KHÔNG phải `=0`) để tránh EF default conflict. Existing rows `Status=1` (Active) không bị ảnh hưởng |
| 3 | **M3 RESOLVED 2026-08-25:** Crawl SĐT (legal_rep_phone từ trangvangvietnam supplement) + store raw `CrawledPhone` trong `TenantSettings`. **Pending profile KHÔNG hiển thị SĐT** (hide section entirely, không mask) — tránh công khai dữ liệu cá nhân chưa consent per Luật 91/2025/QH15 Điều 16. Sau owner Claim + Verify: `ContactPhone` set từ **owner-provided** form (consent rõ ràng), `CrawledPhone` giữ internal cho admin verify. | **Residual legal risk:** Luật 91/2025 + ND356/2025 — storage = processing, SĐT là dữ liệu cá nhân cơ bản (Điều 3(7)). User chấp nhận rủi ro dựa trên "internal use only for owner verification" — ghi chú tech debt + đánh giá tác động định kỳ (per Luật 91/2025 Điều 19(2) cơ chế giám sát). |
| 4 | Trùng MST: `PotentialDuplicateOf` flag (Guid? — **Correction C1**), SysAdmin pick 1 verify, other → Inactive. **NO data merge** | **Correction C1:** FK dùng `Guid?` (PK reference), KHÔNG dùng `TenantId?` value object — tuân thủ Single-Identity Pattern (HARD STOP) |
| 4b | **NEW (Option A approved):** Active tenant sync PG→SQLite qua NATS để đảm bảo tenant identity nhất quán (tránh accounting split — order hôm nay gắn tenantId X PG, mai gắn tenantId Y SQLite → số liệu sai). `TenantVerifiedEvent` + `TenantProfileUpdatedEvent` → outbox → NATS → `TenantSyncSubscriber` (ShopERP) upsert SQLite row (cùng `Guid` tenantId). Pending KHÔNG sync. | Follow `OrderSyncSubscriber` pattern. Cần `TenantProfileUpdatedEvent` (5 events total). Resolves data integrity constraint user-raised 2026-08-25. |
| 5 | Verify flow: Owner claim (GPKD upload) + SysAdmin approve (cross-check MST dangkykinhdoanh.gov.vn) | — |
| 6 | Pending scope: profile read-only public + Claim button. No login, no orders, no accounting. **Pending profile KHÔNG hiển thị SĐT** (M3 resolved — hide section, không mask) | — |
| 7 | Pending slug: `pending-{taxCode}-{random4}`. Verify → switch to clean slug | — |
| 8 | Crawler schedule: on-demand (SysAdmin trigger) + optional cron nightly | — |
| 9 | **~~SMS + email + show credentials once~~** — REVISED (M3): **BỎ SMS** (gửi SMS cho SĐT crawled chưa consent = vi phạm Luật 91/2025 Điều 19 — không có exemption marketing). Giữ: (a) show credentials once trên SysAdmin UI, (b) email nếu crawled source có business email, (c) owner tự tìm Pending profile trên VanAn directory → click Claim. | — |
| 10 | Hybrid architecture: `RestApiAdapter` (config-driven `crawler-sources.json`) + `IHtmlAdapter` (per-site code — trangvangvietnam supplement SĐT) | — |
| 11 | Priority sources: doanhnghiep.vn API (legal business data per Luật Doanh nghiệp 2020) + xinvoice.vn (verify); trangvangvietnam = supplement SĐT only (internal use, not displayed) | **Open M2:** verify endpoint + schema thật trước Phase 5 |
| 12 | `crawler-sources.json` config file — add API source = edit JSON + restart, no recompile | — |

## 4. CRITICAL CORRECTIONS vs LEGACY PLAN

> Các điểm sai đã phát hiện khi review legacy plan 600 dòng. Đã sửa trong master plan + task card.

| # | Legacy plan said | Reality (verified) | Correction |
|---|---|---|---|
| **C1** | `TenantId? PotentialDuplicateOf`, Claim/Crawl `TenantId (TenantId)` | Governance Single-Identity Pattern: FK phải là `Guid` (PK ref), KHÔNG dùng value object | Đổi tất cả FK sang `Guid`/`Guid?` |
| **C2** | "NO ShopERP migration" — claim/crawl PG-only | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs:55` có `DbSet<Tenant> Tenants` (SQLite mirror) | Thêm ShopERP SQLite migration cho 2 cột Tenants mới (`PotentialDuplicateOf`, `Settings_CrawledPhone`) |
| **C3** | Crawler worker port 5003 | `2_Gateway/appsettings.Development.json:18` `"BaseUrl": "http://localhost:5003"` = ShopERP | Crawler dùng port khác (đề xuất 5010) hoặc Docker container riêng |
| **C4** | `UpdateSlug()` guard đổi `if (Status == Inactive)` → `if (Status != Active) throw` | `Tenant.cs:182` hiện cho Suspended + Converted update slug → tighten guard sẽ break | Giữ guard `Status == Inactive`, KHÔNG tighten. Pending tenant set slug trực tiếp qua factory parameter, KHÔNG qua `UpdateSlug()` |
| **H1** | Research nói `Pending=0` (line 41), impl nói `Pending=5` (line 101) | Contradiction | Chốt `Pending=5` |
| **H2** | `CreateUnverified(TenantId, name, settings)` 3 params (line 110) vs `CreateUnverified(..., slug)` 4 params (line 189) | Contradiction | Chốt 4 params: `CreateUnverified(TenantId id, string name, TenantSettings settings, string pendingSlug)` — slug đi thẳng vào Settings, bypass `UpdateSlug()` (giải C4) |
| **H3** | "14 With methods" | Đếm thực tế: **12** With methods. Constructor 16 params (gồm `LegalForm`/`BusinessField`/`CharterCapital` mà legacy With methods chưa thread) | Thêm `CrawledPhone` vào constructor + 12 With methods + preserve LegalForm/BusinessField/CharterCapital ở mỗi With |
| **H4** | `Verify()` chỉ guard `Status == Pending` | Tenant có `PotentialDuplicateOf != null` có verify được không? Ambiguous | Thêm guard: `Verify()` throw nếu `PotentialDuplicateOf != null` — SysAdmin phải resolve duplicate trước |
| **H5** | Crawl batch mark duplicate mỗi cái của cái trước | Chuỗi reference lộn xộn | Service layer: query TaxCode trước, nếu đã có Active/Pending tenant → mark duplicate of **first canonical**, không phải của item trước |
| **H6** | KhachLink thêm `ShopDto.MaskedPhone` field | Gateway mask Phone cho Pending, thêm field = redundant | Bỏ `MaskedPhone`. **M3 new (2026-08-25):** Pending profile không hiển thị SĐT section entirely (hide, không mask) → `Phone` field = null/empty trên Pending DTO, không cần MaskPhone helper trên Gateway. Sau Verify, `Phone` = owner-provided ContactPhone. |
| **H7 (Option A approved 2026-08-25)** | Plan chỉ có 4 events, không có event cho admin update profile | Active tenant sync SQLite cần event khi admin rename/change settings — không có thì SQLite stale | Thêm `TenantProfileUpdatedEvent(Guid TenantId, string NewName, TenantSettingsSnapshot Settings, DateTime OccurredAt)` — **5 events total**. `TenantManagementService.UpdateProfileAsync` publish event khi admin update active tenant. |

## 5. LEGAL / TOS FINDINGS (REVISED 2026-08-25 — M3 resolved)

> ⚠️ **Active law:** Luật 91/2025/QH15 (Bảo vệ dữ liệu cá nhân 2025) + Nghị định 356/2025/NĐ-CP — effective 01/01/2026, thay thế ND13/2023.

### Luật 91/2025/QH15 (currently active)
- **Điều 19** — 5 trường hợp xử lý dữ liệu cá nhân không cần consent: (a) khẩn cấp, (b) an ninh quốc gia, (c) cơ quan nhà nước, (d) hợp đồng, (đ) "trường hợp khác". **KHÔNG có exemption "dữ liệu đã công khai"** như ND13/2023 Điều 17(2).
- **Điều 16** — dữ liệu cá nhân chỉ được **công khai** với consent HOẶC per law. → Crawled SĐT **không được hiển thị công khai trên Pending profile**.
- **ND356/2025 Điều 3(7)** — SĐT là "dữ liệu cá nhân cơ bản".
- "Xử lý" bao gồm "thu thập, ghi, phân tích, **lưu trữ**, công khai..." → store = processing.

### M3 Resolution (user-approved 2026-08-25)

**Crawl SĐT + store raw, KHÔNG hiển thị trên Pending profile:**
- Crawl legal_rep_phone từ trangvangvietnam (supplement source) → store `CrawledPhone` trong `TenantSettings` (internal use).
- **Pending profile: HIDE SĐT section entirely** — `Phone` field = null trên DTO, không mask, không display. Tránh "công khai" per Điều 16.
- Sau owner Claim + Verify (consent rõ ràng): `ContactPhone` = owner-provided từ Claim form. `CrawledPhone` giữ internal cho SysAdmin verify owner identity (legitimate processing — admin access, không public).
- **BỎ SMS notify** (Decision #9) — gửi SMS cho SĐT chưa consent = vi phạm rõ (marketing without consent).
- **Residual legal risk** (user chấp nhận): storage of SĐT crawled chưa consent — Luật 91/2025 không có exemption. Mitigation: ghi tech debt, đánh giá tác động định kỳ per Điều 19(2) cơ chế giám sát, xóa `CrawledPhone` sau khi owner Verify (data minimization — không cần giữ sau khi đã có consented ContactPhone).

### doanhnghiep.vn API (legal source — preferred, primary)
- Free 100 req/day REST API, no auth, từ GDT.
- Returns business registration data (MST, name, address, industry, charter capital, registered_at, status, legal_rep_name) — public per Luật Doanh nghiệp 2020. **Không trả về SĐT.**
- ⚠️ Verify endpoint thật trước Phase 5 (M2 open).

### trangvangvietnam (supplement — SĐT only)
- HTML scrape, sister site trangvang.biz ToS cấm scraping quy mô lớn → batch 50-100/run, polite 3-5s, User-Agent `VanAnCrawler/1.0 (+contact@vanan.vn)`.
- **Purpose:** supplement SĐT (legal_rep_phone) — internal use only, NOT displayed publicly.
- ⚠️ ToS risk + Luật 91/2025 storage risk — user chấp nhận dựa trên "internal verification use".

### xinvoice.vn API (verify source)
- REST API, cần client-id + api-key, từ Tổng cục Thuế.
- Verify MST existed + status — cross-check.
- ⚠️ Verify endpoint thật trước Phase 5 (M2 open).

### Legal risk assessment (M3 resolved — user-approved approach)

| Hành động | Luật 91/2025 risk | Verdict |
|---|---|---|
| Crawl doanhnghiep.vn (business data) | Thấp — public per Luật Doanh nghiệp 2020 | ✅ OK |
| Crawl trangvangvietnam SĐT | Trung bình — ToS + cá nhân SĐT | ⚠️ User accept (internal use only) |
| Store raw SĐT crawled (`CrawledPhone`) | Trung bình — Luật 91/2025 storage = processing | ⚠️ User accept + delete after Verify (data minimization) |
| Display SĐT trên Pending profile | Cao — Điều 16 công khai without consent | ❌ HIDE section entirely |
| Owner Claim form collect SĐT | Thấp — consent rõ ràng | ✅ OK |
| SMS notify owner trên SĐT crawled | Cao — marketing without consent | ❌ BỎ |
| SysAdmin view CrawledPhone internal | Thấp — legitimate processing (owner verification) | ✅ OK |

## 6. GATES & HARD STOPS

| Gate | Áp dụng phase | Action |
|---|---|---|
| **Gate 5 (Domain)** | Phase 1 | User-approved exception. IMPLEMENT mode + Domain Phase active + user approval. AccountingEntry immutable (N/A — không touch). |
| **Single-Identity Pattern** | Phase 1, 2 | FK dùng `Guid`, KHÔNG dùng value object. Audit checklist trong task card Phase 1. |
| **NO new .csproj rule** | Phase 5 | User-approved exception. Document trong commit + AGENTS.md. Architecture test update. |
| **UI Platform compliance** | Phase 6, 7 | Dùng VanAnButton/VanAnCard/VanAnAlert/VanAForm/VanATable, KHÔNG custom HTML/CSS. |
| **Playwright Gate 3** | Phase 1-7 | Playwright DISABLED trong IMPLEMENT. Chỉ enable ở Phase 8 sau khi build pass. |
| **KhachLink HTTP-only** | Phase 6 | KHÔNG inject `IVanAnDbContext` ở KhachLink — gọi Gateway HTTP only. |
| **CoreHub = Class Library** | All phases | CoreHub KHÔNG có `OutputType=Exe`. (Legacy plan note "existing violation" — verify lại, flag tech debt nếu vẫn còn) |

## 7. RESEARCH SNAPSHOT

> Codebase research findings (file paths, line numbers, method signatures) tách riêng để dễ refresh.
> **File:** `docs/AI/plans/crawl-onboarding-research.md`
> **Snapshot taken:** 2026-08-25 @ commit `73f77f14` (main)
> ⚠️ **Warning:** Line numbers sẽ stale sau mỗi commit. Re-verify trước mỗi phase.

## 8. OPEN QUESTIONS (resolve trước phase tương ứng)

| # | Question | Resolve before | Owner |
|---|---|---|---|
| M2 | doanhnghiep.vn + xinvoice.vn endpoint/schema thật? | Phase 5 | Dev (curl verify) |
| ~~M3~~ | ~~ND13/2023 — store raw `CrawledPhone` crawled (chưa consent) có vi phạm không?~~ | ~~Phase 1~~ | **RESOLVED 2026-08-25 (user-approved):** Crawl SĐT + store `CrawledPhone` raw, **HIDE SĐT section trên Pending profile** (không display → tránh "công khai" per Luật 91/2025 Điều 16). Sau Verify, `ContactPhone` = owner-provided (consent). BỎ SMS notify. CrawledPhone giữ internal cho SysAdmin verify, xóa sau Verify (data minimization). Residual risk: storage = processing chưa consent — user chấp nhận + đánh giá định kỳ per Điều 19(2). |
| ~~M5~~ | ~~Rate limit impl: `Microsoft.AspNetCore.RateLimiting` policy code cụ thể~~ | ~~Phase 4~~ | **RESOLVED 2026-08-25:** `AddRateLimiter` đã configured trong `2_Gateway/Program.cs:103-137` với 3 policies (checkout/catalog/auth). Pattern: `options.AddPolicy("name", ctx => RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions { PermitLimit, Window, QueueProcessingOrder.OldestFirst, QueueLimit=0 }))`. Phase 4 add policy `claim-submit`: `PermitLimit=3, Window=TimeSpan.FromHours(24)`. Apply `[EnableRateLimiting("claim-submit")]` trên `POST /api/v1/tenants/{id}/claims`. `UseRateLimiter()` middleware đã active (line 649). |
| ~~O1~~ | ~~KhachLink đã có image upload service (Cloudinary)?~~ | ~~Phase 6~~ | **RESOLVED 2026-08-25:** NO service trong KhachLink (HTTP-only, can't inject CoreHub services). BUT `IImageStorageService` + `CloudinaryImageStorageService` exist in `3_CoreHub/Services/`, registered in `5_WebApps/ShopERP/Program.cs:480`. **Decision:** Add Gateway endpoint `POST /api/v1/images/upload` (or reuse existing if any) that uses `IImageStorageService` → KhachLink calls via HTTP. Respects KhachLink HTTP-only rule. Alternative (b) client-side Cloudinary unsigned upload = security risk, rejected. |
| ~~O2~~ | ~~Gateway đã có API key auth cho non-user clients?~~ | ~~Phase 5~~ | **RESOLVED 2026-08-25:** YES — `2_Gateway/Services/HmacApiKeyLookupAdapter.cs` adapts `IApiKeyManagementService` (CoreHub) to `IHmacApiKeyLookup` (Gateway). Pattern: API key auth via HMAC signing. Phase 5 crawler authenticates via HMAC API key — register new key for crawler via `IApiKeyManagementService`, sign requests with HMAC. |
| ~~O3~~ | ~~`VanAnDbContextTestFactory` cần update cho 2 DbSet mới?~~ | ~~Phase 8~~ | **RESOLVED 2026-08-25:** NO factory change needed. `6_Tests/VanAn.Core.Tests/TestInfrastructure/VanAnDbContextTestFactory.cs` uses `VanAnDbContext` with `EnsureCreated` (SQLite in-memory) — new DbSets (`TenantClaimRequests`, `CrawlSources`) auto-created from EF model. Migrations not used in test factory. |
| ~~M4~~ | ~~Research line refs stale sau commits?~~ | ~~Every phase~~ | **VERIFIED 2026-08-25 @ branch HEAD `7e8afec7`:** All key line refs accurate: `TenantStatus.cs:6`, `TenantSettings.cs:7` (12 With methods verified), `Tenant.cs:10/180`, `TenantEvents.cs:8` (5 existing events verified), `TenantConfiguration.cs:48/52/65`, `ShopERPDbContext.cs:55`. Research snapshot current. |
| ~~O4~~ | ~~Pending tenant có cần sync sang ShopERP SQLite qua NATS không?~~ | ~~Phase 1~~ | **RESOLVED 2026-08-25 (Option A approved):** Active tenant (sau Verify) MUST sync sang SQLite qua NATS để đảm bảo tenant identity nhất quán (cùng `Guid` tenantId ở cả PG + SQLite) — nếu không, order/accounting có thể split giữa 2 tenant ID khác nhau → số liệu kế toán sai. **Pending tenant KHÔNG sync** (chưa có business activity). Implement: `VerifyAsync` + `UpdateProfileAsync` publish outbox `TenantVerifiedEvent` / `TenantProfileUpdatedEvent` → NATS `vanan.cloud.tenant.verified` / `tenant.profile.updated` → NEW `TenantSyncSubscriber` ở ShopERP upsert tenant row SQLite (cùng Guid, copy Name + Settings). Follow `OrderSyncSubscriber` pattern. **Phase 1 thêm `TenantProfileUpdatedEvent` (5 events total).** **C2 SQLite migration vẫn cần** cho 2 cột `PotentialDuplicateOf` + `Settings_CrawledPhone` (schema consistency). |

## 9. ACCEPTANCE CRITERIA (whole feature)

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` PASS
- [ ] All new tests PASS (domain + service + integration + crawler)
- [ ] Manual flow end-to-end: crawl batch → Pending tenant on `/store/pending-*` (masked phone) → owner submit claim → SysAdmin approve → tenant Active → `/store/clean-slug` full profile + owner login works
- [ ] No governance violation: Single-Identity Pattern, UI Platform, KhachLink HTTP-only, CoreHub class library
- [ ] RV 5-layer PASS (per `.devin/rules/runtime-verification.md`)

## 10. RELATED FILES

- **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
- **Task cards:** `docs/AI/tasks/crawl-onboarding/task_phase{1-8}_*.md`
- **Legacy plan (deprecated):** `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md`
- **Governance:** `.devin/rules/governance.md` (Single-Identity Pattern, Gate 5, workflow modes)
- **Workflow:** `.devin/workflows/newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
