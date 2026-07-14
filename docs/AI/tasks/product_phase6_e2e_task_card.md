# TASK CARD — Phase 6: E2E Tests

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 7)
> **Branch:** `feature/product-mgmt-phase6-e2e`
> **Priority:** 3 (Final validation)
> **Mode:** IMPLEMENT (Playwright ENABLED — Phase 6 là phase duy nhất được dùng Playwright)
> **Prerequisite:** Phase 5 merged (full Product Management + QR + Print hoạt động)

---

## 0. CONTEXT & DECISIONS (locked)

### Test facts (verified 2026-07-14)
- E2E test directory: `6_Testing/e2e-tests/` (master plan reference) — **verify tồn tại trước khi bắt đầu**
- Playwright governance: `.devin/rules/playwright.rules.md` + `.devin/skills/playwright_cost_optimizer.md`
- Playwright DISABLED trong Phase 1-5 (governance). Phase 6 là phase duy nhất Playwright ENABLED.
- Pattern reference: existing specs trong `6_Testing/e2e-tests/` (verify trước khi viết mới)

### User decisions (locked 2026-07-14)
- **G11 — DB migration:** Không cần migration mới cho Phase 6 (test chỉ verify existing schema). Nếu test cần seed data → dùng test fixture, KHÔNG tạo migration production.
- **Test isolation:** Mỗi spec chạy với tenant test riêng (test fixture tạo tenant + cleanup sau test). Tuân thủ `playwright_cost_optimizer.md` — deterministic cost tiers.

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P6-T0 | **Verify prerequisite:** `6_Testing/e2e-tests/` tồn tại + Playwright config + existing specs pattern. Nếu chưa có → STOP, report, ask user. | `6_Testing/e2e-tests/` (inspect) | ⬜ |
| 2 | P6-T1 | Tạo Page Object `ProductManagementPage.ts` — selectors: DataGrid rows, Create button, Create modal fields (Name, Price, Category, VatRate), Submit, Edit button, Delete button, Confirm delete, Reactivate button, QR icon, QR modal, Print button, Checkbox, Batch print button. Methods: `navigate()`, `createProduct(dto)`, `editProduct(id, dto)`, `deleteProduct(id)`, `reactivateProduct(id)`, `openQrModal(id)`, `printQr(id)`, `selectForBatch(ids)`, `batchPrint()`. | `6_Testing/e2e-tests/pages/ProductManagementPage.ts` (NEW) | ⬜ |
| 3 | P6-T2 | Spec `product-crud-flow.spec.ts`: (1) Owner login → navigate `/products` → verify DataGrid load. (2) Create product → verify xuất hiện trong list. (3) Edit product → verify update. (4) Deactivate → verify status badge. (5) Reactivate → verify status. (6) Delete → verify product biến khỏi list. (7) Multi-tenant: tenant B login → KHÔNG thấy product tenant A. | `6_Testing/e2e-tests/product-crud-flow.spec.ts` (NEW) | ⬜ |
| 4 | P6-T3 | Spec `product-qr-print.spec.ts`: (1) Owner login → `/products` → bấm QR icon → verify QR modal mở + QR image render (`<img>` src chứa `/api/products/{id}/qr`). (2) Bấm "In QR code" → verify print dialog trigger (mock `window.print` hoặc verify JS call). (3) Checkbox 3 products → "In QR đã chọn" → verify print layout generate với 3 QR cards. | `6_Testing/e2e-tests/product-qr-print.spec.ts` (NEW) | ⬜ |
| 5 | P6-T4 | Spec `quicksetup-flow.spec.ts`: (1) SystemAdmin login → `/admin/tenants` → bấm "Khởi tạo nhanh" trên 1 tenant. (2) Verify redirect `/quick-setup?tenantId={id}`. (3) Verify page render + template list load. (4) Chọn template → nhập info → Complete Setup. (5) Verify POST `/api/v1/onboarding/shops/{id}/quick-setup` trả 200. (6) Verify success screen hiển thị. | `6_Testing/e2e-tests/quicksetup-flow.spec.ts` (NEW) | ⬜ |
| 6 | P6-T5 | Run all 3 specs + fix flaky issues (max 3 rounds per flaky test per `governance.md` 3-Round Fix Limit). | `6_Testing/` | ⬜ |
| 7 | P6-T6 | Verify: all E2E tests pass, không flaky. | `6_Testing/` | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `ProductManagementPage.ts` page object tồn tại với đầy đủ selectors + methods
- [ ] `product-crud-flow.spec.ts` pass: create → edit → deactivate → reactivate → delete + multi-tenant isolation
- [ ] `product-qr-print.spec.ts` pass: QR modal + print 1 + batch print
- [ ] `quicksetup-flow.spec.ts` pass: SystemAdmin → tenant selection → quick-setup → success
- [ ] Không có flaky test (chạy 3 lần liên tiếp đều pass)
- [ ] E2E coverage: product CRUD, QR view, QR print, batch print, QuickSetup, multi-tenant isolation
- [ ] **RV tests (Section 6): all 55 RV tests MUST pass** — Live Runtime Verification sau khi E2E specs pass

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Bypass Playwright governance (`playwright.rules.md`, `playwright_cost_optimizer.md`)
- ❌ Hardcode tenant/product IDs (dùng test fixture seed + cleanup)
- ❌ Test trên production database (dùng test database hoặc test tenant riêng)
- ❌ Sleep/wait hardcoded (dùng Playwright auto-waiting + `waitForSelector`)
- ❌ Skip multi-tenant isolation test (critical security test)
- ❌ Chạy > 3 rounds fix flaky mà không report (3-Round Fix Limit)
- ❌ Cross-test dependency (mỗi spec độc lập, tự seed data)

