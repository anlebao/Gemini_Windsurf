# W0 AUDIT: API CONTRACT GAP — Universal AI Assistant

> Date: 2026-11-22 · Map SRS v2.0 requirements → existing endpoints

## EXISTING (verified)

| Contract | Endpoint | Ghi chú |
|---|---|---|
| Public chat | `POST /api/public/tenant-ai/chat` (Gateway) | anonymous, rate-limited, session cap, idempotent |
| Widget config | `GET /api/public/tenant-ai/widget-config?tenantId=` | flag-gated; upstream `/v1/config/widget` cần verify trạng thái |
| Session history | `GET /api/public/tenant-ai/sessions/{sid}/messages` | resume history |
| Semantic search | `GET /api/public/tenant-ai/search` | one-shot grounded answer |
| TenantAI chat | `POST /v1/chat` (vanan-ai, caller key + signed ctx) | RequireScope public-chat |
| TenantAI widget config | `GET /v1/config/widget` | verify stub status |
| Knowledge ingest | `POST /v1/ingest/*` (caller ingest scope) | documents + products batch |
| Knowledge admin | docs list/approve/reject | TenantAdminService |
| Metering check | `GET /v1/wallets/{tenantId}/check?serviceCode=` | QuotaGate dùng |
| Metering usage | `POST /v1/usage/*` ingest + `GET /v1/usage/events` | idempotency key |
| Rate cards | `/v1/rate-cards*` CRUD + resolve | append-only versions |
| Shop settings | `IShopFeatureSettingsService` (CoreHub) + sync event | AIChat_Enabled flag |

## MISSING (cần build — W1+)

### Public surface (Gateway)

| Contract | Mô tả | Wave |
|---|---|---|
| `GET /api/public/assistant/{slug}/config` | Public config theo link slug — display + capabilities, no secrets, origin-validated khi có | W1 |
| `POST /api/public/assistant/{slug}/sessions` | Session bootstrap theo slug (hoặc tái dùng chat trực tiếp với sessionId client-gen như hiện tại — quyết định: **có thể không cần endpoint riêng**, facade chat đã nhận sessionId; token hóa session là enhancement) | W1 (optional) |
| `POST /api/public/assistant/{slug}/messages` | Chat qua slug resolution — hoặc generalize `/chat` thêm `assistantRef` | W1 |
| `GET /api/public/widgets/{key}/config` | Widget public config theo PublicWidgetKey + origin check | W2 |
| `POST /api/public/widgets/{key}/sessions|messages` | Widget chat — có thể share endpoint với slug nếu resolution unified | W2 |

### Management (authenticated, owner-scoped)

| Contract | Mô tả | Wave |
|---|---|---|
| `POST/GET/PATCH /api/assistants` | CRUD assistant (1/tenant default) | W1 |
| `POST /api/assistants/{id}/publish|pause|resume` | lifecycle | W1 |
| `POST /api/assistants/{id}/preview` | test chat trước publish | W1 |
| `POST/GET/DELETE /api/assistants/{id}/links` + revoke/regenerate | AI Link management | W1 |
| `GET /api/assistants/{id}/qr` | QR PNG/SVG | W1 |
| `GET /api/assistants/{id}/usage` | usage summary (từ Metering events) | W1 |
| `PATCH /api/assistants/{id}/budget` | hard-cap chi tiêu | W1 |
| `POST/GET /api/assistants/{id}/domains` + verification/* | domain mgmt | W2 |
| `POST /api/installations*` | installation jobs | W4/P3 |
| Knowledge auto-builder endpoints | extract/profile/confirm | W3 |
| Handoff/lead endpoints | L3/L4 | W4 |

### MODIFY needed

| Contract | Thay đổi |
|---|---|
| `PublicTenantAiController` | Generalize `ResolveStoreTenantAsync` → `ResolveAssistantContextAsync` hỗ trợ slug/widgetKey/storefront-host; giữ storefront path backward compat |
| `IsAiChatEnabledAsync` | Đọc từ Assistant.Status==Published thay vì chỉ flag (migration path C1) |
| Session model | Optional: facade-issued session token (signed, TTL) chứa sessionId+tenant — TenantAI contract không đổi |
| Metering metadata | Thêm channel/assistantRef/session vào usage event metadata (attribution) |

## CONFLICT

| # | Vấn đề | Resolution |
|---|---|---|
| K1 | Hai cách chat public: storefront (tenantId/host) vs assistant (slug) | Unified `assistantRef` resolution — storefront host map tới assistant của tenant đó; slug map trực tiếp. Một chat endpoint, nhiều resolution keys |
| K2 | `widget-config` upstream có thể stub | Verify prod trong W1 task đầu tiên; nếu stub → implement theo assistant config |
| K3 | SessionId client-generated vs session token | Quyết định: W1 giữ client sessionId (đơn giản, đã chạy) + thêm signed session token optional cho continuity W5; không block |
