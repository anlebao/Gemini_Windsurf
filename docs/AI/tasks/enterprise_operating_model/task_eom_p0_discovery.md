# Task EOM-P0 — Repository Discovery & Anchor Re-Verification

> **Status:** ✅ DONE 2026-11-07 — deliverables landed; chờ G0 review
> **Gate:** G0 — master plan `docs/AI/plans/enterprise-operating-model-master-plan.md` · SRS v2.1 §14-P0, §2.3, §9.1, §6.5
> **Mode:** REVIEW_ONLY-style — KHÔNG sửa code/schema/config (SRS §16.2). Output = docs + YAML only.
> **Song song:** EOM-P0b (scaffold) chạy cùng — không block nhau.

## Objective

Khảo sát repository độc lập, re-verify từng claim trong anchors §2.3, và sản xuất evidence register + coverage map cho Gate 0. Anchors được coi là **re-verification checklist** — KHÔNG được copy thành kết luận (SRS 0.1.3, §16.13).

## Deliverables

```
docs/enterprise-operating-model/
  00-overview/system-context.md           # draft — từ findings P0
  11-validation/evidence-register.md      # bảng evidence: claim → path@commit → status
  11-validation/unresolved-questions.md   # business questions cần owner
registries/seed (nếu P0b chưa xong thì note handoff)
```

## Requirements — checklist từ SRS §14-P0 (KHÔNG bỏ item)

| # | Task | Output |
|---|---|---|
| 1 | Inspect repo structure, sln files, project graph | Repo inventory (SYS- candidates) |
| 2 | **Re-verify từng anchor §2.3** — per-component status `VERIFIED/DRIFTED/UNVERIFIED` + coverage `NOT-SURVEYED/PARTIAL/COVERED` | Anchors status table (vào evidence-register) |
| 3 | Databases & migrations: Gateway PG (schema/migrations), ShopERP per-tenant SQLite, metering DB, vanan-ai PG+pgvector | DATA- seed list |
| 4 | APIs, workers, scheduled jobs, NATS subjects | API- seed list |
| 5 | External integrations (Gemini, payment, e-invoice…) | External actor seeds |
| 6 | Review existing SRS/arch/business specs | Docs inventory |
| 7 | **Existing-docs index + dedup decision** — `docs/` ~70 mục; per-cụm: `INDEX` (twin trỏ tới) / `SUPERSEDE` (ghi rõ lý do) / `GAP` | Docs index table (SRS §16.7, §14-P0) |
| 8 | Identify existing Mermaid diagrams | Link list vào docs index |
| 9 | Implemented vs planned modules | Status per SYS- seed |
| 10 | **Critical process classification** — chấm F1–F7+ theo 6 tiêu chí §6.5 | Criticality table |
| 11 | Evidence register: mọi claim kèm `path@commit`; record commit SHA hiện tại | `evidence-register.md` |
| 12 | Unresolved questions — ghi câu hỏi, KHÔNG bịa (§16.9) | `unresolved-questions.md` |
| 13 | Coverage map đề xuất (thứ tự §3.1) | Trong G0 report |

## Anchor re-verification table (bắt buộc điền — mỗi dòng 1 claim)

| Claim (SRS §2.3) | Verify bằng | Status | Evidence | Coverage |
|---|---|---|---|---|
| `1_Shared/Domain.cs` = SSOT domain | Đọc file + check entities khác ngoài | | | |
| Gateway Option C order creator + PG scope | Controller/service + DbContext entities | | | |
| CoreHub = class lib (no exe) | `.csproj` OutputType | | | |
| ShopERP per-tenant SQLite + Products here | DbContext + config | | | |
| KhachLink no local DB | grep DbContext/Db refs | | | |
| metering standalone + auth pattern | `8_Metering` sln + middleware | | | |
| vanan-ai PG+pgvector + gated P1-G1..G5 | `9_TenantAI` + master plan status | | | |
| NATS `vanan.cloud.order.created.{shopInstanceId}` | publisher/subscriber code | | | |
| ShopInstances routing multi-VPS | entity + routing logic | | | |
| Order status sync SQLite→PG | reverse sync code | | | |
| `GET /api/catalog/recommended` pure PG | controller | | | |
| YARP product browse forward → ShopERP | yarp config | | | |

## Validation

- Mọi claim trong deliverables có `path@commit` (hoặc `#L` + note) — check thủ công trước khi nộp gate.
- Record drift: anchor sai → giữ nguyên anchor text, thêm entry DRIFTED + evidence mới.
- Mermaid (nếu có sơ đồ sớm) phải render.

## Acceptance

- [ ] 13 task rows hoàn thành, output đủ 3 file deliverables
- [ ] Anchors table điền đủ status + evidence cho mọi dòng
- [ ] Docs index phủ toàn bộ `docs/` top-level + subtrees chính
- [ ] Unresolved questions list (kể cả khi rỗng — ghi "none")
- [ ] G0 report: coverage map + đề xuất vào P1
