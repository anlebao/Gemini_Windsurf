# Task EOM-P3 — Software & Data Traceability

> **Status:** ✅ DONE 2026-11-07 — traceability landed; chờ G3 review
> **Gate:** G3 — SRS v2.1 §7 (all), §8 (all), §9.1, §14-P3
> **Depends:** EOM-P2a + EOM-P2b

## Objective

Map business activities (F4+F1) xuống code thật và data stores thật: software inventory đầy đủ, component map, integration map, data-flow catalog với system-of-record rõ ràng, và business→code traceability matrix cho 2 track.

## Deliverables

```
04-software-architecture/
  software-inventory.md        # SYS- catalog narrative
  component-map.md             # mermaid component diagram
  integration-map.md           # NATS/HTTP/YARP/external integrations
05-data-architecture/
  data-flow-catalog.md         # per-flow 10 fields §8.1
  data-ownership.md            # owner + system-of-record per data class §8.2/8.3
  data-entity-map.md           # erDiagram mức twin (PG/SQLite/pgvector boundaries)
registries/components.yaml apis.yaml data-entities.yaml  # fill
10-digital-twin/traceability-matrix.md  # draft — finalize P5
```

## Requirements — checklist

| # | Task | SRS ref |
|---|---|---|
| 1 | Software inventory đầy đủ các loại §7.1 (frontend/backend/services/workers/jobs/brokers/DBs/standalone/auth/external/deploy) — extend anchors, không rediscover | §7.1 |
| 2 | Component map: 12 fields §7.2 per SYS- (deployment boundary: in-process/hosted/separate-VPS) | §7.2 |
| 3 | Implementation status thật: implemented/partial/planned — chống AC-07 | §7.2, AC-07 |
| 4 | Business→code chain cho mọi activity quan trọng F4+F1: Activity→Module→Handler→API/Event→Entity→Output; unverifiable link = UNKNOWN/UNVERIFIED | §7.3 |
| 5 | Data flow catalog: 10 fields §8.1 per critical flow | §8.1 |
| 6 | Data classification 12 categories §8.2 | §8.2 |
| 7 | **System-of-record vs replica** explicit mọi store: PG=Orders/Accounting/Tenants SoT · SQLite=Products+POS replica · pgvector=AI knowledge · metering DB=wallet/usage | §8.3 |
| 8 | DataFlow diagrams: create/transform/validate/sync/store/retrieve/share/delete-archive | §8.3 |
| 9 | **AI boundary component-level** cho vanan-ai (6 attrs §7.4) — ở mức component, deep model ở X3 | §7.4 |
| 10 | AI data rules: knowledge classification, Confidential→extraction path, retention note; no secrets trong docs | §8.4, NFR-07 |
| 11 | NATS subjects + YARP routes + service-to-service auth (caller keys) vào integration map | §7.1 |
| 12 | Sequence diagrams cho technical interactions | §6.2 rule 2 |
| 13 | Evidence + drift recording như mọi card | §9.1 |

## Validation

- Validator green — referential integrity giữa ACTV↔SYS↔API↔DATA.
- Spot-check: random 5 activities trace đến code thật.
- Không có API/table/service nào bị bịa — UNKNOWN đếm được và công khai.

## Acceptance

- [ ] 4 docs dir-04/05 + 3 registries fill + matrix draft
- [ ] SoT table cho mọi data class
- [ ] UNKNOWN list + unresolved questions cập nhật
