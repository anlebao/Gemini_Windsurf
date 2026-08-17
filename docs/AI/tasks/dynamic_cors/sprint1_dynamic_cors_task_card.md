# SPRINT 1: Dynamic CORS Core — Task Card

> **Sprint:** 1 — Dynamic CORS Core
> **Status:** ✅ COMPLETE — merged to `main` via PR #133 (squash `d9545d5e`), CD Multi-VPS deployed, RV 8/8 PASS
> **Branch:** `feature/dynamic-cors` (merged + deleted)
> **Commit:** `d9545d5e` (squash merge 2026-08-17)
> **PR:** https://github.com/anlebao/Gemini_Windsurf/pull/133
> **Files changed:** 17 (674 insertions, 45 deletions)

## Tasks

| # | Task | Files | Status |
|---|---|---|---|
| 1 | `IDynamicCorsService` + `DynamicCorsService` (Singleton + IMemoryCache) | `3_CoreHub/Services/IDynamicCorsService.cs`, `DynamicCorsService.cs` | ✅ COMPLETE |
| 2 | `DynamicCorsCacheHostedService` (background pre-warm + 5 min refresh) | `3_CoreHub/Services/DynamicCorsCacheHostedService.cs` | ✅ COMPLETE |
| 3 | `GetActiveCustomDomainsAsync()` — lightweight query | `3_CoreHub/Services/IKhachLinkInstanceService.cs`, `KhachLinkInstanceService.cs` | ✅ COMPLETE |
| 4 | `CanonicalizeDomain()` — CustomDomain validation in constructor | `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkInstance.cs` | ✅ COMPLETE |
| 5 | Gateway `Program.cs` CORS policy swap (`AllowAll` → `DynamicCors`) | `2_Gateway/Program.cs` | ✅ COMPLETE |
| 6 | `Cors:StaticOrigins` in appsettings | `2_Gateway/appsettings.json`, `appsettings.Production.json` | ✅ COMPLETE |
| 7 | Remove `Cors__AllowedOrigins__*` from docker-compose | `docker-compose.prod.yml`, `docker-compose.gateway.yml` | ✅ COMPLETE |
| 8-10 | Unit tests (DynamicCorsService + HostedService + Canonicalize) | 3 test files (17 tests) | ✅ COMPLETE |
| 11 | Integration tests (CORS header present/absent) | `DynamicCorsIntegrationTests.cs` (4 tests) | ✅ COMPLETE |
| 12 | Build + guard-check + test gate | — | ✅ COMPLETE |
| 13 | Commit + push + create PR + merge | PR #133 | ✅ COMPLETE |
| 14 | RV on VPS after CD deploy | 8/8 PASS | ✅ COMPLETE |

## Verification

| Gate | Result |
|---|---|
| guard-check.ps1 | ALL PASSED |
| dotnet build | 0 errors |
| Core.Tests | 1361 passed (incl. 13 DynamicCors + 10 Canonicalize) |
| Integration.Tests | 251 passed (incl. 4 DynamicCorsIntegration) |
| Architecture.Tests | 39 passed |
| CI pre-push pipeline | ALL PASSED (1200s) |
| CD Multi-VPS | SUCCESS |
| RV on VPS (8 tests) | 8/8 PASS |

## 4 Architecture Fixes Applied (from review)

1. **No `BuildServiceProvider()`** — late-binding `IServiceProvider` captured after `builder.Build()`
2. **No `.GetAwaiter().GetResult()`** — background HostedService pre-warms cache, CORS callback reads IMemoryCache only
3. **`GetActiveCustomDomainsAsync()`** — lightweight query (SELECT CustomDomain WHERE IsActive=true), not `GetAllAsync()` full entities
4. **`CanonicalizeDomain()`** — strips scheme/path/port/slash, validates hostname format

## Notes

- Unit.Tests project has pre-existing compile errors (TestBase, TDDCustomerOnboarding, TDDLeadConversion) — not caused by this PR. DynamicCors tests moved to `VanAn.Core.Tests` which builds successfully.
- `InternalsVisibleTo("VanAn.Core.Tests")` already existed in `3_CoreHub/Properties/AssemblyAttributes.cs` — no new assembly attribute needed.
- Cache invalidation for deactivating compromised domains: 5-min TTL acceptable (CORS not a security boundary). If immediate invalidation needed in future, add `IDynamicCorsService.InvalidateCache()` + call from KhachLinkInstance deactivate endpoint.
