# Hướng dẫn sử dụng — Vạn An Enterprise Operating Model (Digital Twin)

> Twin = lớp tài liệu truy vấn được của hệ sinh thái. Model captured at `64789992` (2026-11-07).

## 1. Xem tài liệu (3 cách)

| Cách | Lệnh | Phù hợp |
|---|---|---|
| **HTML portal** | `python -m mkdocs build -f docs/mkdocs-twin.yml` → `python -m http.server 8765 -d docs/twin-portal-site` | Đọc, search, xem mermaid render — **owner/HTX operator** |
| **GitHub/IDE** | Mở trực tiếp `.md` trong repo | Dev đọc nhanh, review PR |
| **Truy vấn YAML** | Đọc/grep `registries/*.yaml` | Trả lời câu hỏi có cấu trúc |

## 2. Trả lời câu hỏi thường gặp

| Câu hỏi | Nhìn vào |
|---|---|
| Ai làm activity này, manual hay auto? | `registries/activities.yaml` → tìm `ACTV-xxx` |
| Module/API/bảng nào đứng sau nghiệp vụ X? | `10-digital-twin/traceability-matrix.md` |
| Dữ liệu này lưu ở đâu, SoT là gì? | `registries/data-entities.yaml` (`systemOfRecord`) + `05-data-architecture/` |
| Ai duyệt bước này? | `06-governance/approval-matrix.md` |
| Đã implement chưa hay mới ý định? | field `confidence` + `implStatus` trong registry; `07-as-is/` |
| Nên tự động hóa gì tiếp? | `09-gap-analysis/automation-gap-register.md` + roadmap |
| Deploy HTX mới cần gì? | `03-processes/process-catalog.md` (CORE vs VARIANT) + F4 swimlane |

## 3. Kiểm tra tính toàn vẹn

```bash
python scripts/validate-twin.py        # 6 checks: ID, required fields, refs, enums, evidence, coverage
python scripts/twin-drift-report.py    # code vs twin drift → 11-validation/drift-report.md
```

Warnings (không phải errors) là findings hợp lệ: activity không có software link, risk chưa có control.

## 4. Quy tắc khi đọc

- `registries/*.yaml` = **single source of truth**; file `.md` chỉ là narrative — nếu lệch nhau, tin YAML.
- Evidence `path@commit-sha` — click sang file code tương ứng để verify. Entry nào không re-verify được = `STALE`.
- Confidence: **VERIFIED** (code-proven) → **INFERRED** → **ASSUMED** (chờ owner). Status: `ACTIVE → PROPOSED → APPROVED → RETIRED`.
- ID không đổi: `ACTR` actor, `PROC` process, `ACTV` activity, `SYS` component, `RULE`, `CTRL`, `RISK`, `GAP`...

## 5. Cập nhật twin (2-tier — `10-digital-twin/maintenance-rules.md`)

- **Mandatory** (cùng change batch): đổi business rule/flow, permission, tenant boundary, NATS subject, voucher type mới, AI boundary → update registries + diagram liên quan.
- **Conditional** (registry touch-up): endpoint nội bộ mới, refactor, entity không ảnh hưởng process → chỉ thêm dòng YAML.
- Chạy `validate-twin.py` sau mỗi sửa; chạy `twin-drift-report.py` định kỳ để bắt drift.

## 6. Không được làm

- Không sửa ID đã gán (retire bằng `status: RETIRED`).
- Không ghi ASSUMED như VERIFIED; unknown → `unresolved-questions.md`.
- Không đưa secrets/PII thật vào docs (NFR-07).
- Không xóa/ghi đè docs hiện có ngoài `enterprise-operating-model/` (twin INDEXES, không absorb).
