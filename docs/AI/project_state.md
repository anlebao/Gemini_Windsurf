# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.
**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `UI.Platform` (Shared components) — `6_Tests`.
**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

**VPS Access (GCP — for RV + manual deploy):**
- GCP project: `vanan-prod` (gcloud SDK at `C:\Users\lebao\AppData\Local\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd`)
- SSH command pattern: `gcloud compute ssh <INSTANCE_NAME> --zone <ZONE> --project vanan-prod`
- Instances (4): `vanan-gateway` (asia-southeast1-a, 136.85.94.119) · `vanan-shop-a` (asia-southeast1-b, 34.177.89.248) · `vanan-khachlink` (asia-southeast1-c, 136.85.111.51) · `vanan-khachlink-20260815-timlathay-com` (asia-southeast1-c, 136.85.78.51)
- CD: `cd-multivps.yml` (push to `main`) deploys to all 3 VPS + smoke tests. Legacy `cd.yml` (push to `oracle-prod`) deploys to single VPS — SSH broken since 2026-08-06 (GitHub runner IP blocked), use multi-VPS CD only.

---

## 2. Current Objective

**PLATE-AS-METADATA REFACTOR (PHASE 1) + GUARD QR FIXES — COMPLETE + DEPLOYED + RV FULL PASS (L1-L4).** Commits `154faf19` (plate optional) + `d9ebd538` (QR fixes) on `main` + `oracle-prod`. CD Multi-VPS runs `32364470036` + `32383443810` SUCCESS. RV L1-L3 on VPS: migration applied ✅, PlateNumber nullable ✅, all 3 VPS healthy ✅, Guard API 401 (feature enabled) ✅, Guard__QrVerifyEnabled=true ✅, 37 existing sessions intact ✅, 0 error logs ✅. **RV L4 (manual browser) PASS** — user verified: Scan UI photo-first ✅, issue QR without plate ✅, "Kế tiếp" button ✅, PrintTicket shows real tenant name+address+phone ✅, KhachLink Wallet shows short code + QR image ✅.

**Previous: R2 PHOTO CLEANUP SERVICE — COMPLETE + DEPLOYED + RV FULL PASS.** 3 commits: `60972c7c` + `a98e6f7e` (auth scheme fix) + `e7911e23` (RV spec). R2CleanupHostedService running on VPS (retention=30d, interval=24h).

**Source:** User insight — xe vào/xe ra không bắt buộc phải biết biển số dạng text. Photo entry + guard visual compare at exit (already implemented) is the real verifier. Plate text only useful for stats. Making plate optional eliminates OCR blocker: guard issues QR in <2s with photo only, OCR becomes opt-in for stats enrichment.
**Branch:** `main` @ `154faf19` (committed + pushed + deployed)
**Plan:** Inline (this session) — 8-step coding plan reviewed + approved by user before implementation.

**PHASE-1 SCOPE (9 files + 1 migration + tests):**
- Domain (`1_Shared/Domain.cs`): `PlateNumber: string → string?`, constructor removed plate validation, normalizes empty/whitespace → null
- EF Config (`VehicleSessionConfiguration.cs`): `IsRequired(false)`
- Migration (`20260820094830_PlateNumberOptional`): `AlterColumn PlateNumber → nullable: true` (PostgreSQL `character varying(20)`)
- DTOs (`IGuardService.cs`): `IssueRequest` + 5 result records: `string → string?`
- Controller (`GuardController.cs`): Removed `BadRequest("Plate number is required")` validation
- API Client (`GuardApiClient.cs`): `IssueRequestDto` + 3 result DTOs: `string → string?`
- UI ShopERP (`Scan.razor` + `.cs` + `PrintTicket.razor`): `plateNumber` nullable, `canIssue` always true (photo check at issue time), label "(tùy chọn)", placeholder explains skip, null display "(xem ảnh)" / "—"
- UI KhachLink (`Wallet.razor` + `.cs` + `GuardQrApiClient.cs`): `PlateNumber` nullable, display "Vé #<shortCode>" when null
- Tests: `VehicleSessionPlateOptionalTests.cs` (7 new tests) + updated existing `VehicleSessionTests.Create_WithEmptyPlate_Throws` → `Create_WithEmptyPlate_NormalizesToNull`

**TECH DEBT DEFERRED (OCR Hub Sprint 3 — no longer critical path):**
- TD-OCR-01 [MEDIUM] PaddleOCR init hang — no timeout. DEFER: OCR optional, guard can skip
- TD-OCR-02 [LOW] det.onnx dead weight (4.7MB unused). DEFER: no runtime impact
- TD-OCR-03 [LOW] rec.onnx 10.8MB in Git. DEFER: not catastrophic (3.7% of packfile)
- TD-OCR-04 [LOW] ort.min.js from jsDelivr CDN. DEFER: try/catch fallback already covers
- TD-OCR-05 [LOW] No Cache-Control for ONNX. DEFER: ETag cache works

**Remaining:**
1. Commit Phase 1 changes (git add + commit)
2. Push + create PR
3. CD Multi-VPS deploy
4. RV — 10 tests (API issue with null plate, UI scan skip-OCR, verify null display, admin list "—", wallet "Vé #")

