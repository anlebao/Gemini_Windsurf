# TASK CARD: ADMIN MOBILE PWA (SHOPERP OPS ON PHONE/TABLET)

> Created: 2026-10-30 (user duyệt G1–G7 theo đề xuất mặc định)
> Source SRS: `docs/requirements/van_an_admin_mobile_pwa_srs_v1.md` (v1.0 — APPROVED)
> Master plan: `docs/AI/plans/admin-mobile-pwa-master-plan.md` (ACTIVE — APPROVED 2026-10-30)
> Branch: `main`
> Status: **✅ DONE (2026-10-30 — P0→P4 1 phiên: P0 manifest+icons · P1 SW+offline+pwa.js · P2 role landing · P3 E2E spec · P4 push  + CD SUCCESS + RV L1 files 200 + E2E 3/3 PASS production. L5 manual: user cài app trên điện thoại.)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** ShopERP thành **Admin Mobile PWA** — installable icon trên phone/tablet, role landing tự động, standalone như app native cho nhân viên vận hành.
- **Quyết định đã chốt (G1–G7):** không offline nghiệp vụ (chỉ shell) · app chỉ 7 trang ops · role landing hardcode · 70 admin pages ghi nợ · SW không cache API · POS mất mạng reload (đơn đã save còn) · icon upscale favicon tạm.
- **Constraint cốt lõi:** Blazor Server online-only — SW chỉ cache shell + `offline.html` fallback.
- **Không được:** migration · Domain · project mới · sửa layout desktop · cache API POST.

## 2. ACTIVE WORKFLOW ROUTING

- **Mode:** IMPLEMENT (SRS + master plan approved) — phase isolation theo checkpoint, pattern phiên liên tục như Tra soát.
- **Workflow ref:** `newfeaturebuild.md` (ANALYZE đã qua SRS/plan) · commit sau MỖI phase.
- **Gates:** PWA/UI change → **Gate 4** E2E spec bắt buộc (`6_Testing/e2e-tests/`).

## 3. SCOPE (5 phases — plan §4)

### P0 — Installable shell
- [ ] `5_WebApps/ShopERP/wwwroot/manifest.json` (theo plan §5.1: name/short_name/standalone/any/lang vi/icons 72→512)
- [ ] `App.razor` `<head>`: `<link rel="manifest">` + `theme-color` + `apple-touch-icon` + apple-mobile-web-app-capable
- [ ] `wwwroot/icons/` — bộ icon từ `favicon.png` upscale (G7 — 72/96/128/144/152/192/384/512, purpose any+maskable)
- [ ] **Checkpoint:** build 0 error + guard + commit

### P1 — Service worker + PWA bootstrap
- [ ] Port `KhachLink/wwwroot/js/pwa.js` → `ShopERP/wwwroot/js/pwa.js` (giữ nguyên SRI-deadlock fix + update toast)
- [ ] `wwwroot/service-worker.js` — `shoperp-pwa-v1` · pre-cache shell (`/`, `offline.html`, `app.css`, `_framework/*` essentials, icons) · **network-first** HTML/SignalR (fallback offline.html) · **cache-first** static GET · **exclude** `/api/*` + POST/PUT/DELETE (G5)
- [ ] `wwwroot/offline.html` — "Mất kết nối mạng — chạm để thử lại" + auto-reload `navigator.onLine`
- [ ] `App.razor` `<script src="js/pwa.js">` + `vananPWA.registerServiceWorker()` (pattern `KhachLink/wwwroot/index.html`)
- [ ] **Checkpoint:** build + guard + commit

### P2 — Role landing + ops pages verify
- [ ] Role landing map (hardcode — G3): Masterchef→`/kitchen` · Guard→`/guard/scan` · Staff|StoreKeeper→`/pos` · else→`/sitemap` — tái dùng logic Home.razor hiện có nếu đã map role
- [ ] Verify 7 trang standalone: `/pos` · `/kitchen` · `/guard/scan` · `/orders`+Detail+PrintBill · `/booking/create` · `/inventory/dashboard` · `/inventory/alerts` · `/sitemap`
- [ ] Kiểm: camera getUserMedia (Guard) · SignalR reconnect banner · internal nav không reload
- [ ] **Checkpoint:** build + guard + commit

### P3 — Tests (Gate 4)
- [ ] `6_Testing/e2e-tests/shoperp-admin-pwa.spec.ts` — manifest 200 · SW registered · role landing route · offline.html tồn tại
- [ ] `6_Testing/playwright-rv-shoperp-pwa.config.ts` (pattern `playwright-rv-reconcile.config.ts`)
- [ ] SSR/bUnit nhẹ cho landing map nếu implement trong Home.razor
- [ ] **Checkpoint:** tests pass + guard + commit

### P4 — Push → CD → RV production
- [ ] Push (pre-push CI hoặc FAST PUSH nếu guard+build+test đã pass phiên này)
- [ ] CD Multi-VPS → ShopERP deploy lên `vanan-shop-a`
- [ ] **RV:** L1 `curl https://app2/manifest.json` 200 + `/service-worker.js` 200 · L2 install thật Android (hoặc DevTools manifest check) · L4 T1–T10 manual trên production (theo SRS §7) · L5 user xác nhận
- [ ] Update `docs/AI/project_state.md` + task card status DONE

## 4. PREP NOTES (đọc trước khi code)

- `docs/AI/project_state.md` §2 (objective hiện tại) + `governance.md` (hard stops)
- `5_WebApps/KhachLink/wwwroot/index.html` — cách gọi `vananPWA.registerServiceWorker()`
- `5_WebApps/KhachLink/wwwroot/js/pwa.js` — port nguyên trạng
- `5_WebApps/KhachLink/wwwroot/service-worker.js` — SW logic mẫu
- `5_WebApps/KhachLink/wwwroot/manifest.json` — cấu trúc icons/meta
- `5_WebApps/ShopERP/Components/App.razor` — chỗ chèn link/script (sau viewport :6)
- `5_WebApps/ShopERP/Components/Pages/Home.razor` + `NavMenu` — role map hiện có (Masterchef→`/kitchen` đã có)
- `5_WebApps/ShopERP/wwwroot/favicon.png` — nguồn icon upscale

## 5. NGUYÊN TẮC RÀNG BUỘC (nhắc lại)

- Chỉ sửa `5_WebApps/ShopERP/wwwroot/` + `App.razor` + (nếu cần) landing logic — **không đụng** Gateway/CoreHub/Domain/migration.
- UI Platform components — không HTML tùy ý trong ops pages.
- Secrets: không set `DevToken:Secret`; RV window theo `dev-token-secret.md` nếu cần.
- Playwright: chỉ chạy spec mới (Gate 4) + RV — không diện rộng khi đang code.

## 6. HISTORY

- **2026-10-30:** SRS v1.0 draft → user duyệt G1–G7 theo đề xuất → master plan + task card approved → sẵn sàng khởi động P0.
