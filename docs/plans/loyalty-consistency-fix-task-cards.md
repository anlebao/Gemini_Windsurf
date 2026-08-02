# Loyalty Point Storage Consistency — Task Cards

**Master plan:** `loyalty-consistency-fix-master-plan.md`
**Mode:** IMPLEMENT (Phase 0) + FIX_ONLY (Phase 1-3)
**Architecture:** Option B — HTTP proxy + cache + idempotency (multi-VPS ready)
**Total:** 5 sessions, 9 bugs

---

## TC-S1: Phase 0 — HTTP Proxy Infrastructure (BUG #0)
| Field | Value |
|-------|-------|
| Session | 1 |
| Mode | IMPLEMENT |
| Boundary | `2_Gateway/`, `5_WebApps/ShopERP/`, `1_Shared/`, `3_CoreHub/` |
| Depends on | D1 approved ✅ |
| Blocks | TC-S2, TC-S3, TC-S4 |
| Status | **COMPLETE (2026-08-03) — commits `0f924ec9` + `aa4d008c` + `8d7e2c25`** |

**Deliverables:**

**Gateway side:**
- [x] `InternalApiKeyAttribute.cs` — action filter validating `X-Internal-Api-Key` header against config `InternalLoyalty:ApiKey`
- [x] `InternalLoyaltyController.cs` — 5 endpoints (all `[InternalApiKey]`):
  - `GET /api/internal/loyalty/effective-config/{tenantId}` → `{ mode, maxWalletPoints, isAllianceMember }`
  - `POST /api/internal/loyalty/points/add` → calls `AllianceWalletService.AddPointsAsync` with idempotency check
  - `POST /api/internal/loyalty/points/deduct` → calls `DeductPointsAsync`
  - `POST /api/internal/loyalty/points/refund` → calls `RefundAsync`
  - `GET /api/internal/loyalty/wallet/{deviceId}` → returns wallet DTO
- [x] `2_Gateway/Program.cs` — bind `InternalLoyalty:ApiKey` from config

**ShopERP side:**
- [x] `AllianceWalletServiceHttpProxy.cs` — implements `IAllianceWalletService` via HTTP to Gateway internal API:
  - `GetWalletByDeviceIdAsync` → GET, cached 10s (`IMemoryCache`)
  - `AddPointsAsync/DeductPointsAsync/RefundAsync` → POST with idempotency key in body; invalidates wallet cache for device
  - `GetOrCreateWalletAsync` → returns existing or stub (ShopERP never creates wallets, only Gateway does)
  - `GetTransactionsAsync/GetTransactionsByTenantAsync` → `throw NotSupportedException` (Gateway-only)
  - `ConsolidateWalletsAsync/SplitWalletsAsync` → `throw NotSupportedException` (Gateway-only admin ops)
  - Auto-generates idempotency key (GUID) if caller doesn't provide one + logs warning
- [x] `LoyaltyModeResolverHttpProxy.cs` — implements `ILoyaltyModeResolver` via HTTP + `IMemoryCache` (60s TTL):
  - `GetEffectiveModeAsync` → GET `/effective-config/{tenantId}`, cache result
  - `GetEffectiveMaxWalletPointsAsync` → same endpoint, cache
  - `IsAllianceMemberAsync` → same endpoint, cache
- [x] `5_WebApps/ShopERP/Program.cs` — register:
  - `HttpClient "GatewayInternal"` with `X-Internal-Api-Key` default header + base address = Gateway URL
  - `ILoyaltyModeResolver` → `LoyaltyModeResolverHttpProxy`
  - `IAllianceWalletService` → `AllianceWalletServiceHttpProxy`

**Shared/CoreHub side (idempotency infrastructure):**
- [x] `1_Shared/Domain.cs` — add `public string? IdempotencyKey { get; protected set; }` to `AllianceTransaction` + constructor param
- [x] `AllianceTransactionConfiguration.cs` — map column + non-unique index on `IdempotencyKey` (`IX_AllianceTransactions_IdempotencyKey`)
- [x] New PG migration `20260802201947_AddAllianceTransactionIdempotencyKey` (nullable `character varying(200)` column + index)
- [x] `1_Shared/Services/IAllianceWalletService.cs` — add `string? idempotencyKey = null` optional param to `AddPointsAsync`, `DeductPointsAsync`, `RefundAsync`
- [x] `3_CoreHub/Services/AllianceWalletService.cs` — if `idempotencyKey` non-null: check `AllianceTransactions.FirstOrDefaultAsync(t => t.IdempotencyKey == key)` → if found, return cached `(true, existingTx.BalanceAfter, null)`; else process + set `tx.IdempotencyKey = key`