---

## 4. ROLLBACK PLAN

Nếu Phase 6 fail sau 3 rounds per spec:
1. Mark failing test as `test.skip` với comment reason + TODO
2. Report per spec: what was tried, evidence, error state
3. **KHÔNG** xóa test files (giữ để debug sau)
4. Ask user decision: skip + debt, continue debugging, hoặc escalate

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build (ensure no regression) — LOCAL
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check — LOCAL
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Run E2E tests — LOCAL (Playwright specs)
cd 6_Testing/e2e-tests
npx playwright test product-crud-flow.spec.ts --reporter=list
npx playwright test product-qr-print.spec.ts --reporter=list
npx playwright test quicksetup-flow.spec.ts --reporter=list
# Expected: all pass

# 4. Flaky check (chạy 3 lần) — LOCAL
for i in 1..3 { npx playwright test --reporter=line }
# Expected: 3/3 pass, không có flaky

# 5. Coverage verify
# - product-crud-flow: create, edit, deactivate, reactivate, delete, multi-tenant
# - product-qr-print: QR modal, print 1, batch print
# - quicksetup-flow: SystemAdmin login, tenant select, quick-setup, success

# 6. Deploy to VPS — merge main → push origin → CD trigger
#    CHỜ 5-10 PHÚT cho CD hoàn tất trước khi RV

# 7. VPS health check — confirm deploy success
curl -sS https://api.khachvip.online/shoperp/api/products | head -c 200
# Expected: 200 OK + JSON product list (KHÔNG 302 → /Login, KHÔNG 500)
# Nếu fail → CD chưa xong, chờ thêm 2-3 phút rồi retry

# 8. RV tests on VPS — see Section 6 (55 RV tests, all MUST pass)
#    Gateway: https://api.khachvip.online
#    ShopERP: https://api.khachvip.online/shoperp
#    KhachLink: https://diemthuong.khachvip.online
```

---

## 6. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)

> Static checks (build + E2E specs + guard-check) KHÔNG đảm bảo runtime works end-to-end.
> Phải test HTTP/UI thực tế trên **VPS khachvip.online** trước khi mark Phase 6 COMPLETE.
> RV tests chạy **SAU** khi tất cả E2E specs pass — đây là tầng verification cuối cùng.

### Deployment flow (CRITICAL)
1. Merge phase branch vào `main` → push origin
2. CI trigger build → CD trigger deploy lên VPS khachvip.online
3. **CHỜ 5-10 PHÚT** để CD hoàn tất (build + transfer + restart containers)
4. Verify deploy thành công: `curl -sS https://api.khachvip.online/shoperp/api/products | head -c 200` → 200 OK với JSON product list (KHÔNG 302 redirect to /Login, KHÔNG 500)
5. **SAU KHI** deploy confirmed → bắt đầu chạy RV tests bên dưới

> ⚠️ **KHÔNG chạy RV tests khi CD đang deploy** — app có thể restart giữa chừng → false negative.
> Nếu RV fail không rõ nguyên nhân → verify lại CD status (re-run step 4) trước khi retry.

