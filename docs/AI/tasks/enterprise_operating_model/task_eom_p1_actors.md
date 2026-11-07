# Task EOM-P1 — Ecosystem & Actor Model

> **Status:** ✅ DONE 2026-11-07 — G1 evidence ready; chờ G1 review
> **Gate:** G1 — SRS v2.1 §4 (all), §2.1, §2.2, §6.1 (L0–L2), §14-P1
> **Depends:** EOM-P0 (findings), EOM-P0b (schemas + conventions)

## Objective

Model toàn bộ actors, organizations và capability map của ecosystem — backbone dùng chung cho cả 2 track F4/F1. Mọi entry điền vào YAML registries theo schema P0b; narrative ở Markdown trỏ vào registry IDs (§16.15).

## Deliverables

```
01-actors/
  actor-registry.md            # narrative + trỏ registries/actors.yaml
  organization-registry.md     # narrative + trỏ registries/organizations.yaml
  responsibility-matrix.md     # RACI
02-business-capabilities/
  capability-map.md            # 21 nhóm §2.2 + capabilities phát hiện thêm
03-processes/
  process-catalog.md           # L0–L2 hierarchy entries (L3–L4 ở P2a/P2b)
00-overview/ecosystem-overview.md  # context diagram (mermaid)
registries/actors.yaml organizations.yaml capabilities.yaml processes.yaml  # điền data
```

## Requirements — checklist SRS §14-P1 + §4

| # | Task | SRS ref | Notes |
|---|---|---|---|
| 1 | Business entities → `organizations.yaml` | §2.1 | 12 stakeholders seed + discover thêm từ code/docs (P0 findings) |
| 2 | Org boundaries + quan hệ (HTX↔member, Vạn An Tech↔Ecommerce) | §2.1, §4.3 | Ghi rõ legal-boundary caveat §4.3 |
| 3 | Actor registry đủ 6 loại §4.1 | §4.1 | AI AGENT: `vanan-ai` assistant + LLM parser services; EXTERNAL: Gemini API, payment, government |
| 4 | Actor attributes đủ 11 field §4.2 | §4.2 | `orgRef` trỏ ORG-, không dual-register (§4.4) |
| 5 | RACI matrix cho critical processes đã classify ở P0 | §4.3 | Responsible/Accountable/Supporting/Approval per activity-type |
| 6 | Capability map 21 nhóm §2.2 | §2.2 | `capabilities.yaml` — mỗi CAP- link actor + SYS- seed (P0) |
| 7 | L0–L2 process hierarchy | §6.1 | L0 ecosystem, L1 domains, L2 processes; PROC- IDs + `variability` field |
| 8 | Ecosystem context diagram | §14-P1 | Mermaid flowchart L0: orgs + phần mềm + external, dùng classDef chuẩn |
| 9 | Confidence + evidence mọi entry | §5.2, §9.1 | ASSUMED items → sang unresolved-questions |
| 10 | Actor "discovered in actual system" — scan code for service accounts, background workers, external callers | §2.1 | Caller keys metering/AI → SOFTWARE actors |

## Validation

- `scripts/validate-twin.ps1` → 0 error.
- Mermaid render check.
- Review pass: mọi actor có type + org hợp lệ; mọi CAP- có ít nhất 1 actor hoặc cờ "no-actor-yet" (warning hợp lệ).

## Acceptance

- [ ] 3 registries (actors/orgs/capabilities) + processes L0–L2 điền data
- [ ] RACI + context diagram + capability map hoàn thành
- [ ] Validator green; G1 report kèm coverage status
