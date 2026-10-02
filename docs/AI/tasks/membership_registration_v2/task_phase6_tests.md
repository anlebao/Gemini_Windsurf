# TASK CARD: Phase 6 — Tests + validation

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. TEST LIST

| # | Test | Nơi | Nội dung |
|---|---|---|---|
| M1 | Domain `MembershipApplication` capital guard | VanAn.Core.Tests | Submit(): Official/LinkedCapital thiếu amount → throw · NonCapital có amount → factory throw · hợp lệ pass |
| M2 | Domain `Tenant.CreateMembershipProfile` | VanAn.Core.Tests | Type=null · Status=Active · BusinessType=HouseholdBusiness |
| M3 | Service `EnsureVerifiedTenant` | VanAn.Core.Tests | Tenant Pending/Suspended/không tồn tại → throw · Active → pass (Create + Approve) |
| M4 | Service `CollaboratorTenantProvisioning` | VanAn.Core.Tests | IsCollaborator (Salesman/Shipper active, không phải role khác/không active) · GetOrCreate: tạo mới → reuse (idempotent, 1 tenant/customer) |
| M5 | Service `ApproveAsync` auto-provision | VanAn.Core.Tests | Collaborator → Member.MemberTenantId = auto-tenant (OwnerCustomerId = applicant) · non-collaborator → MemberCustomerId · BusinessTenantId → MemberTenantId = tenant đó |
| M6 | API SystemAdmin review | VanAn.Core.Tests (controller) | SystemAdmin approve hồ sơ HTX khác (200) · Owner hồ sơ HTX khác (Forbid) · List với/không htxTenantId |
| M7 | Component KhachLink wizard | VanAn.ShopERP.Tests? (bUnit) | 3 card loại · field vốn hiện/ẩn theo loại · my-applications render |
| M8 | Component ShopERP review | VanAn.ShopERP.Tests | Cột vốn render · SystemAdmin dropdown HTX |

## 2. VALIDATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests
dotnet test 6_Tests\VanAn.ShopERP.Tests
dotnet test 6_Tests\VanAn.Architecture.Tests
powershell -ExecutionPolicy Bypass -File guard-check.ps1
```

## 3. ACCEPTANCE

- [ ] Tất cả test mới PASS + full suites không regress + guard-check PASS
