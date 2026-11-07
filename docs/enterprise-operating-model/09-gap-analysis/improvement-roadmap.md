# Improvement Roadmap — PROPOSED (awaits G4 sign-off)

> Prioritized by replication impact × risk reduction. Everything here is recommendation, not approved work. `64789992`.

| # | Item | Addresses | Effort | Phase-fit |
|---|---|---|---|---|
| 1 | Order routing dead-letter monitor + replay (CTRL-009) | RISK-001, GAP-004 | S | infra — unblocks safe scale |
| 2 | KYC retention policy + access audit (CTRL-010) | RISK-005, GAP-012 | S | compliance hygiene |
| 3 | HTX member registration portal (GAP-003) | replication goal | M | F4 completion — highest strategic value |
| 4 | Enumerate `vanan.shoperp.*` subjects + document (GAP-010) | traceability completeness | XS | twin hygiene |
| 5 | Sync-drift detection metric (RISK-004, KPI-005) | replica integrity | M | observability |
| 6 | AI degrade observability (GAP-011) | AI boundary ops | S | vanan-ai pilot hardening |
| 7 | Metering client consolidation decision (GAP-007) | maintainability | S | tech-debt call — document either way |
| 8 | Sales lead/CRM tooling (GAP-001) | F4 human layer | M | after core items |
| 9 | Rename/document dual recommended endpoints (GAP-009) | confusion removal | XS | trivial |
| 10 | Marketplace/procurement scope decision (GAP-005/006) | capability gaps | owner | strategic decision, not dev task |

**Suggested batching:** #1+#2 (risk fixes) → #3 (replication) → #4+#5+#6 (observability batch) → rest per owner priority.
