# TASK CARD: Phase 7 — RV production + tài liệu

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. RV CHECKLIST

- [ ] **L1 API:** `POST /api/admin/membership/htx-profile` (tenant → Type=HTX + TT71) · `POST /api/admin/membership/members` (3 loại + vốn; chưa Active → 409) · `POST /api/admin/membership/collaborator-upgrade` (tạo/reuse) · `GET /api/membership/htx-profiles` 200
- [ ] **L2 Markers:** binaries có `MembershipAdminController` + `CollaboratorTenantProvisioningService` + `CreateMembershipProfile` (CoreHub) + migration `AddMembershipCapitalContribution` applied (PG `Members.CapitalContributionAmount` exists)
- [ ] **L3/L4 Playwright:** ShopERP SystemAdmin: `/admin/tenants` nút "Chuyển thành HTX" (upload doc tùy chọn) → tenant HTX · `/admin/membership/members` chọn HTX → list + add tenant (3 loại) + nâng cấp CTV
- [ ] **L5 Browser manual:** user thao tác 2 luồng trên production → verify PG (Tenants Type=5 · HtxProfiles · Members row MemberTenantId + vốn)

## 2. DATA/QUY TRÌNH

- Migration `AddMembershipCapitalContribution` chạy khi Gateway deploy (PG).
- Collaborator test: dùng customer có role Salesman/Shipper (production hoặc fixture) — sau RV dọn data test (cleanup pristine).
- Verify: `Members` row (MemberTenantId = tenant, CapitalContributionAmount) + auto-tenant (`Tenants` Type null, OwnerCustomerId = customer).

## 3. TÀI LIỆU

- [ ] `docs/AI/project_state.md` Sections 2/3/4/10
- [ ] Task cards Phase 1-7 → COMPLETE
- [ ] Master plan → DONE
- [ ] Ghi chú legacy: application flow (SRS) giữ nguyên — không xóa
