# Loyalty Alliance System — Task Cards

## TC-P1A: Domain Entities
| Field | Value |
|-------|-------|
| Phase | 1A |
| Session | 1 |
| Mode | IMPLEMENT |
| Boundary | `1_Shared/Domain.cs` |
| Depends on | Spec approved |
| Blocks | TC-P1B |

**Deliverables:**
- [ ] `LoyaltyMode` enum: `Silo = 0, Alliance = 1`
- [ ] `TransactionType` enum: `EARN = 0, REDEEM = 1, ADJUST = 2`
- [ ] `LoyaltyGlobalConfig` entity (NOT tenant-scoped, BaseEntity)
- [ ] `LoyaltyTenantConfig` entity (tenant-scoped, IMustHaveTenant)
- [ ] `AllianceWallet` entity (NOT tenant-scoped, BaseEntity)
- [ ] `AllianceTransaction` entity (NOT tenant-scoped, BaseEntity)

**Acceptance:** Build passes, entities follow Single-Identity Pattern (Id = business key)

---

## TC-P1B: EF Configs + Migration + DI
| Field | Value |
|-------|-------|
| Phase | 1B |
| Session | 2 |
| Mode | IMPLEMENT |
| Boundary | `3_CoreHub/Infrastructure/` |
| Depends on | TC-P1A |
| Blocks | TC-P2A |

**Deliverables:**
- [ ] 4 EF Configuration classes: `LoyaltyGlobalConfigConfiguration`, `LoyaltyTenantConfigConfiguration`, `AllianceWalletConfiguration`, `AllianceTransactionConfiguration`
- [ ] Add DbSets to `IVanAnDbContext` + `VanAnDbContext`
- [ ] PG migration (dotnet ef migrations add LoyaltyAlliance)
- [ ] DI registration in `2_Gateway/Program.cs` (repositories for new entities)

**Acceptance:** `dotnet build` passes, migration applies cleanly on PG

---

## TC-P2A: LoyaltyModeResolver + AllianceWalletService
| Field | Value |
|-------|-------|
| Phase | 2A |
| Session | 3 |
| Mode | IMPLEMENT |
| Boundary | `3_CoreHub/Services/`, `1_Shared/Services/` |
| Depends on | TC-P1B |
| Blocks | TC-P2B, TC-P2C |

**Deliverables:**
- [ ] `ILoyaltyModeResolver` interface: `Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId)`, `Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId)`
- [ ] `LoyaltyModeResolver` implementation: tenant override → global fallback
- [ ] `IAllianceWalletService` interface: `GetOrCreateWalletAsync`, `AddPointsAsync`, `DeductPointsAsync`, `GetWalletAsync`, `GetTransactionsAsync`, `RefundAsync`
- [ ] `AllianceWalletService` implementation: wallet CRUD + transaction logging + MaxWalletPoints enforcement

**Acceptance:** Build passes, unit tests for mode resolution + wallet operations

---

## TC-P2B: Modify OrderWorkflowService (EARN branch)
| Field | Value |
|-------|-------|
| Phase | 2B |
| Session | 4 |
| Mode | IMPLEMENT |
| Boundary | `3_CoreHub/Services/OrderWorkflowService.cs` |
| Depends on | TC-P2A |
| Blocks | TC-P3B |

**Deliverables:**
- [ ] Inject `ILoyaltyModeResolver` + `IAllianceWalletService` into `OrderWorkflowService`
- [ ] Modify `ProcessLoyaltyPointsAsync`: call `GetEffectiveModeAsync(order.TenantId)` before awarding
- [ ] Silo branch: existing `_loyaltyRewardsService.AddPointsAsync` (unchanged)
- [ ] Alliance branch: `_allianceWalletService.AddPointsAsync(customerDeviceId, tenantId, points, reason)` + check IsAllianceMember + MaxWalletPoints

**Acceptance:** Build passes, existing Silo flow unchanged, Alliance flow routes to PG wallet

---

## TC-P2C: Modify RedemptionService (REDEEM branch) + NATS Sync
| Field | Value |
|-------|-------|
| Phase | 2C |
| Session | 5 |
| Mode | IMPLEMENT |
| Boundary | `3_CoreHub/Services/RedemptionService.cs`, `5_WebApps/ShopERP/Services/` |
| Depends on | TC-P2A |
| Blocks | TC-P3B |

**Deliverables:**
- [ ] Inject `ILoyaltyModeResolver` + `IAllianceWalletService` into `RedemptionService`
- [ ] Modify `RedeemAsync`: call `GetEffectiveModeAsync` before deducting
- [ ] Silo branch: existing SQLite flow (unchanged)
- [ ] Alliance branch: check `IsAllianceMember` → deduct from `AllianceWallet` → create RedemptionRecord + Voucher in local SQLite
- [ ] Create `LoyaltySyncSubscriber.cs` in ShopERP: subscribe `vanan.cloud.loyalty.changed.*` → update local `LoyaltyRewards.PointBalance`
- [ ] Publish `LoyaltyPointsChanged` NATS event from `AllianceWalletService`

