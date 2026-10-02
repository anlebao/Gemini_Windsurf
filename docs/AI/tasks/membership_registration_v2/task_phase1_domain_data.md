# TASK CARD: Phase 1 — Domain/Data (góp vốn + Tenant.CreateMembershipProfile + migration)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** 🔨 IN-FLIGHT (code chưa commit — chờ duyệt plan)

## 1. CHANGES

| File | Change |
|---|---|
| `1_Shared/.../MembershipAggregate/MembershipApplication.cs` | + `CapitalContributionAmount` (decimal?) — factory param + guard (LinkedNonCapital + amount ≠ null → throw) + `Submit()` guard (Official/LinkedCapital + amount null/≤0 → throw) |
| `1_Shared/.../MembershipAggregate/Member.cs` | + `CapitalContributionAmount` (decimal?) — factory param `capitalContributionAmount` (snapshot từ application) |
| `1_Shared/.../TenantAggregate/Tenant.cs` | + factory `CreateMembershipProfile(TenantId, name, settings?)`: Type=null · Status=Active · BusinessType=HouseholdBusiness · TenantCreatedEvent |
| `3_CoreHub/Infrastructure/Configurations/MembershipApplicationConfiguration.cs` | + `CapitalContributionAmount` (precision 18,2) |
| `3_CoreHub/Infrastructure/Configurations/MemberConfiguration.cs` | + `CapitalContributionAmount` (precision 18,2) |
| `3_CoreHub/Infrastructure/Migrations/20261002144144_AddMembershipCapitalContribution` | PG migration (numeric(18,2) ×2) — **đã tạo** |

## 2. LƯU Ý

- D5 (plan): bắt buộc góp vốn > 0 cho Official/LinkedCapital — enforce tại `Submit()` (Draft vẫn lưu được thiếu — assisted registration).
- `CreateMembershipProfile`: Type=null là cố ý (D4 — "không loại hình, chỉ phục vụ membership"); OwnerCustomerId gán bởi service (`AssignOwnerCustomer`).
- Migration chạy ở Gateway startup (PG — VanAnDbContext).

## 3. ACCEPTANCE

- [ ] Domain tests: Submit capital guard 3 loại · factory NonCapital + amount throw · CreateMembershipProfile (Type null, Active)
- [ ] Build sln 0 errors
