# Task EOM-X3 — Expansion: F5 Vạn An AI (`vanan-ai`) Deep Model

> **Status:** ✅ DONE 2026-11-07 — F5 deep model: 10 ACTV, 6 RULE, 2 CTRL, 2 KPI; swimlane + AC-13 boundary table; validator 0 err
> **Gate:** sau X1 · SRS v2.1 §3.1, §7.4, §8.4, §10.2, NFR-10, AC-13
> **Pattern:** P2→P4 repeat + AI boundary deep-dive

## Objective

Model đầy đủ vanan-ai theo §7.4 boundary — phần component-level đã có từ P3, card này model process-level: storefront widget → Gateway signed ctx → intent/policy → SQL+RAG retrieval → grounded answer → metering `ai.chat.message` → product suggestion → cart (KHÔNG tạo order).

## Scope notes — AI boundary bắt buộc (§7.4, AC-13)

- Advisory vs executing per activity; authoritative source per data item (structured > vector khi conflict).
- Tenant isolation: signed ctx, allowlist, cross-tenant leak risk → RISK- + CTRL-.
- Knowledge lifecycle: ingest → `pending_review` → `approved` (HYBRID approval theo đúng định nghĩa mới — decompose: ingest=AUTOMATED, review=MANUAL) → retrieval → staleness TTL.
- Degrade paths: metering deny → structured-only / refuse per policy; `ai.chat.degraded` MeterOnly.
- Knowledge classification: Public vs Confidential → Approved Public Fact Extraction path.
- AI governance §10.2: knowledge owner per tenant, accountability chain, quota-as-control.
- Cross-ref `docs/AI/plans/tenant-ai-master-plan.md` + SRS `van_an_tenant_ai_srs_v1.md` — twin INDEXES, không duplicate (§16.7).

## Deliverables

`03-processes/ += f5-*` · `06-governance/ += AI section` · `09-gap-analysis/ += AI boundary gaps` · registries += (ACTR- AI agents đã có từ P1 — enrich)

## Acceptance

- [ ] AC-13 pass cho mọi AI activity (advisory/executing + SoT + isolation + metering link)
- [ ] Knowledge lifecycle + degrade paths có swimlane/sequence
- [ ] RISK-/CTRL- entries cho tenant isolation
- [ ] Validator green
