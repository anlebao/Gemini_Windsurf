# Audit Controls — P4

> What evidence is retained per critical process (SRS §10). `64789992`.

| Control | Mechanism | Evidence retained | Status |
|---|---|---|---|
| CTRL-001 AccountingEntry immutability | append-only + reversal entries | full journal history | VERIFIED |
| CTRL-002 Tenant isolation | TenantId on every entity | per-tenant data boundary | VERIFIED |
| CTRL-003 Metering deny | quota check before metered op | usage events (metered) | VERIFIED |
| CTRL-004 AI knowledge gate | pending_review→approved | approval state per doc | VERIFIED (SRS) |
| CTRL-005 AI signed tenant ctx | HMAC ctx + scopes + allowlist | caller audit | VERIFIED |
| CTRL-006 Internal API key | X-Internal-Api-Key | internal call auth | VERIFIED |
| CTRL-007/008 DevToken gate | secret unset by default; RV-window only | endpoint 404 default | VERIFIED (rules) |
| ACTV audit flags | activities marked auditLog=true in registry | app/tenant/order transitions | PARTIAL — per-activity verify at X-cards |
| MarkAsHtx audit | idempotent + audited per code comment | HTX transitions | VERIFIED |
| Order lifecycle events | NATS events as transition log | created/status/payment subjects | VERIFIED (event-sourced trail) |

## Gaps in audit coverage (→ gaps.yaml)

- GAP-004: routing-miss has NO audit/alert — silent failure.
- GAP-008: order cancel reason capture unverified.
- GAP-012: KYC doc access audit absent.

## Rule

Reversal/correction authority: accounting → reversal entries only (never edit); tenant lifecycle → suspend/reactivate are audited transitions; order cancel path needs verification.
