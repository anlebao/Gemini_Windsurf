# Task EOM-P5 — Digital Twin Packaging & Final Validation

> **Status:** ✅ DONE 2026-11-07 — twin packaged; G5 report delivered
> **Gate:** G5 — SRS v2.1 §11, §12.1, §13, §15 (AC-01..14), §17, §14-P5
> **Depends:** EOM-P4 + **G4 owner sign-off**

## Objective

Hoàn thiện Digital Twin layer: chuẩn hóa identifiers, hoàn thiện cross-references, finalize registries, maintenance rules, và sản xuất validation report theo đúng 14 acceptance criteria + 15-artifact checklist §17.

## Deliverables

```
10-digital-twin/
  digital-twin-schema.md       # schema overview (mô tả các YAML schemas)
  traceability-matrix.md       # FINAL — 2 directions §11.2
  model-index.md               # master index toàn bộ twin
11-validation/
  validation-report.md         # AC-01..14 từng mục + coverage claim có căn cứ
06-governance/ (finalize nếu G4 có feedback)
Maintenance rules doc — §12.1 2-tier → hướng dẫn cụ thể + đề xuất validator vào CI/pre-push (proposal, không tự ý wire)
```

## Requirements — checklist

| # | Task | SRS ref |
|---|---|---|
| 1 | Standardize identifiers — audit toàn bộ registries, fix lệch format trước validator | §11.1 |
| 2 | Cross-references hoàn chỉnh 2 chiều §11.2 | §11.2 |
| 3 | Traceability matrix final: Business→Process→Activity→Software→API→DB + ngược lại | §11.2, §17#12 |
| 4 | Model index: navigation entry point cho business owner/architect/dev/HTX operator | §17 |
| 5 | Maintenance rules doc: 2-tier triggers §12.1 + hướng dẫn update + proposal CI wiring validator | §12.1 |
| 6 | **Validation report theo từng AC-01..14** — mỗi AC: PASS/PARTIAL/FAIL + evidence | §15 |
| 7 | Artifact checklist §17 (15 items ↔ D01–D06) — tick từng cái | §17 |
| 8 | NFR audit NFR-01..10 — đặc biệt NFR-07 (no secrets), NFR-09 (unknowns explicit), NFR-10 (AI boundary) | §13 |
| 9 | Coverage claim: chỉ claim phần đã cover, liệt kê unknown/inaccessible; không claim 100% | AC-01, DoD |
| 10 | Diagram consistency pass: classDef đúng chuẩn, mọi mermaid render, references resolve | AC-11, NFR-05/06 |
| 11 | Final report sections: completed/incomplete deliverables, unverified areas, business questions, technical gaps, next steps | AC-14 |
| 12 | Expansion backlog handoff: X1–X4 ready với data backbone đã có | Master plan §3 |

## Validation

- `validate-twin.ps1` → **0 errors** (gate cứng AC-11 mở rộng).
- Self-review: đọc lại twin như "HTX operator mới" — index có dẫn được không?

## Acceptance

- [ ] Validator 0 errors, mermaid render all, references resolve
- [ ] Validation report AC-01..14 + artifact checklist tick
- [ ] G5 package → owner acceptance → **Definition of Done** theo SRS §17
