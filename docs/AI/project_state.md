# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 + 2026-09-06 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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

**GTM DRILL MACHINE MVP — W1 (MERCHANT AUDIT) PRODUCTION RV PASS.** 🟢
- **Task card:** `docs/AI/tasks/gtm_drill_mvp_task_card.md` (approved 2026-09-06: D1/D2 domain mods + Directory landing + SystemAdmin-dynamic referral commission)
- **Branch:** `main` @ `a21fcffc` (W1 impl `e8cd4e62` + nginx routing fix + Gateway 429 fix)
- **W1 COMPLETE + PRODUCTION RV PASS (2026-09-07):**
  - Gateway `GrowthController` (GET /api/v1/growth/audit?name&mst — anonymous, rate-limit `growth-audit` 10/IP/h, XFF-forwarded client IP, 429 response)
  - Directory `/kiem-tra-cua-hang` landing (Blazor Server SSR, `VanAn.Directory.styles.css`)
  - E2E `gtm-audit.spec.ts` (4 tests, chưa chạy — cần ecosystem)
  - **nginx fix:** timlathay.com page/asset/blazor traffic routed to Directory SSR (port 8080) thay vì KhachLink WASM (port 80) — root cause của W1 RV failure trước đó
  - **RV results:** Directory SSR renders ✅ · Gateway audit JSON ✅ · active tenant found:true ✅ · pending tenant privacy (no phone/email/address) ✅ · claimUrl provided ✅ · rate limit kicks in after 10 req ✅
- **W1 remaining:** E2E run cần ecosystem lên (env: DIRECTORY_URL + AUDIT_TENANT_NAME/AUDIT_PENDING_TENANT_NAME) · CD deploy Gateway 429 fix (pushed `a21fcffc`, chờ CD)
- **W2-W5 theo card:** Demo preview → Revenue Proof counters (D1) → Merchant Referral (D2) → consent + flag `GrowthMachine:Enabled` default OFF + deploy + RV
- Đối chiếu vs `docs/requirements/Ý tưởng việc tự động hóa (Phễu khách hàng).md`: card hiện thực 6/7 MVP steps, defer AI SDR/scoring (Gate G1)

---

## 3. Current Status

