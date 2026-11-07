# Task EOM-P2b — AS-IS Process Model: Track B (F1 Order Lifecycle)

> **Status:** ✅ DONE 2026-11-07 — P2 tracks modeled; chờ G2 review
> **Gate:** G2 — SRS v2.1 §5, §6 (all), §9.1, §14-P2 · Track B per §3.1
> **Depends:** EOM-P1 · **Song song:** EOM-P2a (Track A)

## Objective

Model AS-IS cho slice F1: order lifecycle end-to-end theo Option C — KhachLink checkout → Gateway tạo order trong PG (snapshot ProductName+VatRate từ client) → NATS `vanan.cloud.order.created.{shopInstanceId}` → ShopERP kitchen/POS display → status sync ngược SQLite→PG.

## Scope (điều tra thực tế)

- Checkout flow KhachLink: cart → order request (fields, pricing snapshot, payment path nếu có).
- Gateway order creator: controller → service → PG entities; ShopInstanceId routing decision.
- NATS publish/subscribe: subject pattern, outbox/retry, DLQ nếu có.
- ShopERP receive: order display POS/kitchen, status transitions, ai update (employee).
- Status sync back: SQLite→PG mechanism.
- Failure paths: NATS down, ShopERP offline, duplicate delivery, tenant routing miss.
- Payment/credit/loyalty touchpoints trong checkout (note cho X1/X2, không model sâu).

## Deliverables

```
03-processes/
  process-catalog.md            # += PROC- entries F1 (L3–L4)
  swimlane-diagrams/f1-*.md     # swimlane per critical workflow
  process-details/f1-*.md       # + sequenceDiagram cho technical handoffs (§6.2 rule 3)
registries/processes.yaml activities.yaml rules.yaml  # += F1 data
07-as-is/current-operating-model.md  # += section F1
```

## Requirements — checklist (giống P2a, áp cho F1)

| # | Task | SRS ref |
|---|---|---|
| 1 | L3–L4 hierarchy + parent L2 | §6.1 |
| 2 | Variability class mỗi PROC (order flow kỳ vọng nhiều CORE) | §6.6 |
| 3 | Swimlane critical workflows — customer/merchant/employee/Gateway/NATS/ShopERP lanes | §6.2, §6.3 |
| 4 | sequenceDiagram cho technical flow (KhachLink→Gateway→NATS→ShopERP, status back) — tài liệu bổ sung, không thay swimlane | §6.2 rule 2–3 |
| 5 | 18-field metadata mọi activity; decompose-first cho HYBRID | §5.1, §5.2 |
| 6 | Exception paths: NATS down, duplicate, routing miss, shop offline, reversal/cancel | §6.3 |
| 7 | RULE- entries (pricing snapshot rule, routing rule, retry policy…) | §11 |
| 8 | Evidence `path@commit`; discrepancy → gap draft | §9.1 |
| 9 | classDef đúng; không mix diagram conventions | §5.3, §6.2 |

## Validation

- Validator green; mermaid render.
- Sanity: order lifecycle có cả MANUAL (confirm đơn) + AUTOMATED (routing, sync) — phân bổ hợp lý.
- Re-verify anchors F1 đã check ở P0 (NATS subject, Option C) — cite lại evidence P0, không redo.

## Acceptance

- [ ] F1 catalog + swimlanes + sequence diagrams + registry data
- [ ] Exception coverage cho mọi critical workflow
- [ ] Draft gaps → P4