**Existing callers (pass idempotency keys for retry-safety):**
- [x] `OrderWorkflowService.cs` — `AddPointsAsync(..., idempotencyKey: $"earn:{order.Id}")`
- [x] `RedemptionService.cs` `RedeemAsync` — `DeductPointsAsync(..., idempotencyKey: $"redeem:{record.Id}")`

**Config:**
- [x] `2_Gateway/appsettings.json` + `5_WebApps/ShopERP/appsettings.json` — add `InternalLoyalty:ApiKey` + `Gateway:BaseUrl`
- [x] `2_Gateway/appsettings.Production.json` + `5_WebApps/ShopERP/appsettings.Production.json` — add `InternalLoyalty:ApiKey` + `Gateway:BaseUrl`
- [x] `docker-compose.yml` + `docker-compose.prod.yml` — env var `InternalLoyalty__ApiKey` (prod key `vanan-internal-loyalty-prod-2026`, dev key `vanan-internal-loyalty-dev-key-2026`)

**Verification gate (local, no VPS):**
1. [x] `dotnet build VanAn.sln` PASS — 0 errors
2. [x] `dotnet test` — existing tests pass (AllianceWalletServiceTests, LoyaltyModeResolverTests, ConsolidateWalletsTests — idempotencyKey is optional, backward compat)
3. [x] New `ShopErpDiRegistrationTests` — `ServiceProvider.GetService<ILoyaltyModeResolver>()` + `IAllianceWalletService>()` non-null in ShopERP
4. [x] New `InternalApiKeyAuthTests` — Gateway internal endpoint rejects missing/wrong API key
5. [x] New `IdempotencyTests` — same key → no double processing

**Acceptance:** Build + tests green; ShopERP DI resolves HTTP proxies; Gateway internal API secured. ✅ DONE

---

## TC-S2: Phase 1 — Point-Write Mode Routing (BUG #1, #2, #3, #6)
| Field | Value |
|-------|-------|
| Session | 2 |
| Mode | FIX_ONLY |
| Boundary | `3_CoreHub/Services/`, `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` |
| Depends on | TC-S1 |
| Blocks | TC-S5 |
| Status | **COMPLETE (2026-08-03) — commit `70897151`** |

**Deliverables:**
- [x] **BUG #1** `MissionService.cs`: inject `ILoyaltyModeResolver?` + `IAllianceWalletService?`; add `AwardPointsWithModeRoutingAsync` helper; replace `AddPointsAsync` at lines 131 + 252; pass `idempotencyKey: $"mission:{completion.Id}"`. Document eventual-consistency per D4 (idempotent retry covers partial failure).
- [x] **BUG #2** `RedemptionService.cs` `CancelAsync`: add `RefundPointsWithModeRoutingAsync` helper; reorder voucher lookup BEFORE refund; pass `idempotencyKey: $"refund:{record.Id}"`.
- [x] **BUG #3** `LoyaltyController.cs` `Redeem`: D3 — return `410 Gone` (deprecate, no routing).
- [x] **BUG #6** `LoyaltyRewardsService.cs` `ActivateCustomerAsync`: inject `ILoyaltyModeResolver?` + `IAllianceWalletService?` + `ICustomerRepository?`; route welcome bonus 100 pts; pass `idempotencyKey: $"welcome:{customerId}"`.
- [x] Tests: `MissionServiceAllianceTests` (4 tests), `RedemptionCancelAllianceTests` (3 tests), `LoyaltyRewardsActivateAllianceTests` (3 tests). Mock `IAllianceWalletService` + `ILoyaltyModeResolver` (same pattern as existing `OrderWorkflowAllianceTests`).

**Acceptance:** `dotnet build` + `dotnet test` green; no point-award path bypasses PG in Alliance mode. ✅ DONE

---

## TC-S3: Phase 2 — Point-Read Mode Routing (BUG #4, #5, #7, #8)
| Field | Value |
|-------|-------|
| Session | 3 |
| Mode | FIX_ONLY |
| Boundary | `5_WebApps/ShopERP/Controllers/` |
| Depends on | TC-S1 |
| Blocks | TC-S5 |
| Status | **COMPLETE (2026-08-03) — commit `70897151`** |