**Prerequisites:**
- [ ] CD pipeline đã hoàn tất (chờ 5-10 phút sau merge/push origin main)
- [ ] VPS deploy health check pass: `curl -sS https://api.khachvip.online/shoperp/api/products | head -c 200` → 200 OK (KHÔNG 302 → /Login, KHÔNG 500)
- [ ] Gateway running on `https://api.khachvip.online` (behind reverse proxy)
- [ ] ShopERP running on `https://api.khachvip.online/shoperp` (behind reverse proxy)
- [ ] KhachLink running on `https://diemthuong.khachvip.online` (subdomain, behind reverse proxy)
- [ ] CoreHub running on `https://api.khachvip.online/corehub` (behind reverse proxy)
- [ ] Login Owner trên ShopERP (`https://api.khachvip.online/shoperp`) + Login SystemAdmin trên ShopERP
- [ ] Cloudinary credentials configured trên VPS (env vars trong `.env` hoặc `appsettings.Production.json`) — verify `IImageStorageService` resolve không throw
- [ ] Tenant test đã seed (qua TenantManagement hoặc test fixture) — có ít nhất 2 tenants (A, B) để test multi-tenant isolation
- [ ] Tenant A có ít nhất 3 products (1 active, 1 inactive, 1 deleted) để test filter logic
- [ ] Browser Chrome/Edge (cần `window.print()` support cho QR print test)
- [ ] Phone hoặc QR scanner app (cho QR payload scan test — scan QR hiển thị trên VPS)

**RV tests (all MUST pass):**

### RV Block A — QuickSetup Flow (Phase 1)

- [ ] **RV1 — SystemAdmin tenant selection:** SystemAdmin login → `/admin/tenants` → bấm "Khởi tạo nhanh" trên row tenant → verify redirect `/quick-setup?tenantId={id}` (query string có tenantId)
- [ ] **RV2 — QuickSetup page render:** Page `/quick-setup?tenantId={id}` render KHÔNG crash → `@rendermode InteractiveServer` hoạt động → buttons click được (KHÔNG disabled)
- [ ] **RV3 — QuickSetup authorize:** User KHÔNG phải SystemAdmin vào `/quick-setup?tenantId={id}` → redirect login hoặc 403 (policy `SystemAdminOnly` enforce)
- [ ] **RV4 — Template list load:** QuickSetup page load → `GET /api/v1/onboarding/templates` qua `GatewayClient` → template list hiển thị (KHÔNG spinner vô tận)
- [ ] **RV5 — TemplateType Guid fix:** Chọn template → nhập info → Complete Setup → `POST /api/v1/onboarding/shops/{tenantId}/quick-setup` trả **200 OK** (KHÔNG 400 Bad Request — verify TemplateType là Guid string parse được)
- [ ] **RV6 — Tenant info display:** QuickSetup page hiển thị tên tenant đang setup (KHÔNG blank) — verify tenant info fetch hoạt động
- [ ] **RV7 — Missing tenantId guard:** Vào `/quick-setup` (KHÔNG có `?tenantId=...`) → hiển thị error "Thiếu tenantId" + link về `/admin/tenants` (KHÔNG crash, KHÔNG generate random Guid)
- [ ] **RV8 — IHttpClientFactory DI:** QuickSetup page KHÔNG throw `InvalidOperationException: No service for type HttpClient` — verify `IHttpClientFactory` resolve OK

### RV Block B — Product Domain (Phase 2)

- [ ] **RV9 — Update() audit trail:** Update 1 product qua API → query DB → verify `UpdatedAt` thay đổi (>`CreatedAt` hoặc > timestamp trước update)
- [ ] **RV10 — Deactivate vs MarkAsDeleted:** Deactivate 1 product → query DB → `IsActive=false`, `IsDeleted=false`. Delete 1 product → query DB → `IsDeleted=true` (verify 2 fields separate, KHÔNG đánh đồng)
- [ ] **RV11 — Domain purity:** Build KHÔNG có warning `Microsoft.EntityFrameworkCore` trong `1_Shared/Domain.cs` — verify Update/Deactivate/Activate/MarkAsDeleted KHÔNG import EF Core

### RV Block C — Product CRUD API (Phase 3)

