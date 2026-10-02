# TASK CARD: Phase 7 — Tests + validation

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING

## 1. TEST LIST

| # | Test | Nơi | Nội dung |
|---|---|---|---|
| M1 | Domain `Tenant.CreateMembershipProfile` | VanAn.Core.Tests | Type=null · Status=Active · BusinessType=HouseholdBusiness |
| M2 | Domain `Member` capital | VanAn.Core.Tests | CapitalContributionAmount snapshot qua CreateActive |
| M3 | Service `AddMemberAsync` | VanAn.Core.Tests | Guard D6 (Official/LinkedCapital thiếu vốn → throw; NonCapital + vốn → throw) · guard D7 (tenant Pending/Suspended/không tồn tại → throw; Active → pass) · HTX chưa có HtxProfile → throw · duplicate (htx, tenant) → throw · thành công trả memberId + memberNumber |
| M4 | Service `CollaboratorTenantProvisioning` | VanAn.Core.Tests | IsCollaborator (Salesman/Shipper active; role khác/không active → false) · GetOrCreate: tạo mới → reuse (idempotent, 1 tenant/customer) · Type null + OwnerCustomerId |
| M5 | Service `HtxProfileService.ListAsync` | VanAn.Core.Tests | Trả list profile + tenant name |
| M6 | API `MembershipAdminController` | VanAn.Core.Tests | SystemAdmin htx-profile (200 → Type=HTX) · members add (200 {memberId,memberNumber}; 409 chưa Active; 400 NonCapital+vốn) · collaborator-upgrade (200 {tenantId}; reuse) · Owner → 403 |
| M7 | Component ShopERP Luồng 1 | VanAn.ShopERP.Tests | Tenants: nút "Chuyển thành HTX" hiện đúng (Active + chưa HTX) · modal render |
| M8 | Component ShopERP Luồng 2 | VanAn.ShopERP.Tests | MembershipMembers: dropdown HTX · list thành viên · modal add (loại + vốn) · modal nâng cấp CTV |

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
