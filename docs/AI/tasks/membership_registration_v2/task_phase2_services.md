# TASK CARD: Phase 2 — Services (guard verify + collaborator auto-provision)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** 🔨 IN-FLIGHT (code chưa commit — chờ duyệt plan)

## 1. CHANGES

| File | Change |
|---|---|
| `3_CoreHub/Services/Membership/CollaboratorTenantProvisioningService.cs` (MỚI) | `ICollaboratorTenantProvisioningService`: `IsCollaboratorAsync` (CommunityRoles active Salesman/Shipper, !IsDeleted) · `GetExistingProfileAsync` (Tenant.OwnerCustomerId reuse) · `GetOrCreateProfileAsync` (tạo `CreateMembershipProfile` + `AssignOwnerCustomer` + save, idempotent) |
| `3_CoreHub/Services/Membership/MembershipApplicationService.cs` | inject provisioning service · `CreateApplicationAsync`: pass `CapitalContributionAmount` + guard D6 (BusinessTenantId → tenant Active) · `ApproveAsync`: re-check D6 + auto-provision D3 (BusinessTenantId → tenant; null && collaborator → auto-tenant; null → customer) + pass capital vào Member · helper `EnsureVerifiedTenantAsync` |
| `3_CoreHub/Services/Membership/MemberRegistryService.cs` | `MapToDto` += `CapitalContributionAmount` |

## 2. LƯU Ý

- D3 (plan): auto-provision CHỈ tại Approve (không lúc nộp đơn) — member party = tenant profile auto-tạo.
- Idempotent: 1 tenant / 1 customer (OwnerCustomerId) — dùng chung cho nhiều HTX.
- Guard D6: Domain không query DB → guard ở service (pattern hiện có), Create chặn sớm + Approve re-check.

## 3. ACCEPTANCE

- [ ] Service tests: EnsureVerifiedTenant (Pending/Suspended → throw) · provisioning (tạo mới/reuse/idempotent) · ApproveAsync (collaborator → MemberTenantId auto-tenant; non-collaborator → MemberCustomerId; BusinessTenantId → tenant đó)
- [ ] Build sln 0 errors
