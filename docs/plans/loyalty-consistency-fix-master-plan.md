# Loyalty Point Storage Consistency — Master Plan

**Created:** 2026-08-03
**Status:** APPROVED + IN PROGRESS — Layer 1 (TC-S1 Phase 0) implementation 8/12 sub-tasks done
**Mode:** IMPLEMENT (Phase 0 — new infra) + FIX_ONLY (Phase 1-3 — consistency fixes)
**Supersedes:** `loyalty-consistency-fix-plan.md` (rolled into this plan + 9 review gaps)
**Reference:** `loyalty-alliance-master-plan.md` (Phase 7 COMPLETE — this plan hardens it)
**Execution strategy:** Layered Batch — Layer 1 (Phase 0 infra, verify gate, commit) → Layer 2 (Phase 1+2+3 writes+reads+sync, 1 commit, TDD per-bug) → Layer 3 (Phase 4 VPS RV 14-step)

## Problem Summary

Loyalty points are stored in **2 parallel systems** routed by `LoyaltyModeResolver.GetEffectiveModeAsync(tenantId)`:

| System | Database | Mode | Source of truth |
|---|---|---|---|
| `LoyaltyRewards` | SQLite (per-tenant, ShopERP) | Silo | Yes in Silo |
| `AllianceWallet` + `AllianceTransaction` | PostgreSQL (Gateway, cross-tenant) | Alliance | Yes in Alliance |

Sync PG→SQLite is best-effort via NATS `LoyaltySyncSubscriber` (fire-and-forget, not transactional).

**9 inconsistencies identified** — all stem from missing Alliance mode routing in point write/read paths.

## Architecture Decision: Option B (HTTP Proxy + Cache + Idempotency)

**Multi-VPS ready.** ShopERP does NOT connect to PG directly. All Alliance operations route through Gateway HTTP internal API.

```
ShopERP (any VPS)                       Gateway (VPS-A, PG source of truth)
┌───────────────────────────┐           ┌───────────────────────────┐
│ LoyaltyModeResolver       │─GET──────→│ InternalLoyaltyController │
│   HttpProxy               │+cache 60s │   /effective-config       │
│   (IMemoryCache)          │           │   [InternalApiKey]        │
├───────────────────────────┤           ├───────────────────────────┤
│ AllianceWalletService     │─POST─────→│   /points/add             │
│   HttpProxy               │+idempot.  │   /points/deduct          │
│   (IMemoryCache 10s       │key header │   /points/refund          │
│    for wallet reads)      │           ├───────────────────────────┤
│                           │─GET──────→│   /wallet/{deviceId}      │
└───────────────────────────┘           └─────────┬─────────────────┘
                                                  │ direct PG
                                        ┌─────────▼─────────────────┐
                                        │ AllianceWalletService     │
                                        │ LoyaltyModeResolver       │
                                        │ (real impls, unchanged)   │
                                        │ + idempotency check       │
                                        └─────────┬─────────────────┘
                                                  │
                                        ┌─────────▼─────────────────┐
                                        │ PostgreSQL                │
                                        │ + AllianceTransaction.    │
                                        │   IdempotencyKey column   │
                                        └───────────────────────────┘
```

**Auth:** `X-Internal-Api-Key` header (shared secret in config). Gateway validates; ShopERP sends on all internal HTTP calls.

**Cache:** Mode resolution 60s TTL (rare admin changes). Wallet balance 10s TTL (frequent changes, 10s staleness acceptable for UI). Write ops invalidate wallet cache for that device.

**Idempotency:** Caller generates stable key (`earn:{orderId}`, `mission:{completionId}`). HTTP proxy forwards in `X-Idempotency-Key` header. Gateway checks `AllianceTransactions.IdempotencyKey` before processing. Retry-safe.

