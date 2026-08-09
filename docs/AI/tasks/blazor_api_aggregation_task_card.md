# Task Card: Blazor API Aggregation (Bootstrap Endpoint)

> **Status:** DEFERRED — requires page audit + refactor
> **Priority:** Medium (architectural improvement, not urgent)
> **Created:** 2026-08-09 (nginx 503 fix iter 4)

## Problem

Each Blazor Server page load fires 2-5 API calls (settings, feature flags,
permissions, profile, dashboard data). Fast navigation = many API requests.

Current nginx fix (separate /api/ rate limit) handles this, but the root
cause is excessive API calls per page load. Reducing API calls is a
structural improvement that reduces:
- Network round-trips (latency)
- Backend load (CPU, DB queries)
- Rate limit pressure

## Solution

Create bootstrap endpoints that aggregate multiple API responses into one:

```
GET /api/admin/bootstrap
→ {
    "shop": { ... },          // ShopFeatureSettings
    "features": [ ... ],      // FeatureFlags
    "permissions": [ ... ],   // User permissions
    "profile": { ... },       // User profile
    "tenants": [ ... ]        // Accessible tenants
  }
```

### Per-page bootstrap

- `/api/admin/valcn-features/bootstrap` — feature flags + shop settings
- `/api/admin/network-dashboard/bootstrap` — metrics + date range defaults
- `/api/admin/loyalty-config/bootstrap` — loyalty config + budget caps + tenant list

### Global bootstrap (initial page load)

- `/api/bootstrap` — user profile + permissions + accessible tenants + global settings
- Called once on Blazor circuit init, cached in CircuitState

## Scope

- [ ] Audit all admin pages — list API calls per page
- [ ] Design bootstrap response shapes (per-page vs global)
- [ ] Implement bootstrap controllers (aggregate existing services)
- [ ] Refactor Blazor pages to use bootstrap instead of individual API calls
- [ ] Handle cache invalidation (when 1 setting changes, invalidate relevant cache)
- [ ] Benchmark: measure API calls before/after, latency improvement

## Prerequisites

- None (independent of per-user rate limit)

## Related

- Per-user rate limit (independent, complementary)
- API classification (independent, complementary)
