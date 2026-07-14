# TASK CARD — Phase 5: QR Code View + Print

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 6)
> **Branch:** `feature/product-mgmt-phase5-qr-print`
> **Priority:** 2 (Medium)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 4 merged (ProductManagement.razor tồn tại với DataGrid)

---

## 0. CONTEXT & DECISIONS (locked)

### QR facts (verified 2026-07-14)
- `IShopQrCodeService.GenerateProductQRCode(productId, shopId, tableNumber?) → byte[]` PNG — existing
- Endpoint `GET /api/products/{id:guid}/qr?tenantId={tenantId}&tableNumber={tableNumber?}` — existing, `[AllowAnonymous]` returns PNG
- KhachLink QR scan: `5_WebApps/KhachLink/Pages/Scan.razor` line 97 — đã có logic parse QR payload
- `QRCodePayload` class: `1_Shared/DTOs/QRCodePayload.cs` (namespace `VanAn.Shared.DTOs`)

### User decisions (locked 2026-07-14)
- **G10 — QR payload:** Reconcile inconsistency. Final payload JSON: `{ "ProductId": "...", "ShopId": "...", "Timestamp": "...", "TableNumber": "..." (optional) }`. `TableNumber` chỉ include khi có giá trị (existing behavior trong `ProductsController.GetProductQrCode` line 175-190 — chỉ add khi `QR_TableNumber_Enabled = ON`).
- **Print:** Browser native `window.print()` + CSS `@media print` + `@page size A4`. KHÔNG thêm PDF library.
- **Batch print:** Checkbox column trong DataGrid → chọn nhiều → "In QR đã chọn" → generate print layout.

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P5-T1 | Thêm column "QR" vào DataGrid trong `ProductManagement.razor` — icon QR code (SVG/emoji 📱). `@onclick` mở `_showQrModal=true` + set `_selectedProduct = p`. | `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` | ⬜ |
| 2 | P5-T2 | QR Modal (VanAnModal): hiển thị QR image qua `<img src="@($"/api/products/{_selectedProduct.ProductId}/qr?tenantId={_tenantId}")" />` + product name + price (FormatVND) + shop name (lấy từ tenant info). | same | ⬜ |
| 3 | P5-T3 | QR Modal: nút "In QR code" (VanAnButton) → `IJSRuntime.InvokeVoidAsync("printQrCode", _selectedProduct.ProductId)`. JS function `printQrCode` mở window mới với QR image + product info + gọi `window.print()`. | same | ⬜ |
| 4 | P5-T4 | Thêm `<script>` block trong ProductManagement (hoặc separate JS file `wwwroot/js/qr-print.js` + `<script src>`): function `printQrCode(productId)` — `window.open()` → write HTML với QR img + `window.print()` → `window.close()` sau print. | same (or `5_WebApps/ShopERP/wwwroot/js/qr-print.js` NEW) | ⬜ |
| 5 | P5-T5 | Batch print: thêm checkbox column (Blazor `<InputCheckbox>`) đầu DataGrid. State `HashSet<Guid> _selectedForPrint`. Checkbox `@onclick` toggle product ID vào HashSet. | same | ⬜ |
| 6 | P5-T6 | Batch print button (VanAnButton) "In QR đã chọn" — disabled khi `_selectedForPrint.Count == 0`. `@onclick` → `IJSRuntime.InvokeVoidAsync("printBatchQrCodes", _selectedForPrint.ToArray(), _tenantId)`. | same | ⬜ |
| 7 | P5-T7 | JS function `printBatchQrCodes(productIds, tenantId)` — generate HTML print layout: A4 portrait, 2 columns x 5 rows = 10 QR codes per page, page break giữa trang. Mỗi card: QR img (200x200px) + product name + price + shop name. | same (JS) | ⬜ |
| 8 | P5-T8 | CSS `@media print` + `@page { size: A4 portrait; margin: 10mm; }`. Mỗi QR card: `width: 95mm; height: 95mm; page-break-inside: avoid; border: 1px solid #ccc; padding: 5mm; text-align: center;`. Grid: `display: grid; grid-template-columns: 1fr 1fr; gap: 5mm;`. | same (CSS) | ⬜ |
| 9 | P5-T9 | Print layout: page break sau mỗi 10 QR codes (`@media print { .qr-card:nth-child(10n+1) { page-break-before: always; } }` — first card of each new page). | same (CSS) | ⬜ |
| 10 | P5-T10 | Verify QR payload scan: manual test — scan QR bằng phone camera → mở KhachLink URL với JSON payload → KhachLink `Scan.razor` parse → AddToCart. | Manual test | ⬜ |
| 11 | P5-T11 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass. | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] DataGrid có column "QR" với icon → click mở QR modal
- [ ] QR Modal hiển thị QR image (gọi `GET /api/products/{id}/qr`) + product name + price + shop name
- [ ] Nút "In QR code" → `window.print()` → in 1 QR code trên A4
- [ ] Checkbox column đầu DataGrid → chọn nhiều products
- [ ] Nút "In QR đã chọn" → in batch (nhiều QR codes)
- [ ] Print layout: A4 portrait, 2 cols x 5 rows = 10 QR/page, mỗi card có QR + name + price + shop name
- [ ] Page break giữa các trang (nếu > 10 QR codes)
- [ ] Scan QR bằng phone → JSON payload → KhachLink AddToCart hoạt động (manual verify)
- [ ] Build: 0 errors

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Thêm PDF library (QuestPDF, iTextSharp, etc.) — dùng browser native print
- ❌ Generate QR code client-side (jsPDF + qrcode.js) — dùng existing server endpoint
- ❌ Custom HTML/CSS cho modal (dùng VanAnModal)
- ❌ Hardcode tenantId trong QR URL (lấy từ `ITenantProvider`)
- ❌ Bypass `@attribute [Authorize(Policy = "OwnerOnly")]`
- ❌ Thêm dependency mới (QRCoder đã có — không thêm)
- ❌ Render QR image base64 inline (dùng `<img src="/api/...">` trực tiếp — browser fetch)

---

## 4. ROLLBACK PLAN

Nếu Phase 5 fail sau 3 rounds:
1. Revert `ProductManagement.razor` về commit Phase 4 (giữ CRUD, bỏ QR/print)
2. Revert JS file (nếu tạo mới)
3. Report: error cụ thể, evidence, recommend next step
4. **KHÔNG** sửa `IShopQrCodeService` hoặc `ProductsController.GetProductQrCode` (existing — out of scope)

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Manual smoke test (Owner login)
# - Vào /products → DataGrid có column QR icon
# - Bấm QR icon trên 1 row → modal mở với QR image + product info
# - Bấm "In QR code" → print dialog mở → preview A4 với 1 QR card
# - Tick checkbox vài products → bấm "In QR đã chọn" → print dialog với 2 cols x 5 rows
# - Test > 10 products → verify page break
# - Scan QR bằng phone → verify mở KhachLink → AddToCart

# 4. Print layout verify (Chrome print preview)
# - A4 portrait
# - 2 columns x 5 rows = 10 cards per page
# - Mỗi card: QR 200x200px + product name + price + shop name
# - Page break đúng (không cắt card giữa chừng)
```
