# TASK CARD: Phase 2 — Services (provisioning CTV + AddMemberAsync + ListAsync)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** 🔨 IN-FLIGHT (1 phần code chưa commit — chờ duyệt plan)

## 1. CHANGES

| File | Change |
|---|---|
| `3_CoreHub/Services/Membership/CollaboratorTenantProvisioningService.cs` (MỚI — đã code) | `ICollaboratorTenantProvisioningService`: `IsCollaboratorAsync` (CommunityRoles active Salesman/Shipper, !IsDeleted) · `GetExistingProfileAsync` (Tenant.OwnerCustomerId reuse) · `GetOrCreateProfileAsync` (CreateMembershipProfile + AssignOwnerCustomer, idempotent — D5) |
| `3_CoreHub/Services/Membership/MemberRegistryService.cs` (hoặc service mới) | + **`AddMemberAsync(htxTenantId, memberTenantId, membershipType, capitalContributionAmount?, reviewedByUserId)`** (Luồng 2 — SystemAdmin add trực tiếp): guard D7 (member tenant Active — `EnsureVerifiedTenantAsync`) · guard D6 (Official/LinkedCapital → capital > 0; LinkedNonCapital → null) · guard HTX có HtxProfile · duplicate (UX_Members_HtxTenant) · `Member.CreateActive` + member number |
| `3_CoreHub/Services/Membership/HtxProfileService.cs` + `IHtxProfileService` | + `ListAsync()` (join tenant name — dropdown chọn HTX ShopERP) |

## 2. LƯU Ý

- `EnsureVerifiedTenantAsync` helper (TenantStatus.Active, IgnoreQueryFilters) — dùng chung AddMember.
- Không đụng `MembershipApplicationService` (legacy flow — ngoài scope).
- Member number pattern: `MEM-{htxSlug}-{seq:D6}` (SRS §18 — tái dùng).

## 3. ACCEPTANCE

- [ ] Service tests: AddMember (guard D6/D7, HTX chưa có HtxProfile → throw, duplicate → throw) · provisioning (tạo/reuse/idempotent) · ListAsync
- [ ] Build sln 0 errors
