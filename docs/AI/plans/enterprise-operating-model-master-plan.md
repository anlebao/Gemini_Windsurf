# MASTER PLAN: ENTERPRISE OPERATING MODEL & DIGITAL TWIN (`docs/enterprise-operating-model/`)

> **Ngày:** 2026-11-07 · **Trạng thái:** 🟡 DRAFT — chờ owner approve (chưa bắt đầu Phase 0)
> **SRS:** `docs/requirements/srs__Digital Twin__Modeling_EnterpriseOperating.md` **v2.1** (commit SRS tại `64789992` — anchors §2.3 re-verify bắt buộc)
> **Bản chất:** Documentation-only project — KHÔNG sửa application code/schema/API/config ở mọi phase (SRS §16.2, AC-12)
> **Chiến lược thực thi:** phase nhỏ → gate → duyệt → tiếp · 2 track song song F4 ∥ F1 · registry machine-readable từ đầu · mọi claim AS-IS có evidence `path@commit`

---

## 1. MỤC TIÊU

Build Vạn An Operating Model — lớp tài liệu Digital Twin traceable end-to-end (Business → Process → Activity → Software → API → Database), version-controlled, làm chuẩn nhân rộng cho hàng nghìn HTX. 6 deliverables D01–D06, 15 artifacts (SRS §17), 12+1 registries machine-readable.

**Tại sao trước khi scale:** SRS §1.3 — twin là replication kit cho HTX expansion, không phải documentation đẹp-cho-có.

---

## 2. QUYẾT ĐỊNH ĐÃ CHỐT (từ SRS v2.1 — không mở lại)

| # | Quyết định | SRS ref |
|---|---|---|
| D1 | Evidence format `path@commit` (+ `#L` kèm note decay); STALE flag khi không re-verify được | §9.1 |
| D2 | Confidence scale: VERIFIED / INFERRED / ASSUMED | §5.2 |
| D3 | Execution types: MANUAL / AUTOMATED / HYBRID + **decompose-first rule** (HYBRID hiếm, inseparable atomic interaction only) | §5.1 |
| D4 | Critical process = 6 tiêu chí | §6.5 |
| D5 | Swimlane flowchart bắt buộc cho critical business process (subgraph lanes); sequenceDiagram chỉ cho technical interaction; không mix trong 1 diagram | §6.2 |
| D6 | classDef chuẩn: `human` blue · `auto` green · `hybrid` amber · `external` grey · `error` red · `ai` green+dashed | §5.3 |
| D7 | Variability classes: CORE / CONFIGURABLE / VARIANT (VARIANT phải ref parent CORE + chỉ ghi deltas; orphan VARIANT = validator error) | §6.6 |
| D8 | Registry = YAML + JSON Schema + validator (`scripts/validate-twin.ps1`); Markdown=narrative, Mermaid=visuals — KHÔNG build platform mới | §11.3 |
| D9 | ID scheme 13 prefix; ACTR (actor) vs ACTV (activity); ORG không dual-register; RETIRED không reuse | §11.1, §4.4 |
| D10 | AI AGENT = actor subtype + §7.4 boundary (advisory≠executing, authoritative source, tenant isolation, approval gate, degrade, metering) | §4.1, §7.4 |
| D11 | MVP order: **F4 ∥ F1 parallel → F2+F3 → F6 → F5 → F7+**; F2+F3 start sau khi ≥1 track qua gate đầu | §3.1 + owner decision 2026-11-07 |
| D12 | TO-BE = PROPOSED → APPROVED chỉ sau Gate 4 owner sign-off | §9.2 |
| D13 | Anchors §2.3 = re-verification checklist, KHÔNG thay Phase 0; per-component verification + coverage status; drift → evidence register | §2.3 |
| D14 | Twin indexes existing docs, KHÔNG duplicate/absorb | §16.7 |
| D15 | Maintenance triggers 2 tier (Mandatory business-visible / Conditional internal) | §12.1 |

