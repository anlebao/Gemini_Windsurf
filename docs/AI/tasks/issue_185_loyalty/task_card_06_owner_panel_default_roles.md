# Task Card 185-6: /community/owner-panel — mặc định show danh sách Salesman + Shipper

> **Status:** DONE 2026-09-23 — default list = active collaborators (Salesman/Shipper); "＋ Thêm cộng tác viên" mở list khách hàng để nâng cấp
> **Priority:** P2
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** 3-4h (có thể cần endpoint mới)

## Problem

`/community/owner-panel` hiện mặc định show "khách hàng đủ điều kiện" (IdentityLevel ≥ Verified + LoyaltyPoints ≥ 1000). Owner muốn mở trang thấy ngay **danh sách Salesman + Shipper đang active** của tenant mình.

## Verify (VERIFIED — `ShopERP/Components/Pages/Community/OwnerPanel.razor`)

- `LoadData` (line 188-207) → `ApiClient.GetEligibleAsync(page, size, includeIneligible: _showAll)` → list eligible customers; role chỉ hiện ở cột "Vai trò hiện tại" + nút +/− per row.
- Không có view/tab nào list riêng CTV active → đúng như issue: chưa có.

## Solution (đề xuất — cần duyệt UI approach)

### Phase A — API
- Kiểm tra `TenantCommunityAdminApiClient` / Gateway `CommunityAdminController` đã có endpoint list-by-role chưa (`eligible?includeIneligible` trả `ExistingRoles` — có thể filter client-side nếu dataset nhỏ, hoặc cần param `role=`/`onlyActive=true` server-side).
- [ ] A1: Nếu chưa có → thêm `GET /api/community/tenant/collaborators?role=Salesman|Shipper` (hoặc `?activeOnly=true`) trả danh sách customer có CommunityRole active thuộc tenant. **Lưu ý:** dùng scalar projection (không materialize PII) — bài học CryptographicException 2026-09-21; phone hiển "***" hoặc masked.
- [ ] A2: Reuse `EligibleCustomerItem` DTO hoặc DTO mới `CollaboratorItem` (CustomerId, FullName, PhoneMasked, Roles[], ActivatedAt).

### Phase B — UI OwnerPanel
- [ ] B1: Thêm tab/section mặc định "Cộng tác viên hiện tại" — 2 nhóm Salesman + Shipper (hoặc 1 bảng cột Vai trò), mỗi row có nút "− gỡ vai trò".
- [ ] B2: Tab thứ hai "Khách hàng đủ điều kiện" = list hiện tại (giữ nguyên flow nâng cấp + confirm bypass).
- [ ] B3: Default tab = "Cộng tác viên hiện tại".

### Phase C — Tests + E2E (Gate 4)
- [ ] C1: Unit test service query (active role filter, tenant scoping, PII không materialize).
- [ ] C2: E2E spec — owner login → owner-panel → tab mặc định hiện danh sách CTV; deactivate → row biến mất.

## Acceptance
- [ ] Mở owner-panel thấy ngay Salesman + Shipper đang active của tenant
- [ ] Không 500 trên customer có legacy PII (scalar projection)
- [ ] E2E spec pass
