# Task EOM-P2a — AS-IS Process Model: Track A (F4 Merchant Onboarding & HTX Provisioning)

> **Status:** ✅ DONE 2026-11-07 — P2 tracks modeled; chờ G2 review
> **Gate:** G2 — SRS v2.1 §5, §6 (all), §9.1, §14-P2 · Track A per §3.1
> **Depends:** EOM-P1 · **Song song:** EOM-P2b (Track B)
> **Caveat (SRS §3.1):** F4 ít implement nhất → kỳ vọng nhiều ASSUMED/INFERRED. Đó là finding, không phải lỗi — đừng "làm đẹp" coverage.

## Objective

Model AS-IS cho slice F4: cách một cửa hàng/HTX được đưa vào hệ sinh thái hiện nay — từ tiếp cận, đăng ký, tạo tenant, provision ShopInstance, phân quyền, kích hoạt feature, tới khi shop vận hành được.

## Scope (điều tra thực tế — không assume)

- Merchant onboarding: form/đăng ký, duyệt, tạo Tenant record (Gateway PG).
- HTX formation + membership (HtxProfile, MarkAsHtx, HTX governance docs hiện có).
- Tenant provisioning: ShopInstance creation, routing table, per-tenant SQLite init.
- Feature activation: `TenantFeatureSettings`/`IShopFeatureSettingsService`, flag-gated modules.
- Human steps: sales tiếp cận, Hạt Nhân hỗ trợ, owner xác nhận — tìm evidence trong docs + code.
- CTV/collaborator role trong onboarding nếu có.

## Deliverables

```
03-processes/
  process-catalog.md            # += PROC- entries F4 (L3–L4)
  swimlane-diagrams/f4-*.md     # swimlane flowchart mỗi critical workflow (§6.2 rule 1)
  process-details/f4-*.md       # per-workflow: metadata, exceptions, evidence
registries/processes.yaml activities.yaml rules.yaml  # += F4 data
07-as-is/current-operating-model.md  # += section F4
```

## Requirements — checklist

| # | Task | SRS ref |
|---|---|---|
| 1 | Process hierarchy L3–L4 cho F4 + link parent L2 | §6.1 |
| 2 | **Variability class** mỗi PROC: CORE/CONFIGURABLE/VARIANT; VARIANT phải có parentRef | §6.6 |
| 3 | Swimlane flowchart mỗi critical workflow (6 tiêu chí §6.5) — subgraph lanes, đủ 12 element §6.3, **không happy-path-only** | §6.2, §6.3 |
| 4 | Mọi activity: đủ 18 field metadata §5.2 — execution type (decompose-first!), actors, trigger, rules, exceptions, audit, module/API/DB refs, KPI, evidence, confidence | §5.1, §5.2 |
| 5 | Approval gates + exception paths + external deps + handoffs trong diagram | §6.3 |
| 6 | Business rules → `rules.yaml` (RULE-) | §11 |
| 7 | Human vs Software execution map contribution (mermaid classDef đúng §5.3) | §5.3 |
| 8 | Evidence `path@commit` mọi AS-IS claim; discrepancy docs-vs-code → gap draft | §9.1 |
| 9 | Unknown → unresolved-questions; ASSUMED ghi rõ | §16.9, §5.2 |

## Validation

- Validator green (errors=0); warnings liệt kê trong card.
- Mỗi critical workflow ≥1 exception path.
- Đếm execution types: report tỷ lệ MANUAL/AUTOMATED/HYBRID — sanity check HYBRID hiếm (§5.1 rule).

## Acceptance

- [ ] F4 process catalog + swimlanes + registry data
- [ ] Execution-type distribution report
- [ ] Draft gaps chuyển P4; unresolved questions cập nhật