**Acceptance:** Build passes, existing Silo redeem unchanged, Alliance redeem deducts from PG wallet + creates local voucher

---

## TC-P3A: SystemAdmin API (Gateway)
| Field | Value |
|-------|-------|
| Phase | 3A |
| Session | 6 |
| Mode | IMPLEMENT |
| Boundary | `2_Gateway/Controllers/` |
| Depends on | TC-P2A |
| Blocks | TC-P5A |

**Deliverables:**
- [ ] `LoyaltyConfigController.cs` with endpoints:
  - `GET /api/platform/loyalty/config` — global config
  - `PUT /api/platform/loyalty/config` — update global (SystemAdmin only)
  - `GET /api/platform/loyalty/tenant/{tenantId}/config` — per-tenant config
  - `PUT /api/platform/loyalty/tenant/{tenantId}/config` — update per-tenant (SystemAdmin only)
- [ ] Authorization: SystemAdmin role only on PUT endpoints

**Acceptance:** Build passes, endpoints return correct data, non-SystemAdmin gets 403

---

## TC-P3B: Customer API (Gateway + ShopERP)
| Field | Value |
|-------|-------|
| Phase | 3B |
| Session | 7 |
| Mode | IMPLEMENT |
| Boundary | `2_Gateway/Controllers/`, `5_WebApps/ShopERP/Controllers/` |
| Depends on | TC-P2B, TC-P2C |
| Blocks | TC-P5B |

**Deliverables:**
- [ ] `GET /api/loyalty/wallet` — customer wallet (total balance + breakdown by tenant + recent transactions)
- [ ] Modify `POST /api/redemption/redeem` — accept optional `tenantId` for cross-tenant redeem
- [ ] Gateway forward endpoints to ShopERP or direct PG query (wallet is PG-only)

**Acceptance:** Build passes, wallet endpoint returns breakdown, cross-tenant redeem works

---

## TC-P4: Mode Switch Migration
| Field | Value |
|-------|-------|
| Phase | 4 |
| Session | 8 |
| Mode | IMPLEMENT |
| Boundary | `3_CoreHub/Services/AllianceWalletService.cs` |
| Depends on | TC-P2A |
| Blocks | TC-P6A |

**Deliverables:**
- [ ] `ConsolidateWalletsAsync(tenantId)`: Silo→Alliance — merge all `LoyaltyRewards` into `AllianceWallet` by CustomerDeviceId
- [ ] `SplitWalletsAsync(tenantId)`: Alliance→Silo — calculate net EARN per-tenant from `AllianceTransaction`, distribute `TotalPointBalance` proportionally, freeze wallet
- [ ] Edge case: tenant with net EARN ≤ 0 gets no allocation
- [ ] API trigger: `POST /api/platform/loyalty/migrate` (SystemAdmin only)

**Acceptance:** Build passes, consolidation + split logic verified with test data

---

## TC-P5A: Admin UI (ShopERP Blazor)
| Field | Value |
|-------|-------|
| Phase | 5A |
| Session | 9 |
| Mode | IMPLEMENT |
| Boundary | `5_WebApps/ShopERP/Pages/Admin/` |
| Depends on | TC-P3A |
| Blocks | TC-P6B |

**Deliverables:**
- [ ] `LoyaltyConfigAdmin.razor` — SystemAdmin panel:
  - Global mode toggle (Silo/Alliance)
  - Global MaxWalletPoints input
  - Per-tenant list with mode override + IsAllianceMember toggle + MaxWalletPoints override
  - Mode switch trigger button (with confirmation dialog)
- [ ] Use UI Platform components (MudBlazor or existing component library)

**Acceptance:** Build passes, UI renders, config changes persist via API

---

## TC-P5B: Customer UI (KhachLink)
| Field | Value |
|-------|-------|
| Phase | 5B |
| Session | 10 |
| Mode | IMPLEMENT |
| Boundary | `5_WebApps/KhachLink/` |
| Depends on | TC-P3B |
| Blocks | TC-P6B |

**Deliverables:**
- [ ] Wallet view page: total balance + breakdown by tenant (tenant name + points) + recent transactions
- [ ] Cross-tenant redeem UI: select tenant → browse catalog → redeem with wallet balance
- [ ] Use existing KhachLink component patterns

**Acceptance:** Build passes, wallet displays correct balance, cross-tenant redeem UI functional

---

## TC-P6A: Unit + Integration Tests
| Field | Value |
|-------|-------|
| Phase | 6A |
| Session | 11 |
| Mode | IMPLEMENT |
| Boundary | `6_Tests/` |
| Depends on | TC-P4 |
| Blocks | None |