**NATS:** unchanged — existing `LoyaltySyncSubscriber` for PG→SQLite balance sync (BUG #9 extends to history).

## Bug Index

| # | Title | Severity | Layer | Session |
|---|---|---|---|---|
| #0 | DI registration gap — Alliance services absent from ShopERP | CRITICAL | Infra | S1 |
| #1 | MissionService mission points → SQLite only | CRITICAL | Write | S2 |
| #2 | RedemptionService.CancelAsync refund → SQLite only | CRITICAL | Write | S2 |
| #3 | LoyaltyController.Redeem (legacy) → SQLite only | CRITICAL | Write | S2 |
| #4 | Customer-facing balance display — `/api/loyalty/my` not mode-aware | MEDIUM | Read | S3 |
| #5 | (rolled into #4 — same root cause) | — | — | — |
| #6 | Welcome bonus (`ActivateCustomerAsync`) → SQLite only | CRITICAL | Write | S2 |
| #7 | `/api/customers/me` (`GetMe`) returns SQLite balance | CRITICAL | Read | S3 |
| #8 | ShopERP admin CRM reads SQLite balance | MEDIUM | Read | S3 |
| #9 | `LoyaltySyncSubscriber` syncs balance only, not history | MEDIUM | Sync | S4 |

## Key Decisions

| ID | Decision | Status |
|---|---|---|
| D1 | BUG #0 DI: **Option B — HTTP proxy + cache + idempotency** (multi-VPS ready) | ✅ APPROVED |
| D2 | Balance-read: mode-aware `/api/loyalty/my` at source | ✅ APPROVED (implicit) |
| D3 | BUG #3 legacy redeem: deprecate (410 Gone) | ✅ APPROVED (implicit) |
| D4 | PG/SQLite atomicity: eventual-consistency + idempotent retry | ✅ APPROVED (implicit) |
| D5 | `LoyaltySyncSubscriber` history: append summary entries | ✅ APPROVED (implicit) |

## Phase Dependency

```
Phase 0 (HTTP Infra) → Phase 1 (Writes) → Phase 2 (Reads) → Phase 3 (Sync) → Phase 4 (Tests + RV)
```

## Phase Summary

| Phase | Sessions | Boundary | Output |
|---|---|---|---|
| 0 | 1 | `2_Gateway/`, `5_WebApps/ShopERP/`, `1_Shared/`, `3_CoreHub/` | HTTP proxies + Gateway internal API + auth + cache + idempotency + Domain column + migration — **IN PROGRESS 8/12 sub-tasks (2026-08-03)** |
| 1 | 1 | `3_CoreHub/Services/`, `5_WebApps/ShopERP/Controllers/` | Mode routing in MissionService + Cancel + LoyaltyController.Redeem + ActivateCustomer |
| 2 | 1 | `5_WebApps/ShopERP/Controllers/` | Mode-aware reads: `/api/loyalty/my` + `/api/customers/me` + admin CRM |
| 3 | 1 | `5_WebApps/ShopERP/Services/` | NATS sync extends to history |
| 4 | 1 | `6_Tests/` + VPS | Unit tests + integration + VPS RV |

**Total: ~5 sessions**

---

## Session Boundaries

### Session 1 — Phase 0: HTTP Proxy Infrastructure (BUG #0)
**Status:** IN PROGRESS (2026-08-03) — 8/12 sub-tasks done
**Boundary:** `2_Gateway/`, `5_WebApps/ShopERP/`, `1_Shared/Domain.cs`, `3_CoreHub/`
**Depends on:** D1 approved ✅
**Done (8):** Domain `IdempotencyKey` + EF config + PG migration `20260802201947_AddAllianceTransactionIdempotencyKey` + `IAllianceWalletService` interface (3 methods +idempotencyKey) + `AllianceWalletService` real impl (idempotency check in 3 methods) + `InternalApiKeyAttribute.cs` (NEW) + `InternalLoyaltyController.cs` (NEW — 5 endpoints) + `AllianceWalletServiceHttpProxy.cs` (NEW — ShopERP HTTP proxy + cache 10s + write invalidation)
**Pending (4):** `LoyaltyModeResolverHttpProxy.cs` (NEW) + DI registration (Gateway `Program.cs` API key + ShopERP `Program.cs` proxies + named HttpClient "GatewayInternal") + existing callers pass idempotency keys (`OrderWorkflowService` + `RedemptionService`) + appsettings.json config (`InternalLoyalty:ApiKey` + `Gateway:BaseUrl`)
**Then tests:** `ShopErpDiRegistrationTests` + `InternalApiKeyAuthTests` + `IdempotencyTests`
**Then verify gate:** `dotnet build VanAn.sln` + `dotnet test` PASS
**Then commit + push Layer 1**
**Files to create:**
- `2_Gateway/Filters/InternalApiKeyAttribute.cs` — auth filter
- `2_Gateway/Controllers/InternalLoyaltyController.cs` — 5 internal endpoints
- `5_WebApps/ShopERP/Services/AllianceWalletServiceHttpProxy.cs` — IAllianceWalletService via HTTP + cache
- `5_WebApps/ShopERP/Services/LoyaltyModeResolverHttpProxy.cs` — ILoyaltyModeResolver via HTTP + cache
**Files to edit:**
- `1_Shared/Domain.cs` — add `IdempotencyKey` to `AllianceTransaction`
- `3_CoreHub/Infrastructure/Configurations/AllianceTransactionConfiguration.cs` — map + index
- `1_Shared/Services/IAllianceWalletService.cs` — add optional `idempotencyKey` param
- `3_CoreHub/Services/AllianceWalletService.cs` — idempotency check + store key
- `2_Gateway/Program.cs` — configure internal API key
- `5_WebApps/ShopERP/Program.cs` — register HTTP proxies + named HttpClient "GatewayInternal"
- `3_CoreHub/Services/OrderWorkflowService.cs` — pass idempotency key `earn:{orderId}`
- `3_CoreHub/Services/RedemptionService.cs` — pass idempotency key `redeem:{recordId}` in RedeemAsync
**Migration:** new PG migration for `AllianceTransaction.IdempotencyKey` column + index
**Output:** Alliance services resolvable in ShopERP via HTTP proxy; Gateway internal API secured

### Session 2 — Phase 1: Point-Write Mode Routing (BUG #1, #2, #3, #6)
**Boundary:** `3_CoreHub/Services/`, `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`
**Depends on:** S1 complete
**Files to edit:** `MissionService.cs` (routing + idempotency `mission:{completionId}`), `RedemptionService.cs` `CancelAsync` (routing + idempotency `refund:{recordId}`), `LoyaltyController.cs` `Redeem` (D3: 410 Gone), `LoyaltyRewardsService.cs` `ActivateCustomerAsync` (routing + idempotency `welcome:{customerId}`)
**Output:** All point-award/deduct paths route by mode; legacy redeem deprecated

### Session 3 — Phase 2: Point-Read Mode Routing (BUG #4, #5, #7, #8)
**Boundary:** `5_WebApps/ShopERP/Controllers/`
**Depends on:** S1 complete
**Files to create:** `3_CoreHub/Services/LoyaltyReadRouter.cs` — shared balance-read helper (uses IAllianceWalletService → HTTP proxy)
**Files to edit:** `LoyaltyController.cs` `GetMyLoyalty`, `CustomerIdentityController.cs` `GetMe`+`VerifyOtp`, `CustomerController.cs` `List`+`PreviewSegment`+`ListGlobal`
**Output:** All balance reads return PG wallet balance in Alliance mode (via HTTP proxy, cached 10s)

### Session 4 — Phase 3: NATS Sync Fidelity (BUG #9)
**Boundary:** `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs`, `3_CoreHub/Services/AllianceWalletService.cs`
**Depends on:** S1 complete
**Files to edit:** `AllianceWalletService.cs` `PublishLoyaltyChangedAsync` (extend payload), `LoyaltySyncSubscriber.cs` `SyncLoyaltyBalanceAsync` (append history)
**Output:** SQLite mirror has balance + history consistent with PG wallet

### Session 5 — Phase 4: Tests + VPS Runtime Verification
**Boundary:** `6_Tests/`, VPS via SSH
**Depends on:** S1-S4 + build green + CD deployed
**Files to create:** 6 test files (see detail plan)
**Output:** All routing paths covered + 12-step VPS RV pass

**VPS RV checklist:**
1. ShopERP container starts cleanly with HTTP proxy registration
2. `X-Internal-Api-Key` validated — unauthenticated call → 401
3. Set mode=Alliance + IsAllianceMember=true via Gateway admin API
4. New customer OTP verify → welcome bonus to PG (BUG #6) — verify `AllianceTransactions.IdempotencyKey = welcome:{customerId}`
5. Complete mission → PG wallet +points (BUG #1) — verify idempotency key
6. Redeem catalog item → PG deducts, voucher in SQLite
7. Cancel redemption → PG refund (BUG #2) — verify idempotency key
8. `GET /api/loyalty/my` → returns PG balance (BUG #4)
9. `GET /api/customers/me` → returns PG balance (BUG #7)
10. ShopERP admin `GET /api/customers` → PG balance (BUG #8)
11. NATS sync → SQLite history appended (BUG #9)
12. `POST /api/loyalty/redeem` (legacy) → 410 Gone (BUG #3/D3)
13. Docker logs: gateway + shoperp — no errors, no missing-table exceptions
14. Retry test: re-send same idempotency key → Gateway returns cached result (no double points)

**Acceptance:** All 14 steps pass on VPS, no runtime errors in docker logs.
