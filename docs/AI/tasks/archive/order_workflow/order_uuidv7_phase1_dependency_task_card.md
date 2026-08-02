# TASK CARD — Phase 1: Add UUIDNext Dependency (CPM)

> **Master plan:** `docs/AI/tasks/order_uuidv7_identity_master_plan.md` (Section 2)
> **Branch:** `feature/order-uuidv7-phase1-dependency`
> **Priority:** 0 (Critical — BLOCKING Phase 2+)
> **Mode:** IMPLEMENT
> **Prerequisite:** Master plan approved

---

## 0. CONTEXT & DECISIONS (locked)

### Library facts (verified 2026-07-16)
- **UUIDNext 4.2.4** — published 2026-04-04 (>3 months, pass 7-day governance rule)
- .NET 8.0 compatible + netstandard2.0 compatible
- 381 GitHub stars, 0BSD license, zero dependencies
- API: `Uuid.NewDatabaseFriendly(Database.PostgreSql)` → `Guid` (UUIDv7)
- **Batch-safe:** đảm bảo mỗi UUID > previous ngay cả khi cùng ms (khác .NET 9 `Guid.CreateVersion7()`)
- NuGet API verified: `https://api.nuget.org/v3-flatcontainer/uuidnext/index.json` — 4.2.4 là latest stable

### CPM facts (verified 2026-07-16)
- Repo sử dụng Central Package Management: `Directory.Packages.props` line 3: `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
- Pattern: `<PackageVersion Include="..." Version="..." />` trong `Directory.Packages.props`
- Project reference: `<PackageReference Include="..." />` (KHÔNG có Version attribute — CPM quản lý)

### Projects cần UUIDNext (5 total)
| Project | Lý do |
|---------|-------|
| `1_Shared/VanAn.Shared.csproj` | `Order.Create` lives here (Phase 2) |
| `3_CoreHub/VanAn.CoreHub.csproj` | `OrderService` + `OmnichannelOrderService` (Phase 3) |
| `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | `OrdersController` (Phase 3) |
| `2_Gateway/VanAn.Gateway.csproj` | Future use (DataSyncSubscriber unchanged but project may need reference for consistency) |
| `5_WebApps/KhachLink/VanAn.KhachLink.csproj` | `OfflineOrderDto.ToDomain` calls `Order.Create` (Phase 2 dependency) |

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P1-T1 | Add `<PackageVersion Include="UUIDNext" Version="4.2.4" />` vào `<ItemGroup>` trong CPM (sau line 24 Qdrant.Client hoặc cuối ItemGroup đầu tiên) | `Directory.Packages.props` | ⬜ |
| 2 | P1-T2 | Add `<PackageReference Include="UUIDNext" />` vào `1_Shared/VanAn.Shared.csproj` | `1_Shared/VanAn.Shared.csproj` | ⬜ |
| 3 | P1-T3 | Add `<PackageReference Include="UUIDNext" />` vào `3_CoreHub/VanAn.CoreHub.csproj` | `3_CoreHub/VanAn.CoreHub.csproj` | ⬜ |
| 4 | P1-T4 | Add `<PackageReference Include="UUIDNext" />` vào `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | ⬜ |
| 5 | P1-T5 | Add `<PackageReference Include="UUIDNext" />` vào `2_Gateway/VanAn.Gateway.csproj` | `2_Gateway/VanAn.Gateway.csproj` | ⬜ |
| 6 | P1-T6 | Add `<PackageReference Include="UUIDNext" />` vào `5_WebApps/KhachLink/VanAn.KhachLink.csproj` | `5_WebApps/KhachLink/VanAn.KhachLink.csproj` | ⬜ |
| 7 | P1-T7 | `dotnet restore` — verify tất cả packages resolve, không có NU1xxx warnings | Solution-wide | ⬜ |
| 8 | P1-T8 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `Directory.Packages.props` có `<PackageVersion Include="UUIDNext" Version="4.2.4" />`
- [ ] 5 .csproj files có `<PackageReference Include="UUIDNext" />` (không có Version attribute)
- [ ] `dotnet restore` thành công — không có NU1xxx warnings
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — PASS

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Add `<PackageReference Include="UUIDNext" Version="4.2.4" />` (có Version attribute — CPM quản lý version)
- ❌ Dùng floating range (`Version="*"`, `Version="4.2.4-*"`)
- ❌ Add UUIDNext vào từng .csproj với version hardcoded (phải qua CPM)
- ❌ Add UUIDNext vào test projects (tests không generate UUIDv7 — chỉ verify)
- ❌ Upgrade .NET target framework (giữ net8.0)

---

## 4. ROLLBACK PLAN

Nếu Phase 1 fail sau 3 rounds:
1. Revert `Directory.Packages.props` + 5 .csproj files về commit trước phase
2. `dotnet restore` — verify clean
3. Report: restore error cụ thể, evidence
4. **KHÔNG** workaround bằng cách add UUIDNext source code trực tiếp

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Restore
dotnet restore
# Expected: no NU1xxx warnings, all packages resolved

# 2. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 3. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 4. CPM verification (manual)
# - Mở Directory.Packages.props
# - Verify có <PackageVersion Include="UUIDNext" Version="4.2.4" />
# - Mở 5 .csproj files
# - Verify mỗi file có <PackageReference Include="UUIDNext" /> (no Version attribute)
```
