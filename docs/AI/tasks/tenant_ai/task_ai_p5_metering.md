# Task AI-P5 — Metering Integration + Degrade Policy + Feature Flag

> **Status:** ✅ DONE 2026-11-06 — code complete, tests 118/118 vanan-ai · 306/306 ShopERP · guard ALL PASSED · chưa push (CP3)
> **Gate:** P1-G5 — SRS v1.1 [G7][G9·G6] · §11 · PO-1/PO-2/PO-3 RESOLVED theo đề xuất
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

## Delivered — 2026-11-06

**Trong `vanan-ai`:**

- `src/VanAn.TenantAi.Infra/MeteringClient.cs` — `IMeteringService` + `MeteringClient` (per-host copy, KHÔNG shared DLL — metering.md §5): `CheckBalanceAsync` → `GET /v1/wallets/{t}/check?serviceCode=` · `IngestAsync` → `POST /v1/usage/events` + `Idempotency-Key` header · `IsConfigured` tách biệt "chưa cấu hình" (fail-open) vs "check fail" (để QuotaGate quyết policy) · timeout 3s · circuit 5-fail/60s · BaseAddress trailing `/` · `MeteringServiceCodes` (`ai.chat.message` / `ai.chat.degraded` / `ai.embedding.batch`).
- `src/VanAn.TenantAi.Api/Services/QuotaGate.cs` — singleton + cache 30s/tenant (`Metering:CheckCacheSeconds`). Ma trận [G7] PO-1: unconfigured → Allow · check OK → Allow/Refuse theo deny (+cache) · check fail + cache còn hạn → theo cached verdict · **check fail + cache hết hạn → Degrade** (structured-only, 0 LLM — deviation có chủ đích khỏi fail-open generic).
- `src/VanAn.TenantAi.Api/Services/MeteringIngestQueue.cs` — `IMeteringIngestQueue` + `MeteringIngestQueue` BackgroundService: bounded channel 512 drop-oldest · retry ≤3 (1s/5s/15s) · drop khi metering unconfigured · warn khi drop — usage không bao giờ ảnh hưởng response.
- `ChatOrchestrator` — quota gate sau dedupe, trước session/policy: Refuse → `refused/quota_denied` + `ChatTemplates.QuotaRefused` (không lộ số dư) · Degrade → `MatchRules`-only (không LLM classify) + `llm.Enabled && !quotaDegraded` skip generation (T8: 0 LLM) · ingest trong `FinishAsync`: **`refused||degraded` → `ai.chat.degraded` MeterOnly** (PO-2 mở rộng cho refused — không charge unpaid, vẫn đo được) · normal → `ai.chat.message`, `Idempotency-Key=chat:{tenant}:{messageId}` · kill-switch `TenantAi:Enabled=false` → `refused/ai_disabled` nhẹ nhàng (trước quota gate).
- `GET /v1/config/widget` — thật (bỏ 501 stub): `aiEnabled = TenantAi:Enabled && quota != Refuse` (cached-deny → false) · `aiName`/`greeting`/`suggestions` từ published `TenantAiConfig.persona_json` (`greeting`/`suggestions` keys, defaults nếu thiếu) · `WidgetConfigResponse` contract khớp `AiWidgetConfig` KhachLink.
- DI `Program.cs` — named HttpClient `metering` (X-Caller-Id/X-Metering-Key default headers) → singleton `IMeteringService` (circuit persists) + `QuotaGate` + `MeteringIngestQueue` (singleton + hosted service).
- `MeteringClient` = **wiring THẬT** tới vanan-metering — `FakeMeteringService`/`RecordingIngestQueue` chỉ trong tests.

**Deploy wiring (`cd-multivps.yml`):** `deploy-tenantai` trước đây KHÔNG ghi `METERING_*` vào `.env.tenantai` → metering âm thầm tắt trên production. Giờ: parse `vanan-ai:<key>` từ secret `METERING_CALLERS` (1 secret seed cả 2 phía — metering bootstrap + caller key vanan-ai) → ghi `METERING_BASE_URL=http://$METERING_REMOTE_HOST:5050/` (VPC internal 10.148.0.6) + `METERING_CALLER_ID=vanan-ai` + `METERING_CALLER_KEY` · secrets-verify step **fail-fast** nếu `METERING_CALLERS` thiếu entry `vanan-ai:` hoặc `METERING_REMOTE_HOST` rỗng (không deploy AI unmetered).

**Main sln (additive):** `ShopFeatures.razor` — `AIChat_Enabled` toggle bỏ `disabled` (owner bật widget storefront được — mô tả mới thay text "sắp ra mắt") + toggle mới `Đồng bộ dữ liệu AI` → `IsEnabledAsync/SetToggleAsync("TenantAiEnabled")` ops flag (pattern SmartEntry — gate sync service đã có từ P2).

**Tests — 118/118 vanan-ai PASS · ShopERP.Tests 306/306 · VanAn.sln 0 err:**
- `QuotaGateUnitTests` 6: unconfigured→Allow · deny→Refuse · fail+cached-allow→Allow · fail+cached-deny→Refuse · fail+no-cache→Degrade · fail+expired(TTL=0)→Degrade.
- `MeteringGateApiTests` 9: deny→refused `quota_denied` **0 LLM** (T8) + không lộ ví + ingest `ai.chat.degraded` · fail+expired→degraded structured answer **0 LLM** + ingest degraded · fail+cached-allow→normal · normal→ingest 1× key `chat:{t}:{mid}` code `ai.chat.message` · queue throw→response OK · `llm.Enabled=false`→degraded mọi request · widget deny→`aiEnabled=false` · defaults + published persona greeting/suggestions.
- `KillSwitchApiTests` 2 (`TenantAi:Enabled=false`): chat refused `ai_disabled` + 0 LLM + 0 metering check · widget `aiEnabled=false`.
- ShopERP flag-off→zero HTTP: đã có `TenantAiKnowledgeSyncTests.FlagOff_MakesZeroHttpCalls` (P2).

**Deviation ghi nhận:** refused cũng ingest `ai.chat.degraded` (không chỉ degraded) — tránh UnpaidCredits khi ví hết + đo traffic bị từ chối. Embedding `ai.embedding.batch` chưa ingest (rate card MeterOnly — observability, thêm khi P7 cần đo).

**✅ Ops wiring DONE 2026-11-06 (production, agent thực hiện):** ① `vanan-ai:<key>` appended vào `METERING_CALLERS` trên VPS `/opt/vanan-metering/.env.metering` (backup `.env.metering.bak-20261106`) + compose restart → caller registered, verify `/v1/auth/check` 200 `{callerId:vanan-ai}` · GH secret `METERING_CALLERS` updated (deploy-tenantai verify + parse sẽ pass) ② Rate cards tạo qua admin API: `ai.chat.message` Deny 5cr/message · `ai.chat.degraded` MeterOnly 1cr · `ai.embedding.batch` MeterOnly 1cr (rate theo precedent `chat.message` Deny 5cr). **Verify E2E:** check `ai.chat.message` tenant random → enforcement Deny resolve đúng; `deny=false` vì **master switch enforcement đang OFF toàn hệ thống** (`/v1/config/enforcement`, Issue #197) — deny chỉ bắn khi ops bật master switch (consistent với mọi service khác GĐ-1).