**Deliverables:**
- [ ] `LoyaltyModeResolverTests.cs`: tenant override > global, null = inherit, IsAllianceMember check
- [ ] `AllianceWalletServiceTests.cs`: AddPoints, DeductPoints, MaxWalletPoints enforcement, Refund to tenant
- [ ] `ModeSwitchMigrationTests.cs`: consolidate (Silo→Alliance), split-by-source (Alliance→Silo), edge cases
- [ ] `OrderWorkflowAllianceTests.cs`: EARN routes to Alliance wallet when mode=Alliance
- [ ] `RedemptionAllianceTests.cs`: REDEEM routes to Alliance wallet, IsAllianceMember=false blocks

**Acceptance:** All tests pass, coverage ≥ 80% for new code

---

## TC-P6B: E2E Tests
| Field | Value |
|-------|-------|
| Phase | 6B |
| Session | 12 |
| Mode | IMPLEMENT |
| Boundary | `6_Testing/e2e-tests/` |
| Depends on | TC-P5A, TC-P5B |
| Blocks | None |

**Deliverables:**
- [ ] `loyalty-alliance.spec.ts`:
  1. SystemAdmin sets mode=Alliance for tenant A + B
  2. Customer places order at tenant A → completes → earns 30 points
  3. Customer opens wallet → sees 30 points
  4. Customer redeems 50-point item at tenant B → fails (insufficient)
  5. Customer places order at tenant A → completes → earns 30 more points (total 60)
  6. Customer redeems 50-point item at tenant B → success → voucher created
  7. Customer wallet shows 10 remaining points
  8. SystemAdmin cancels voucher → refund to tenant B's LoyaltyRewards
- [ ] `loyalty-silo.spec.ts`: verify existing Silo flow unchanged

**Acceptance:** E2E tests pass on local environment

---

## TC-P7: VPS Runtime Verification
| Field | Value |
|-------|-------|
| Phase | 7 |
| Session | 13 |
| Mode | IMPLEMENT (runtime testing) |
| Boundary | VPS via SSH + API endpoints |
| Depends on | TC-P6A, TC-P6B, CD pipeline complete |
| Blocks | None |

**Prerequisite:**
- All code committed + pushed to main branch
- CD pipeline completed successfully
- Docker containers restarted on VPS with new images
- PG migration applied automatically by CD

**SSH access:**
```bash
ssh -i C:\VibeCoding\CD\SSH\vanan.pem ubuntu@<vps-ip>
```

**API endpoints:**
- Gateway: `https://api.khachvip.online`
- ShopERP: `https://khachvip.online`

**Verification checklist (14 steps):**

| # | Step | Command/Action | Expected Result |
|---|------|----------------|-----------------|
| 1 | PG migration applied | `docker exec gateway psql -U postgres -d vanan -c "\dt \"LoyaltyGlobalConfigs\""` | Table exists |
| 2 | AllianceWallets table | `docker exec gateway psql -U postgres -d vanan -c "\dt \"AllianceWallets\""` | Table exists |
| 3 | AllianceTransactions table | `docker exec gateway psql -U postgres -d vanan -c "\dt \"AllianceTransactions\""` | Table exists |
| 4 | SystemAdmin login | `POST /api/platform/login` with sysadmin@vanan.vn | 200 OK + cookie |
| 5 | Set global mode=Alliance | `PUT /api/platform/loyalty/config` `{ mode: "Alliance" }` | 200 OK |
| 6 | Set tenant A alliance member | `PUT /api/platform/loyalty/tenant/{tenantAId}/config` `{ isAllianceMember: true }` | 200 OK |
| 7 | Set tenant B alliance member | `PUT /api/platform/loyalty/tenant/{tenantBId}/config` `{ isAllianceMember: true }` | 200 OK |
| 8 | Customer OTP login at tenant A | `POST /api/customer/otp/send` + `POST /api/customer/otp/verify` | 200 OK + customer token |
| 9 | Place order at tenant A | `POST /api/public/orders/checkout` | 200 OK + orderId |
| 10 | Complete order | `PUT /api/orderworkflow/{orderId}/status` `{ status: "completed" }` | 200 OK |
| 11 | Verify wallet earned | `GET /api/loyalty/wallet` with X-Customer-Token | totalPointBalance > 0 |
| 12 | Verify PG wallet | `docker exec gateway psql -c "SELECT \"TotalPointBalance\" FROM \"AllianceWallets\""` | Balance > 0 |
| 13 | Verify NATS sync to SQLite | `docker exec shoperp sqlite3 /data/shoperp.db "SELECT PointBalance FROM LoyaltyRewards"` | Balance matches wallet |
| 14 | Docker logs clean | `docker logs gateway --tail 50` + `docker logs shoperp --tail 50` | No errors/exceptions |

**Additional verification (if time permits):**
- [ ] Cross-tenant redeem: customer redeems at tenant B → voucher created in tenant B SQLite
- [ ] Wallet balance decreased after redeem
- [ ] Voucher cancellation → refund to tenant B's LoyaltyRewards
- [ ] Mode switch back to Silo → split migration → balances distributed back

**Acceptance:** All 14 steps pass on VPS, no runtime errors in docker logs, data consistent between PG and SQLite