> **Previous: GUARD QR VERIFY (ISSUE #126) — ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED. Ready to close.** See history log below.

**OCR HUB R1 — COMPLETE + MERGED + DEPLOYED + RV PASS:**
- Sprint 1 — QR Wallet Merge + OCR Plate Improvements ✅ (PR #149) — `/qr/wallet` 2-tab merge (Vé của tôi + Nhận QR mới), bỏ login requirement, `/qr/claim` redirect, OCR tách 2 hàng PSM 7 + char whitelist
- Sprint 2 — OCR Config Infrastructure + Client Hub ✅ (PR #149) — `IOcrConfigService` + `OcrConfigController` (Gateway) + `OcrConfigApiClient` (ShopERP) + `OcrSettings.razor` admin UI + `ocr-hub.js` client abstraction + `guard-camera.js` refactor
- Sprint 3 — PaddleOCR Integration ✅ (PR #151 + S3-fix `7a38fcb8`) — PaddleOCR ONNX models (det 4.5MB + rec 10.4MB + dict 6623 chars) in `wwwroot/js/lib/ocr/paddle/`, PaddleAdapter in `ocr-hub.js` using ONNX Runtime Web, `.onnx` MIME type fix via `StaticFileOptions`
- **#150 fix** ✅ (`6c67f594`) — QR wallet "Vé không hợp lệ" root cause: `Wallet.razor.cs` `LoadWalletAsync` deserialize localStorage camelCase JSON into PascalCase `WalletSession` with case-sensitive `JsonSerializer` → all fields null. Fix: `PropertyNameCaseInsensitive = true`.
- **#142 comment fix** ✅ (`6c67f594`) — Voice search auto-submit sau 2.5s silence + fill textbox realtime (interimResults=true + `UpdateVoiceTranscript` JSInvokable)
- **QR white screen fix** ✅ (`9f8495e9`) — Root cause: vendored `qrcode.js` (28KB, trimmed) bị corrupt — `QRErrorCorrectionLevel`, `QRRSBlock`, `QRMath` scope issues → `qrcode()` throw "Cannot read properties of undefined" → QR generation NEVER worked. Fix: replace with official qrcode-generator v1.4.4 (56KB, full from jsDelivr) + append `vananQR` interop API. Also switched from `<canvas>` to `<img src="data:image/png;base64,...">` via `generateDataUrl()` to avoid Blazor WASM render timing issues.
- **OCR 2-row plate fix** ✅ (`b07ec9cb`) — `_ocrTwoRows` blind 50% cut → new `_detectRowGap()` using horizontal projection profile to find actual gap between 2 text rows.
- **Sitemap OCR link** ✅ (`061a53dd`) — Added "Thiết lập thư viện OCR" link to `/sitemap` SystemAdmin card (was only in AdminLayout sidebar).

**RV Results (production VPS — diemthuong2.khachvip.online + app2.khachvip.online):**
- L1 (API): Gateway 200, ShopERP 200, KhachLink 200, Guard API 401/200 (auth works), OCR Config API 401/200/204 (CRUD works) — PASS
- L2 (Static): ONNX models 200, ocr-hub.js PaddleAdapter+CTC, voice-note.js fix, qrcode.js official v1.4.4 with generateDataUrl, guard-camera.js _detectRowGap, blazor.boot.json new hash — PASS
- L3 (Playwright): QR Wallet tap vé → QR img 350x350 data URL 6278 chars ✅ PASS; ShortCode vé → "ABC123" displayed ✅ PASS
- L3 (VPS SSH): ShopERP container healthy, Guard business flow (issue→checkout) end-to-end PASS, OCR config saved to PostgreSQL
- L3 (Manual browser): PENDING — PaddleOCR plate scanning + voice search auto-submit (cần user test trên browser có micro)

**R2 (S4 EasyOCR) — DEFERRED:**
- Use case (menu OCR by photo) chưa có tenant F&B yêu cầu thực tế
- EasyOCR model ~1GB RAM khi active → OOM risk trên Gateway VPS e2-small (2GB)
- Tesseract.NET fallback đã có sẵn — đủ cho menu input quy mô nhỏ
- Khi nào làm: upgrade VPS lên 4GB RAM (~$13/tháng) hoặc VPS riêng + có tenant demand

**Remaining:**
1. Manual browser RV PaddleOCR plate scanning (admin switch Tesseract→PaddleOCR → Guard Scan → verify accuracy)
2. Manual browser RV voice search auto-submit (Issue #142 comment — mic → nói → text fill → 2.5s auto-redirect)
3. R2 (S4 EasyOCR) — deferred until user demand + VPS upgrade
4. Issue #130 (Guard QR creation) — still pending VPS RV + close

> **Previous: GUARD QR VERIFY (ISSUE #126) — ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED. Ready to close.** See history log below.

---

## 3. Current Status

- **Branch:** `main` (uncommitted Phase 1 Plate-as-metadata refactor) · **Build:** 0 errors · **Guard-check:** ALL PASSED · **Tests:** 1400/1420 PASS (20 skipped)
- **.NET SDK:** 8.0.422
- **CI/CD:** CI SUCCESS — 1367 unit tests + 251 integration tests + 39 architecture tests ALL PASS (last run on `main`). CD Multi-VPS SUCCESS — all 3 VPS deployed with `60972c7c`. CD (cd.yml) build+push SUCCESS (Gateway image rebuilt with `--no-cache`, pushed to GHCR `latest`).
- **OCR Hub R1 (S1+S2+S3):** ✅ COMPLETE + MERGED + DEPLOYED + RV L1+L2+L3(Playwright+VPS SSH) PASS. PR #149 (S1+S2) + PR #151 (S3) + S3-fix `7a38fcb8` + #150 fix `6c67f594` + QR/OCR fix `b07ec9cb` + Sitemap `061a53dd` + QR img `b5fa411b` + QR root cause `9f8495e9`. QR wallet 2-tab merge + OCR config infra + PaddleOCR ONNX client-side. R2 (S4 EasyOCR) DEFERRED (RAM risk + no demand).
- **QR white screen:** ✅ FIXED + DEPLOYED + PLAYWRIGHT RV PASS (`9f8495e9`). Root cause: vendored qrcode.js (28KB trimmed) corrupt — QRErrorCorrectionLevel/QRRSBlock scope issues → qrcode() never worked. Fix: official qrcode-generator v1.4.4 (56KB) + `<img>` data URL approach. Playwright RV: QR img 350x350, data URL 6278 chars.
- **OCR 2-row plate:** ✅ FIXED + DEPLOYED (`b07ec9cb`). `_detectRowGap()` using horizontal projection profile instead of blind 50% cut.
- **Sitemap OCR link:** ✅ FIXED + DEPLOYED (`061a53dd`). "Thiết lập thư viện OCR" added to `/sitemap` SystemAdmin card.
- **Issue #150 "Vé không hợp lệ":** ✅ FIXED + DEPLOYED + CLOSED (`6c67f594`). Root cause: JSON case mismatch (camelCase localStorage vs PascalCase WalletSession). Fix: `PropertyNameCaseInsensitive = true`.
- **Issue #142 comment (voice search auto-submit):** ✅ FIXED + DEPLOYED (`6c67f594`). Voice search: interimResults=true + 2.5s silence debounce auto-submit + `UpdateVoiceTranscript` realtime textbox fill.
- **KhachLink Multi-Profile R1:** ✅ ALL 6 SPRINTS COMPLETE + MERGED (`5047ed8c`) + ENABLED (`b3af97a1`). timlathay.com LIVE as Directory type (`3d952c75`). Feature flag `KhachLink:MultiProfileEnabled` ON.
- **Dynamic CORS:** ✅ SPRINT 1 COMPLETE + MERGED (`d9545d5e` via PR #133) + DEPLOYED + RV 8/8 PASS. `DynamicCorsService` (Singleton + IMemoryCache) + `DynamicCorsCacheHostedService` (5 min refresh) + `CanonicalizeDomain()` in KhachLinkInstance. No more `Cors__AllowedOrigins__*` env vars. Admin adds domain via `/admin/khachlink-instances` → CORS works within 5 min, no restart.
- **Issue #130 "Guard: không tạo QRcode được":** FIXES APPLIED (5 commits: timeout + circuit reconnect + JS-first photo upload + Gateway CORS proxy + QR compression). Pending VPS RV + close.
- **Issue #126 Guard QR Verify:** ✅ ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED. R1 `ee109800` + R2 `08f8ff60` (PR #128) + R3 `4dd1a0a4` (PR #129). 33 Guard unit tests + 5 integration tests PASS. Ready to close after manual RV.
- **Sprint A+B (previous):** ✅ Hardcoded tenant ID cleanup + Settlement History admin page + NavMenu completeness (30/30). On `main`.
- **GitHub Issues Batch:** ✅ #114 + #123 + #124 + #125 ALL FIXED + DEPLOYED + RV 33/33 PASS on VPS (previous sprint).
- **VALCN v2.0 RV:** 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED (archived)
- **Order Sync:** ✅ FIXED + VERIFIED end-to-end (archived 2026-08-10).
- **GCP VPS (3 instances):** `vanan-gateway` (e2-small 2GB) — Gateway + Nginx + PG + NATS · `vanan-khachlink` (e2-micro) — KhachLink · `vanan-shop-a` (e2-micro) — ShopERP
- **Domains:** `api2.khachvip.online` (Gateway), `app2.khachvip.online` (ShopERP), `diemthuong2.khachvip.online` (KhachLink), `www2.khachvip.online` (main)
- **nginx:** 5-layer rate limit (static/api/auth/blazor/page) — 0 503 in load test (500+ requests)
- **Background Service Toggle:** `/admin/background-services` — 8 services toggleable
- **Loyalty Alliance:** FULLY OPERATIONAL. Tenant in Silo mode — Alliance infrastructure ready.
- **Cloudflare R2:** `vanan-guard-photos` bucket created + verified (Account ID: 18947627801f833aecc202f086d66af5). Used by Guard QR Verify (Sprint 1+) + R2 Cleanup Service (auto-delete photos >30 days post-checkout, runs every 24h via `R2CleanupHostedService`).
- **R2 Cleanup Service:** ✅ COMPLETE + DEPLOYED + RV FULL PASS. 3 commits: `60972c7c` (7 phases — backend + admin UI + tests) + `a98e6f7e` (auth scheme fix) + `e7911e23` (RV spec + state). Sprint 1 (Backend): `IR2StorageService` +3 methods + `IVehicleSessionRepository` +3 methods + `IR2CleanupService` + `R2CleanupService` + `R2CleanupHostedService` + `R2CleanupOptions` + DI + appsettings config. Sprint 2 (Admin UI): `R2StorageController` + `R2StorageApiClient` + `R2StorageAdmin.razor` + nav/sitemap links. Sprint 3 (Tests): 6 unit tests PASS. Auth fix: `[Authorize]` without `AuthenticationSchemes` defaulted to cookie auth → 302 redirect → YARP fallback proxied to KhachLink → HTML 200. Fix: `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` (matching OcrConfigController + GuardController). RV 7/7 PASS: L1 API auth (401 no auth, 200 with JWT), L1.5 authenticated stats 200 JSON (`{"platePhotoCount":0,...}`), L3 admin UI (no login redirect, "Lưu trữ ảnh R2" in nav), L4 sitemap R2 link, L5 `R2CleanupHostedService started: retention=30d, interval=24h` (verified via VPS SSH).
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Option B — Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001

---

## 4. Next Actions

**R2 Cleanup Service — COMPLETE + RV FULL PASS (no further action needed):**
- ✅ 3 commits: `60972c7c` (7 phases) + `a98e6f7e` (auth fix) + `e7911e23` (RV spec + state). All pushed + CD deployed.
- ✅ RV 7/7 PASS: L1 API auth (401 no auth, 200 with JWT), L1.5 authenticated stats 200 JSON, L3 admin UI, L4 sitemap, L5 background service running.
- ✅ Auth fix root cause: `[Authorize]` without `AuthenticationSchemes` → cookie auth → 302 redirect → YARP fallback → HTML 200. Fix: `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme`.
- Future enhancement (if needed): Manual cleanup button test with real R2 objects (current RV uses empty tenant — stats return 0 photos).

**Plate-as-metadata Refactor (Phase 1) — IMPLEMENTED + BUILD + TESTS + GUARD-CHECK PASS (pending commit + PR + CD + RV):**
1. **(Commit + PR)** `git add --all` + commit "feat: Plate-as-metadata refactor — PlateNumber optional, photo+QR primary verifier" + push + PR
2. **(CD Multi-VPS)** Auto-deploy after PR merge
3. **(RV 10 tests)** API issue with null plate → 200 OK; UI scan skip-OCR → QR issued; verify null display "(xem ảnh)"; admin list "—"; wallet "Vé #<shortCode>"; migration applied (information_schema nullable=YES)

**Domain Reseller R1 — COMPLETE + MERGED + DEPLOYED + RV 9/9 PASS:**
- ✅ PR #137 squash-merged `124c65ef` → main. Branch deleted.
- ✅ Phase 1-5: TenantDomain entity + GoDaddy API v1 + DomainRegistrarController (11 endpoints) + admin UI (`/admin/domains`) + auto-link KLI + `init-ssl-tenant-domains.sh` cron.
- ✅ GoDaddy PAT verified 2026-08-17 (read + write + delete all PASS on production `khachvip.online`).
- ✅ Fix: `GodaddyRegistrarService` defer API key check to first use (prevent controller creation failure when env var not set).
- ✅ Fix: `StubDomainRegistrarService` in test factory (fix KLI-1/KLI-2 500→404).
- ✅ Fix: class-level `[Authorize]` on DomainRegistrarController (W12-G7 architecture test).
- ✅ CD: `GODADDY_API_KEY` GitHub secret set + env vars added to docker-compose.gateway.yml + cd-multivps.yml.
- ✅ Fix `c9061a2c`: `DEPLOY_GODADDY_API_KEY` + `DEPLOY_VPS_GATEWAY_HOST` added to `appleboy/ssh-action` `envs:` parameter (root cause: ssh-action only forwards env vars listed in `envs:` to remote script — vars in step `env:` block alone are NOT forwarded). Verified on VPS: container env var `DomainRegistrar__GoDaddy__ApiKey` = 59 chars (was 0), `DOMAIN_REGISTRAR_DEFAULT_VPS_IP=136.85.94.119` (was empty), GoDaddy API availability check returns `{"available":true,...}` for `1999cafe-cuchi-vip.com`.
- ✅ EF migration `AddTenantDomains` auto-applied on Gateway startup (MigrateAsync).
- ✅ RV 9/9 PASS: Gateway /health 200, by-domain 404/200 (no regression), Domains endpoint 401 (deployed), KhachLink WASM 200, ShopERP 302, timlathay.com 200, CI+CD+Accounting all success.
- **R2 (next):** Auto-registration via GoDaddy v3 quote-execute API + Namecheap sandbox backup + FailoverRegistrarService.

**Dynamic CORS — COMPLETE (no further action needed):**
- ✅ Sprint 1 merged via PR #133 (`d9545d5e`), CD deployed, RV 8/8 PASS.
- Future enhancement (if needed): `IDynamicCorsService.InvalidateCache()` for immediate cache invalidation on domain deactivation (currently 5-min TTL acceptable).

**KhachLink Multi-Profile + Issue #130 — Immediate:**

1. **(RV Issue #130 — QR creation)** On VPS: Guard → `/guard/scan` → Issue → capture photos → verify QR generation works (no circuit disconnect, photo upload via JS-first to R2/Gateway proxy, QR compression applied). 5 fix commits applied.
2. **(Close Issue #130)** After RV pass — `gh issue close 130` with summary comment.
3. **(RV timlathay.com Directory instance)** On VPS: `timlathay.com` → verify nav flags (Home + Stores + Profile only, cart/rewards/scan hidden) + "Tìm hiểu" redirect + static content rebrand.
4. **(R2 — Sprint 7 Reseller)** Branch `feature/khachlink-multi-profile-r2` from `main` → implement `ForProfile(Reseller)` preset (all true) + SystemAdmin UI enable + `CommerceMode.Reseller` integration verify + tests → merge → deploy → RV.
5. **(R3 — Sprint 8-9 Logistics + JobMarket)** Branch `feature/khachlink-multi-profile-r3` from `main` after R2 merge → Sprint 8: `ForProfile(Logistics)` + Community Commerce verify + tests → Sprint 9: `ForProfile(JobMarket)` + `/jobs.razor` page (reuse /stores + filter) + tests → merge → deploy → RV.

**Issue #126 Guard QR Verify — Post-close follow-up (ALL releases merged):**

6. **(R2 Manual RV — KhachLink)** On VPS: KhachLink → `/qr/claim` → camera scan + 6-digit code → `/qr/wallet` → fullscreen QR. Verify Channel A + B + C→A migration.
7. **(R1 Manual RV — Demo flow 6 steps)** Login ShopERP as Guard → `/guard/scan` → Issue → capture photos → tạo QR → in vé → Verify → scan QR → Match → checkout → Today tab real stats.
8. **(R1 Manual RV — OCR end-to-end)** Capture plate photo → "Nhận diện biển số" button → verify Tesseract.js OCR prefill (~70-85% accuracy, guard must confirm).
9. **(R1 Manual RV — Feature flag toggle OFF)** Set `GUARD_QR_VERIFY_ENABLED=false` → verify `/guard/scan` returns 503 (flag OFF = endpoint disabled).
10. **(Close Issue #126)** After RV pass — `gh issue close 126` with summary comment.
11. **(E2E Playwright spec — DEFERRED)** `6_Testing/e2e-tests/guard-qr-verify.spec.ts` — full flow (issue → claim → verify → checkout) + Channel C→A migration sub-flow. Not blocking Issue #126 close.

**Deferred / monitoring (from previous sprints):**
8. **(Deploy Sprint A+B)** Deploy `f7201ef4` to VPS via CD pipeline (when ready).
9. **(Browser RV — Settlements page)** Login ShopERP as SystemAdmin → /admin/settlements → verify page renders, filters work, pagination works.
10. **(Browser RV — #114 POS price entry + notes + voice + Kitchen TTS)** Login ShopERP → /pos → verify inline price/name/VAT inputs, voice notes, kitchen TTS.
11. **(Browser RV — #124 redeem button + #125 bottom nav)** KhachLink → /rewards → verify redeem; mobile view → verify bottom nav.
12. **(Post-PoC remaining gaps)** Kitchen-initiated orders (not yet implemented). Native app GPS + attestation limitations (documented, deferred).
13. **(GCP Data Seeding)** Seed production data vào GCP DB (fresh DB chỉ có 3 tenants test).
14. **(#99-3 Phase B APPROVAL)** Alliance VND Normalization — HIGH risk, feature-gated. Awaiting user approval.
15. **(Hybrid Strategy Bước 2 — Monitor)** Trigger khi CPU sustained > 70% / Memory > 80%.
16. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (skipped via `Category!=Flaky` CI filter).
17. **Tech debt cleanup** — TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001.
18. **(VPS Disk Monitoring)** Cân nhắc `docker image prune -af` vào deploy script hoặc cron job.
19. **(v3.0 deferred)** INV-009 (PointValue field), payment provider integration (VNPay/Momo), Ops Cost metric, Tier Distribution.
20. **(nginx deferred task cards)** `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md`

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

**Deployment Modes:** SaaS (`docker-compose.prod.yml` — all on 1 VPS) ‖ Edge (`docker-compose.edge.yml` — Server A: ShopERP+SQLite+NATS, Server B: Gateway+PG+KhachLink).

---

## 6. History Log (compressed — see archive + git log)

* [2026-08-17] **DYNAMIC CORS FROM KHACHLINKINSTANCE REGISTRY — SPRINT 1 COMPLETE + MERGED + DEPLOYED + RV 8/8 PASS.** PR #133 squash-merged `d9545d5e`. Replaced hardcoded `Cors__AllowedOrigins__*` env vars in docker-compose with dynamic lookup from `KhachLinkInstance.CustomDomain` registry. Architecture: `DynamicCorsService` (Singleton + IMemoryCache, sync read-only CORS callback) + `DynamicCorsCacheHostedService` (BackgroundService, pre-warm + 5 min refresh via `IServiceScopeFactory`) + `GetActiveCustomDomainsAsync()` (lightweight query) + `CanonicalizeDomain()` (strip scheme/path/port/slash in KhachLinkInstance constructor). Static origins from `appsettings.Production.json` (`Cors:StaticOrigins`). 4 architecture fixes from review: no `BuildServiceProvider()`, no `.GetAwaiter().GetResult()`, lightweight query, CustomDomain validation. 17 unit + 4 integration tests. RV 8/8 PASS on VPS (incl. "add new domain via admin API → CORS works after 5 min, NO restart"). CD Multi-VPS SUCCESS.
* [2026-08-15] **KHACHLINK MULTI-PROFILE R1 COMPLETE + MERGED + ENABLED + timlathay.com LIVE.** R1 "Multi-Profile Core + Type 1 + 4 + Multi-domain" — all 6 sprints merged via `5047ed8c` + enabled via `b3af97a1` (docker-compose env var `KHACHLINK_MULTIPROFILE_ENABLED`). Sprint 1: `KhachLinkProfile` enum + `KhachLinkNavFlags` VO + `KhachLinkInstance` entity + EF migration (`d99882d5`). Sprint 2: Gateway API 6 endpoints (`41a8994b` + `398610f9`). Sprint 3: KhachLink runtime — NavMenu flag-driven 15 items + KhachLinkLayout refactor (`8ccaa942`). Sprint 4: SystemAdmin `/admin/khachlink-instances` page (`e2d4bece`). Sprint 5: nginx wildcard + SSL SAN expand (`afe84723`). Sprint 6: R1 tests (`50f55e8d`). timlathay.com rebranded as Directory type instance (`3d952c75` + `2e7ef9b0` — static content rebrand + "Tìm hiểu" redirect + blast radius isolation). Feature flag `KhachLink:MultiProfileEnabled` ON. Next: R2 (Reseller) + R3 (Logistics + JobMarket).
* [2026-08-15] **ISSUE #130 "Guard: không tạo QRcode được" — 5 FIX COMMITS APPLIED (pending RV + close).** Root cause: base64 photo over SignalR → circuit disconnect → QR creation fails. Fixes: (1) `4c07753f` timeout + defensive error handling. (2) `119cef2e` Blazor circuit reconnect UI. (3) `7da32cf1` JS-first photo upload — eliminate base64 over SignalR (root cause fix). (4) `cc3abaf0` proxy photo upload through Gateway — fix R2 CORS issue. (5) `2e7ef9b0` QR photo compression + Directory "Tìm hiểu" redirect. Issue #130 still OPEN — pending VPS RV + close.
* [2026-08-15] **GUARD QR VERIFY (ISSUE #126) — ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED.** R1 `ee109800` (Paper Ticket Flow — Sprint 0+1+2+3+5) + R2 `08f8ff60` PR #128 (Digital Claim Flow — Sprint 4: KhachLink `/qr/claim` + `/qr/wallet` + `GuardQrApiClient` + `qr-wallet.js` + nav link) + R3 `4dd1a0a4` PR #129 (Tested + Production Ready — Sprint 6: 15 domain + 18 service + 15 integration tests). 33 unit + 5 integration PASS, 10 skipped (pre-existing JWT factory). E2E Playwright spec DEFERRED. Build 0 errors · guard-check ALL PASSED · Architecture 39/39. Ready to close Issue #126 after manual RV.
* [2026-08-14] **GUARD QR VERIFY (ISSUE #126) — RELEASE R1 COMPLETE (Sprint 0+1+2+3+5).** Branch `feature/guard-qr-r1` (Sprint 0+1) + `feature/guard-qr-r2-sprint2` (Sprint 2) + `feature/guard-qr-r3-sprint3` (Sprint 3) + `feature/guard-qr-r1-sprint5` (Sprint 5). Sprint 0: 6 integration points verified + 8 BR spec + R2 bucket. Sprint 1: Domain entities + EF config + migration + R2 storage + repositories + DI. Sprint 2: `IGuardService` + `GuardService` (9 methods) + `GuardController` (9 endpoints) + QR payload + short code + feature flag. Sprint 3: Deleted hardcode `Scan.cshtml` + Blazor `Scan.razor` (3 tabs) + `GuardApiClient` + `guard-camera.js`. Sprint 5: `PrintTicket.razor` (58mm thermal, auto-print, QR on canvas) + "In vé" button wired. R1 = Channel C (paper ticket) end-to-end. Build 0 errors · guard-check ALL PASSED · Architecture tests 39/39 · Fast test gate PASSED. Next: R2 (Sprint 4 — KhachLink Claim).
* [2026-08-13] **HARDCODED TENANT ID CLEANUP + SETTLEMENT HISTORY UI — SPRINT A+B COMPLETE.** Commit `f7201ef4`. Sprint A: 4 files fixed (ProductReferralConfigService, SocialAuthController, CustomerIdentityController, PermissionGroupManagement) — all hardcoded `Guid.Parse("00000000-...")` replaced with `IConfiguration["Seed:TenantId"]` fallback. Sprint B1: Settlement History admin page (`SettlementAdminController.cs` + `SettlementApiClient.cs` + `Settlements.razor`) + NavMenu completeness (30/30 admin pages have nav links, added Background Services link). Sprint B2: Tenant Settings already covered by TenantManagement edit modal. CI: 1261 unit + 233 integration + 39 architecture tests ALL PASS.
* [2026-08-11] **GITHUB ISSUES BATCH #114/#123/#124/#125 — ALL 4 FIXED + DEPLOYED + RV 33/33 PASS.** 3 commits: `716e7eec` (#123+#124+#125) + `07228b7e` (#114 initial) + `f46f544c` (#114 r1/r2/r3 revisions). RV on VPS: 33 PASS + 0 FAIL + 1 WARN (false positive). All 3 VPS healthy, no regression.
* [2026-08-09] **VALCN v2.0 PLATFORM-LIGHT — ALL 3 WAVES COMPLETE + DEPLOYED + RV PASS.** 7 commits. RV 10 PASS + 1 PARTIAL + 2 FAIL→FIXED. nginx 503 fixed (5-layer rate limit). 3 deferred task cards created.
* [2026-08-09] **GATEWAY REFACTOR HYBRID BƯỚC 1 COMPLETE + DEPLOYED + RV 11/11 PASS.** REQ-1.1 (poll 5s→10s) + REQ-1.2 (6 background service toggles) + REQ-1.3 (logging reduction ~90%).
* [2026-08-03] **TT 99/2025/TT-BTC COMPLIANCE FIXES — 3 WAVES COMPLETE.** 8 gaps fixed. RV 10/10 per wave.
* [2026-08-03] **TENANT MANAGEMENT + ACCOUNTING UI FIXES — 4 PHASES COMPLETE.** RV PASS.
* [2026-08-03] **LOYALTY CONSISTENCY FIX COMPLETE.** RV 37/37. 9 bugs fixed.
* [2026-08-02] **LOYALTY ALLIANCE ALL 7 PHASES COMPLETE.** RV 14/14.
* [2026-07-30] **COMMUNITY COMMERCE SPRINTS 4-7 COMPLETE.** Commerce Mode Toggle + Wallet + COD + Salesman + QR Referral.
* [2026-07-20] **MULTI-VPS OPTION C PHASES 1-7 COMPLETE.** ShopInstance + Order Creator + NATS routed.
* **Older:** See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/dynamic_cors/` | Dynamic CORS master plan + Sprint 1 task card (COMPLETE) |
| `docs/AI/tasks/valcn_v2_platform_light/` | VALCN v2.0 master plan + task cards + RV report |
| `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md` | Deferred nginx improvement task cards |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-24 + 2026-08-03 + 2026-08-09) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
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
- **Verified Facts:** Branch=`main` @ `e7911e23` (R2 Cleanup Service COMPLETE + RV PASS). Build 0 errors. 6 R2Cleanup unit tests PASS. CI: 1367 unit + 251 integration + 39 arch ALL PASS. CD Multi-VPS SUCCESS (all 3 VPS deployed). R2Storage API auth fix verified on VPS: 401 no auth, 200 with JWT (`{"platePhotoCount":0,"customerPhotoCount":0,"totalSizeBytes":0,"oldestPhotoDate":null}`). R2CleanupHostedService running on VPS (retention=30d, interval=24h). Admin UI `/admin/r2-storage` loads (no login redirect, "Lưu trữ ảnh R2" in nav). Sitemap has R2 link. Playwright RV 7/7 PASS. Auth fix root cause: `[Authorize]` without `AuthenticationSchemes` → cookie auth → 302 redirect → YARP fallback → HTML 200. Fix: `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` (matching OcrConfigController + GuardController).
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (20+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-08-20 — PLATE-AS-METADATA REFACTOR (PHASE 1) — COMPLETE + COMMITTED + DEPLOYED (CD Multi-VPS SUCCESS) + RV L1-L3 PASS (L4 pending).** Commit `154faf19` on `main` (pushed + fast-forward merged to `oracle-prod`). CD Multi-VPS run `32364470036` SUCCESS — Build & Push (4m36s) + Pre-Deploy Validation + Deploy to Gateway VPS + Deploy to ShopERP VPS + Deploy to KhachLink VPS + Post-Deploy Smoke Test (all 3 health checks PASS). Legacy `cd.yml` (oracle-prod single-VPS) fail SSH timeout (persistent since 2026-08-06, GitHub runner IP blocked — not code issue, not production path). **RV L1-L3 on VPS (via gcloud SSH):** L1.1 Migration `20260820094830_PlateNumberOptional` applied ✅ · L1.2 `VehicleSessions.PlateNumber` is_nullable=YES ✅ · L1.3 Gateway /health=200 ✅ · L1.4 Guard API GET/POST=401 (auth required, not 503=feature enabled) ✅ · L1.5 Image=`vanan-gateway:latest` ✅ · L1.8 37 existing sessions intact (0 null, 37 with plates — no data loss) ✅ · L1.9 Container up 51 min healthy ✅ · L1.10 No errors in Gateway logs ✅ · L2 ShopERP /health=200, /guard/scan=302 (auth redirect), /login=200 ✅ · L2 KhachLink /health=200, image=latest, up 50 min ✅ · L3 Guard__QrVerifyEnabled=true ✅. **L4 (browser UI flow) PENDING:** Login as Guard → /guard/scan → issue QR without plate → verify PrintTicket shows "(xem ảnh QR)" → verify KhachLink Wallet shows "Vé #<shortCode>". User insight: xe vào/xe ra không bắt buộc phải biết biển số dạng text — photo entry + guard visual compare at exit (already implemented) is the real verifier. Plate text only useful for stats. Making plate optional eliminates OCR blocker: guard issues QR in <2s with photo only, OCR becomes opt-in for stats enrichment. Changes: Domain (`1_Shared/Domain.cs`) `PlateNumber: string → string?` + constructor removed plate validation + normalizes empty/whitespace → null. EF Config (`VehicleSessionConfiguration.cs`) `IsRequired(false)`. Migration `20260820094830_PlateNumberOptional` (PostgreSQL `AlterColumn PlateNumber → nullable: true`). DTOs (`IGuardService.cs`) `IssueRequest` + 5 result records `string → string?`. Controller (`GuardController.cs`) removed `BadRequest("Plate number is required")` validation. API Client (`GuardApiClient.cs`) `IssueRequestDto` + 3 result DTOs `string → string?`. UI ShopERP (`Scan.razor` + `.cs` + `PrintTicket.razor`) `plateNumber` nullable + `canIssue` always true + label "(tùy chọn)" + null display "(xem ảnh)" / "—". UI KhachLink (`Wallet.razor` + `.cs` + `GuardQrApiClient.cs`) `PlateNumber` nullable + display "Vé #<shortCode>" when null. Tests: 7 new `VehicleSessionPlateOptionalTests` + updated `VehicleSessionTests.Create_WithEmptyPlate_Throws` → `Create_WithEmptyPlate_NormalizesToNull`. Build 0 errors · 1400/1420 tests PASS · guard-check ALL PASSED. **Tech debt DEFERRED:** OCR Hub Sprint 3 items (TD-OCR-01 timeout, TD-OCR-02 det.onnx dead weight, TD-OCR-03 rec.onnx in Git, TD-OCR-04 CDN dep, TD-OCR-05 cache headers) — OCR no longer critical path, guard can skip OCR entirely. Benchmark snippets (5 `[BENCH]` console.log wraps added to ocr-hub.js + guard-camera.js) still in code — useful if guard opts to use OCR.
* **2026-08-20 — R2 PHOTO CLEANUP SERVICE — COMPLETE + DEPLOYED + RV FULL PASS.** 3 commits: `60972c7c` (7 phases — backend + admin UI + tests) + `a98e6f7e` (auth scheme fix) + `e7911e23` (RV Playwright spec + state). R2 photos (plate + customer) on Cloudflare R2 had no auto-cleanup → risk of storage exhaustion after ~50,000 vehicle sessions (10GB free tier). Sprint 1 (Backend): `IR2StorageService` +3 methods (ListObjectsByPrefixAsync, DeleteObjectsAsync batch 1000, GetPlatePrefix/GetCustomerPrefix) + `IVehicleSessionRepository` +3 methods (GetExpiredSessionsAsync, GetTenantsWithExpiredSessionsAsync, ClearPhotoKeysAsync) + `IR2CleanupService` + `R2CleanupService` (per-tenant stats, single/all-tenants cleanup) + `R2CleanupHostedService` (background, 24h interval, 30-day retention) + `R2CleanupOptions` + DI + appsettings config. Sprint 2 (Admin UI): `R2StorageController` (GET stats, POST cleanup, SystemAdmin only) + `R2StorageApiClient` + `R2StorageAdmin.razor` + nav/sitemap links. Sprint 3 (Tests): 6 unit tests PASS. CI: 1367 unit + 251 integration + 39 arch ALL PASS. CD Multi-VPS SUCCESS. **Auth fix `a98e6f7e`:** R2StorageController `[Authorize]` without `AuthenticationSchemes` → defaulted to cookie auth (Blazor Server) → 302 redirect to `/login` → YARP fallback-route proxied to KhachLink cluster → HTML 200 instead of 401 JSON. Fix: `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` on all `[Authorize]` attributes (matching OcrConfigController + GuardController). Verified via VPS SSH (`gcloud compute ssh vanan-gateway`): `docker logs vanan-gateway-1` showed `GET /api/r2storage/stats/{guid} -> 302 -> /login?ReturnUrl=... -> fallback-route -> proxied to KhachLink -> 200 text/html`. **RV 7/7 PASS:** L1 API auth (401 no auth, 200 with JWT), L1.5 authenticated stats 200 JSON (`{"platePhotoCount":0,"customerPhotoCount":0,"totalSizeBytes":0,"oldestPhotoDate":null}`), L3 admin UI (`/admin/r2-storage` loads, no login redirect, "Lưu trữ ảnh R2" in nav, Blazor WebSocket connected), L4 sitemap R2 link present, L5 `R2CleanupHostedService started: retention=30d, interval=24h` (verified via VPS SSH). Playwright spec: `6_Testing/e2e-tests/rv-r2-storage.spec.ts`. Plan: `docs/AI/tasks/r2_cleanup/master_plan.md`.
* **2026-08-20 — QR WHITE SCREEN ROOT CAUSE FIX + OCR 2-ROW PLATE FIX + SITEMAP OCR LINK.** 4 commits: `b07ec9cb` (QR canvas→vananQR.generate + OCR _detectRowGap) + `061a53dd` (Sitemap OCR link + QR retry loop) + `b5fa411b` (QR canvas→img data URL) + `9f8495e9` (QR root cause: replace corrupt qrcode.js with official v1.4.4). QR white screen root cause: vendored `qrcode.js` (28KB, trimmed) bị corrupt — `QRErrorCorrectionLevel` defined at line 302 inside IIFE but referenced at line 15 → scope issue → `qrcode()` throw "Cannot read properties of undefined (reading 'M')" → QR generation NEVER worked (both canvas + img approaches failed). Fix: replace with official qrcode-generator v1.4.4 (56KB, full from jsDelivr CDN) + append `vananQR` interop API (generate, download, generateDataUrl). Also switched Wallet.razor from `<canvas>` to `<img src="data:image/png;base64,...">` via `generateDataUrl()` to avoid Blazor WASM render timing issues. OCR 2-row plate fix: `_ocrTwoRows` blind 50% cut → new `_detectRowGap()` using horizontal projection profile (count dark pixels per row, find min in middle 60%) to find actual gap between 2 text rows. Sitemap: added "Thiết lập thư viện OCR" link to `/sitemap` SystemAdmin card. Playwright RV: QR Wallet tap vé → QR img 350x350 data URL 6278 chars ✅ PASS; ShortCode vé → "ABC123" displayed ✅ PASS. CI: 1361 unit + 251 integration + 39 arch ALL PASS. CD Multi-VPS SUCCESS.
* **2026-08-19 — OCR HUB R1 (S1+S2+S3) COMPLETE + MERGED + DEPLOYED + RV PASS + #150 FIX + #142 COMMENT FIX.** 4 commits: PR #149 (S1+S2 squash) + PR #151 (S3 squash) + `7a38fcb8` (S3-fix .onnx MIME type) + `6c67f594` (#150 + #142 comment fix). R1 "Client Phase" — QR Wallet 2-tab merge (`/qr/wallet` with "Vé của tôi" + "Nhận QR mới" tabs, no login required, `/qr/claim` redirect) + OCR plate improvements (2-row ROI split, PSM 7 per row, char whitelist) + OCR config infra (`IOcrConfigService` + `OcrConfigController` + `OcrConfigApiClient` + `OcrSettings.razor` admin UI) + client OCR Hub (`ocr-hub.js` with TesseractAdapter + PaddleAdapter using ONNX Runtime Web, `guard-camera.js` refactor) + PaddleOCR ONNX models (det 4.5MB + rec 10.4MB + dict 6623 chars in `wwwroot/js/lib/ocr/paddle/`). RV: L1 API (Gateway/ShopERP/KhachLink/Guard/OCR Config) PASS, L2 Static (ONNX 200 application/octet-stream, ocr-hub.js PaddleAdapter+CTC, voice-note.js fix) PASS, L3 VPS SSH (Guard issue→checkout end-to-end, OCR config CRUD→PostgreSQL `Ocr:PlateEngine=PaddleOCR` verified then reverted to Tesseract) PASS. #150 root cause: `Wallet.razor.cs` `LoadWalletAsync` deserialize localStorage camelCase JSON into PascalCase `WalletSession` with case-sensitive `JsonSerializer` → all fields null → "Vé không hợp lệ". Fix: `PropertyNameCaseInsensitive = true`. #142 comment: voice search auto-submit sau 2.5s silence + `UpdateVoiceTranscript` realtime textbox fill (interimResults=true). R2 (S4 EasyOCR) DEFERRED — RAM risk on Gateway VPS e2-small (2GB, EasyOCR needs ~1GB active) + no user demand. Plan: `docs/AI/tasks/ocr_hub/master_plan.md`.
* **2026-08-18 — DOMAIN RESELLER R1 ENV VAR FIX `c9061a2c`.** Root cause: `appleboy/ssh-action` only forwards env vars listed in `envs:` parameter to the remote script — env vars set in step `env:` block alone are NOT forwarded to VPS. `DEPLOY_GODADDY_API_KEY` and `DEPLOY_VPS_GATEWAY_HOST` were in `env:` block but missing from `envs:` → `.env.gateway` had `GODADDY_API_KEY=` (empty) + `DOMAIN_REGISTRAR_DEFAULT_VPS_IP=` (empty) → `GodaddyRegistrarService.EnsureConfigured()` threw "ApiKey not configured" on every domain search. Fix: added both to `envs:` line + added `DEPLOY_VPS_GATEWAY_HOST: ${{ secrets.VPS_GATEWAY_HOST }}` to `env:` block. Verified on VPS post-deploy: container env var = 59 chars, `DOMAIN_REGISTRAR_DEFAULT_VPS_IP=136.85.94.119`, GoDaddy API availability check returns `{"available":true,"price":12990000}` for `1999cafe-cuchi-vip.com`. CD run `32100829608` SUCCESS.
* **2026-08-17 — DYNAMIC CORS FROM KHACHLINKINSTANCE REGISTRY — SPRINT 1 COMPLETE + MERGED + DEPLOYED + RV 8/8 PASS.** PR #133 squash-merged `d9545d5e`. Replaced hardcoded `Cors__AllowedOrigins__*` env vars in docker-compose with dynamic lookup from `KhachLinkInstance.CustomDomain` registry. Architecture: `DynamicCorsService` (Singleton + IMemoryCache, sync read-only CORS callback) + `DynamicCorsCacheHostedService` (BackgroundService, pre-warm + 5 min refresh via `IServiceScopeFactory`) + `GetActiveCustomDomainsAsync()` (lightweight query) + `CanonicalizeDomain()` (strip scheme/path/port/slash in KhachLinkInstance constructor). Static origins from `appsettings.Production.json` (`Cors:StaticOrigins`). 4 architecture fixes from review: no `BuildServiceProvider()`, no `.GetAwaiter().GetResult()`, lightweight query, CustomDomain validation. 17 unit + 4 integration tests. RV 8/8 PASS on VPS (incl. "add new domain via admin API → CORS works after 5 min, NO restart"). CD Multi-VPS SUCCESS. Plan: `docs/AI/tasks/dynamic_cors/master_plan.md`.
* **2026-08-15 — KHACHLINK MULTI-PROFILE R1 COMPLETE + MERGED + ENABLED + timlathay.com LIVE.** R1 "Multi-Profile Core + Type 1 + 4 + Multi-domain" — all 6 sprints merged via `5047ed8c` + enabled via `b3af97a1`. Sprint 1 (`d99882d5`): `KhachLinkProfile` enum + `KhachLinkNavFlags` VO + `KhachLinkInstance` entity + EF config + migration + seed. Sprint 2 (`41a8994b` + `398610f9`): Repository + Service + DTOs + `KhachLinkInstanceController` 6 endpoints + DI + feature flag. Sprint 3 (`8ccaa942`): `KhachLinkInstanceHttpService` + KhachLinkLayout refactor + NavMenu flag-driven 15 items + header icons. Sprint 4 (`e2d4bece`): ShopERP `/admin/khachlink-instances` page + `KhachLinkInstanceApiClient` + NavMenu link. Sprint 5 (`afe84723`): nginx wildcard server block + `init-ssl-khachlink-instances.sh` SAN expand + deployment guide. Sprint 6 (`50f55e8d`): R1 tests (domain unit + service integration + API integration). R1 Enable (`b3af97a1`): docker-compose env var `KHACHLINK_MULTIPROFILE_ENABLED` + CD preserve. timlathay.com (`3d952c75` + `2e7ef9b0`): rebrand static content for timlathay.com Directory type + "Tìm hiểu" redirect + blast radius isolation + #130 QR photo compression. Feature flag `KhachLink:MultiProfileEnabled` ON. Next: R2 (Sprint 7 Reseller) + R3 (Sprint 8-9 Logistics + JobMarket).
* **2026-08-15 — ISSUE #130 "Guard: không tạo QRcode được" — 5 FIX COMMITS APPLIED (pending RV + close).** Root cause: base64 photo over SignalR → Blazor circuit disconnect → QR creation fails. Fixes: (1) `4c07753f` timeout + defensive error handling for QR issue flow. (2) `119cef2e` Blazor circuit reconnect UI to App.razor. (3) `7da32cf1` JS-first photo upload — eliminate base64 over SignalR (root cause fix). (4) `cc3abaf0` proxy photo upload through Gateway — fix R2 CORS issue. (5) `2e7ef9b0` QR photo compression + Directory "Tìm hiểu" redirect + blast radius isolation. Issue #130 still OPEN on GitHub — pending VPS RV + `gh issue close 130`.
* **2026-08-15 — GUARD QR VERIFY (ISSUE #126) ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED.** R1 `ee109800` (Paper Ticket Flow — Sprint 0+1+2+3+5) + R2 `08f8ff60` PR #128 (Digital Claim Flow — Sprint 4: KhachLink `/qr/claim` + `/qr/wallet` + `GuardQrApiClient` + `qr-wallet.js` + nav link) + R3 `4dd1a0a4` PR #129 (Tested + Production Ready — Sprint 6: 15 domain + 18 service + 15 integration tests in `VanAn.Core.Tests/Guard/` + `VanAn.Integration.Tests/Guard/`). 33 unit + 5 integration tests PASS, 10 integration skipped (pre-existing JWT factory issue). E2E Playwright spec DEFERRED. Build 0 errors · guard-check ALL PASSED · Architecture 39/39 · Fast test gate PASSED. CD auto-deploy for R3 in progress. Ready to close Issue #126 after manual RV.
* **2026-08-14 — GUARD QR VERIFY (ISSUE #126) RELEASE R1 COMPLETE (Sprint 0+1+2+3+5).** Branches: `feature/guard-qr-r1` (Sprint 0+1) + `feature/guard-qr-r2-sprint2` (Sprint 2) + `feature/guard-qr-r3-sprint3` (Sprint 3) + `feature/guard-qr-r1-sprint5` (Sprint 5) → all merged to `main`. R1 = "Paper Ticket Flow" — Channel C end-to-end: Guard chụp ảnh → tạo QR → in vé giấy → khách giữ giấy → guard quét QR → verify → checkout. Components: Domain entities + EF migration + R2 storage + GuardController (9 endpoints) + GuardService + Blazor Scan.razor (3 tabs) + PrintTicket.razor (58mm thermal) + GuardApiClient + guard-camera.js. Feature flag `Guard:QrVerifyEnabled` default OFF. Build 0 errors · guard-check ALL PASSED · Architecture tests 39/39 · Fast test gate PASSED. Next: R2 (Sprint 4 — KhachLink Claim).
* **2026-08-13 — SPRINT A+B COMPLETE + PUSHED.** Commit `f7201ef4`. Sprint A: 4 hardcoded tenant IDs → config-driven (`IConfiguration["Seed:TenantId"]`). Sprint B1: Settlement History admin page (Gateway `SettlementAdminController` + ShopERP `SettlementApiClient` + `Settlements.razor` + NavMenu link). Sprint B2: Tenant Settings already in TenantManagement edit modal. NavMenu: 30/30 admin pages now have nav links (added Background Services). CI: 1261 unit + 233 integration + 39 arch tests ALL PASS. Guard-check ALL PASSED.
* **2026-08-11 — GITHUB ISSUES BATCH #114/#123/#124/#125 — ALL 4 FIXED + DEPLOYED + RV 33/33 PASS.** 3 commits: `716e7eec` (#123 SQLite IsGlobal migration + #124 redeem button IsAvailable + admin menu + #125 KhachLink bottom nav responsive) + `07228b7e` (#114 initial — IsPosOnly field + Product entity + DTO + EF migration + seed + filter + POS Create.razor) + `f46f544c` (#114 r1/r2/r3 — seed update existing products IsPosOnly=true + Include Items in kitchen query + POS CustomerNotes + voice note STT + Kitchen TTS auto-read). RV on VPS: 33 PASS + 0 FAIL + 1 WARN (false positive — Login page SSR no "blazor" keyword). All 3 VPS healthy, no 502/503/504, no regression. POS-only "Sản phẩm dịch vụ" hidden from public catalog + grouped catalog. JS files served: pos-voice-note.js + tts-reader.js. Global catalog has isAvailable field (#124 verified).
  - **#114 r1.1 root cause:** Product seeded BEFORE IsPosOnly flag added → `if(exists) continue` skipped update → IsPosOnly=false → IsPriceEditable=false → no inline price input.
  - **#114 r1.2 root cause:** `OrderRepository.GetByStatusAsync` used `AsNoTracking()` without `.Include(o => o.Items)` → Items null → kitchen shows empty items.
  - **#114 r2:** Added CustomerNotes textarea + Web Speech API (vi-VN) STT button + `pos-voice-note.js` + `CreateOrderCommand.CustomerNotes` (was missing for POS orders).
  - **#114 r3:** Kitchen Display.razor — "Đọc ghi chú" TTS button on all 3 columns + AutoReadNewOrderNotes (orders <30s old) using existing `tts-reader.js`.
* **2026-08-10 — ORDER SYNC FIX COMPLETE + DEPLOYED + VERIFIED END-TO-END.** 7 commits: `55ece765` (Gateway seed + OrderSyncSubscriber retry) + `6d4bec87` (voice search StoreFinder) + `2c701e94` (test hang fix) + `ffe76c89` (CD .env.gateway + container name) + `e3700af4` (seed auto-create + reassign drifted) + `76378549` (CD env section) + `7bbc26c2` (CD SSH envs list). Root cause: 4-layer CD config gap → SHOP_INSTANCE_ID never reached Gateway VPS → seed fallback to wrong ShopInstance → NATS subject mismatch. RV full test: 8/8 PASS. Order sync verified: ShopInstance `9e94f876-...` auto-created, 10 tenants reassigned, GMV +108,900 exact.
  - **RV Full Test (8 cases):** Login PASS · Feature Flags PASS · Network Dashboard PASS · Background Services PASS · Toggle Flag PASS · Order Sync PASS · Voice Search PASS · Dashboard Metrics PASS.
  - **3 Issues verified:** (1) Order sync mismatch — FIXED. (2) GMV cache 10-min — by design, not a bug. (3) ActiveCustomers=0 — guest checkout CustomerId=null, defer to CRM phase.
* **2026-08-09 — VALCN v2.0 PLATFORM-LIGHT — WAVE 3 COMPLETE + DEPLOYED + RV PASS.** 7 commits: `9a4d0e9b` (W3 code) + `d1e71f21` (CD SSH fix) + `f9f59ef6` (DI fix) + `f0e42a28` (NavMenu + user guide fix) + `33b4c40f` (SQLite migration) + `e7514adc` (nginx 5-layer rate limit) + `bb698f7c` (deferred task cards). RV: 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED. CI/CD SUCCESS. Build: 0 errors.
  - **NavMenu fix:** AdminLayout.razor missing 3 nav entries → added + verified.
  - **SQLite migration:** ShopFeatureSettings.PlatformFeeRate missing in SQLite → migration added → GET+PUT 200.
  - **nginx 503 fix:** Root cause = API + page loads shared rate limit quota. 5-layer strategy: /api/ (zone=api burst=200) + /Login (zone=auth 5r/m) + /_blazor (limit_conn only) + / (zone=web burst=200). Load test: 0 503 across 500+ requests.
  - **3 deferred task cards:** per-user rate limit, Blazor bootstrap, API classification.
* **2026-08-09 — VALCN v2.0 WAVE 3 CODE COMPLETE (Phase 4 + Phase 7).** RefundOrchestrationService (4-step reversal) + NetworkDashboardService (8 metrics). Both feature-flagged, default OFF.
* **2026-08-09 — VALCN v2.0 WAVE 2 COMPLETE (Phase 2 + Phase 3).** Platform Fee + Loyalty Budget. Both feature-flagged, default OFF.
* **2026-08-09 — VALCN v2.0 WAVE 1 COMPLETE (Phase 0 + Phase 1).** 12 additive fields + LoyaltyIssuanceRecord + feature flag infra. All flags default OFF.
* **2026-08-09 — GATEWAY REFACTOR HYBRID BƯỚC 1 COMPLETE + RV 11/11 PASS.** Poll 10s + 6 toggles + logging reduction.
* **2026-08-09 — PROJECT STATE ARCHIVED (reduction 423 → ~190 lines).** Wave 1-3 details, history log, maintenance log moved to archive.
