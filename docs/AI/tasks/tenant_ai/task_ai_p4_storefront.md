# Task AI-P4 — Gateway Endpoint + AiChatWidget + Product Cards→Cart

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G4 — SRS v1.1 [D7][G8] · FR-5 · §6.1 · **main sln changes — additive + flag-gated**
> **Phụ thuộc:** P3 (chat API đã hoạt động ở CP2)

## Objective

Đưa AI chat ra storefront: Gateway public endpoint (resolve tenant + ký ctx + forward) + widget KhachLink + product cards "Thêm vào giỏ" theo Option C.

## Gateway side (`2_Gateway` — additive)

| Thành phần | Spec |
|---|---|
| `Controllers/PublicTenantAiController.cs` | `POST /api/public/tenant-ai/chat` anonymous + rate-limited (pattern `PublicBookingController` — arch-test exemption list) · `GET /api/public/tenant-ai/widget-config` · `GET /api/public/tenant-ai/sessions/{sid}/messages` |
| Tenant resolve | Từ `KhachLinkInstance`/`ShopInstance` binding (server-side — pattern catalog/booking resolve hiện hữu); **client gửi `tenantId` thì bỏ qua/override** — T6 |
| `Services/TenantAiContextSigner.cs` | HMAC-SHA256 ký `{tenantId, callerId:"gateway", iat, exp(+2ph), requestId=messageId}` → `X-Tenant-Ctx`+`X-Tenant-Ctx-Sig` — copy logic từ `VanAn.TenantAi.Contracts` (per-host, không shared DLL) |
| `TenantAiForwarder` | HttpClient typed → `TenantAi:BaseUrl` internal · timeout 35s (>LLM 30s) · lỗi AI service → 503 graceful `{aiEnabled:false}` |
| Rate limit | per-IP + `sessionId` cap 20 msg/giờ [G8 PO-3] · config `TenantAi:SessionCap` |
| Idempotency | `messageId` unique per session → duplicate retry không double (persist dedupe map ngắn hạn hoặc truyền xuống service dedupe — chọn cách đơn giản: service `conversation_messages` unique `(tenant_id, message_id)`) |

## KhachLink side (`5_WebApps/KhachLink` + `UI.Platform`)

| Thành phần | Spec |
|---|---|
| `UI.Platform/Components/Ai/AiChatWidget.razor` | Widget chat: bubble panel, input `VanAInput`, message list, typing indicator, product card render `{image,name,price}` + `VanAnButton` "Thêm vào giỏ" — **UI Platform components only** (hard stop) |
| `AiChatWidget` logic | Load `widget-config` → `aiEnabled=false` → không render · sessionId = localStorage `vanan_ai_session` (reuse pattern `customer_device_id`) · optimistic UI + `messageId` dedupe · error → fallback message |
| Product card → cart | Click "Thêm vào giỏ" → gọi cart service hiện hữu của KhachLink (Option C — snapshot ProductName/price từ card, KHÔNG qua AI) — kiểm tra exact API cart trong codebase khi implement |
| Mount | KhachLink layout/store page: `<AiChatWidget />` có điều kiện theo widget-config |

## Tests bắt buộc (main sln)

- [ ] Gateway unit: resolve tenant đúng từ instance · client gửi `tenantId` fake → bị override (T6) · sign ctx deterministic (test vector cố định)
- [ ] Rate limit: 21st msg/session/giờ → 429
- [ ] Widget bUnit: render khi enabled · ẩn khi `aiEnabled=false` · card có nút "Thêm vào giỏ" · click → cart service được gọi với đúng product snapshot
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests — **điều kiện bắt buộc trước CP3**
- [ ] E2E spec `6_Testing/e2e-tests/ai-chat-widget.spec.ts` (Gate 4 rules — UI change → E2E): hỏi → answer → card → add to cart (chạy ở CP3 trên production)

## Không được làm

