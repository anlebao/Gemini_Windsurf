# Task EOM-X2 — Expansion: F6 Loyalty & Customer Loop

> **Status:** ✅ X2 DONE — F6: 5 ACTV + RULE-019 + GAP-015
> **Gate:** sau X1 · SRS v2.1 §3.1
> **Pattern:** P2→P4 repeat trong slice

## Objective

Model loyalty points & promotional benefits: accrual từ orders, redemption, expiry, alliance rules (`SRS — VẠN AN LOYALTY ALLIANCE.md`, `loyalty-alliance-spec.md`), CTV/customer wallet touchpoints.

## Scope notes

- Points accrual rules (per order/category/tenant), redemption paths, fraud controls.
- Cross-cutting: touches F1 (order), F3 (accounting impact nếu có), F6 customer data.
- VARIANT candidates: per-HTX loyalty policies.
- Docs đối chứng code — loyalty SRS đã audit trước (`loyalty-alliance-audit.md`), re-verify status.

## Deliverables

`03-processes/ += f6-*` · `04/05 += loyalty components + data` · `09-gap-analysis/ +=` · registries +=

## Acceptance

- [ ] Swimlane accrual + redemption + exceptions
- [ ] Loyalty rules → RULE- entries, controls → CTRL-
- [ ] Validator green; gaps fed P4-pattern