---

## 3. EXECUTION MODEL — 2 TRACK + BACKBONE

```
Track A (F4): Merchant onboarding → HTX → tenant provisioning → ShopInstance → feature activation
Track B (F1): KhachLink checkout → Gateway PG → NATS → ShopERP → status sync back
Backbone  : actors/orgs/capabilities + software/data inventory + registries/tooling (dùng chung 2 track)
```

| Gate | Sau | Owner review | Evidence nộp |
|---|---|---|---|
| **G0** | EOM-P0 + P0b | Coverage map + anchor re-verification + dedup decision | repo inventory, anchors status table, docs index, evidence register, unresolved questions |
| **G1** | EOM-P1 | Actor/org/capability model | 3 registries + responsibility matrix + context diagram |
| **G2** | EOM-P2a + P2b | AS-IS process model 2 track (process inventory + representative diagrams) | process catalog F4+F1, swimlanes, execution matrix |
| **G3** | EOM-P3 | Software/data traceability | component map, data flows, business→code matrix |
| **G4** | EOM-P4 | **TO-BE sign-off** (PROPOSED→APPROVED) + gap/risk/roadmap | gap register, risk register, roadmap, TO-BE drafts |
| **G5** | EOM-P5 | Full package + acceptance report | twin complete, validator green, validation report |

**Expansion (sau khi ≥1 track qua G2):** X1 (F2+F3) → X2 (F6) → X3 (F5/AI) → X4 (F7+) — mỗi expansion chạy lại pattern P2→P3→P4 trong phạm vi slice, update registries (không viết lại backbone).

**Hard rules mọi card:** §16.1–§16.15 — không đụng code; unknown → ghi question không bịa; drift → record không rewrite anchor; mermaid phải render; evidence format bắt buộc.

---

## 4. REQUIREMENT COVERAGE MATRIX — chống rơi requirement

Mọi section của SRS v2.1 map vào card. **Nếu card nào bị cắt, phải chuyển coverage sang card khác trước khi xóa.**