- Không động checkout/order logic · không bypass UI Platform · không để widget load khi service chưa deploy (config `TenantAi:BaseUrl` rỗng = widget ẩn hoàn toàn)

## Acceptance

- [ ] Main sln build + guard pass · widget tests xanh · spec sẵn sàng cho CP3

## Delivered — 2026-11-06

- `2_Gateway/Controllers/PublicTenantAiController.cs` — `POST /api/public/tenant-ai/chat` + `GET /widget-config` + `GET /sessions/{sid}/messages` · anonymous + `[EnableRateLimiting("tenant-ai-public")]` (60/min/IP) · session cap 20/giờ qua IMemoryCache (`TenantAi:SessionCap`) · **resolve tenant server-side (T6):** KhachLinkInstance bound to Request.Host + OwnerTenantId thắng — client tenantId bị bỏ qua; còn lại validate tenant exists+Active, không bao giờ ký ctx raw.
- `2_Gateway/Services/TenantAiContextSigner.cs` — per-host copy của `VanAn.TenantAi.Contracts` signer (HMAC-SHA256, requestId=messageId, TTL 2ph).
- `2_Gateway/Services/TenantAiForwarder.cs` — typed HttpClient (timeout 35s; `X-Caller-Id`/`X-Ai-Key` default headers trong DI; missing `TenantAi:BaseUrl`/`CtxSigningSecret` → self-disable; transport fail → null → controller 503 graceful `{aiEnabled:false}`). Config: `TenantAi:BaseUrl|CallerId|GatewayCallerKey|CtxSigningSecret|SessionCap`.
- `UI.Platform/Components/Ai/AiChatWidget.razor` (+.razor.css) + `IAiChatClient.cs` — bubble + VanAnCard panel, VanAInput/VanAnButton only · sessionId `localStorage vanan_ai_session` · optimistic UI + messageId dedupe · product card "Thêm vào giỏ" → `OnAddToCart` callback (host owns cart — widget không reference KhachLink).
- `5_WebApps/KhachLink`: `TenantAiPublicHttpService : IAiChatClient` (fail-safe null) + DI + `Store.razor` mount thay placeholder (gate `_features.AIChat_Enabled`) · add-to-cart: prefer `_products` ProductDto thật (đúng VatRate) else card snapshot (VatRate 0.10 default như RecommendedProductDto).
- **Deviation (additive):** implement luôn upstream `GET /v1/chat/sessions/{sid}/messages` trong vanan-ai (SRS §6.1 đã spec nhưng còn thiếu) + contracts `SessionMessageDto`/`SessionMessagesResponse` — cần cho session resume hoạt động thật. `/v1/config/widget` vẫn 501 stub (đúng scope — lands P5 "widget enable"); gateway map 501/4xx → 200 `{aiEnabled:false}`, 5xx khác → 503.
- Arch-test: `PublicTenantAiController` thêm vào `AuthorizationEnforcementTests` exemption (pattern PublicBookingController).
- Tests: `PublicTenantAiControllerTests` 11 + `AiChatWidgetTests` 7 (bUnit) trong Core.Tests — T6 override · validated tenantId · cap 21st→429 · graceful paths · sign vector · widget render/hidden/card→cart/fallback · vanan-ai `ChatApiTests` +2 session-history (26/26).
- E2E: `6_Testing/e2e-tests/ai-chat-widget.spec.ts` — self-gating `AI_CHAT_TEST_STORE_URL` (+`AI_CHAT_TEST_QUESTION`), chạy CP3.
- Validation: `dotnet build VanAn.sln` 0 err · VanAn.TenantAi.sln 0 err · new tests 18/18 + 26/26 · ShopERP.Tests 306/306 · Core.Tests 2740/2761 (1 fail cũ, đang rerun trx xác định — không liên quan TenantAi) · guard-check: untracked/SQL/encoding/windsurf-guard/architecture + Roslyn PASSED (fast-gate đang chạy).
- **Không push** — CP3 deploy sau P5+P6 (push policy §3).