- [ ] **RV12 — IProductService DI:** `POST /api/products` KHÔNG throw `InvalidOperationException: No service for type IProductService` — verify DI register OK trong Program.cs
- [ ] **RV13 — Create product:** `POST /api/products` với `CreateProductRequest` hợp lệ → 201 Created + `ProductDetailDto` trả về + product xuất hiện trong `GET /api/products/manage`
- [ ] **RV14 — Create validation:** `POST /api/products` với `Name=""` → 400 Bad Request (validation DataAnnotations enforce). `Price=-1` → 400. `VatRate=-0.5` → 400.
- [ ] **RV15 — Update product:** `PUT /api/products/{id}` với `UpdateProductRequest` → 200 OK + `GET /api/products/{id}` reflect changes
- [ ] **RV16 — Delete (MarkAsDeleted):** `DELETE /api/products/{id}` → 204 No Content → `GET /api/products/manage` KHÔNG còn product (IsDeleted=true filter) → `GET /api/products` (public catalog) cũng KHÔNG thấy
- [ ] **RV17 — Activate/Deactivate:** `PUT /api/products/{id}/deactivate` → 200 → `GET /api/products/manage` hiển thị `IsActive=false`. `PUT /api/products/{id}/activate` → 200 → `IsActive=true`.
- [ ] **RV18 — Manage endpoint filter:** `GET /api/products/manage` trả về products có `IsActive=false` (inactive) nhưng KHÔNG trả `IsDeleted=true` (deleted) — verify filter logic `!IsDeleted` OK
- [ ] **RV19 — Multi-tenant isolation:** Owner tenant A tạo product → Owner tenant B `GET /api/products/manage` → KHÔNG thấy product của A (verify `TenantId` filter trong `ProductRepository`)
- [ ] **RV20 — Tenant ownership verify:** Owner tenant A `PUT /api/products/{id-of-tenant-B-product}` → 404 Not Found hoặc 403 (KHÔNG update được product của tenant khác — verify `ProductService` check tenant ownership)
- [ ] **RV21 — OwnerOnly policy:** User KHÔNG phải Owner (e.g. StoreKeeper) `POST /api/products` → 403 Forbidden (policy enforce)
- [ ] **RV22 — Cloudinary image upload:** `POST /api/products/{id}/image` với file `.jpg` 1MB → 200 OK + response `{ imageUrl: "https://res.cloudinary.com/..." }` → `GET /api/products/{id}` có `ImageUrl` populated
- [ ] **RV23 — Cloudinary file validation:** `POST /api/products/{id}/image` với file `.exe` → 400 Bad Request (extension reject). File 10MB → 400 (size > 5MB reject).
- [ ] **RV24 — Cloudinary credentials missing:** Nếu Cloudinary config empty → upload returns 500 + log error `Cloudinary config missing` (KHÔNG crash app, graceful error)
- [ ] **RV25 — Existing GET endpoints:** `GET /api/products` (public catalog) + `GET /api/products/{id}` + `GET /api/products/{id}/qr` vẫn hoạt động (KHÔNG regression sau Phase 3 thêm endpoints)

### RV Block D — Product Management UI (Phase 4)

- [ ] **RV26 — Page render:** Owner login → `/products` → page render KHÔNG crash → `@rendermode InteractiveServer` hoạt động → DataGrid hiển thị rows (KHÔNG trống — verify VanAnDataGrid render order fix OK)
- [ ] **RV27 — Authorize:** User KHÔNG phải Owner vào `/products` → redirect login hoặc 403 (policy `OwnerOnly` enforce)
- [ ] **RV28 — DataGrid columns:** DataGrid hiển thị đủ columns: Tên, Category, Price, VAT, Trạng thái, Hành động — verify VanAnDataGrid register columns OK
- [ ] **RV29 — Price format VNĐ:** Price column hiển thị `55.000 ₫` (KHÔNG `55000` raw) — verify shared `CurrencyHelper.FormatVND` hoạt động
- [ ] **RV30 — VanAnButton disabled fix:** Buttons "Tạo sản phẩm", "Sửa", "Xóa" click được (KHÔNG disabled false-positive) — verify `disabled="@(cond ? true : null)"` fix OK
- [ ] **RV31 — Create modal:** Click "Tạo sản phẩm" → VanAnModal mở → VanAForm + VanAnInput hiển thị → nhập info → submit → product mới xuất hiện trong DataGrid (list refresh)
- [ ] **RV32 — Edit modal:** Click "Sửa" trên row → VanAnModal mở → fields pre-fill existing values → đổi Name → submit → DataGrid refresh + Name updated
- [ ] **RV33 — Delete confirm:** Click "Xóa" → confirm dialog (VanAnModal) → confirm → product biến khỏi DataGrid (IsDeleted=true)
- [ ] **RV34 — Reactivate/Deactivate:** Click "Deactivate" trên active product → status badge đổi "Inactive". Click "Activate" trên inactive → badge đổi "Active".
- [ ] **RV35 — Image upload UI:** Edit modal → `<InputFile>` chọn file `.jpg` → upload → preview image hiển thị → save → DataGrid row có image thumbnail (nếu UI hiển thị)
- [ ] **RV36 — Navigation:** NavMenu sidebar có "Sản phẩm" → click → `/products`. AccountingLayout sidebar có "Sản phẩm". Sitemap có card "Sản phẩm" → click → `/products`.
- [ ] **RV37 — UI Platform compliance:** ProductManagement.razor KHÔNG có custom `<table>`, `<form>`, `<button>` HTML — tất cả dùng VanAnDataGrid, VanAForm, VanAnButton, VanAnModal, VanAnInput (verify no Hard Stop violation)
- [ ] **RV38 — Loading state:** `OnInitializedAsync` load products → `_isLoading=true` → spinner hiển thị → load xong → `_isLoading=false` → DataGrid hiển thị (KHÔNG blank page trong lúc load)
- [ ] **RV39 — Error state:** Nếu API fail (e.g. stop ShopERP) → `_errorMessage` hiển thị alert (VanAnAlert) → KHÔNG crash page, KHÔNG blank

