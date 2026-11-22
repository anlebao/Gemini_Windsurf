# TASK W0: P0 EVIDENCE AUDIT — Universal AI Assistant

> Wave: W0 · Mode: ANALYZE only (no feature code)
> Master plan: `docs/AI/plans/universal-ai-assistant-master-plan.md`
> SRS: `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md` §13.1
> Output dir: `docs/AI/tasks/universal_ai_assistant/w0_audit/`

## Mục tiêu

Trả lời bằng evidence (code + DB + config + deploy), không giả định: cái gì đã tồn tại, cái gì thiếu, assistant config nên nằm ở đâu, rate card ra sao. Mọi wave sau build trên facts này.

## User value của wave này

Không có product demo — giá trị là **quyết định đúng**: tránh build trùng `PublicTenantAiController`/`AiChatWidget` đã chạy, tránh đặt Assistant entity sai chỗ, tránh pricing sai.

## Audit checklist (mỗi mục → 1 section trong report)

### A. TenantAI public contract
- [ ] `9_TenantAI/vanan-ai`: chat endpoint auth model, caller scopes, signed tenant context (HMAC?), metering gate deny/fail behavior
- [ ] `/v1/config/widget` endpoint contract
- [ ] Conversation storage: schema, retention, session model
- [ ] Knowledge ingest: input types (URL/PDF/text?), `IngestSanitizer`, usage-permission flags
- [ ] ThreatSuite T1–T10 coverage → cái gì đã được bảo vệ sẵn

### B. Gateway public facade patterns
- [ ] `PublicTenantAiController` full read — resolution logic, rate limit policy, error contract
- [ ] `PublicBookingController` pattern (anonymous public surface thứ 2)
- [ ] `TenantAiForwarder` — signing, timeout, fallback
- [ ] YARP routes hiện có liên quan public/AI

### C. Identity & session
- [ ] Account/Identity/Device Trust: anonymous/public session capability — có sẵn hay phải tạo
- [ ] Guest identity model hiện tại trên KhachLink (cookie? session?)

### D. Assistant host decision (Q1)
- [ ] CoreHub: tenant/shop config entities hiện có — chỗ nào chứa per-tenant settings phù hợp
- [ ] Gateway: scope hiện tại, có chỗ cho assistant CRUD/config không
- [ ] Đề xuất A/B/C/D theo SRS §5 + evidence → `ASSISTANT_HOST_DECISION.md`

### E. Metering & pricing (Q3)
- [ ] Rate cards hiện có trong `8_Metering` — `ai.chat.message` đã seed chưa
- [ ] Ingestion contract: event types, idempotency, metadata schema
- [ ] TenantAI `QuotaGate`/`MeteringClient` behavior khi deny/unreachable
- [ ] Cost attribution hiện tại: metering event có đủ chiều (tenant/session/channel) chưa
- [ ] → `PRICING_AND_QUOTA_MODEL.md`: rate card proposal + hard-cap defaults + attribution schema

### F. Knowledge pipeline (Q4)
- [ ] Ingest service input types + file support
- [ ] Auto-builder feasibility: extract từ menu/ảnh/PDF hiện support đến đâu
- [ ] → `KNOWLEDGE_PIPELINE_AUDIT.md`

### G. KhachLink chat relation
- [ ] `AiChatWidget` usage sites, `IAiChatClient` implementations
- [ ] Storefront chat vs future standalone link: share được gì (component? contract? session?)
- [ ] → `KHACHLINK_CHAT_RELATION.md`

### H. UI.Platform public surface
- [ ] Guest-facing page conventions: host app nào phù hợp cho standalone assistant page (KhachLink? page riêng? Gateway-served?)
- [ ] Public layout, no-login patterns hiện có

### I. Voice provider audit (Q6 — không block, làm sớm)
- [ ] `10_Voice` confirm human-to-human only; không STT/TTS/AI audio
- [ ] Provider candidates: Gemini Live API / realtime speech — capability, Vietnamese quality, streaming, barge-in, cost
- [ ] → `VOICE_PROVIDER_AUDIT.md`

### J. Deployment & ops
- [ ] Static asset hosting/CDN conventions cho JS SDK (W2)
- [ ] nginx/routes cho hosted assistant page domain (W1)
- [ ] Secrets conventions

## Deliverables (bắt buộc)

1. `AS_IS_MODIFY_MISSING_CONFLICT.md` — capability matrix theo SRS §2.3
2. `ASSISTANT_HOST_DECISION.md` — Q1 với evidence + recommendation
3. `API_CONTRACT_GAP.md` — FR → EXISTING/MODIFY/MISSING/CONFLICT
4. `PRICING_AND_QUOTA_MODEL.md` — Q3
5. `KNOWLEDGE_PIPELINE_AUDIT.md` — Q4
6. `KHACHLINK_CHAT_RELATION.md`
7. `VOICE_PROVIDER_AUDIT.md` — Q6
8. `SECURITY_THREAT_MODEL.md` — public surface mới (link slug, external domain)
9. `IMPLEMENTATION_TASK_CARDS.md` — refine W1–W6 tasks nếu audit phát hiện sai lệch
10. `P0_DECISION_REGISTER.md` — tất cả quyết định cần PO approve

## Acceptance

- Mọi claim "existing" có file:line evidence.
- Mọi "missing" có chứng minh đã search.
- Q1/Q3 có recommendation rõ ràng, không để ngỏ.
- PO sign-off trên decision register trước khi W1 IMPLEMENT.

## Dependencies

Không — W0 chạy đầu tiên. Song song được với đọc SRS/TenantAI SRS.
