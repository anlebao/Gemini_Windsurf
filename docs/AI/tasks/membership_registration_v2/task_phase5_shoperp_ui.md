# TASK CARD: Phase 5 — ShopERP UI (review SystemAdmin + cột vốn + link/QR)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING (chưa code)

## 1. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/MembershipApplications.razor` | Cho phép SystemAdmin (dropdown chọn HTX từ `GET /api/membership/htx-profiles` — review mọi HTX; Owner giữ tenant claim) + cột **"Số vốn góp cam kết"** + bộ lọc theo loại thành viên + hiện MemberNumber sau approve |
| `5_WebApps/ShopERP/Components/Pages/Admin/MembershipHtxProfile.razor` | Nút **"Link đăng ký thành viên"** (copy) + **QR code** (IQrCodeService — pattern PrintTicket/ProductManagement) → `https://{khachlink}/membership/register/{htxId}` |
| `5_WebApps/ShopERP/Services/MembershipApiClient.cs` | `ListHtxProfilesAsync()` (SystemAdmin) + DTO capital trong application list |

## 2. LƯU Ý

- Authorize: trang hiện `[Authorize(Policy = "OwnerOnly")]` → cho phép thêm SystemAdmin (policy `OwnerOrSystemAdmin` hoặc xử lý trong code theo role).
- UI Platform compliance: VanAnDataGrid/VanAButton/VanASelect — không custom.
- QR: IQrCodeService (CoreHub) — pattern PrintTicket (server-side QRCoder).

## 3. ACCEPTANCE

- [ ] SystemAdmin chọn HTX → list hồ sơ + approve/reject/request-info hoạt động
- [ ] Cột "Số vốn góp cam kết" hiển thị đúng
- [ ] Link + QR đăng ký thành viên render + copy được
- [ ] ShopERP.Tests component PASS
