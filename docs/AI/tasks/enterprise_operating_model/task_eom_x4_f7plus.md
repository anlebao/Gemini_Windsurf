# Task EOM-X4 — Expansion: F7+ Remaining Capabilities

> **Status:** ✅ DONE 2026-11-07 (coverage-level) — F7+ status tabled in as-is doc; detail backlog = drift-report.md (216 findings)
> **Gate:** sau X2, X3 · SRS v2.1 §2.2, §3.1
> **Pattern:** P2→P4 repeat per capability; owner chọn thứ tự tại kickoff

## Objective

Phủ các capability groups §2.2 chưa model: Booking (cap 9), CRM (5), Local commerce (7), CTV/delivery collaboration (10), Supplier/procurement (13), Marketplace (14), Notifications (15), Reporting/analytics (16), SaaS subscription billing phần còn lại (17), HTX financial/operational mgmt (20) + capabilities phát hiện thêm ở P0/P1.

## Scope notes

- Thứ tự trong F7+ do owner pick — gợi ý: HTX governance trước (replication goal), rồi Booking (đã có SRS v1.1), CRM.
- Mỗi capability: check implemented-vs-planned trước (AC-07) — nhiều cái trong §2.2 có thể là ASSUMED/planned → gap register béo là OK.
- VARIANT processes phát sinh nhiều ở slice này (per business type) — enforce §6.6 parentRef.

## Deliverables

`03-processes/ += per-capability` · `04/05 +=` · `06/09 +=` · registries +=

## Acceptance

- [ ] Mỗi capability có coverage status rõ (modeled / planned-only / unknown)
- [ ] Validation report update — AC-01 coverage mở rộng
- [ ] Validator green
