# Loyalty Alliance System — Master Plan

## Spec Reference
`docs/specs/loyalty-alliance-spec.md` (v1.0 — 5 decisions resolved)

## Architecture Summary
- **PostgreSQL (Gateway)**: `LoyaltyGlobalConfig`, `LoyaltyTenantConfig`, `AllianceWallet`, `AllianceTransaction`
- **SQLite (ShopERP)**: existing `LoyaltyRewards`, `RedemptionCatalogItem`, `RedemptionRecord`, `Voucher` — unchanged
- **NATS**: `vanan.cloud.loyalty.changed.{customerDeviceId}` — sync wallet → local SQLite
- **Mode routing**: `LoyaltyModeResolver.GetEffectiveModeAsync(tenantId)` → Silo (SQLite) | Alliance (PG)

## Phase Dependency
```
Phase 1 (Domain+Infra) → Phase 2 (Services) → Phase 3 (API) → Phase 4 (Mode Switch) → Phase 5 (UI) → Phase 6 (Testing)
```

## Phase Summary

| Phase | Sessions | Boundary | Output |
|-------|----------|----------|--------|
| 1 | 2 | 1_Shared/Domain.cs + 3_CoreHub/Infrastructure | 4 entities + 4 EF configs + migration + DI |
| 2 | 3 | 3_CoreHub/Services + 1_Shared/Services | AllianceWalletService + LoyaltyModeResolver + modify OrderWorkflow + modify Redemption |
| 3 | 2 | 2_Gateway/Controllers + 5_WebApps/ShopERP/Controllers | 6 new endpoints + 2 modified endpoints |
| 4 | 1 | 3_CoreHub/Services (migration logic) | Silo↔Alliance switch + wallet consolidate/split |
| 5 | 2 | 5_WebApps/ShopERP/Pages + 5_WebApps/KhachLink | Admin config panel + customer wallet/redeem UI |
| 6 | 2 | 6_Tests | Unit + integration + E2E |
| 7 | 1 | VPS (SSH + API) | Runtime verification on production VPS |

**Total: ~13 sessions**

---

## Session Boundaries (JIT Plan format)

### Session 1 — Phase 1A: Domain Entities
**Boundary**: `1_Shared/Domain.cs` only
**Files to edit**: `1_Shared/Domain.cs`
**Create**: None
**Output**: `LoyaltyMode` enum, `TransactionType` enum, `LoyaltyGlobalConfig`, `LoyaltyTenantConfig`, `AllianceWallet`, `AllianceTransaction` entities

### Session 2 — Phase 1B: EF Configs + Migration + DI
**Boundary**: `3_CoreHub/Infrastructure/Configurations/`, `3_CoreHub/Infrastructure/VanAnDbContext.cs`, `3_CoreHub/Infrastructure/IVanAnDbContext.cs`, `2_Gateway/Program.cs` (DI)
**Files to edit**: `IVanAnDbContext.cs`, `VanAnDbContext.cs`, `Program.cs` (Gateway)
**Files to create**: `LoyaltyGlobalConfigConfiguration.cs`, `LoyaltyTenantConfigConfiguration.cs`, `AllianceWalletConfiguration.cs`, `AllianceTransactionConfiguration.cs`
**Output**: DbSet registrations + EF configs + PG migration + DI registration

### Session 3 — Phase 2A: LoyaltyModeResolver + AllianceWalletService
**Boundary**: `3_CoreHub/Services/`, `1_Shared/Services/`
**Files to create**: `ILoyaltyModeResolver.cs`, `LoyaltyModeResolver.cs`, `IAllianceWalletService.cs`, `AllianceWalletService.cs`
**Output**: Mode resolution logic + wallet CRUD + transaction logging

### Session 4 — Phase 2B: Modify OrderWorkflowService (EARN branch)
**Boundary**: `3_CoreHub/Services/OrderWorkflowService.cs`, `3_CoreHub/Services/LoyaltyRewardsService.cs`
**Files to edit**: `OrderWorkflowService.cs` (inject `ILoyaltyModeResolver` + `IAllianceWalletService`, branch `ProcessLoyaltyPointsAsync`)
**Output**: EARN flow routes to Alliance wallet when mode=Alliance

### Session 5 — Phase 2C: Modify RedemptionService (REDEEM branch) + NATS sync
**Boundary**: `3_CoreHub/Services/RedemptionService.cs`, `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` (or new `LoyaltySyncSubscriber`)
**Files to edit**: `RedemptionService.cs` (inject mode resolver + wallet service, branch `RedeemAsync`)
**Files to create**: `LoyaltySyncSubscriber.cs` (ShopERP — listens `vanan.cloud.loyalty.changed.*`, updates local `LoyaltyRewards.PointBalance`)
**Output**: REDEEM flow routes to Alliance wallet + NATS sync subscriber