| SRS § | Requirement (tóm tắt) | Card cover |
|---|---|---|
| §1.1–1.3 | Objective, scope, strategic rationale | Master plan §1 (context, không phải deliverable riêng) |
| §2.1 | Stakeholder list 12 + discover thêm | **P1** (actor/org registries) |
| §2.2 | 21 capability groups | **P1** (capability map) · F7+ → **X4** |
| §2.3 | Anchors re-verify + verification/coverage status + drift | **P0** |
| §3.1 | MVP slice F4∥F1→F2+F3→F6→F5→F7+ | Master plan §3 · **P2a, P2b, X1–X4** |
| §4.1 | Actor classification 6 loại (gồm AI AGENT, HYBRID role) | **P0b** (schema enum) + **P1** |
| §4.2 | Actor registry 11 attributes | **P0b** (schema) + **P1** (data) |
| §4.3 | RACI + legal-responsibility rule | **P1** |
| §4.4 | Org registration rule (không dual-register) | **P0b** (schema+validator) + **P1** |
| §5.1 | MANUAL/AUTOMATED/HYBRID + decompose-first | **P0b** (conventions) + **P2a/P2b** (apply) |
| §5.2 | Activity metadata 18 fields + confidence scale | **P0b** (activity schema) + **P2a/P2b** |
| §5.3 | classDef color conventions (6) | **P0b** (shared mermaid snippet) |
| §6.1 | Process hierarchy L0–L4 unique IDs | **P1** (L0–L2) + **P2a/P2b** (L3–L4) |
| §6.2 | Diagram types table + swimlane mandatory + no-mix | **P0b** (conventions doc) + **P2a/P2b** |
| §6.3 | Flowchart 12 elements, no happy-path-only | **P2a/P2b** (+ X1–X4) |
| §6.4 | Mermaid standards + render validation | **P0b** + mọi diagram card |
| §6.5 | Critical process 6 criteria | **P0** (classify) + **P2a/P2b** |
| §6.6 | CORE/CONFIGURABLE/VARIANT + parent-ref rule | **P0b** (schema field) + **P2a/P2b** |
| §7.1 | Software inventory (all categories) | **P0** (initial) + **P3** (full) |
| §7.2 | Component mapping 12 fields | **P0b** (schema) + **P3** |
| §7.3 | Business→code traceability chain + UNKNOWN marking | **P3** + **P5** (matrix) |
| §7.4 | AI boundary 6 attributes | **P3** (component-level) + **X3** (deep) |
| §8.1 | Data flow mapping 10 fields | **P0b** (schema) + **P3** |
| §8.2 | Data classification 12 categories | **P3** |
| §8.3 | DFD elements + system-of-record distinction | **P3** |
| §8.4 | AI data rules (knowledge class, Confidential, no secrets) | **P3** + **X3** |
| §9.1 | Evidence hierarchy 7 + format + STALE + discrepancy record | **P0** (evidence register) + mọi card |
| §9.2 | TO-BE 8 items + PROPOSED→APPROVED | **P4** |
| §9.3 | Automation gap table + 10 gap classes (gồm AI boundary gap) | **P4** |
| §10 | Governance 10 items + 7 questions/critical process + boundary | **P4** (06-governance/ outputs) |
| §10.1 | Control anchors (AccountingEntry, TenantId, metering, AI gate, DevToken) | **P4** |
| §10.2 | AI governance (knowledge owner, accountability, quota-as-control) | **P4** + **X3** |
| §11.1 | 13 ID prefixes + RETIRED rule | **P0b** |
| §11.2 | Bidirectional traceability | **P0b** (schema refs) + **P5** (matrix) |
| §11.3 | YAML + JSON Schema + validator 6 checks + scope honesty | **P0b** |
| §12 | Doc structure 12 dirs + registries/ | **P0b** |
| §12.1 | Maintenance triggers 2-tier | **P5** (maintenance rules doc + validator CI wiring proposal) |
| §13 NFR-01..10 | Accuracy/Traceability/Maintainability/VC/Readability/Consistency/Security/Extensibility/Evidence/AI-boundary | Cross-cutting — checklist trong mọi card + **P5** final audit |
| §14 P0–P5 | Phase tasks/outputs/gates | **P0, P0b, P1, P2a, P2b, P3, P4, P5** (1:1) |
| §15 AC-01..14 | Acceptance criteria | **P5** (validation report từng AC) + per-card acceptance |
| §16 | 15 execution rules + phase gates | Master plan §3 + mọi card header |
| §17 | 15 artifacts ↔ D-mapping + DoD | **P5** (packaging checklist) |

---

## 5. TASK CARDS (`docs/AI/tasks/enterprise_operating_model/`)

