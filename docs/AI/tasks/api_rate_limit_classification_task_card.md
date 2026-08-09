# Task Card: API Rate Limit Classification

> **Status:** DEFERRED — depends on per-user rate limit
> **Priority:** Low (nice-to-have, not urgent)
> **Created:** 2026-08-09 (nginx 503 fix iter 4)

## Problem

Current rate limiting treats all API calls equally (zone=api, 30r/s).
In practice, different API types have different cost and risk:

| API type | Cost | Risk | Ideal limit |
|----------|------|------|-------------|
| GET (read) | Low (cached query) | Low | 30-60 r/s/user |
| POST (write) | High (DB write, outbox) | Medium | 5-10 r/s/user |
| Auth (login, OTP) | Medium (BCrypt) | High (brute-force) | 5 r/min/user |
| Export (PDF, Excel) | Very high (CPU, memory) | Low | 1-2 r/min/user |

## Solution

Classify API endpoints and apply different rate limits:

### nginx location-based

```
# Read API — high rate
location /api/ {
    limit_req zone=api_read burst=200 nodelay;
}

# Write API — lower rate
location ~ ^/api/(orders|refunds|accounting|loyalty)/.*$ {
    limit_req zone=api_write burst=20 nodelay;
}

# Export API — very low rate
location ~ ^/api/.*/export$ {
    limit_req zone=api_export burst=2 nodelay;
}
```

### Prerequisite: per-user rate limit

This classification is most effective with per-user (JWT claim) rate
limiting, not per-IP. See `nginx_per_user_rate_limit_task_card.md`.

## Scope

- [ ] Classify all API endpoints (read/write/auth/export)
- [ ] Configure nginx zones for each class
- [ ] Add location blocks for write/export patterns
- [ ] Test: write API rate limit triggers before read API
- [ ] Document API classification in user guide

## Prerequisites

- Per-user rate limit (task card: nginx_per_user_rate_limit)

## Related

- Per-user rate limit (prerequisite)
- Blazor bootstrap endpoint (complementary — reduces total API calls)