### RV Block E — QR Code + Print (Phase 5)

- [ ] **RV40 — QR icon column:** DataGrid có column QR icon (📱 hoặc SVG) → click → QR modal mở
- [ ] **RV41 — QR modal render:** QR modal hiển thị `<img src="/api/products/{id}/qr?tenantId={tid}">` → image render (KHÔNG broken image) + product name + price + shop name
- [ ] **RV42 — Print 1 QR:** Click "In QR code" → `window.print()` trigger → print preview A4 portrait với 1 QR card (QR 200x200px + product name + price + shop name)
- [ ] **RV43 — Batch print checkbox:** Checkbox column đầu DataGrid → tick 3 products → "In QR đã chọn" button enable (KHÔNG disabled khi count > 0)
- [ ] **RV44 — Batch print layout:** Click "In QR đã chọn" → print preview A4 với 3 QR cards (2 cols x 2 rows = 4 slots, 3 filled) — verify grid layout OK
- [ ] **RV45 — Page break > 10 QR:** Tick 12 products → batch print → print preview 2 trang (page 1: 10 QR, page 2: 2 QR) — verify `page-break-inside: avoid` + `page-break-before: always` OK
- [ ] **RV46 — QR scan → AddToCart:** Scan QR bằng phone camera → mở KhachLink URL với JSON payload `{ProductId, ShopId, Timestamp}` → KhachLink `Scan.razor` parse → AddToCart → product xuất hiện trong cart
- [ ] **RV47 — QR payload TableNumber optional:** Generate QR với `tableNumber=null` → scan → payload KHÔNG có field `TableNumber`. Generate với `tableNumber="5"` + `QR_TableNumber_Enabled=true` → payload có `TableNumber: "5"`.
- [ ] **RV48 — Print CSS:** Print preview dùng CSS `@media print` + `@page size A4 portrait` — verify layout KHÔNG bị break khi print (KHÔNG có navigation/sidebar trong print)

### RV Block F — E2E + Build (Phase 6)

- [ ] **RV49 — E2E product-crud-flow pass:** `npx playwright test product-crud-flow.spec.ts` → all tests pass (create → edit → deactivate → reactivate → delete + multi-tenant)
- [ ] **RV50 — E2E product-qr-print pass:** `npx playwright test product-qr-print.spec.ts` → all tests pass (QR modal + print 1 + batch print)
- [ ] **RV51 — E2E quicksetup-flow pass:** `npx playwright test quicksetup-flow.spec.ts` → all tests pass (SystemAdmin → tenant select → quick-setup → success)
- [ ] **RV52 — No flaky:** Chạy 3 lần liên tiếp tất cả E2E specs → 3/3 pass (KHÔNG flaky)
- [ ] **RV53 — Build 0 errors:** `dotnet build VanAn.sln` → 0 errors, 0 warnings (nếu warnings → verify không phải EF Core trong Domain)
- [ ] **RV54 — Guard check pass:** `.\scripts\guard-check.ps1` → PASS
- [ ] **RV55 — No regression existing tests:** Chạy existing test suite (unit + integration) → KHÔNG có test fail mới (verify Phase 1-5 không break existing functionality)