| Card | Phase/Gate | Scope | Phụ thuộc |
|---|---|---|---|
| `task_eom_p0_discovery` | P0 → G0 | Repo inventory · anchors re-verify (per-component status) · DB/migrations/APIs/workers · existing-docs index + dedup · evidence register + unresolved questions + coverage map | — |
| `task_eom_p0b_scaffold` | P0 → G0 | `docs/enterprise-operating-model/` tree · 13 YAML registries + JSON Schemas · validator script · glossary + classDef/conventions doc · ID registry rules | — (∥ P0) |
| `task_eom_p1_actors` | P1 → G1 | Actor + Org registries · capability map 21 nhóm · responsibility matrix (RACI) · ecosystem context diagram · L0–L2 process hierarchy | P0, P0b |
| `task_eom_p2a_f4_processes` | P2 → G2 | **Track A F4:** onboarding→HTX→tenant→ShopInstance→feature activation · process catalog + swimlanes + activity metadata + variability class | P1 |
| `task_eom_p2b_f1_processes` | P2 → G2 | **Track B F1:** order lifecycle Option C end-to-end · cùng deliverables | P1 (∥ P2a) |
| `task_eom_p3_traceability` | P3 → G3 | Software inventory full · component map · integration map · data-flow catalog + ownership + entity map · business→code matrix cho F4+F1 | P2a, P2b |
| `task_eom_p4_tobe_gap` | P4 → G4 | TO-BE (PROPOSED) · automation gap register · risk register · governance outputs (authz/approval/audit) · roadmap | P3 |
| `task_eom_p5_twin` | P5 → G5 | Registries finalize · traceability matrix + model index · maintenance rules · validation report AC-01..14 | P4 + G4 sign-off |
| `task_eom_x0_drift_detector` | Tooling | Script scan code→registry drift report (controllers/APIs, Domain entities, NATS subjects, voucher registrations) — báo "code có, twin chưa ghi"; KHÔNG auto-ghi | P0b |
| `task_eom_x1_f2f3` | Expansion | Metering + Voucher posting flows (P2→P4 pattern trong slice) | ≥1 track qua G2 |
| `task_eom_x2_f6_loyalty` | Expansion | Loyalty accrual/redemption | X1 |
| `task_eom_x3_f5_tenantai` | Expansion | vanan-ai deep model — §7.4 boundary đầy đủ, knowledge lifecycle, degrade paths | X1 |
| `task_eom_x4_f7plus` | Expansion | Booking, CRM, marketplace, procurement, HTX governance + capabilities còn lại | X2, X3 |
| `task_eom_x5_docs_portal` | Post-G5 (optional) | mkdocs-material static HTML portal cho twin (search, nav, mermaid render, YAML→tables) — host nginx/GitHub Pages | P5 |

**X0 note:** drift detector chỉ *phát hiện* lệch (code có mà registry chưa ghi) — không auto-ghi, vì registry chứa business meaning code không khai báo. Output = report cho người duyệt.

---

## 6. ARTIFACT ↔ DELIVERABLE CHECKLIST (SRS §17 — kiểm ở G5)

| # | Artifact | D | Card sinh ra |
|---|---|---|---|
| 1 | Ecosystem Context Diagram | D01 | P1 |
| 2 | Business Capability Map | D01 | P1 |
| 3 | Organization & Actor Map | D01 | P1 |
| 4 | Human vs Software Execution Map | D02 | P2a/P2b |
| 5 | AS-IS Operating Model | D02 | P2a/P2b + P3 |
| 6 | TO-BE Operating Model | D03 | P4 |
| 7 | Swimlane diagrams critical processes | D02/D03 | P2a/P2b (+X) |
| 8 | Software Architecture Diagram | D04 | P3 |
| 9 | Data Flow Architecture Diagram | D04 | P3 |
| 10 | Governance & Responsibility Model | D02/D05 | P1 + P4 |
| 11 | Automation Gap Register | D05 | P4 |
| 12 | Business→Code Traceability Matrix | D04/D06 | P3 + P5 |
| 13 | Digital Twin Registry (12+1) | D06 | P0b + fill qua P1–P4 |
| 14 | Improvement Roadmap | D05 | P4 |
| 15 | Final Validation Report | D06 | P5 |

---

## 7. RỦI RO ĐÃ BIẾT

| Risk | Mitigation |
|---|---|
| F4 ít implement → nhiều ASSUMED | Chấp nhận — discovery value; ghi rõ trong validation report |
| Docs hiện có rất nhiều (docs/ ~70 mục) → dedup phức tạp | P0 deliverable riêng: docs index + quyết định index-vs-supersede từng cụm |
| Anchor bias (SRS 0.1.3) | P0 bắt buộc per-component re-verify + coverage status |
| Registry YAML lớn → merge conflict | 1 file/registry; ID sort; không refactor ID |
| Validator script mới trong scripts/ | Documentation tooling — được phép; KHÔNG đụng application code |
| Scope creep sang modeling F7+ sớm | Hard rule: chỉ expansion sau gate; capability map P1 chỉ ở mức map, không model sâu |