**Deliverables:**
- [x] **NEW** `3_CoreHub/Services/LoyaltyReadRouter.cs` — shared helper: `GetEffectiveBalanceAsync(tenantId, deviceGuid, sqliteBalance)` → queries `IAllianceWalletService.GetWalletByDeviceIdAsync` (HTTP proxy, cached 10s) when mode=Alliance; returns SQLite balance otherwise. Graceful fallback on HTTP error.
- [x] Register `LoyaltyReadRouter` in ShopERP `Program.cs`
- [x] **BUG #4+#5** `LoyaltyController.cs` `GetMyLoyalty`: inject `LoyaltyReadRouter`; return PG balance when Alliance. Per D2, fix at source — NO new endpoint.
- [x] **BUG #7** `CustomerIdentityController.cs`: inject `LoyaltyReadRouter`; `GetMe` + `VerifyOtp` return PG balance.
- [x] **BUG #8** `CustomerController.cs`: inject `LoyaltyReadRouter`; replace SQLite reads in `List`, `PreviewSegment`, `ListGlobal` with `LoyaltyReadRouter.GetEffectiveBalanceAsync`.
- [x] Tests: `LoyaltyReadRoutingTests` — 6 tests (Alliance+member→PG, Silo→SQLite, opt-out→SQLite, null DeviceId→SQLite, null deps→SQLite, Gateway exception→graceful fallback).

**Acceptance:** `dotnet build` + `dotnet test` green; KhachLink pages + ShopERP admin CRM display PG balance without client-side changes. ✅ DONE

---

## TC-S4: Phase 3 — NATS Sync Fidelity (BUG #9)
| Field | Value |
|-------|-------|
| Session | 4 |
| Mode | FIX_ONLY |
| Boundary | `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs`, `3_CoreHub/Services/AllianceWalletService.cs` |
| Depends on | TC-S1 |
| Blocks | TC-S5 |
| Status | **COMPLETE (2026-08-03) — commit `70897151`** |

**Deliverables:**
- [x] `AllianceWalletService.cs` `PublishLoyaltyChangedAsync`: extend payload with `type`, `points` (signed), `reason`, `tenantId`.
- [x] `LoyaltySyncSubscriber.cs` `SyncLoyaltyBalanceAsync`: when extended fields present, append `LoyaltyHistoryEntry` to `LoyaltyRewards.History` (idempotent: skip if `{timestamp, points, reason}` already exists). Backward compat: legacy payload → balance-only sync.
- [x] Tests: `LoyaltySyncHistoryTests` — 5 tests (extended payload parsing, legacy backward compat, history deserialize valid/invalid JSON, idempotency duplicate detection).

**Acceptance:** `dotnet build` + `dotnet test` green; SQLite mirror has balance + history consistent with PG. ✅ DONE

---

## TC-S5: Phase 4 — Tests + VPS Runtime Verification
| Field | Value |
|-------|-------|
| Session | 5 |
| Mode | IMPLEMENT (tests) + REVIEW (RV) |
| Boundary | `6_Tests/`, VPS via SSH |
| Depends on | TC-S1, TC-S2, TC-S3, TC-S4 + build green + CD deployed |
| Blocks | None (final) |
| Status | **COMPLETE (2026-08-03) — RV 37/37 PASS** |

**Deliverables:**
- [x] `ShopErpDiRegistrationTests.cs` — BUG #0 regression guard
- [x] `InternalApiKeyAuthTests.cs` — internal API auth
- [x] `IdempotencyTests.cs` — same key → no double processing
- [x] All unit tests from TC-S2/S3/S4 green (21 new tests PASS)
- [x] Existing tests regression: `OrderWorkflowAllianceTests`, `RedemptionAllianceTests`, `LoyaltySyncSubscriberTests`, `LoyaltyModeResolverTests`, `AllianceWalletServiceTests` — still green (80 existing loyalty tests PASS)
- [x] `guard-check.ps1` + `dotnet build VanAn.sln` PASS (0 errors)
- [x] Commit + push; wait for CD; run VPS RV smoke test (37 checks — see master plan)
- [x] Update `docs/AI/project_state.md` Section 10

**Acceptance:** VPS RV 37/37 pass; no runtime errors in docker logs; project_state.md updated. ✅ DONE

---

## Decision Tracker

| ID | Decision | Status |
|---|---|---|
| D1 | BUG #0 DI: Option B (HTTP proxy + cache + idempotency) | ✅ APPROVED |
| D2 | Balance-read: mode-aware `/api/loyalty/my` at source | ✅ APPROVED |
| D3 | BUG #3 legacy redeem: deprecate (410 Gone) | ✅ APPROVED |
| D4 | PG/SQLite atomicity: eventual-consistency + idempotent retry | ✅ APPROVED |
| D5 | `LoyaltySyncSubscriber` history: append summary | ✅ APPROVED |

**All decisions resolved.** No blockers — TC-S1 can start immediately.
