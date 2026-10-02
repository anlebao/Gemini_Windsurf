# TASK CARD: Phase 3 — API admin (Luồng 1 htx-profile + Luồng 2 members + nâng cấp CTV)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. CHANGES

| File | Change |
|---|---|
| `2_Gateway/Controllers/MembershipAdminController.cs` (MỚI) | **Luồng 1:** `POST /api/admin/membership/htx-profile` [SystemAdmin] `{ htxTenantId, charterVersion?, termsVersion?, charterUrl? }` → `HtxProfileService.GetOrCreateAsync` (charter mặc định "v1.0"; S1 hook: Type=HTX + TT71) · **Luồng 2:** `POST /api/admin/membership/members` [SystemAdmin] `{ htxTenantId, memberTenantId, membershipType, capitalContributionAmount? }` → `AddMemberAsync` → `{ memberId, memberNumber }` · **Nâng cấp CTV:** `POST /api/admin/membership/collaborator-upgrade` [SystemAdmin] `{ customerId, displayName? }` → provisioning → `{ tenantId }` (idempotent) |
| `2_Gateway/Controllers/MembershipProfileController.cs` | `GET /api/membership/htx-profiles` [SystemAdmin] (đã code in-flight — giữ) — bộ chọn HTX |
| Tenant search | Dùng endpoint list tenant hiện có của ShopERP admin (TenantClaimApiClient) — thêm query search tên/MST nếu cần |
| DTOs | `MembershipDtos.cs`: `AddHtxMemberRequest` + `CollaboratorUpgradeRequest` (mới) |

## 2. LƯU Ý

- SystemAdmin-only (cả 2 luồng do SystemAdmin thực hiện — user directive). Owner KHÔNG dùng admin endpoints này.
- KHÔNG đổi `MembershipApplicationsController` / `MembershipReviewer` (application flow legacy — plan §7; **revert** thay đổi in-flight ở đó).
- ReviewedByUserId = sub claim của SystemAdmin.
- Upload tài liệu (Luồng 1): dùng endpoint upload file/ảnh hiện có (charterUrl = URL) — không tạo cơ chế upload mới.

## 3. ACCEPTANCE

- [ ] SystemAdmin: htx-profile tạo hộ (200, tenant → HTX + TT71) · members add (200 `{memberId, memberNumber}`) · collaborator-upgrade (200 `{tenantId}`, reuse) · htx-profiles (200)
- [ ] Owner/gọi thiếu quyền → 403 · tenant chưa Active → 409 · NonCapital + vốn → 400
- [ ] Build sln 0 errors