- **GTM Drill Machine W1 (Merchant Audit):** ✅ CODE COMPLETE + PRODUCTION RV PASS (2026-09-07) on `main` @ `a21fcffc`. Gateway `GrowthController` (GET /api/v1/growth/audit?name&mst, rate-limit `growth-audit` 10/IP/h, XFF client-IP, 429 response) + Directory `/kiem-tra-cua-hang` landing (Blazor Server SSR) + E2E spec + arch whitelist. **nginx fix:** timlathay.com → Directory SSR (port 8080) thay vì WASM (port 80). RV: Directory SSR ✅ · Gateway JSON ✅ · active tenant ✅ · pending privacy ✅ · rate limit ✅. See Section 2 + Section 10.
- **Branch:** `main` @ `a21fcffc` (W1 Merchant Audit + nginx Directory SSR routing fix + Gateway 429 rate limit. R2.2 Reseller Accounting — PR #169 merged. Crawl-to-Onboard 8 phases complete. Issue #103/#157/#161/#156 deployed). **Build full sln:** 0 errors · **CI:** 1504 core + 17 unit + 276 integration + 41 arch ALL PASS · **.NET SDK:** 8.0.422
- **R2.2 Reseller Accounting:** ✅ COMPLETE + DEPLOYED + RV PASS (2026-09-06). PR #169 merged. 3 tenant booksets (Supplier/Reseller/Platform-skip-when-VA) + `Order.OwnerTenantId` + Auditor UI `/admin/reseller-accounting-reconciliation` + 13 R2.2 tests + 28 pre-existing bUnit test fixes (DI mocks + vi-VN number format). CD Multi-VPS SUCCESS. RV Layer 1+3+4 PASS. Details: Section 10 + archive 2026-09-06.
- **Directory SSR:** ✅ COMPLETE — timlathay.com live (0.04s load, 10 stores, 56MiB). Issue #157 fixed (3 bugs). WebSocket + Leaflet markers fixed. Details: Section 10 + archive 2026-09-06.
- **KhachLink Commerce WASM:** ✅ ThemeType enum + shortcut icons + SW duplicate activate fixed (commit `6c9182da`). Pending RV on `diemthuong2.khachvip.online`.
- **Issue #161 (Accounting):** ✅ Fixed + deployed. Revenue/Expense validation (JS interop DOM read before validate + default formData.Values) + TransactionDate 3-layer fix (Domain optional param → Service pass-through → UI pass user date). Pending RV.
- **Issue #156 (Nav group/collapsible):** ✅ Fixed + deployed. All 4 nav menus converted to grouped/collapsible pattern (ShopERP NavMenu, AccountingLayout, EInvoiceLayout, KhachLink NavMenu). Pending RV.
- **Issue #103 (Impersonation + data isolation):** ✅ Fixed + deployed + RV PASS. Two commits: `c42c4cbe` (Razor Page flow + dual role + banner) + `73f77f14` (strip SystemAdmin role during impersonation → 9 pages auto-fix to tenant-scoped data). 25/25 access matrix tests PASS. RV 9/9 PASS on `app2.khachvip.online`.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS), pending push + PR + CD + RV.
- **Infrastructure (all deployed + RV PASS):** GCP 3 VPS · nginx 5-layer rate limit · Cloudflare R2 (guard photos + auto-cleanup 30d) · Dynamic CORS from KhachLinkInstance registry · KhachLink Multi-Profile R1 enabled · Domain Reseller R1 (GoDaddy API) · Guard QR Verify (Issue #126) · OCR Hub R1 (PaddleOCR client-side) · Plate-as-metadata (PlateNumber optional).
- **Crawl-to-Onboard Tenant Pipeline:** ✅ ALL 8 PHASES COMPLETE + DEPLOYED + RV PASS (2026-08-26). PR #164 merged (Phase 6+7+8). 43 new tests (20 domain + 23 service). RV Layer 1 API + Layer 5 DB all PASS. Pattern #8 bug fix in `DuplicateDetectionService` (commit `9cc83534`). Details: Section 10 + archive 2026-09-06.
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**GTM Drill Machine MVP (✅ W1 COMPLETE + RV PASS — W2 NEXT):**
- Task card: `docs/AI/tasks/gtm_drill_mvp_task_card.md` (5 tuần × 5 mảnh; strategy: `docs/AI/plans/ecosystem-master-business-model.md` Section 4)
- ✅ W1 Merchant Audit code complete (`e8cd4e62`) + nginx fix + 429 fix (`a21fcffc`): Gateway `GrowthController` + rate limit + Directory landing + E2E spec + arch whitelist + nginx routing to Directory SSR
- ✅ W1 Production RV PASS (2026-09-07): Directory SSR renders · Gateway audit JSON · active tenant found:true · pending tenant privacy · rate limit 10/IP/h
- ⏳ W1 remaining: E2E run (cần ecosystem lên + env `DIRECTORY_URL`/`AUDIT_TENANT_NAME`/`AUDIT_PENDING_TENANT_NAME` — chạy theo Playwright rules) · CD deploy Gateway 429 fix (pushed `a21fcffc`)
- Next: **W2 Interactive Demo** (KhachLink `/demo` preview storefront in-memory + prefill `/claim?name=`) → W3 Revenue Proof (D1) → W4 Referral (D2) → W5 consent + flag `GrowthMachine:Enabled` + deploy + RV
- Branch `main` @ `a21fcffc` — pushed, CD triggered
- DEFER theo Gates: AI SDR/auto scoring (G1 — 0 telemetry), search counter, sitemap toàn site (BOM #4 T2), payment gateway (G2)

**Crawl-to-Onboard Tenant Pipeline (✅ COMPLETE — all 8 phases deployed + RV PASS):**
- No further actions. All 8 phases complete, PR #164 merged, CD deployed, RV Layer 1 + Layer 5 PASS.
- Optional follow-up: end-to-end manual test (trigger real crawl → Pending tenant → Claim → Approve → Active) on production with real data.

**Crawler post-deploy RV (pending — commit `942467e0` deployed via CD):**
1. RV crawl trigger on production: (a) trigger crawl with industry + province + search term; (b) verify >20 Pending tenants created (pagination working); (c) verify Gateway log shows forwarded body with camelCase fields populated (industry, province, searchTerm NOT null); (d) verify CrawlSources audit rows inserted with correct RawJson

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
- R2.2 Reseller Accounting: ✅ COMPLETE + DEPLOYED + RV PASS (PR #169)
- R2 Sprint 7: Reseller profile preset + SystemAdmin UI + tests — ✅ COMPLETE (earlier PR #167)
- R3 Sprint 8-9: Logistics + JobMarket profiles — deferred
- **R2.2 follow-up:** Configure `PlatformAccountingTenantId` SystemSetting in production DB (1-time setup by SysAdmin via admin UI) to enable Platform bookset entries for non-Vạn An resellers.

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
| `docs/AI/plans/ecosystem-master-business-model.md` | Ecosystem Master Business Model v1.0 (7 phần + GTM khoan thủng + BOM registry — chiến lược kinh doanh, không phải implementation plan) |
| `docs/AI/tasks/gtm_drill_mvp_task_card.md` | GTM Drill Machine MVP task card (5 tuần, 5 mảnh — APPROVED + W1 complete, W2 next) |
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
- **Verified Facts:** Branch=`feature/gtm-drill-mvp` @ `f78da932` (từ `main` @ `2d98ee77`). GTM W1 Merchant Audit: CODE COMPLETE — Gateway `GrowthController` (audit?name&mst, rate-limit growth-audit, XFF client-IP) + Directory `/kiem-tra-cua-hang` + E2E spec (4 tests, chưa chạy) + arch whitelist GrowthController. Validation: build full sln 0 errors · pre-commit GUARD v6.0 PASS · arch 41/41 PASS. Đối chiếu GTM docs: card hiện thực 6/7 MVP steps. Trên `main`: R2.2 COMPLETE + DEPLOYED + RV PASS (PR #169) · Crawl-to-Onboard 8/8 phases + RV PASS · issue #103/#157/#161/#156 deployed. Production: 3 tenant test (GCP Data Seeding pending).
- **Open Questions:** 1 (PlatformAccountingTenantId not yet configured in production DB — code degrades gracefully, SysAdmin 1-time setup). W1 E2E run pending ecosystem — không phải blocker code.
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (30+), Open Questions (1) < 3 → CLEAR.

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-09-07 — W1 PRODUCTION RV PASS + NGINX DIRECTORY SSR ROUTING FIX (commit `a21fcffc` on `main`).** Production RV phát hiện root cause: nginx `timlathay.com` server block route `location /` → `${KHACHLINK_REMOTE_HOST}:80` (KhachLink WASM) thay vì Directory SSR container (port 8080). Docker-compose có `directory` service trên port 8080 nhưng nginx config không có `map $is_directory` hay upstream nào trỏ tới 8080. Fix: trong section `@@EXT_DOMAIN_START:timlathay.com@@`, đổi tất cả non-API `proxy_pass` từ `${KHACHLINK_REMOTE_HOST}:80` → `:8080` (10 occurrences cho apex + wildcard, cả HTTP + HTTPS). API routes vẫn `gateway:80`. Deploy manual: SCP template → VPS → `docker restart vanan-nginx-1`. Gateway fix: `growth-audit` rate limiter thêm `OnRejected` callback trả 429 + JSON message thay vì default 503. RV results sau fix: (1) `timlathay.com/kiem-tra-cua-hang` → HTTP 200, Blazor Server SSR, `VanAn.Directory.styles.css` ✅; (2) `timlathay.com/` → Directory SSR ✅; (3) Gateway audit empty params → 400 + Vietnamese message ✅; (4) Gateway audit no-match → 200 `{"found":false}` ✅; (5) Gateway audit active tenant "Central Mall" → 200 `{"found":true,"tenant":{...}}` không expose phone/email ✅; (6) Gateway audit pending tenant "DONER LAB" → 200 `{"found":true,"tenant":{"isPending":true,...}}` không expose private contact (M3 privacy) + `claimUrl` provided ✅; (7) Rate limit kicks in after ~10 requests → 503 (sau CD sẽ là 429) ✅. Push `a21fcffc` → main, CD triggered. Pending: CD deploy Gateway 429 fix + E2E run.

* **2026-09-06 — GTM DRILL MACHINE W1 (MERCHANT AUDIT) CODE COMPLETE (commits `e8cd4e62` + docs `f78da932` on `feature/gtm-drill-mvp`).** User approved 3 decisions (D1/D2 domain mods · Directory landing · SystemAdmin-dynamic referral commission). Implementation: Gateway `GrowthController.cs` NEW (GET /api/v1/growth/audit?name&mst — anonymous + rate-limit `growth-audit` 10/IP/h; match MST exact per DuplicateDetection precedent + name ILIKE exact-then-contains per TenantStoreController.Search; report chỉ số đo được per Ground Rule 5: presence/storefront/social/KhachLinkDomain/IndustryPeerCount từ crawl; M3 Pending = name/slug only) + rate policy trong `2_Gateway/Program.cs` + Directory `Audit.razor` NEW (route `/kiem-tra-cua-hang`, copy "bán kết quả không bán phần mềm", no initial load per #157, Pending → claim CTA sang KhachLink) + `GrowthAuditService.cs` NEW (forward end-user IP via X-Forwarded-For — Gateway UseForwardedHeaders rewrite → rate-limit partition theo đúng IP user, không phải IP container Directory) + DI trong Directory `Program.cs` + E2E `gtm-audit.spec.ts` NEW (4 tests env-driven) + arch whitelist `GrowthController` trong `AuthorizationEnforcementTests` (precedent ImageUploadController). Fix lúc build: `OwnerTenantId == tenant.Id` ambiguous (Guid? vs TenantId) → `.Value` phía constant; `AlertVariant.Danger` → `Error`. Validation: build full sln 0 errors · pre-commit GUARD v6.0 PASS · arch 41/41 PASS. Note: guard-check.ps1 standalone FAIL ở encoding check trên untracked `.devin/*.json` cũ của session khác — không phải file W1, không đụng. Cùng commit: docs strategy (master business model + task card + 3 requirement docs) — pre-commit guard bắt buộc source + docs cùng batch. Pending: E2E run (cần ecosystem), W2-W5.
* **2026-09-06 — ECOSYSTEM MASTER BUSINESS MODEL v1.0 CREATED (docs-only, branch `main`).** Tổng hợp 3 tài liệu requirements (tầm nhìn 5 tầng "hình dung Vạn An", GTM "khoan thủng thị trường") + 2 session review (5 BOM → BOM 2.0 → GTM review) thành `docs/AI/plans/ecosystem-master-business-model.md`: Ground Rules (North Star = Active Local GMV, danh sách KHÔNG, business-driven gate + kill-list) · Value Chain 5 tầng × 10 layer (NOW/SAU/NEVER — L4 take-rate giờ NOW vì R2.2 done) · 4 actors (KTV = kênh 1-nhiều, hoa hồng tách theo product line) · Flywheel + 2 động cơ zero-CAC (crawler + KTV) · GTM máy khoan thủng bản vá 3 hố (fake precision → chỉ số đo được; cold-start → SEO danh bạ làm traffic chính; scoring defer vì 0 event tracking) · Unit Economics [V]/[A] · Roadmap 2 tuyến 8 tuần + Year 1-3 + Gates G1-G5 · 5-year model (100 tỷ = valuation story 5x revenue, không phải cash) · BOM Registry 5 mô hình chốt (Anchor Kit qua KTV · HĐĐT prepaid · FI premium · TimLaThay funnel · take-rate) + 2 ẩn số thế giới thực (provider HĐĐT margin, KTV/HKD willingness-to-pay). Không thay đổi code. Verified trong session: QĐ 1568/QĐ-BCT (60% DNNVV TMĐT · 100% HĐĐT · 80% cashless) · `Order.ReferralCode` là salesman-level (merchant-referral chưa có) · 0 telemetry/scoring data source (grep) · search ranking thuần relevance (không paid ranking) · production 3 tenant test. **Follow-up cùng session:** tạo task card `docs/AI/tasks/gtm_drill_mvp_task_card.md` (5 tuần × 5 mảnh GTM: Merchant Audit · Interactive Demo · Revenue Proof counters · Merchant Referral · Remote closing consent; 2 Domain mods D1/D2 + 3 open questions chờ user approve; branch `feature/gtm-drill-mvp`; flag `GrowthMachine:Enabled` default OFF; defer AI SDR/scoring theo Gate G1).
* **2026-09-06 — R2.2 RESELLER ACCOUNTING-CASHFLOW ALIGNMENT COMPLETE + DEPLOYED + RV PASS.** PR #169 merged to `main` (commit `2d98ee77`). 3 commits: `ff8827d8` (docs — M2+ design approved) + `598597a8` (impl — 3 tenant booksets) + `9de99c15` (test fix — 28 pre-existing bUnit failures). Implementation: `Order.OwnerTenantId` (Guid?) + `SetResellerPricing` overload + `SourceDomain` end-to-end (KhachLink→Gateway→CoreHub) + Reseller branch in `GenerateAccountingEntriesAsync` (Supplier 511/3331/632 + Reseller 511/3331/632/1331 + Platform 511 skip-when-VA) + Auditor UI `/admin/reseller-accounting-reconciliation` + PG migration `20260906072702_AddOrderOwnerTenantId` + SQLite migration `20260906072856_AddOrderOwnerTenantId` + 13 R2.2 tests. Pre-existing bUnit fixes: `VasReportPageTestBase` missing `IFinancialReportExportService` mock (24 tests) + `ComponentTestBase` missing `ITenantManagementService` mock (3 tests) + `HKDBookDetailTests` vi-VN number format `"20.000.000"` (1 test). CI: 1506+17+276+41+99 ALL PASS. CD Multi-VPS SUCCESS (Gateway+KhachLink+ShopERP+smoke). RV: Layer 1 API 200+CORS ✅, Layer 3 PG+SQLite migrations applied ✅, Layer 4 auditor page 302 (exists) ✅. Follow-up: configure `PlatformAccountingTenantId` SystemSetting in production (1-time SysAdmin setup). Branch: `main` @ `2d98ee77`.
* **2026-08-27 — CRAWLER POST-DEPLOY FIXES (commit `942467e0` on `main`).** 2 bugs from VPS logs: (1) Only 20 tenants despite maxResults=100 — doanhnghiep.vn API hard-caps at 20 items/page regardless of `limit` param; adapter sent limit=100, got 20, stopped. Fix: paginate via `page=1,2,3...` until MaxResults reached or empty page; deduplicate MSTs across pages via HashSet; log page count. (2) Filters not applied (industry=null, province=null in crawler logs) — Gateway forwarded anonymous object with PascalCase props via `PostAsJsonAsync`; crawler minimal API binds case-insensitively but explicit camelCase is safer. Fix: serialize with `JsonSerializerDefaults.Web` (camelCase) + log forwarded body for debugging. Files: `7_Tooling/VanAn.Crawler/Adapters/RestApiAdapter.cs` + `2_Gateway/Controllers/CrawlController.cs`. Build PASS. CI PASS (1454+17+273+41). CD Multi-VPS SUCCESS. Branch: `main` @ `942467e0`. Pending RV: trigger crawl → verify >20 Pending tenants + filters applied.
* **2026-08-26 — CRAWL-TO-ONBOARD PHASE 6+7+8 COMPLETE + DEPLOYED + RV PASS.** PR #164 merged to `main` (commit `845f19e4` + `9cc83534`). Phase 6: KhachLink UI (ImageUploadController + ImageUploadService + ClaimHttpService + Store.razor Pending banner + Claim.razor form). Phase 7: ShopERP Admin UI (TenantClaimApiClient + TenantManagement 3 tabs + ClaimsQueue + CrawlTrigger + NavMenu). Phase 8: 43 new tests (20 domain + 10 OnboardUnverified service + 13 TenantClaimService) + bug fix (ListPendingClaimsAsync c.Id→c.TenantId) + W12-G7 whitelist ImageUploadController. RV Layer 1 API: pending 200, duplicates 200 (after Pattern #8 fix in `9cc83534`), claims 200, crawl/trigger 202, images/upload 400. RV Layer 5 DB: TenantClaimRequests (17 cols) + CrawlSources (11 cols) + Tenants.PotentialDuplicateOf + Settings_CrawledPhone + Settings_ContactPhone all verified in PG. CI: 1454+17+273+41 ALL PASS. CD Multi-VPS SUCCESS. Branch: `main` @ `9cc83534`.
* **2026-08-25 — CRAWL-TO-ONBOARD TENANT PIPELINE — PLAN COMPLETE.** New objective. Plan file: `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` (600 dòng). Pipeline: crawl trangvangvietnam.com (HTML) + doanhnghiep.vn/xinvoice.vn (REST API) → Pending tenant (read-only, SĐT mask ND13/2023) → Owner claim (GPKD upload) → SysAdmin approve → Active tenant + admin user. 12 design decisions locked (governance exceptions: new `7_Tooling/VanAn.Crawler.csproj` + Domain modification). 8 phases: Domain → Migration → Services → API → Crawler → UI KhachLink → UI Admin → Tests+RV. Research verified against codebase. Legal: trangvangvietnam ToS cấm scraping quy mô lớn → batch nhỏ + polite 3-5s; doanhnghiep.vn API = preferred legal source. Awaiting implementation start.
* **2026-08-25 — ISSUE #103 DATA ISOLATION (FOLLOW-UP).** Commit `73f77f14` on `main`. Root cause: impersonation copied ALL claims + added Owner role but did NOT remove SystemAdmin role → user had BOTH roles → `IsInRole("SystemAdmin")=true` → 9 pages showed cross-tenant data (Orders "ALL tenants" dropdown with `IgnoreQueryFilters()`, Accounting/EInvoice EMPTY default, UserManagement/ShopFeatures tenant selector). Fix: strip SystemAdmin role during impersonation in `Impersonate.cshtml.cs` + `AdminController.cs` (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims). Re-add SystemAdmin role on exit in `ExitImpersonate.cshtml.cs` + `AdminController.ExitImpersonation`. `[AllowAnonymous]` on `exit-impersonation` API endpoint + `wasImpersonating` guard. 2 new tests (AM-S24: `/admin/tenants` denied after impersonation; AM-S25: accessible after exit). 25/25 tests PASS. Pushed, CI PASS (1411+17+273+39), CD deployed. RV 9/9 PASS on `app2.khachvip.online` (login → impersonate → /admin/tenants 302 denied → /orders 200 tenant-scoped → exit → /admin/tenants 200 restored → API backward compat 200+200).
* **2026-08-25 — ISSUE #103 IMPERSONATE BUTTON NOT WORKING.** Commit `c42c4cbe` on `main`. Switched from HttpClient POST to Razor Pages (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) for proper HTTP context handling (Set-Cookie + redirect). Dual role (SystemAdmin + Owner) + `impersonating` marker claim. Global banner in `MainLayout.razor` with exit button. `NavMenu.razor` hides "Hệ thống" menu when impersonating. `AdminLayout.razor` renders Owner menu. `TenantManagement.razor` uses NavigateTo. 23 integration tests PASS (18 original + 5 new Razor Page flow). Pushed, CI PASS, CD deployed. RV ALL PASS.
* **2026-08-23 — ISSUE #156 NAV GROUP + COLLAPSIBLE ALL MENUS.** Commit `4ee64719` on `main`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based grouped), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu desktop sidebar (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên, mobile bottom bar giữ nguyên). Pushed, CI PASS (1411+17+266+39), CD deployed. Comment on #156.
* **2026-08-23 — ISSUE #161 ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** Commit `5c5a07c5` on `main`. Bug 1: Revenue/Expense validation fail — JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on account/category selects. Bug 2: TransactionDate wrong — 3-layer fix (Domain `AccountingEntry` constructor + `CreateRevenue`/`CreateExpense` factory methods add optional `transactionDate` param → `IAccountingService` + `AccountingEntryService` pass-through → `RevenueEntry.razor` + `ExpenseEntry.razor` pass user-entered date). 5 test files updated for Moq `It.IsAny<DateTime?>()`. Also fixed mojibake em dash in `AccountingEntryDto.cs`. Pushed, CI PASS, CD deployed. Comment on #161.
* **2026-08-23 — POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS.** 3 commits on `main` @ `6c9182da`. `e7848be9`: issue #157 (nginx `/_framework/` proxy for Directory domains + Home page no initial tenant load + voice search `@bind:event="oninput"`). `1eeb4615`: nginx WebSocket upgrade headers for `/_blazor` + Leaflet marker icons → unpkg CDN. `6c9182da`: KhachLink Commerce WASM — `ThemeType` enum `JsonStringEnumConverter` in `ShopConfigHttpService` + `TenantProfileHttpService` + missing shortcut icons + SW duplicate `activate` merge (v18 → v19-merge-activate). All pushed, CI PASS, CD deployed. Pending RV.
* **2026-08-23 — DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app (port 8080, 256MB). nginx map-based routing with Docker DNS resolver. 4 runtime fixes: nginx upstream DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum string conversion. CD 4 runs SUCCESS. RV D3-D8 all PASS: 0.04s cached load, 10 stores, Commerce unaffected, 56MiB. Local test PASS. Pre-push CI: 1411 unit + 266 integration + 39 arch ALL PASS.
* **2026-08-21 — FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE.** Branch `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
