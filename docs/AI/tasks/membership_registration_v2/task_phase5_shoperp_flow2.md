# TASK CARD: Phase 5 — ShopERP Luồng 2 (Quản lý thành viên HTX — add/search/nâng cấp CTV)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-03 (P5 `1803698a` — MembershipMembers + admin client)

## 1. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/MembershipMembers.razor` (MỚI) | Trang `/admin/membership/members` (SystemAdmin): **bộ chọn HTX** (dropdown từ `GET /api/membership/htx-profiles`) → **danh sách thành viên** (Member list: số thành viên, tên tenant, loại, vốn góp, trạng thái) + nút **"Thêm thành viên"** → modal: **search tenant** (tên/MST) → chọn → chọn **loại thành viên** (3 card/select) + **số vốn góp cam kết** (bắt buộc Official/LinkedCapital, ẩn LinkedNonCapital) → `POST /api/admin/membership/members` |
| `MembershipMembers.razor` (tiếp) | Nút **"Nâng cấp CTV"** → modal: search cộng tác viên (customer Salesman/Shipper — tên/SĐT) → `POST /api/admin/membership/collaborator-upgrade` → trả tenant mới → tiếp tục "Thêm thành viên" với tenant đó |
| Menu (ShopErpMenuService) | SystemAdmin group += "Thành viên HTX" → `/admin/membership/members` |
| `5_WebApps/ShopERP/Services/MembershipApiClient.cs` | + `ListHtxProfilesAsync()` · `AddMemberAsync(htxTenantId, memberTenantId, membershipType, capitalAmount?)` · `UpgradeCollaboratorAsync(customerId)` · `ListMembersAsync(htxTenantId)` |

## 2. LƯU Ý

- D3/D4/D7: chỉ add tenant **Active**; collaborator phải nâng cấp thành tenant trước (2 bước riêng).
- Member hành động tối thiểu: list + add (Suspend/Terminate tùy chọn — MemberRegistryService có sẵn, thêm sau nếu cần).
- Data ghi PG qua Gateway API — đúng kiến trúc (không sync SQLite mới).
- UI Platform compliance: VanAnDataGrid/VanAnModal/VanASelect/VanAButton.

## 3. ACCEPTANCE

- [ ] Chọn HTX → list thành viên · add tenant (3 loại + vốn đúng) · search tenant hoạt động
- [ ] Nâng cấp CTV → tenant mới (Type null) → add thành viên được
- [ ] ShopERP.Tests component PASS
