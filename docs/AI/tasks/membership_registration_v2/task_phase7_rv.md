# TASK CARD: Phase 7 — RV production + tài liệu

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. RV CHECKLIST

- [ ] **L1 API:** tạo hồ sơ 3 loại với góp vốn (Official/LinkedCapital bắt buộc, NonCapital chặn) · tenant chưa Active → 409 · SystemAdmin approve hồ sơ (mọi HTX) · `GET /api/membership/htx-profiles` 200
- [ ] **L2 Markers:** binaries có `MembershipReviewer` (Gateway) + `CollaboratorTenantProvisioningService` (CoreHub) + `CreateMembershipProfile` + migration `AddMembershipCapitalContribution` applied (PG)
- [ ] **L3/L4 Playwright:** tenant HTX: KhachLink `/membership/register/{htxId}` wizard 3 card + góp vốn · submit → my-applications · ShopERP review (SystemAdmin) cột vốn + approve → collaborator auto-tenant + Member
- [ ] **L5 Browser manual:** user tạo HTX → applicant nộp 3 loại → SystemAdmin duyệt → verify collaborator auto-tenant

## 2. DATA/QUY TRÌNH

- Migration `AddMembershipCapitalContribution` chạy khi Gateway deploy (PG).
- Collaborator test: dùng customer có role Salesman/Shipper (production hoặc fixture) — sau RV dọn data test (cleanup pristine).
- Auto-tenant tạo tại Approve — kiểm tra `Tenants` row (Type null, OwnerCustomerId = customer) + `Members` row (MemberTenantId = auto-tenant).

## 3. TÀI LIỆU

- [ ] `docs/AI/project_state.md` Sections 2/3/4/10
- [ ] Task cards Phase 1-7 → COMPLETE
- [ ] Master plan → DONE
