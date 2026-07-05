# TASK CARD — SaaS W0: Gateway Architecture Decision (Option B — Monolithic Mode)

> **Status:** IN PROGRESS (Option B approved 2026-07-05) → IMPLEMENT
> **Prerequisite:** VAS Stream F complete (W0-W9 merged)
> **Branch:** `feature/saas-w0-gateway-architecture-fix`
> **Estimated sessions:** 1
> **Sprint:** 1 (Blockers)

## Objective
Accept current Gateway monolithic pattern (Option B). Update governance rule + architecture test. Document as architectural decision.

## Decision: Option B (Approved 2026-07-05)
**Original task card** proposed removing DbContext from Gateway (Option A). INVESTIGATE revealed:
- 40+ CoreHub service registrations in Gateway Program.cs
- 15+ controllers with business logic
- 2 SignalR hubs
- ShopERP already has duplicate registrations

Removing all of this = 3-5 sessions, high regression risk, no test coverage to catch breaks. The "pure proxy" rule was aspirational but codebase never followed it.

**Option B (approved):** Accept monolithic pattern. Gateway hosts in-process CoreHub services + DbContext (Npgsql) for low-latency access. YARP remains for forwarding select traffic. Document as architectural decision.

## Files to Modify
| File | Changes |
|------|---------|
| `6_Tests/VanAn.Integration.Tests/GatewayStartupTests.cs:120-137` | INVERT test: verify DbContext IS registered (not absent) |
| `.windsurfrules:71` | UPDATE Gateway rule: monolithic mode (Option B) |
| `.devin/rules/governance.md:70` | UPDATE no-business-logic rule: Gateway exception documented |
| `docs/AI/tasks/saas_w0_task_card.md` | UPDATE this card with Option B decision |

## Detailed Task List

### W0-T1: Update governance rules ✅
- `.windsurfrules` line 71: "Gateway operates in MONOLITHIC MODE (Option B approved 2026-07-05)"
- `.devin/rules/governance.md` line 70: "Gateway hosts in-process CoreHub services per Option B"

### W0-T2: Invert architecture test ✅
- `GatewayStartupTests.cs:120-137`: Test now verifies `IVanAnDbContext` IS registered (not absent)
- Test name: `Gateway_Architecture_DbContext_Registered_Monolithic_Mode`
- Assert.NotNull(dbContextService)

### W0-T3: Update task card ✅
- Replace Option A content with Option B decision + rationale

### W0-T4: Build + guard + tests pass
- `dotnet build VanAn.sln` Release — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- `dotnet test` — all tests pass

## Verification
- [x] `.windsurfrules` Gateway rule updated to monolithic mode
- [x] `.devin/rules/governance.md` Gateway exception documented
- [x] `GatewayStartupTests.cs` test inverted (verifies DbContext IS registered)
- [ ] Build 0 errors
- [ ] Guard pass
- [ ] All tests pass

## Rollback
- Git revert (restore pure proxy rule + absence test)
- If Option B causes issues later: spawn new stream for Option A (full pure proxy migration, 3-5 sessions)

## Rationale (for future reference)
The Gateway currently has:
- 40+ CoreHub service registrations (lines 117-225 of Program.cs)
- 15+ controllers injecting business services
- 2 SignalR hubs (OrderHub, KitchenHub)
- 4 middleware (HMAC, Localization, Error, ForwardedHeaders)
- IVanAnDbContext + Npgsql

ShopERP has duplicate registrations with ShopERPDbContext. The system operates as a monolith with Gateway as the primary entry point. Removing all this would require:
1. YARP config for all `/api/*` routes → ShopERP
2. Move all controllers to ShopERP
3. Update KhachLink to call Gateway → ShopERP (via YARP)
4. Full E2E test coverage to catch breaks
5. 3-5 sessions of work

Option B accepts reality and documents it. Option A can be pursued later if separation becomes a hard requirement (e.g., scale Gateway independently).
