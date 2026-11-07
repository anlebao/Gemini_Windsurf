# Risk Register — P4

> Data source: `registries/risks.yaml` (6 RISK). Controls → `registries/controls.yaml`.

| RISK | Category | Severity | Control coverage |
|---|---|---|---|
| RISK-001 Order routing miss | OPERATIONAL | HIGH | ⚠️ CTRL-009 PROPOSED — no existing control |
| RISK-002 Cross-tenant AI leak | TENANT_ISOLATION | CRITICAL | ✅ CTRL-004 + CTRL-005 (signed ctx + approval gate) |
| RISK-003 Metering fail-open on caller | OPERATIONAL | MEDIUM | ✅ CTRL-003 (service-side deny) |
| RISK-004 SQLite/PG drift | OPERATIONAL | MEDIUM | ❌ none — monitoring gap |
| RISK-005 KYC PII retention | COMPLIANCE | HIGH | ⚠️ CTRL-010 PROPOSED — no existing control |
| RISK-006 Endpoint conflation | OPERATIONAL | LOW | ❌ none — doc/naming fix |

## Notes

- Two HIGH risks lack implemented controls — both land on roadmap (CTRL-009/010 are PROPOSED pending owner sign-off).
- RISK-002 is the best-controlled CRITICAL risk — AI boundary design carries real weight; keep it evidenced in twin as the reference pattern.
