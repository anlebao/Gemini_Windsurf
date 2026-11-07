# Task EOM-P0b — Twin Scaffolding: Registries, Schemas, Validator, Conventions

> **Status:** ✅ DONE 2026-11-07 — deliverables landed; chờ G0 review
> **Gate:** G0 — master plan `docs/AI/plans/enterprise-operating-model-master-plan.md` · SRS v2.1 §11, §12, §4.4, §5.2/5.3, §6.2/6.4/6.6, §9.1
> **Song song:** EOM-P0 (discovery) — card này là tooling, không cần đợi P0.

## Objective

Dựng khung machine-readable của Digital Twin TRƯỚC khi data đổ vào: cây thư mục §12, 13 file YAML registry rỗng-có-header, JSON Schema từng registry, validator script, và conventions doc (classDef, evidence format, ID rules). Không viết narrative content — đó là việc của P1+.

## Deliverables

```
docs/enterprise-operating-model/
  <full tree theo SRS §12 — 12 dirs + placeholder README mỗi dir>
  00-overview/glossary.md                  # thuật ngữ + ID conventions + confidence scale
  00-overview/diagram-conventions.md       # classDef snippet + swimlane rule + evidence format
  registries/
    actors.yaml  organizations.yaml  capabilities.yaml  processes.yaml
    activities.yaml  components.yaml  apis.yaml  data-entities.yaml
    rules.yaml  controls.yaml  risks.yaml  kpis.yaml  gaps.yaml
    schema/<tên tương ứng>.schema.json     # 13 schema files
scripts/validate-twin.ps1                  # validator §11.3 — 6 checks
```

## Requirements

### 1. YAML registry format

- Mỗi file: header comment (registry name, ID prefix, schema version) + `entries:` list.
- ID theo §11.1: `ACTR ORG CAP PROC ACTV SYS API DATA RULE CTRL RISK KPI GAP` — format `PREFIX-\d{3,}`.
- Required fields per registry theo SRS: actor=§4.2 (11 attrs), activity=§5.2 (18 fields + confidence + KPI ref), process=§6.1+§6.6 (`variability` ∈ CORE/CONFIGURABLE/VARIANT + `parentRef` bắt buộc khi VARIANT), component=§7.2 (12 fields), data entity=§8.1, control/risk từ §10, gap=§9.3 classes enum, kpi tự do tối thiểu {id,name,formula?,target?,evidence}.
- Mọi entry có `status` (ACTIVE/PROPOSED/RETIRED — §11.1 RETIRED rule) + `evidence` + `confidence` (VERIFIED/INFERRED/ASSUMED — §5.2).

### 2. Validator — 6 checks (SRS §11.3)

1. ID format + uniqueness per registry.
2. Required fields per schema.
3. Referential integrity: mọi `XxxRef` resolve; **orphan VARIANT process (không có parentRef tới CORE) = ERROR** (§6.6).
4. Enum values: executionType, confidence, variability, status, actorType, gapClass.
5. Evidence format khớp `path@commit` hoặc `path#L` (§9.1).
6. Coverage warnings: activity thiếu owner / thiếu software link → **warning** (không error — đó là finding hợp lệ).

Exit code: 0 khi 0 error (warning được phép, in ra). Chạy được standalone: `scripts/validate-twin.ps1`.

### 3. Conventions doc (`diagram-conventions.md`)

- classDef snippet copy-paste (§5.3): `human blue, auto green, hybrid amber, external grey, error red, ai green+stroke-dasharray:5 5`.
- Swimlane subgraph rule §6.2 + "không mix convention trong 1 diagram".
- sequenceDiagram chỉ cho technical interaction.
- Evidence format §9.1 + STALE flag rule.
- Decompose-first rule §5.1 (nhắc lại cho diagram authors).

### 4. Glossary

- Định nghĩa: actor types (§4.1), execution types, confidence, critical process (6 tiêu chí §6.5), variability classes, system-of-record vs replica, advisory-vs-executing (§7.4).

## Validation

- `scripts/validate-twin.ps1` chạy green trên registries rỗng (0 entries = 0 error).
- Thử entry mẫu sai (bad ID, orphan VARIANT, thiếu evidence) → validator báo đúng lỗi — ghi kết quả test vào card.
- Mermaid snippet trong conventions render được.

## Deviation / lưu ý

- `scripts/validate-twin.ps1` là documentation tooling — được phép tạo mới; KHÔNG đụng `scripts/` hiện có trừ khi cần tham chiếu.
- CI wiring (chạy validator trong workflow) chỉ **đề xuất** ở P5, không implement ở card này (tránh động CI khi chưa có data).

## Acceptance

- [ ] Tree §12 đầy đủ 12 dirs + registries/
- [ ] 13 YAML + 13 schema files
- [ ] Validator 6 checks hoạt động, test-case evidence trong card
- [ ] conventions + glossary đủ 4 mục trên
- [ ] Card handoff note cho P1 (schema quyết định fields nào P1 phải điền)
