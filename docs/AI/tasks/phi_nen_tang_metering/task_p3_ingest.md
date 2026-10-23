# Task P3 — Usage Ingest + Idempotency + Summary/Events + Shadow Report

> **Status:** ✅ IMPLEMENTED + DEPLOYED + RV PRODUCTION 7/7 (`f8ba7268`, CD #39956713322 — metering-only deploy) — tests 46/46 (T01–T15 + boundary) · HTTP smoke 14/14 local + 7/7 production (ingest/replay/`:batch` partial/summary unpriced/events-list/401). **Deviation:** route `events:batch` — `:` illegal in route template literal & minimal-hosting routing chạy trước user middleware (rewrite không tái match) → map `POST /v1/usage/{verb}` + kiểm tra `verb=="events:batch"`; URL contract đúng như card.
> **Phase:** P3 (GĐ-1) · **Gate:** tests pass — idempotent replay, summary khớp
> **Căn cứ:** SRS §6.1 (usage endpoints) · [G5] clock skew · [G6] idempotency · [G17] batch/counter · FR-1/2 · NFR-2/3

---

## 1. Mục tiêu

Caller ghi usage event (single + batch), idempotent chống double-count; service trả counter theo tenant/service/kỳ và lịch sử event; report "shadow" = usage × định mức rate card hiệu lực tại `occurredAtUtc`. Chưa trừ ví (P7).

## 2. Quyết định thiết kế

| # | Điểm | Đề xuất |
|---|---|---|
| D1 | Idempotency | Mutation yêu cầu `Idempotency-Key` header (single) hoặc `idempotencyKey` per event (batch). Unique `(CallerId, IdempotencyKey)` trên `UsageEvents`. Replay → trả event đầu + `idempotentReplay:true`, **không insert lại** |
| D2 | Batch | `POST /v1/usage/events:batch` ≤500 events/request — mỗi event kèm `idempotencyKey` riêng. **Partial-failure semantics:** trả `accepted/rejected/replayed` per item (không all-or-nothing — usage không cần atomic, event xấu không chặn event tốt) |
| D3 | Clock skew [G5] | `occurredAtUtc` lệch server >±5 phút → vẫn ghi + `LateEvent=true`. Tương lai >5 phút → cũng flag late (không reject — clock caller có thể chậm/nhanh) |
| D4 | Unpriced event | `serviceCode` không có rate version hiệu lực → vẫn ghi event (metering ghi fact), summary trả `credits=null` cho nhóm đó — không reject |
| D5 | Validation | `tenantId` rỗng → 400 · `quantity <= 0` → 400 · `serviceCode`/`unit` trống → 400 · `occurredAtUtc` năm <2020 → 400 (rõ ràng sai) |
| D6 | Summary | `GET /v1/usage/summary?tenantId=&period=YYYY-MM` → nhóm theo serviceCode `{quantity, unit, creditsComputed, priced}`. credits = Σ(quantity × rate tại occurredAtUtc từng event) — **không** dùng rate hiện tại cho event quá khứ (đúng [G7]) |
| D7 | Events list | `GET /v1/usage/events?tenantId=&serviceCode=&from=&to=&page=&pageSize=` — pageSize default 50, max 200; order `OccurredAtUtc DESC` |
| D8 | Shadow report | Chính là summary ở D6 — không endpoint riêng (đủ cho GĐ-1) |
| D9 | Idempotency-Key format | Caller tự chọn (khuyến nghị `{callerShortCode}:{guid}`); service chỉ enforce non-empty + ≤128 chars + unique per caller |
| D10 | Deduct-at-ingest | **KHÔNG có ở P3** — deduct gắn vào P7 cùng wallet. P3 chỉ ghi + đếm (giữ đúng thứ tự: ingest độc lập, wallet thêm sau) |

## 3. Flow

```
POST /v1/usage/events  (header Idempotency-Key + body UsageEventRequest)
  → validate (D5) → check (CallerId, IdempotencyKey) tồn tại? → replay trả {eventId, idempotentReplay:true}
  → LateEvent flag (D3) → insert → 201 {eventId}

POST /v1/usage/events:batch  (body UsageEventRequest[] each with idempotencyKey)
  → per item: validate → dedupe → insert → results[i] = {index, status: accepted|replayed|rejected, eventId?, error?}
  → 200 {accepted:n, replayed:n, rejected:n, results[]}

GET summary → group events theo serviceCode trong period → per serviceCode: resolve rate tại occurredAtUtc từng event → Σcredits
```

## 4. Edge cases đã liệt kê hết

**Single:** (a) thiếu Idempotency-Key → 400 · (b) trùng key cùng caller → replay 200 + id cũ · (c) trùng key khác caller → được phép (unique theo cặp) · (d) payload invalid → 400 không ghi · (e) event rất cũ (>90 ngày) → vẫn ghi + LateEvent · (f) `occurredAtUtc` future → ghi + LateEvent

**Batch:** (a) >500 items → 400 toàn request · (b) item thiếu idempotencyKey → item rejected, các item khác xử lý bình thường · (c) 2 item cùng key trong 1 batch → item đầu accepted, item sau replayed (dedupe in-batch) · (d) mixed valid+invalid → partial results, không rollback · (e) empty array → 400

**Summary:** (a) period format sai → 400 · (b) tenant không có event → `{items:[]}` 200 · (c) serviceCode unpriced → `credits=null, priced=false` · (d) rate đổi giữa kỳ → event trước/sau áp đúng version · (e) period quá khứ vẫn tính được (rate versions giữ lịch sử)

**Race:** 2 request cùng idempotency key đồng thời → unique index chặn → catch `DbUpdateException` → replay trả bản ghi thắng (không 500).

## 5. Test matrix — `VanAn.Metering.Tests` (service-level, EF InMemory; race-condition test dùng SQLite in-memory để enforce unique index)

| # | Test | Expect |
|---|---|---|
| T01 | Ingest valid → row đủ cột, ReceivedAtUtc=now | |
| T02 | Thiếu Idempotency-Key | 400 |
| T03 | Replay cùng key+payload | eventId cũ, replay=true, count=1 |
| T04 | Cùng key khác caller | cả 2 insert |
| T05 | Invalid (quantity≤0 / code trống / tenantId empty / năm<2020) | 400, không ghi |
| T06 | Skew ±>5' (quá khứ & tương lai) | ghi + LateEvent=true |
| T07 | Batch mixed → per-item status | accepted+replayed+rejected đúng từng index |
| T08 | Batch >500 / empty | 400 |
| T09 | Batch duplicate key in-batch | 1 accepted + 1 replayed |
| T10 | Concurrent same key (SQLite mem) | 1 insert, caller 2 nhận replay — **cần SQLite in-memory, InMemory provider không enforce unique** |
| T11 | Summary group + period filter | khớp Σ |
| T12 | Summary rate đổi giữa kỳ | credit tính theo version tại occurredAt từng event |
| T13 | Summary unpriced code | credits=null, priced=false |
| T14 | Summary tenant rỗng | items=[] |
| T15 | Events list paging + filter | order desc, pageSize cap 200 |

## 6. File plan

| File | Action |
|---|---|
| `Contracts/UsageContracts.cs` | += `UsageBatchItem`, `UsageBatchResponse`, `UsageSummaryResponse`, `UsageEventDto` |
| `Infra/UsageService.cs` | mới — ingest single/batch + dedupe + late flag + summary + list |
| `Api/Program.cs` | += 4 endpoints |
| `tests/…/UsageServiceTests.cs` | mới — T01–T15 |

**Out of scope:** deduct ví (P7) · public ingest Gateway (P4) · DLQ/counter aggregation table (GĐ-2 nếu tải cao).
