# TASK CARD: Phase 1 — Domain/Data (Member góp vốn + Tenant.CreateMembershipProfile + migration)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-03 (P1 `8e7a8149` — Member góp vốn + CreateMembershipProfile + migration)

## 1. CHANGES

| File | Change |
|---|---|
| `1_Shared/.../MembershipAggregate/Member.cs` | + `CapitalContributionAmount` (decimal?) — factory param trên `CreateActive` (snapshot khi add — D6) |
| `1_Shared/.../TenantAggregate/Tenant.cs` | + factory `CreateMembershipProfile(TenantId, name, settings?)`: Type=null · Status=Active · BusinessType=HouseholdBusiness · TenantCreatedEvent (D5) |
| `3_CoreHub/Infrastructure/Configurations/MemberConfiguration.cs` | + `CapitalContributionAmount` (precision 18,2) |
| `3_CoreHub/Infrastructure/Migrations/AddMembershipCapitalContribution` | PG migration (numeric(18,2) — **chỉ Members**) |

## 2. LƯU Ý

- KHÔNG thêm `CapitalContributionAmount` vào `MembershipApplication` (application flow = legacy — ngoài scope, plan §7).
- `CreateMembershipProfile`: Type=null là cố ý (D5); OwnerCustomerId gán bởi service (`AssignOwnerCustomer`).
- Migration chạy ở Gateway startup (PG — VanAnDbContext).

## 3. ACCEPTANCE

- [ ] Domain tests: `CreateMembershipProfile` (Type null, Active) · Member capital snapshot
- [ ] Build sln 0 errors
