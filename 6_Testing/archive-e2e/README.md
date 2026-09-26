# archive-e2e — One-off Diagnostic / Debug Specs (P2, 2026-09-26)

These specs were written for ONE-OFF diagnostics / RV windows / button-debugging.
They are **archived, not deleted** — `git mv` preserved history, and
`playwright.config.ts` excludes this folder via `testIgnore: 'archive-e2e/**'`.

## Why archived

- 46 specs were one-off (diag*, debug-*, gtm-*, rv-*, verify-*, khachlink-pwa-*-debug/verify,
  polling-interval, minimal-flow, auth-fix) — they bloated tier-full regression runs,
  added noise, and mixed diagnostics with real feature coverage.
- 50 stable feature specs remain in `e2e-tests/` (order/accounting/community/loyalty/
  realtime/payment/...).

## Restore a spec

If a diagnostic spec becomes a real regression candidate, bring it back:

```bash
git mv 6_Testing/archive-e2e/<spec>.spec.ts 6_Testing/e2e-tests/<spec>.spec.ts
```

Then update its title/selectors to stable form (data-testid over text/css), and
re-run it via the tier-smoke/tier-golden/tier-full projects.

## Inventory by family

| Family | Files | Purpose |
|---|---|---|
| diag* | 11 | SignalR/console/marker/reconnect diagnostics |
| debug-* | 2 | map + panel HTML debugging |
| gtm-* | 2 | GTM tag audit/demo |
| rv-* | 14 | one-off RV windows (batch2-4, QR, R2, currency, issues 142/143) |
| verify-* | 9 | VanAnButton modal/onclick debugging |
| khachlink-pwa-* | 5 | PWA install/manifest/offline debug |
| khachlink-polling-interval | 1 | polling interval measurement |
| khachlink-minimal-flow | 1 | superseded by khachlink-full-order-flow |
| auth-fix | 1 | one-off auth debugging |
