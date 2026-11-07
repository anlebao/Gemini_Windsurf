# Twin Maintenance Rules — SRS §12.1 (two-tier)

> Keeps the twin alive without doc-churn. Validator = `scripts/validate-twin.py` (run manually now; CI wiring = proposal).

## MANDATORY update (same change batch or linked task)

Business-visible changes only:

| Change | Update |
|---|---|
| New/changed business rule or transaction flow | RULE + affected ACTV/PROC + diagram |
| New voucher type (`VoucherTypeRegistry.AddVoucherType`) | ACTV + RULE + CTRL + diagram |
| Permission/role/responsibility change | authorization-map, approval-matrix, RACI |
| Tenant-boundary or SoT change | data-ownership + governance + flows |
| Integration behavior change (NATS subject, sync direction, external contract) | apis.yaml + integration-map |
| New actor-facing workflow or approval gate | process-catalog + swimlane + RACI |
| AI boundary change (new intent/knowledge class/policy, advisory→executing) | §7.4 attrs + CTRL/RISK |

## CONDITIONAL update (registry touch-up only — no diagram churn)

| Change | Update |
|---|---|
| New internal API endpoint | `apis.yaml` if referenced by modeled activity; else defer |
| Refactor/rename inside a module | update evidence refs only if component already referenced |
| New entity/table, no process impact | `data-entities.yaml` row only |
| Internal service/helper added | SYS entry only if it crosses a documented boundary |

Neither tier touched → no twin update. Honest scope: validator checks **link integrity + coverage**, not semantic equivalence vs code — semantic drift is caught by drift-detector (EOM-X0) + periodic re-verification.

## Working agreements

- Evidence always `path@commit` (or `#L` + decay note); record capture SHA in evidence-register.
- Never renumber/reuse IDs; retire = status RETIRED.
- TO-BE items: PROPOSED → APPROVED only via owner gate (G4 pattern).
- Markdown references registry IDs; restating registry facts in prose is a validator-reported smell.
- CI wiring proposal: run `validate-twin.py` + `--fail-on-drift` drift report in pre-push — **pending owner approval**, not implemented.
