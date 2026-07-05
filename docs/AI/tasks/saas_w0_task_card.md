# TASK CARD — SaaS W0: Gateway Architecture Fix

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** VAS Stream F complete (W0-W9 merged)
> **Branch:** `feature/saas-w0-gateway-architecture-fix`
> **Estimated sessions:** 1-2
> **Sprint:** 1 (Blockers)

## Objective
Fix Gateway architecture violation: remove DbContext registration, restore pure reverse proxy pattern per governance rules.

## Prerequisites (verify before code)
- [ ] VAS Stream F complete (1114/1114 tests PASS)
- [ ] Verify `2_Gateway/Program.cs:54-58` — AddDbContext registration
- [ ] Verify `6_Tests/VanAn.Integration.Tests/GatewayStartupTests.cs:125-137` — test checks wrong type
- [ ] Verify `6_Tests/VanAn.Integration.Tests/Infrastructure/GatewayWebApplicationFactory.cs:66-80` — factory cheat
- [ ] Grep all services in Gateway that depend on `IVanAnDbContext` (blast radius)

## Problem Statement
**File:** `2_Gateway/Program.cs:54-58`
```csharp
// Register CoreHub DbContext for monolithic architecture (in-process services)
_ = builder.Services.AddDbContext<IVanAnDbContext, VanAnDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**Governance violation:** "Gateway MUST remain pure stateless Reverse Proxy (YARP). NO DbContext, NO EF Core namespaces, NO business logic/services."

**Test cheat:** `GatewayWebApplicationFactory` removes DbContext then replaces with SQLite. Test checks `DbContext` type (not `IVanAnDbContext`), so it passes despite the violation.

## Files to Modify
| File | Changes |
|------|---------|
| `2_Gateway/Program.cs` | REMOVE AddDbContext lines 54-58. Move dependent services to ShopERP or HTTP calls. |
| `2_Gateway/Controllers/VietQrController.cs` | Verify no DbContext dependency (should be HTTP-only) |
| `2_Gateway/Controllers/ApiKeyController.cs` | If depends on IVanAnDbContext → move to ShopERP or use HTTP |
| `6_Tests/VanAn.Integration.Tests/GatewayStartupTests.cs` | FIX test: check `IVanAnDbContext` not `DbContext` |
| `6_Tests/VanAn.Integration.Tests/Infrastructure/GatewayWebApplicationFactory.cs` | REMOVE DbContext cheat (lines 66-80) if no longer needed |

## Detailed Task List

### W0-T1: INVESTIGATE — Blast radius analysis
- Grep `IVanAnDbContext` usage in `2_Gateway/` — which controllers/services need it?
- Grep `AddDbContext` in Gateway Program.cs — confirm exact lines
- Check `ApiKeyController.cs` — does it inject `IVanAnDbContext`? If yes, where is ApiKeyRepository?
- Check `VietQrController.cs` — does it inject anything from CoreHub?
- Output: list of services that need migration to ShopERP or HTTP conversion

### W0-T2: Remove DbContext from Gateway
- Delete `AddDbContext<IVanAnDbContext, VanAnDbContext>` lines (54-58)
- Delete `connectionString` variable if no longer used
- For each dependent service:
  - **Option A (preferred):** Move service to ShopERP, Gateway forwards via YARP
  - **Option B:** Convert to HTTP call from Gateway → ShopERP API
- Verify Gateway only has: YARP config, JWT validation, VietQR controller (HTTP-only)

### W0-T3: Fix GatewayStartupTests
- Change test `Gateway_Architecture_No_DbContext_Registered` to check `IVanAnDbContext`:
```csharp
var dbContextService = sp.GetService<IVanAnDbContext>();
if (dbContextService != null)
    Assert.True(false, "Gateway architecture violation: IVanAnDbContext must NOT be registered");
```
- Remove factory cheat in `GatewayWebApplicationFactory.cs` (lines 66-80) if no DbContext needed

### W0-T4: Build + guard + tests pass
- `dotnet build VanAn.sln` Release — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- `dotnet test` — all existing tests pass (may need to fix Gateway tests)

## Verification
- [ ] `2_Gateway/Program.cs` — no `AddDbContext`, no `UseNpgsql`, no `IVanAnDbContext`
- [ ] `Gateway_Architecture_No_DbContext_Registered` test checks `IVanAnDbContext` (not `DbContext`)
- [ ] Gateway tests PASS without factory DbContext cheat
- [ ] All 1114+ existing tests PASS
- [ ] Build 0 errors, guard pass

## Rollback
- Git revert (restore AddDbContext in Gateway)
- If services moved to ShopERP: revert service registrations
- If tests changed: revert test files

## Open Questions
- Q1: ApiKeyController — move to ShopERP or convert to HTTP call from Gateway?
- Q2: Có service nào trong Gateway thực sự cần DbContext không? (INVESTIGATE sẽ trả lời)
- Q3: Gateway còn controller nào khác cần DbContext ngoài ApiKeyController?
