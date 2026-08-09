# Task Card: Per-User Rate Limiting (nginx)

> **Status:** DEFERRED — requires infrastructure change
> **Priority:** Medium (after per-IP rate limit proves insufficient)
> **Created:** 2026-08-09 (nginx 503 fix iter 4)

## Problem

Current nginx rate limiting is per-IP (`$binary_remote_addr`). This has limitations:
- Multiple users behind same NAT (office, café, 4G) share quota
- One user with multiple sessions (phone + laptop) gets double quota
- Cannot distinguish authenticated user from anonymous

## Solution

Rate limit by JWT claim (userId/tenantId) instead of IP.

### Options

1. **nginx lua module** (`ngx_http_lua_module`)
   - Parse JWT from Authorization header in `access_by_lua_block`
   - Extract `sub` or `tenant_id` claim as rate limit key
   - Pros: no external service, fast
   - Cons: custom Docker image (nginx + lua), maintenance burden

2. **`auth_request` + external rate-limit service**
   - nginx calls a small ASP.NET endpoint per request
   - Endpoint validates JWT + checks rate limit (Redis counter)
   - Pros: uses existing .NET stack, Redis for distributed counting
   - Cons: extra hop per request, Redis dependency

3. **`map` + JWT decode in nginx** (no lua)
   - Use `map` to extract JWT from header, base64-decode payload
   - Pros: no extra modules
   - Cons: fragile, JWT parsing in nginx config is ugly, can't verify signature

### Recommendation

Option 1 (lua) if Docker image change is acceptable.
Option 2 if Redis is already planned for other features.

## Prerequisites

- Confirm per-IP rate limiting is insufficient (monitor 429 logs)
- Decide on Redis availability (check with infra team)

## Scope

- [ ] Choose approach (lua vs auth_request)
- [ ] Implement JWT extraction in nginx
- [ ] Configure per-user rate zones (read API, write API, auth)
- [ ] Test: 2 users same IP get independent quotas
- [ ] Test: 1 user 2 sessions gets single quota
- [ ] Fallback: if JWT missing, fall back to per-IP limit

## Related

- API classification task card (depends on this)
- Blazor bootstrap endpoint (independent)
