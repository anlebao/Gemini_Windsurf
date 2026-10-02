# TASK CARD: Phase 4 — ShopERP Luồng 1 (Tenant → HTX từ danh sách Tenants)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/Tenants.razor` | Mỗi dòng tenant **Active** thêm nút **"Chuyển thành HTX"** → VanAnModal: upload tài liệu (file doc/pdf/jpg — **tùy chọn**, không bắt buộc) → nút "Xác nhận" → `POST /api/admin/membership/htx-profile` (charterUrl = URL tài liệu nếu có) → success (badge "HTX", menu kế toán TT 71) |
| `5_WebApps/ShopERP/Services/MembershipApiClient.cs` | + `CreateHtxProfileForTenantAsync(htxTenantId, charterVersion?, charterUrl?)` (SystemAdmin) |
| File upload | Dùng endpoint upload hiện có (pattern ImageUploadService — ShopERP có `UploadGpkdAsync` trong KhachLink; kiểm tra endpoint ShopERP tương đương) |

## 2. LƯU Ý

- Chỉ hiện nút cho tenant Active + chưa phải HTX (đã có HtxProfile → ẩn/badge "HTX").
- Tài liệu upload KHÔNG bắt buộc — mặc định tạo profile với charter "v1.0".
- Sau khi thành HTX: hook S1 tự đổi Type=HTX + AccountingStandard=TT71 → menu kế toán/phiếu thu-chi/BCTC đổi theo (đã có từ TT 71 S1-S4).
- UI Platform compliance: VanAButton/VanAnModal/VanACard/VanAAlert.

## 3. ACCEPTANCE

- [ ] Nút hiển thị đúng (Active + chưa HTX) · modal upload tùy chọn · tạo thành công → tenant thành HTX
- [ ] ShopERP.Tests component PASS
