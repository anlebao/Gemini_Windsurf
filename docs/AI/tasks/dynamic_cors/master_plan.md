# MASTER PLAN: Dynamic CORS from KhachLinkInstance Registry

> **Created:** 2026-08-16
> **Last Updated:** 2026-08-17 (Sprint 1 COMPLETE — merged to `main` via PR #133, squash commit `d9545d5e`. CD Multi-VPS deployed. RV 8/8 PASS on VPS.)
> **Source:** User proposal review — Dynamic CORS + Application Registry
> **Branch:** `feature/dynamic-cors` (merged + deleted)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Domain modification:** YES — `KhachLinkInstance` constructor validation fix (`CanonicalizeDomain`)
> **Architecture change:** YES — Gateway CORS policy switches from static config to dynamic registry lookup
> **Approval:** Approved 2026-08-16

## Release Status

|| Release | Sprints | Branch | Status |
||---|---|---|---|
|| **R1** — Dynamic CORS Core | 1 | `feature/dynamic-cors` (merged `d9545d5e` via PR #133) | ✅ COMPLETE + MERGED + DEPLOYED + RV PASS |

---

## Problem Statement

Current CORS config in `docker-compose.prod.yml` + `docker-compose.gateway.yml` hardcodes allowed origins. Every new frontend domain requires editing docker-compose, committing, pushing, and restarting the Gateway. Issues #131 + #132 were caused by this exact pattern — timlathay.com was missing from the list.

## Solution: Dynamic CORS from `KhachLinkInstance`

`KhachLinkInstance` **already is** the Application/Domain Registry (`CustomDomain`, `IsActive`, `OwnerTenantId`, `Profile`, `NavFlags`). The `by-domain` endpoint already does dynamic domain lookup. We only extended the same source to feed CORS policy.

### What was NOT done (per codebase review)

| Rejected proposal | Reason |
|---|---|
| New `ApplicationOrigin` + `ApplicationDomain` tables | Violates Single Source of Truth — `KhachLinkInstance` already stores this |
| Redis cache for CORS | Overkill — Gateway runs 1 instance. In-memory cache TTL 5 min sufficient |
| `AllowCredentials()` | KhachLink WASM uses JWT Bearer, not cookies. Increases CSRF risk |
| Dynamic nginx for external domains | Out of scope — only 1 external domain currently |

## Architecture

```
PostgreSQL (KhachLinkInstance.CustomDomain)
    ↓
DynamicCorsCacheHostedService (BackgroundService, 5 min refresh)
    ↓
IMemoryCache (origin snapshot)
    ↓
DynamicCorsService (Singleton, sync read-only)
    ↓
CORS middleware (SetIsOriginAllowed callback)
```

### Data flow

1. Admin adds domain via `/admin/khachlink-instances` (existing UI)
2. `KhachLinkInstance` row created in PG
3. `DynamicCorsCacheHostedService` refreshes cache every 5 min → picks up new domain
4. Gateway receives cross-origin request → `DynamicCorsService.IsOriginAllowed()` reads IMemoryCache (sync, no DB)
5. CORS middleware adds `Access-Control-Allow-Origin` if allowed
6. **No docker-compose edit. No restart. No redeploy.**

### Static origins (always allowed, from appsettings)

`api2`/`app2`/`www2`/`diemthuong2` — core infrastructure domains, baked into `appsettings.Production.json`.

## Scope

### In scope (all implemented)
- `DynamicCorsService` (Singleton + IMemoryCache) — sync read-only CORS callback
- `DynamicCorsCacheHostedService` (BackgroundService) — pre-warm + 5 min refresh
- `GetActiveCustomDomainsAsync()` — lightweight query (SELECT CustomDomain WHERE IsActive=true)
- `CanonicalizeDomain()` — CustomDomain validation in KhachLinkInstance constructor
- Gateway CORS policy swap (`AllowAll` → `DynamicCors`, late-binding IServiceProvider)
- `Cors:StaticOrigins` in appsettings
- Removed `Cors__AllowedOrigins__*` from docker-compose
- 17 unit tests + 4 integration tests

### Out of scope (deferred)
- Redis cache (in-memory sufficient for 1 Gateway instance)
- `AllowCredentials()` (not needed for JWT-based KhachLink)
- Dynamic nginx for external domains (separate initiative)
- Cache invalidation endpoint (TTL 5 min acceptable)

## 4 Architecture Fixes from Review

1. **No `BuildServiceProvider()`** — late-binding `IServiceProvider` captured after `builder.Build()`
2. **No `.GetAwaiter().GetResult()`** — background HostedService pre-warms cache, CORS callback reads IMemoryCache only
3. **`GetActiveCustomDomainsAsync()`** — lightweight query, not `GetAllAsync()` full entities
4. **`CanonicalizeDomain()`** — strips scheme/path/port/slash, validates hostname format

## RV Results (2026-08-17, post-deploy)

| # | Test | Result | Status |
|---|---|---|---|
| 1 | `timlathay.com` (registry) → CORS header | `Access-Control-Allow-Origin: https://timlathay.com` | ✅ PASS |
| 2 | `diemthuong2.khachvip.online` (static + registry) → CORS header | Present | ✅ PASS |
| 3 | `app2.khachvip.online` (static) → CORS header | Present | ✅ PASS |
| 4 | `www2.khachvip.online` (static) → CORS header | Present | ✅ PASS |
| 5 | `evil.com` (unknown) → NO CORS header | Absent | ✅ PASS |
| 6 | `random-test-123.example.com` (unknown) → NO CORS header | Absent | ✅ PASS |
| 7 | OPTIONS preflight for `timlathay.com` | `204` + CORS headers | ✅ PASS |
| 8 | Add new domain via admin API → CORS works after 5 min, NO restart | `rv-test-cors.example.com` → CORS header after 5 min | ✅ PASS |

## Success Criteria (all met)

1. ✅ No `Cors__AllowedOrigins__*` env vars in docker-compose (removed)
2. ✅ Admin adds domain via `/admin/khachlink-instances` → CORS works within 5 min (no restart)
3. ✅ Existing domains (timlathay.com, diemthuong2) continue to work
4. ✅ Unknown origins (evil.com) get no CORS header
5. ✅ Gateway starts even if PG is down (static origins from appsettings)
