# Automation Gap Register — F4 + F1 coverage

> Data source: `registries/gaps.yaml` (12 GAP). Status per SRS §9.3 — findings, not commitments. `64789992`.

| GAP | Finding | Class | Priority |
|---|---|---|---|
| GAP-001 | Sales acquisition runs fully manual — no lead/CRM tooling | MANUAL_AUTOMATABLE | MEDIUM |
| GAP-002 | KYC L4 manual — automate checklist, keep human decision | MANUAL_AUTOMATABLE (partial) | MEDIUM |
| GAP-003 | HTX member registration manual | MANUAL_AUTOMATABLE | HIGH (replication lever) |
| GAP-004 | No monitoring when order has no ShopInstance route | MISSING_AUDIT_APPROVAL | HIGH |
| GAP-005 | Marketplace capability — no code evidence | MISSING_FEATURE | LOW (owner scope) |
| GAP-006 | Procurement thin | MISSING_FEATURE | LOW |
| GAP-007 | MeteringApiClient ×4 duplicated | DUPLICATE_FUNCTIONALITY | MEDIUM |
| GAP-008 | Order cancel reason/audit capture unverified | UNKNOWN | MEDIUM |
| GAP-009 | Two `recommended` endpoints conflation | INCONSISTENT_RULE | LOW |
| GAP-010 | `vanan.shoperp.*` subjects not enumerated | UNKNOWN | MEDIUM |
| GAP-011 | AI degrade-path observability | AI_BOUNDARY | MEDIUM |
| GAP-012 | KYC doc retention undefined | SECURITY_PERMISSION | HIGH |

## Manual-activity assessment (SRS AC-09 — not everything should be automated)

| Manual activity | Assessment |
|---|---|
| Sales visit (ACTV-101) | Human stays; **support tooling** (CRM) is the gap, not the human |
| Application review (ACTV-104) | Keep MANUAL — approval authority; automate document checklist only |
| KYC L4 (ACTV-106) | Keep MANUAL — judgment; tooling assist worth it |
| Order confirm (ACTV-207) | Keep MANUAL — merchant decision |
| Kitchen prep (ACTV-208) | Keep MANUAL — physical work |
| HTX member registration (ACTV-114) | **Automatable** — form + classification rules |