### Session 6 — Phase 3A: SystemAdmin API (Gateway)
**Boundary**: `2_Gateway/Controllers/`
**Files to create**: `LoyaltyConfigController.cs` (global config CRUD + per-tenant config CRUD)
**Output**: 4 endpoints — GET/PUT global config, GET/PUT per-tenant config

### Session 7 — Phase 3B: Customer API (Gateway + ShopERP forward)
**Boundary**: `2_Gateway/Controllers/LoyaltyController.cs`, `2_Gateway/Controllers/RedemptionController.cs`, `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`, `5_WebApps/ShopERP/Controllers/RedemptionController.cs`
**Files to edit**: `LoyaltyController.cs` (add `GET /api/loyalty/wallet`), `RedemptionController.cs` (add `tenantId` param to redeem)
**Output**: Wallet endpoint + cross-tenant redeem endpoint

### Session 8 — Phase 4: Mode Switch Migration
**Boundary**: `3_CoreHub/Services/AllianceWalletService.cs` (add migration methods)
**Files to edit**: `AllianceWalletService.cs` (add `ConsolidateWalletsAsync` + `SplitWalletsAsync`)
**Output**: Silo→Alliance consolidation + Alliance→Silo split-by-source

### Session 9 — Phase 5A: Admin UI (ShopERP Blazor)
**Boundary**: `5_WebApps/ShopERP/Pages/Admin/`
**Files to create**: `LoyaltyConfigAdmin.razor` (global mode + per-tenant mode + maxWalletPoints)
**Output**: SystemAdmin config panel

### Session 10 — Phase 5B: Customer UI (KhachLink)
**Boundary**: `5_WebApps/KhachLink/Pages/` or `5_WebApps/KhachLink/Components/`
**Files to create/edit**: Wallet view page (breakdown by tenant + recent transactions), cross-tenant redeem UI
**Output**: Customer wallet + redeem UI

### Session 11 — Phase 6A: Unit + Integration Tests
**Boundary**: `6_Tests/VanAn.Core.Tests/`, `6_Tests/VanAn.Unit.Tests/`
**Files to create**: `LoyaltyModeResolverTests.cs`, `AllianceWalletServiceTests.cs`, `ModeSwitchMigrationTests.cs`
**Output**: Mode resolution + wallet operations + split-by-source + maxWalletPoints enforcement

### Session 12 — Phase 6B: E2E Tests
**Boundary**: `6_Testing/e2e-tests/`
**Files to create**: `loyalty-alliance.spec.ts` (earn at tenant A → redeem at tenant B → verify wallet balance)
**Output**: Cross-tenant E2E validation

### Session 13 — Phase 7: VPS Runtime Verification
**Boundary**: VPS via SSH (`ssh -i C:\VibeCoding\CD\SSH\vanan.pem`), API endpoints (`https://api.khachvip.online`, `https://khachvip.online`)
**Prerequisite**: All code committed + pushed + CD pipeline completed + containers restarted on VPS
**Files to edit**: None (runtime testing only)
**Output**: Verified loyalty alliance system working end-to-end on production VPS

**Verification checklist:**
1. PG migration applied — `docker exec` into Gateway container, query `"LoyaltyGlobalConfigs"`, `"AllianceWallets"` tables exist
2. SystemAdmin login → `PUT /api/platform/loyalty/config` → set mode=Alliance
3. Per-tenant config → `PUT /api/platform/loyalty/tenant/{tenantId}/config` → set IsAllianceMember=true
4. Customer places order at tenant A → `PUT /api/orderworkflow/{orderId}/status` → completed
5. Check `AllianceWallets` table → `TotalPointBalance > 0`
6. Check `AllianceTransactions` → EARN record exists
7. Customer `GET /api/loyalty/wallet` → returns balance + breakdown
8. Customer redeems at tenant B → `POST /api/redemption/redeem` with tenantId=B
9. Check voucher created in tenant B's SQLite
10. Check `AllianceWallets.TotalPointBalance` decreased
11. NATS sync → check tenant B's SQLite `LoyaltyRewards.PointBalance` updated
12. Admin cancels voucher → refund to tenant B's `LoyaltyRewards`
13. Revert: set mode=Silo → verify split migration works
14. Docker logs check: `docker logs gateway` + `docker logs shoperp` — no errors

**Acceptance**: All 14 steps pass on VPS, no runtime errors in docker logs
