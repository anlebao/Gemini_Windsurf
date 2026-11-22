# TASK W1: AI LINK/QR ASSISTANT — P1 HERO

> Wave: W1 · Mode: IMPLEMENT (sau W0 gate)
> Master plan: `docs/AI/plans/universal-ai-assistant-master-plan.md` §W1
> SRS: `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md` §6.1-6.4, §7.1-7.2
> Depends on: W0 (`ASSISTANT_HOST_DECISION`, `PRICING_AND_QUOTA_MODEL`, `API_CONTRACT_GAP`)

## Sản phẩm giao được (wave này phải demo được)

> Chủ shop Vạn An login portal → bật "Trợ lý AI" qua wizard 3 bước → nhận link `https://{domain}/a/{slug}` + QR → gửi link qua Zalo → khách mở trên điện thoại chat text với assistant của shop → owner thấy usage/quota trong dashboard → pause bất cứ lúc nào.

Đây là **sản phẩm bán được đầu tiên** — không cần website, không cần cài gì.

## Scope IN

### Assistant entity & lifecycle
- `TenantAssistant` (hoặc tên theo quyết định Q1): 1 assistant/tenant mặc định; fields: name, greeting, persona/tone, language, status (Draft/Preview/Published/Paused/Suspended), configVersion.
- Publish tạo immutable config version; pause/resume hiệu lực trong bounded cache TTL.
- Owner wizard: bật → tên/lời chào/ngôn ngữ/persona → chọn knowledge source (TenantAI đã ingest) + FAQ thủ công ngắn → preview test chat → kích hoạt.

### Public resolution & hosted page
- `AssistantLink` entity: slug high-entropy unguessable, revocable, regenerate.
- Generalize public facade: `slug → assistant → tenant` server-side (pattern `ResolveStoreTenantAsync`, KHÔNG nhận TenantId từ client).
- Hosted assistant page mobile-first (guest, no login): header tên shop + logo, chat UI tái dùng `AiChatWidget`/`IAiChatClient` contract, states: welcome/typing/answer/error/quota-exceeded/shop-paused.
- Privacy notice link + "xóa cuộc trò chuyện".

### QR
- QR PNG + SVG trỏ về link; download/in được.

### Metering & cost control (tối thiểu W1)
- `ai.chat.message` usage ingest qua Metering hiện có (rate card theo Q3).
- Per-assistant budget check trước LLM call; vượt → degrade structured-only hoặc thông báo hết lượt (KHÔNG unmetered).
- Visitor-level: rate limit + session cap đã có pattern (`tenant-ai-public` policy) — áp cho link surface.
- Cost attribution đủ chiều: tenant+assistant+channel+session (SEC-025).

### Owner dashboard (tối thiểu)
- Trong portal/ShopERP hiện có: trạng thái assistant, link/QR (copy/download), usage tháng + quota bar, pause/resume, revoke/regenerate link.

### Knowledge tối thiểu
- Chọn nguồn TenantAI đã ingest sẵn + nhập FAQ thủ công (giới hạn N mục).
- Readiness checklist plain-language.

### Security & audit
- Audit events: publish/pause/revoke/config change.
- Kill switch per assistant + platform-level.
- Link page không lộ TenantId/quota/nội bộ.

## Scope OUT (wave sau)

- External JS widget SDK, domain verification → W2
- Knowledge Auto-Builder → W3
- Handoff/booking/lead thật → W4
- Extension/Android/MXH → W5; voice → W6

## Tasks

1. Entity + EF config + migration (theo conventions hiện có; single-identity rule; tenant isolation).
2. Assistant CRUD + publish/pause API (authenticated, owner-scoped).
3. Link/QR API + generation.
4. Public resolution endpoint + hosted page (host app quyết ở W0-H; mobile-first).
5. Chat proxy → TenantAI signed ctx (reuse `TenantAiForwarder` pattern).
6. Metering wiring + budget gate.
7. Dashboard UI trong portal hiện có (UI.Platform components).
8. Tests: unit (resolution, slug, states), integration (chat flow, metering deny), security (cross-tenant, slug guessing, forged TenantId), E2E (onboard→publish→link→chat→pause).

## Acceptance / Gate

- [ ] Shop pilot thật: bật assistant <10 phút → link hoạt động mobile → grounded answer
- [ ] QR scan mở đúng shop
- [ ] Cross-tenant isolation pass (T1 pattern)
- [ ] Quota cap enforced; vượt cap không gọi LLM unmetered
- [ ] Metering deny/failure đúng policy TenantAI
- [ ] Pause → link báo "shop tạm ngưng" trong cache TTL
- [ ] Revoke/regenerate link hoạt động
- [ ] Không secret trong client artifacts
- [ ] Regression KhachLink chat pass (AT-025)

## Files likely touched

- `2_Gateway/` (facade, resolution, link controller) hoặc theo Q1
- `3_CoreHub/` (entities/config nếu Q1 chọn B)
- `9_TenantAI/vanan-ai` (chỉ nếu contract cần modify — ưu tiên không)
- `UI.Platform` (assistant page components, dashboard widgets)
- Portal/ShopERP app (dashboard screens)
- `8_Metering` (chỉ rate card seed/verify)

## Risks

- Q1 quyết sai → rework. Mitigate: W0 evidence bắt buộc.
- Hosted page host app chưa rõ → W0-H quyết.
- Rate card chưa seed → W0-E quyết trước khi code metering.
