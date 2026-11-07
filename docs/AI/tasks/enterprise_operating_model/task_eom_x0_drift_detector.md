# Task EOM-X0 — Drift Detector (code → registry diff report)

> **Status:** ✅ DONE 2026-11-07 — twin-drift-report.py live: 216 findings (23 MANDATORY), 4/6 scan sources active
> **Gate:** tooling, chạy sau P0b (cần schemas + registries tồn tại) — master plan §5
> **Nguyên tắc:** DETECT-ONLY — báo "code có mà twin chưa ghi / twin ghi mà code không còn". KHÔNG auto-ghi registry (business meaning không suy được từ code).

## Objective

Script scan một số artifact trong codebase, diff với YAML registries, xuất drift report — biến §12.1 maintenance triggers từ "kỷ luật con người" thành "có máy nhắc".

## Deliverables

```
scripts/twin-drift-report.ps1        # hoặc .py — quyết khi implement (repo đã có cả 2)
docs/enterprise-operating-model/
  11-validation/drift-report.md      # output mẫu/đầu tiên — append hoặc regenerate
```

## Requirements — scan sources

| Source | So với | Phát hiện |
|---|---|---|
| `**/*Controller.cs` routes (`[Route]`, `[Http*]`) | `apis.yaml` | API mới chưa register / API registry trỏ route đã xóa |
| `1_Shared/Domain.cs` entity classes + migrations | `data-entities.yaml` | entity mới / entity dropped |
| NATS subject literals (`vanan.cloud.*`) trong publisher/subscriber | `apis.yaml` + integration docs | subject mới / đổi |
| `VoucherTypeRegistry.AddVoucherType(...)` call sites | `activities.yaml` + `rules.yaml` | voucher type mới chưa có activity/RULE/CTRL |
| `[Authorize]`/role attributes (mức đơn giản) | `controls.yaml` + authorization-map | permission change gợi ý review |
| `TenantFeatureSettings` keys | `processes.yaml` variability=CONFIGURABLE refs | config knob mới chưa map |

**Output format (drift-report.md):** bảng `| Kind | Found in code at path@commit | Registry status | Suggested twin update |` — mỗi dòng 1 finding; phân loại MANDATORY/CONDITIONAL theo §12.1.

## Rules

- Read-only scan — không modify code, không modify registries.
- Idempotent, nhanh (<30s), chạy standalone.
- Exit code: 0 luôn (report-only) khi chạy tay; có flag `--fail-on-drift` để dùng trong CI sau này.
- False-positive tolerance: whitelist file `drift-whitelist.yaml` (vd internal helper endpoints).

## Validation

- Chạy trên repo thật sau P2a/P2b: report phải chứa ít nhất các findings đã biết (vd endpoint mới thêm gần nhất) — nếu report rỗng trong khi code vừa đổi → script sai.

## Acceptance

- [ ] Script chạy được, report có cấu trúc trên
- [ ] ≥4 trong 6 scan sources hoạt động (phần còn lại ghi TODO + lý do)
- [ ] Whitelist mechanism hoạt động
- [ ] Ghi hướng dẫn chạy trong maintenance rules (P5)
