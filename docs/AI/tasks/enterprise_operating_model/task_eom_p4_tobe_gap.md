# Task EOM-P4 — TO-BE, Gap Analysis & Governance Model

> **Status:** ✅ DONE 2026-11-07 — P4 package ready; CHỜ G4 owner sign-off
> **Gate:** G4 — **owner sign-off bắt buộc** trước P5 (SRS §14-P4) · SRS §9.2/9.3, §10 (all), §14-P4
> **Depends:** EOM-P3

## Objective

So sánh AS-IS (F4+F1) với mục tiêu nghiệp vụ → TO-BE PROPOSED, automation gap register, risk register, governance model và improvement roadmap. **Mọi TO-BE item ở status PROPOSED cho tới khi owner duyệt tại G4** (§9.2, AC-08).

## Deliverables

```
08-to-be/target-operating-model.md     # TO-BE workflows PROPOSED + rationale
09-gap-analysis/
  automation-gap-register.md           # GAP- table §9.3
  risk-register.md                     # RISK- table
  improvement-roadmap.md               # prioritized
06-governance/
  authorization-map.md                 # roles × permissions × tenant scope
  approval-matrix.md                   # critical process → who approves
  audit-controls.md                    # controls + evidence retained
registries/gaps.yaml risks.yaml controls.yaml kpis.yaml  # fill
```

## Requirements — checklist

| # | Task | SRS ref |
|---|---|---|
| 1 | TO-BE per covered process: retain-manual / automate / human-approval / new controls / new capabilities / new integrations / required data / responsibility changes (đủ 8 mục §9.2) | §9.2 |
| 2 | TO-BE diagrams — PROPOSED watermark/status, không trình bày như đã implement | §9.2, AC-08 |
| 3 | Automation gap table per §9.3 format + **10 gap classes** (gồm AI boundary gap) per gap | §9.3 |
| 4 | Mọi MANUAL activity có automation assessment; KHÔNG mặc định "manual nào cũng nên auto" | §9.3, AC-09 |
| 5 | Gap classification: missing process/feature/integration/audit/control/dup/inconsistent-rule/data/security/AI-boundary/unknown | §9.3 |
| 6 | Governance: 10 investigation items §10 + **7 questions per critical process** (initiates/executes/approves/receives/legally-responsible/reverses/audit-evidence) | §10 |
| 7 | Control anchors §10.1 mapped thành CTRL-: AccountingEntry immutable, TenantId enforcement, metering deny, AI knowledge gate, DevToken rule | §10.1 |
| 8 | AI governance §10.2: knowledge owner per tenant, accountability rule, quota-as-control | §10.2 |
| 9 | Risk register: operational, security, tenant-isolation, fraud, compliance — mỗi RISK link CTRL (hoặc ghi "no control") | §10, §11 |
| 10 | Approval matrix + authorization map — merchant/HTX/Vạn An boundaries đặc biệt chú ý; legal responsibility KHÔNG suy từ software permissions | §10, §4.3 |
| 11 | Improvement roadmap: prioritized (impact × effort), link GAP-/RISK- | §14-P4 |
| 12 | KPI registry seed: KPIs đo twin goals (automation rate, approval coverage) + per-activity KPI links | §5.2, §11 |

## Validation

- Validator green; mọi GAP- có class; mọi RISK- có link CTRL hoặc flag.
- PROPOSED/APPROVED status rõ ràng trong mọi TO-BE artifact.

## Acceptance

- [ ] 4 dirs + 4 registries fill
- [ ] G4 package cho owner: TO-BE drafts + gap/risk/roadmap — **chờ sign-off**
- [ ] Unapproved items giữ PROPOSED sau gate
