# Task AI-P5 — Metering Integration + Degrade Policy + Feature Flag

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G5 — SRS v1.1 [G7][G9·G6] · §11 · PO-1/PO-2/PO-3 cần duyệt trước card này
> **Phụ thuộc:** P3 (chat pipeline tồn tại để hook)

## Objective

Mọi chat message đi qua metering: check → degrade/refuse → ingest. Per-tenant flag `TenantAiEnabled`. Đây là **deviation có chủ đích** khỏi fail-open generic — ghi rõ vào code comment.

## Deliverables (trong `vanan-ai`)

| Thành phần | Spec |
|---|---|
| `MeteringClient` | HttpClient typed riêng trong `VanAn.TenantAi.Infra` (KHÔNG share với hosts khác — per-host convention): `CheckBalanceAsync(tenantId, "ai.chat.message")` + `IngestAsync(event)` · timeout 3s · circuit breaker 5-fail/60s |
| `QuotaGate` | Bọc trước chat pipeline: ví đủ → pass · `deny=true` → `refused` response ("tính năng AI tạm ngưng", không lộ số dư) · **check fail + cache còn hiệu lực (30s TTL) → allow theo cached** · **check fail + cache hết hạn → `degraded` mode** [G7 PO-1]: pipeline structured-only, `ILlmChatClient` không được gọi |
| Ingest | Sau response: `POST /v1/usage/events` `{tenantId, serviceCode, quantity:1, occurredAtUtc}` · `Idempotency-Key` = `chat:{tenantId}:{messageId}` · degraded → serviceCode `ai.chat.degraded` (MeterOnly, PO-2) · ingest fail → log warn, KHÔNG ảnh hưởng response (fire-and-forget có retry queue nhẹ) |
| Rate card seed | Ghi chú trong card: sysadmin tạo rate card `ai.chat.message` (Deny) + `ai.chat.degraded` + `ai.embedding.batch` (MeterOnly) trên metering trước CP3 — không seed tự động |
| Widget gate | `GET /v1/config/widget` trả `aiEnabled=false` khi: flag off OR cached-deny → client ẩn widget |

## Feature flag

| Thành phần | Spec |
|---|---|
| `TenantAiEnabled` | Flag per-tenant qua `IShopFeatureSettingsService` (pattern `SmartEntryLlmEnabled`) · ShopERP admin page bật/tắt (thêm vào feature settings hiện hữu) · ShopERP sync service check flag trước khi push |
| `TenantAi:Enabled` | Global kill switch env trên `vanan-ai` → mọi chat trả refused nhẹ nhàng |
| `TenantAiLlm:ApiKey` | rỗng → degrade mode toàn hệ (structured-only vẫn chạy) — [G6] chính xác |

## Tests bắt buộc

- [ ] Mock metering: deny → chat refused, **0 LLM call** (assert fake counter=0 — T8)
- [ ] Check timeout + cache valid → allow (dùng cached balance)
- [ ] Check timeout + cache expired → degraded response, **0 LLM call** (T8 core)
- [ ] Ví đủ → normal path, ingest event gọi đúng 1 lần với đúng Idempotency-Key
- [ ] Ingest fail → response vẫn trả bình thường + log warn
- [ ] Flag off → `/v1/config/widget` `aiEnabled=false` · chat refused nhẹ nhàng
- [ ] `TenantAi:Enabled=false` → refused mọi tenant · ApiKey rỗng → degraded mọi request
- [ ] ShopERP: flag off → sync service zero HTTP call

## Acceptance

- [ ] Tests xanh · PO-1/PO-2 đã duyệt và khớp implement · không LLM call nào thoát khi quota không xác minh được
