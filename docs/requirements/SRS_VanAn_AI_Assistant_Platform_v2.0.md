# SOFTWARE REQUIREMENTS SPECIFICATION
# VANAN UNIVERSAL AI ASSISTANT & ZERO-CODE INTEGRATION PLATFORM
## SRS v2.0 — Second-Pass Revision

| Thuộc tính | Giá trị |
|---|---|
| Sản phẩm | Vạn An Universal AI Assistant Platform (tên thương mại: **Vạn An AI Assistant**) |
| Tài liệu | Software Requirements Specification |
| Phiên bản | 2.0 |
| Ngày lập | 22/11/2026 |
| Trạng thái | Proposed – Revised after second-pass product/architecture review |
| Định vị | **One AI Core – Multiple Surfaces – Zero-Code Integration – Unified Tenant Intelligence** |
| Supersedes | `SRS_VanAn_Universal_AI_Assistant_Zero_Code_Integration_Platform_v1.0.md`, `SRS_VanAn_Shop_AI_Assistant_v1.0.md` |
| Đối tượng thực hiện | Devin / Engineering / QA / Product Owner |
| Ngôn ngữ | Vietnamese requirements; technical identifiers in English |

> **Tuyên bố tầm nhìn (giữ nguyên, không thu hẹp):** Vạn An xây dựng **lớp giao tiếp AI dùng chung** mang năng lực hệ sinh thái (identity, knowledge, conversation, voice, workflow, metering) đến mọi điểm tiếp xúc khách hàng: Website Widget, AI Link/QR, WordPress plugin, Shopify, Facebook/Messenger, TikTok, Zalo, Browser Extension, Android. "Universal" nghĩa là **một AI Core với nhiều phương thức tích hợp theo từng môi trường** — KHÔNG phải cam kết native API integration với mọi nền tảng, và KHÔNG phải quyền vượt qua giới hạn của nền tảng thứ ba.
>
> **Đóng gói thương mại đầu tiên:** mỗi shop Vạn An có một AI assistant riêng — trả lời bằng knowledge đã duyệt của shop, tính phí vào ví shop, bật/tắt được. Đây là cách đi đầu tiên để chứng minh AI Core, không phải định nghĩa của sản phẩm.

---

# 0. DOCUMENT CONTROL

## 0.1. Lịch sử phiên bản

- **v1.0 (Universal)**: SRS gốc — đầy đủ yêu cầu, bảo mật, lộ trình P0–P4.
- **v1.1 (Shop Assistant, draft nội bộ)**: thu hẹp tầm nhìn còn "mỗi shop một assistant, Link/QR là hero". **Đã bị v2.0 thay thế** sau phản biện: việc cắt scope là tối ưu cho effort triển khai, không phải tối ưu cho giá trị sản phẩm.
- **v2.0 (bản này)**: khôi phục tầm nhìn Universal; bổ sung những gì review chỉ ra là thiếu — Knowledge Auto-Builder, capability ladder, cost control 3 cấp, conversation continuity, human handoff, AI Voice adapter riêng, và **bằng chứng implementation thực tế từ codebase**.

## 0.2. Nguyên tắc đọc tài liệu

- **MUST / SHOULD / MAY** và **P0–P4** giữ nguyên định nghĩa của v1.0.
- **P0**: audit bắt buộc, không feature code.
- **P1 Commercial MVP**: AI Core + Assistant Management + Knowledge Onboarding + Website Widget + AI Link/QR + Anonymous Session + Quota/Metering + Dashboard + Conversation History/Analytics.
- **P2 Business Integration**: WordPress plugin, Installation Manager, Assisted Installation, Human Handoff, KhachLink/Booking integration, AI Voice Adapter v1.
- **P3 Universal Distribution**: Browser Extension, Android Floating, thêm kênh MXH theo API được phép, Conversation Continuity.
- **P4 AI Business Agent**: Voice Agent hoàn chỉnh, business workflow execution, Order/Booking/CRM automation, multi-agent.

> Thứ tự trên là **ưu tiên**, không phải ràng buộc tuần tự tuyệt đối. Các interface của P3/P4 (vd: `IAiVoiceSessionAdapter`, surface abstraction) phải được thiết kế ngay từ P1 để tránh viết lại.

## 0.3. Phân biệt mức độ xác thực

Mọi mô tả "hiện hữu" phải được Devin xác minh từ codebase. **v2.0 đã kiểm chứng trực tiếp một số năng lực** (§2) — các mục còn lại vẫn là giả định chờ P0.

---

# 1. EXECUTIVE SUMMARY

## 1.1. Bài toán

Hộ kinh doanh/SME không biết code cần một trợ lý AI riêng — trả lời khách bằng chính thông tin kinh doanh của họ — hiện diện ở mọi nơi khách hàng tiếp xúc: website, Facebook, Zalo, QR tại quầy, và sau này trên mọi trình duyệt/thiết bị.

Vạn An đã có sẵn phần lớn "bộ não": Tenant AI (knowledge/RAG/policy), Metering (quota/wallet), Voice Service (realtime call), Gateway (public facade), và **một AI chat widget đang chạy thật trên KhachLink storefront**. Khoảng trống không phải "xây chatbot mới" mà là:

1. Tổng quát hóa assistant config từ "per-storefront flag" thành **Assistant entity** quản lý được (nhiều surface, publish states, branding).
2. Mở rộng surface từ KhachLink-internal sang **hosted link/QR + external website widget + các kênh khác**.
3. Xây **Knowledge Auto-Builder** để zero-code onboarding thực sự (merchant không quản lý RAG).
4. Xây lớp **capability/action** (handoff → booking → order) kết nối assistant vào nghiệp vụ ShopERP/KhachLink/Booking.

## 1.2. Product principles

- **One AI Core, Multiple Surfaces** — giữ nguyên.
- **Một Assistant, nhiều Surface**: cùng identity, knowledge, business rules, conversation policy, tenant quota, analytics. Surface chỉ là kênh hiển thị — cấm cấu hình phân mảnh theo kênh (§9.1).
- **Zero-code cho merchant** ≠ zero-labor cho Vạn An: ba cấp độ self-install / assisted-install / platform-install là **ba sản phẩm bổ sung nhau**, không loại trừ.
- **Tenant Knowledge First; AI tư vấn, không tự ý giao dịch** — action thực thi chỉ qua contract được duyệt (§8).
- **Consent before access; Secure by default.**
- **Tái sử dụng có kiểm chứng**: không giả định service tái dùng được chỉ vì folder Clean Architecture tồn tại — phải qua capability audit §2.

---

# 2. CODEBASE EVIDENCE BASELINE (NEW — verified 2026-11-22)

Đây là kết quả inspect trực tiếp, thay cho giả định. P0 audit phải xác nhận/bổ sung.

## 2.1. Đã tồn tại và đang chạy (production evidence)

| Capability | Evidence | Trạng thái |
|---|---|---|
| Public anonymous chat endpoint | `2_Gateway/Controllers/PublicTenantAiController.cs` — `POST /api/public/tenant-ai/chat`, `AllowAnonymous`, rate-limit policy `tenant-ai-public` | ✅ Production (KhachLink) |
| Server-side tenant resolution | Cùng controller: `ResolveStoreTenantAsync` — KhachLinkInstance bound to Request.Host wins, client `tenantId` ignored/validated | ✅ Implemented |
| Session abuse control | `SessionCap` config (20 msg/session/hour), `TryAcquireSessionSlot` → 429 | ✅ Implemented |
| Idempotency | `messageId` end-to-end key, vanan-ai dedupe `(tenant_id, message_id)` | ✅ Implemented |
| Graceful degradation | NFR-3: AI down → `aiEnabled:false`, storefront không phụ thuộc AI | ✅ Implemented |
| Widget UI | `UI.Platform/Components/Ai/AiChatWidget.razor` + `IAiChatClient` + `AiWidgetConfig`/`AiChatResult` (products, confidence, refused, stale, degraded) | ✅ Blazor component (KhachLink) |
| KhachLink integration | `5_WebApps/KhachLink/Services/Http/TenantAiPublicHttpService.cs`, `StoreSearchBar.razor` (semantic search one-shot) | ✅ Implemented |
| Human handoff (basic) | AI-P8: `Refused` → chip "Hỏi chủ quán trực tiếp" trong widget | ✅ Implemented (UI-level, cần verify đích đến) |
| Tenant AI engine | `9_TenantAI/vanan-ai`: IntentRouter, PolicyEngine, KnowledgeRetriever, AnswerValidator, UsagePermissionFilter, QuotaGate, MeteringClient, IngestSanitizer, ThreatSuite T1–T10 | ✅ Implemented, có test |
| Metering | `8_Metering/vanan-metering` + `MeteringClient` trong TenantAI | ✅ Implemented |
| Human voice/video call | `10_Voice/vanan-voice` + UI.Platform realtime components — video MVP vừa deploy, RV 25/25 | ✅ Production (human-to-human) |

## 2.2. Suy ra ngay (ảnh hưởng scope)

1. **"Widget" trong P1 không phải xây từ zero** — là generalize `AiChatWidget`/`IAiChatClient` pattern sang (a) hosted public page và (b) embeddable JS SDK cho site ngoài. Phần chat/session/metering/policy **đã có contract chạy thật**.
2. **Anonymous session + abuse control + tenant spoofing defense đã được giải** cho storefront context. Mở rộng sang link/QR/external domain chủ yếu là bài toán **resolution key** (slug/domain → tenant) chứ không phải auth model mới.
3. **Handoff concept đã tồn tại ở mức UI** — P2 handoff là nâng cấp đích đến (tạo lead/booking/notify staff) chứ không phải invent.
4. **AI Voice vẫn MISSING** — voice infra là WebRTC human-call; AI voice cần STT→LLM→TTS pipeline riêng (§10).

## 2.3. Capability classification (directive §3)

| # | Capability | Phân loại | Ghi chú |
|---|---|---|---|
| 1 | TenantAI chat/RAG/policy/metering gate | **Production-ready, reusable** | ThreatSuite đã cover injection/cross-tenant/leak |
| 2 | Public anonymous chat facade (Gateway) | **Implemented, cần modify** | Generalize resolution: thêm slug/link + external domain, giữ pattern hiện có |
| 3 | Widget UI | **Implemented (Blazor), cần port** | JS SDK cho external site là build mới; hosted page có thể tái dùng component |
| 4 | Assistant entity/config/publish states | **Missing** | Hiện chỉ có feature flag `aiEnabled` per tenant — chưa có Assistant aggregate |
| 5 | AI Link/QR | **Missing** | Cần slug→assistant resolution + hosted page |
| 6 | Knowledge Auto-Builder | **Missing (UX/orchestration)** | Ingest pipeline (`KnowledgeIngestService`, `IngestSanitizer`) đã có — auto-builder là lớp thu thập + chuẩn hóa + confirm phía trên |
| 7 | Domain verification | **Missing** | Mới hoàn toàn |
| 8 | External JS widget SDK | **Missing** | Shadow DOM/iframe, loader, CSP-safe |
| 9 | AI Voice (STT/LLM/TTS) | **Missing** | Adapter riêng, không đụng Voice Service |
| 10 | Browser Extension | **Missing** | P3 |
| 11 | Android Floating | **Missing** | P4/P3 |
| 12 | Shopify/FB/TikTok/Zalo integrations | **Missing, platform-dependent** | Xem §7 feasibility matrix |
| 13 | Conversation continuity cross-channel | **Missing (architecture cần thiết kế từ P1)** | §9.2 |
| 14 | Business actions (booking/order/handoff đích thực) | **Partially** | Handoff chip có; booking/order integration chưa |

---

# 3. ACTORS & USE CASES

Kế thừa §3 SRS gốc (actors table + UC-01→UC-06) với bổ sung:

- **UC-07 – Knowledge Auto-Builder onboarding** (§6.4): merchant cung cấp thông tin thô → hệ thống tự sinh knowledge profile → merchant confirm → publish.
- **UC-08 – Human handoff** (§8.3): khách hỏi vượt khả năng AI → chuyển nhân viên shop, mang theo context đã consent.
- **UC-09 – Multi-surface consistency**: cùng một khách hỏi trên widget rồi mở link — assistant nhận diện cùng tenant, cùng policy; continuity tùy consent (§9.2).

---

# 4. ARCHITECTURE DECISIONS (ADR v2.0)

| ID | Quyết định | Trạng thái |
|---|---|---|
| ADR-001 | One AI Core, Multiple Surfaces — giữ nguyên tầm nhìn Universal | Chốt |
| ADR-002 | TenantAI = reasoning/knowledge engine; **không** nhồi assistant-management vào trong nó | Chốt |
| ADR-003 | **Assistant Management** = module nghiệp vụ mới có ranh giới rõ (xem §5 — ownership quyết định ở P0 bằng evidence, ưu tiên trong hệ thống hiện hữu: candidate Gateway facade + CoreHub config store) | P0 quyết định |
| ADR-004 | **Một Assistant entity** per tenant mặc định; config chung cho mọi surface; surface chỉ khai báo kênh + override hiển thị tối thiểu | Chốt (mới) |
| ADR-005 | Public surface resolution thống nhất: mọi kênh (link slug, widget domain, extension install) resolve về `assistantRef` → server-side tenant context ký HMAC → TenantAI. **Client không bao giờ gửi TenantId authoritative** | Chốt |
| ADR-006 | Reuse `PublicTenantAiController` pattern cho public facade; generalize `ResolveStoreTenantAsync` thành `ResolveAssistantContextAsync` hỗ trợ: storefront host binding (hiện có), assistant link slug, verified widget domain | Chốt |
| ADR-007 | Cost control 3 cấp (§11.2): visitor session cap → tenant budget/quota → platform circuit breaker | Chốt (mới) |
| ADR-008 | **Per-session/per-assistant/per-source cost traceability là MUST** — mọi LLM call ghi nhận đủ chiều để bill và debug; không chỉ tổng token | Chốt (mới) |
| ADR-009 | AI Voice = lớp riêng (`IAiVoiceSessionAdapter` + provider adapter STT/TTS), tái dùng TenantAI reasoning + identity/metering; Voice Service human-call giữ nguyên | Chốt |
| ADR-010 | Knowledge Auto-Builder = orchestration layer phía trên ingest pipeline hiện có; output là knowledge documents đã sanitize đi qua `IngestSanitizer`/usage-permission hiện hữu | Chốt (mới) |
| ADR-011 | Capability ladder (§8): Answer → Recommend → Capture → Handoff → Convert/Booking → Execute — mỗi nấc là contract riêng, bật theo tenant policy + metering | Chốt (mới) |
| ADR-012 | Conversation continuity: thiết kế `ConversationId/ChannelId/VisitorRef/SessionTransferToken` từ P1 dù chưa implement cross-channel ở P3 | Chốt (mới) |
| ADR-013 | Không rewrite service đang chạy; không microservice mới trừ khi P0 chứng minh ownership/scaling/security cần | Kế thừa |
| ADR-014 | Widget chỉ trên domain shop sở hữu đã verify; không hỗ trợ "widget lên site người khác" trong v1.x | Chốt |

## 5. ASSISTANT MANAGEMENT — ownership analysis (directive §2.A)

**Vấn đề thật:** cần entity quản lý assistant (config, publish state, surfaces, links, domains, quota policy) — hiện chưa tồn tại; chỉ có feature flag `aiEnabled` per tenant.

**Ứng viên và đánh giá sơ bộ (P0 phải quyết bằng evidence):**

| Option | Ưu | Nhược | Verdict sơ bộ |
|---|---|---|---|
| A. Module trong Gateway | Đã có public facade + tenant resolution + rate limit; config đọc qua VanAnDbContext sẵn | Gateway đang là lớp mỏng — nhồi CRUD/config vào có thể phình | Candidate nếu scope giữ nhỏ (config + links + domains) |
| B. Module trong CoreHub | CoreHub là nguồn dữ liệu nghiệp vụ; assistant config là business data | CoreHub exposure ra public surface cần facade anyway | Candidate cho persistence/domain logic |
| C. Extend TenantAI | Gần knowledge/policy nhất | TenantAI là engine thuần — trộn management sẽ vi phạm boundary D-002 | Loại |
| D. Service mới | Ranh giới sạch nhất | Vi phạm "không microservice khi chưa cần"; thêm ops cost | Chỉ khi A+B chứng minh không đủ |

**Khuyến nghị P0:** điều tra A+B kết hợp (Gateway facade + CoreHub persistence theo Clean Architecture sẵn có), chỉ chọn D nếu có bằng chứng cụ thể. Quyết định ghi vào `ASSISTANT_HOST_DECISION.md`.

## 6. KNOWLEDGE ONBOARDING — Auto-Builder (directive §2.F)

**Vấn đề:** merchant không nên phải học RAG/document management. Ingest pipeline đã có (`KnowledgeIngestService`, `IngestSanitizer`, chunking, usage-permission) — thiếu **lớp thu thập + chuẩn hóa + xác nhận**.

### 6.1. Merchant Knowledge Auto-Builder flow

1. **Thu thập**: shop name, ngành nghề, địa chỉ, SĐT, website URL (nếu có), ảnh/menu/bảng giá upload, hoặc chọn nguồn TenantAI đã ingest.
2. **Trích xuất**: AI parse tài liệu → sản phẩm/dịch vụ/giá/giờ HC/chính sách/FAQ candidates. Tái dùng `IngestSanitizer` + structured extraction của TenantAI.
3. **Knowledge Profile**: hệ thống tổng hợp thành profile có cấu trúc per tenant.
4. **Confidence check**: sinh test questions, phát hiện missing/conflict/stale (giá không rõ, chính sách mâu thuẫn) → readiness checklist.
5. **Merchant confirm**: UI review-diff đơn giản — sửa/xác nhận/xóa; KHÔNG bắt merchant hiểu "knowledge base" hay "RAG".
6. **Publish**: knowledge đi qua pipeline ingest chuẩn → assistant publish.

### 6.2. Yêu cầu

| ID | Requirement | Priority |
|---|---|---|
| KB-001 | Wizard MUST tạo được knowledge profile hữu ích từ input tối thiểu (tên shop + 1 nguồn bất kỳ: URL/menu/FAQ text) | P1 |
| KB-002 | Mọi extracted knowledge MUST qua `IngestSanitizer` + usage-permission trước khi public | P1 |
| KB-003 | Hệ thống MUST sinh test questions + chỉ ra thông tin thiếu/mâu thuẫn | P1 |
| KB-004 | Merchant MUST confirm trước khi knowledge public; không auto-publish | P1 |
| KB-005 | Owner MUST chỉnh sửa/xóa từng mục knowledge dễ dàng (plain language) | P1 |
| KB-006 | Crawl website tự động: SHOULD nhưng best-effort, respect robots/CSP, không SSRF (§6.2 gốc FR-029/030) | P2 |
| KB-007 | Knowledge freshness: giá/menu thay đổi phải có cơ chế cập nhật + stale warning (kế thừa FR-157) | P1 |

## 7. INTEGRATION FEASIBILITY MATRIX (directive §1)

| Kênh | Phương thức | Khả thi | Giới hạn nền tảng | Phase |
|---|---|---|---|---|
| Website doanh nghiệp | JS Widget (loader + Shadow DOM/iframe) | **Cao** — engine chat đã có, cần SDK + domain verify | CSP/ad-blocker của host | P1/P2 |
| Hosted AI Link + QR | Link `/a/{slug}` + QR | **Cao** — chỉ cần resolution + hosted page | Không có | P1 |
| WordPress | Plugin gọi cùng public contract | **Cao** sau audit version | WP.org review nếu public | P3 |
| Shopify | App/embed theo platform rules | Trung bình — phụ thuộc app review | Shopify policies | P3+ |
| Facebook Fanpage | AI Link trong bio/CTA/button; Messenger integration nếu được Meta cho phép | Link/QR: cao. Messenger bot: cần Meta app review + policy | **Không nhúng widget vào FB UI** — đây là giới hạn nền tảng, không phải scope | P2/P3 |
| TikTok | Link bio/QR/pinned link khi live; API chính thức nếu có | Link/QR: cao. Live overlay: không tự ý | Không scrape live/comments trái phép | P2/P3 |
| Zalo | Link/QR; Zalo OA API nếu được phép | Link/QR: cao. OA auto-reply: cần OA verified + API | Zalo platform policy | P3 |
| Website bất kỳ (người dùng tự cài) | Browser Extension | Trung bình — store review + permission model | Không bypass browser permissions | P3 |
| Android | Floating overlay app | Thấp-trung bình — OEM restrictions, Play policy | USER must grant overlay; không iOS | P3/P4 |

**Nguyên tắc:** mỗi kênh được đánh giá độc lập; không kênh nào bị loại chỉ vì khó — nhưng cũng không kênh nào được claim vượt giới hạn nền tảng.

## 8. CAPABILITY LADDER & BUSINESS ACTIONS (directive §2.G, §9.3-9.4)

Assistant không chỉ trả lời — là lớp giao tiếp nối vào nghiệp vụ. Mỗi nấc là contract riêng:

| Nấc | Năng lực | Ví dụ | Phase | Ghi chú |
|---|---|---|---|---|
| L1 Answer | Trả lời từ knowledge | "Giờ mở cửa?", "Giá X?" | P1 | Đã có nền (TenantAI) |
| L2 Recommend | Gợi ý sản phẩm/dịch vụ | Product cards trong answer — **đã có** (`AiProductCardItem`) | P1 | Mở rộng catalog source |
| L3 Capture | Thu lead (tên/SĐLZ) có consent | Form trong widget khi refuse/không biết | P2 | Gắn tenant CRM/KhachLink |
| L4 Handoff | Chuyển nhân viên | Chip "Hỏi chủ quán" → nâng cấp: tạo ticket/notify staff kèm context | P2 | Nâng cấp từ AI-P8 hiện có |
| L5 Convert | Tạo yêu cầu mua/booking | "Đặt lịch", "Giữ hàng" → Booking/KhachLink flow | P2-P3 | Qua contract được duyệt, không tự commit |
| L6 Execute | Thực hiện nghiệp vụ được cấp quyền | Order/booking automation có confirm step | P4 | Cần policy engine + audit + approval |

**Nguyên tắc an toàn (kế thừa):** AI không tự xác nhận giao dịch/đổi giá/tồn kho; L5+ luôn qua existing approved flow (cart/booking) hoặc explicit staff approval.

### 8.3. Human handoff design (P2)

- Trigger: `Refused` liên tiếp, keyword escalation, hoặc user yêu cầu.
- Handoff packet: session context đã consent (câu hỏi, intent, contact nếu có) → tạo lead/notification cho staff theo kênh shop dùng (KhachLink inbox, Zalo, email — tùy tích hợp).
- Consent: user phải biết thông tin sẽ chuyển cho người thật.

## 9. MULTI-SURFACE CONSISTENCY & CONTINUITY (directive §9.1-9.2)

### 9.1. Nhất quán bắt buộc

Mọi surface của cùng một assistant chia sẻ: identity, knowledge, business rules, conversation policy, tenant quota, analytics. Surface config chỉ được override: vị trí nút, màu, greeting hiển thị — **cấm** override knowledge/policy/quota theo kênh.

### 9.2. Conversation continuity architecture (thiết kế từ P1)

```text
ConversationId  — logical thread xuyên kênh (khi được phép)
ChannelId       — surface phát sinh (widget|link|extension|android|...)
VisitorRef      — anonymous opaque ref per channel; KHÔNG đồng nhất với user account
SessionTransferToken — short-lived, single-use, signed — chuyển context khi user chủ động
TenantId        — server-resolved, không bao giờ từ client
```

- P1: mỗi channel một session riêng (không continuity).
- P3: continuity opt-in khi visitor đăng nhập Vạn An hoặc explicit transfer token (vd: link trong chat → mở widget giữ context).
- Anonymous sessions KHÔNG bao giờ silently merge.

## 10. VOICE — AI Voice Adapter (directive §2.D)

**Hiện trạng (verified):** `10_Voice/vanan-voice` = human-to-human WebRTC (audio+video vừa ship). **Không có** STT/LLM/TTS pipeline → AI voice = missing, đúng như đánh giá trước.

**Kiến trúc đề xuất:**

```text
Surface (widget/link/app)
   │  audio frames / text
   ▼
IAiVoiceSessionAdapter          ← interface mới, provider-agnostic
   ├── GetCapabilities(assistantId, surface)
   ├── StartSession / SendAudio / ReceiveAssistantAudio / StopSession
   ▼
VoiceProviderAdapter            ← STT + TTS + turn-taking implementation
   ├── Provider A (vd: realtime speech API)
   └── Provider B (future swap — không viết lại widget)
   ▼
TenantAI (reasoning, policy, metering — giữ nguyên)
```

- P0 audit PHẢI xác minh: provider capability thực tế (Vietnamese speech quality, streaming, barge-in, latency, cost), không giả định.
- Reuse: identity, tenant policy, metering (`ai.voice.session`/`ai.voice.minute` rate cards), consent.
- Không route AI speech qua human-call workflow; không ghi âm mặc định; lộ trình: **chat-first, voice ẩn đến khi adapter pass test** (P2 target cho adapter v1).

## 11. SECURITY & COST CONTROL (directive §2.E)

### 11.1. Security

Kế thừa toàn bộ SEC-001→024 của v1.0 + các control đã có sẵn trong code (server-side tenant resolution, session cap, rate limit policy, idempotency). Bổ sung:

- **SEC-025**: Mọi LLM call phải mang cost attribution đủ chiều (tenant, assistant, channel, session, visitorRef hash, feature) — phục vụ billing + abuse detection.
- **SEC-026**: Platform-level circuit breaker per tenant và global — ngắt LLM calls khi anomaly (cost spike, error rate) vượt ngưỡng.
- **SEC-027**: SessionTransferToken single-use, short TTL, signed, gắn consent scope.

### 11.2. Cost control 3 cấp (directive §2.E)

| Cấp | Đối tượng | Cơ chế |
|---|---|---|
| Visitor | anonymous session | rate limit per-IP/session/assistant; session message cap; max context length; conversation turn limit |
| Tenant | shop budget | AI budget/tháng (default theo gói); token cap per session; degrade structured-only khi chạm cap; dashboard cảnh báo |
| Platform | toàn hệ | global budget + circuit breaker; anomaly detection (cost spike per tenant/IP); kill switch per assistant/domain/platform |

Nguyên tắc: tách chi phí AI khỏi storage/bandwidth; một tenant không được ảnh hưởng tenant khác; không silent unmetered LLM call trong mọi failure mode.

## 12. COMMERCIAL MODEL (directive §5)

> **Lưu ý trung thực:** con số dưới đây là **framework + giả định minh họa**, phải validate bằng Metering production data + pilot. Không phải cam kết.

### 12.1. Cost drivers

- LLM cost per message: phụ thuộc model + context length (RAG chunks). Giả định minh họa: ~1-5k input tokens + ~300-800 output tokens/message → chi phí biên cần đo thật theo provider pricing.
- Infra cost per active tenant: nhỏ (config + links) — chi phí chủ yếu là LLM usage.
- Assisted install: labor cost per job — đo trong pilot.

### 12.2. Revenue streams (kết hợp, không chọn 1)

| Stream | Mô tả | Ghi chú |
|---|---|---|
| Subscription | Gói assistant/tháng gồm quota messages | Đơn giản, dễ bán SME |
| Usage fee | vượt quota → per-message hoặc block+nâng gói | Metering-native |
| Setup fee | assisted installation | Dịch vụ có SKU |
| Knowledge management fee | auto-builder premium, cập nhật định kỳ | SHOULD sau pilot |
| Add-on | voice minutes, multi-assistant, advanced integrations | P2+ |

### 12.3. Break-even sketch (illustrative, assumptions stated)

Giả định minh họa: blended LLM cost ~X VND/message (điền sau khi có rate card thật), subscription ~Y VND/shop/tháng gồm Z messages, assisted install ~W VND/job.

| Scale | Câu hỏi then chốt |
|---|---|
| 100 tenants | Self-serve ratio ≥60%? Support tickets/shop/tháng? |
| 1.000 tenants | Abuse rate? Churn do quota? Assisted→self migration rate? |
| 10.000 tenants | Unit economics margin? Platform circuit breaker hoạt động? |

**Metrics bắt buộc đo từ pilot:** cost/conversation, cost/active tenant, abuse scenarios, self-serve install rate, assisted install time + cost, support burden, 30-day self-management rate.

## 13. ROADMAP P0–P4 (directive §4 — revised)

### P0 – Audit (no feature code)

Deliverables bắt buộc:

1. Capability audit theo §2.3 — xác nhận/phân loại lại từng mục.
2. `ASSISTANT_HOST_DECISION.md` — quyết §5 (A/B/C/D) bằng evidence.
3. `API_CONTRACT_GAP.md` — map FR → EXISTING/MODIFY/MISSING/CONFLICT.
4. `KHACHLINK_CHAT_RELATION.md` — quan hệ widget mới vs chat KhachLink hiện có (cùng backend, surface khác — phải ghi rõ).
5. `PRICING_AND_QUOTA_MODEL.md` — rate card, hard-cap defaults, cost attribution schema.
6. `SECURITY_THREAT_MODEL.md` + `COST_ABUSE_MODEL.md`.
7. `INTEGRATION_FEASIBILITY.md` — chi tiết §7: FB Messenger API hiện trạng, TikTok API, Zalo OA, Shopify — những gì thật sự khả dụng hôm nay.
8. `VOICE_PROVIDER_AUDIT.md` — STT/TTS/realtime provider capability + Vietnamese quality + cost.
9. `IMPLEMENTATION_TASK_CARDS.md` + `P0_DECISION_REGISTER.md`.

### P1 – Commercial MVP

- Assistant Management module (theo quyết định P0) + publish states.
- **Generalize public facade**: `ResolveAssistantContextAsync` (storefront + link slug + verified domain).
- Hosted AI Link page (mobile-first) + QR.
- External JS widget SDK v1 (loader + Shadow DOM) trên domain đã verify.
- Knowledge Auto-Builder v1 (KB-001→005).
- Anonymous session + 3-level cost control + per-source attribution.
- Merchant dashboard + conversation history + basic analytics.
- Human handoff v1 (nâng cấp chip hiện có → lead/notify).
- Voice UI ẩn (adapter contract định nghĩa, chưa implement).

**Gate P1:** shop thật bật assistant → khách chat qua link VÀ widget trên site thật → grounded answer → quota enforced → cost attribution truy vết được → isolation tests pass.

### P2 – Business Integration

- WordPress plugin; Installation Manager; Assisted Installation (có SKU).
- Human handoff đầy đủ (L4) + L5 Convert/Booking integration với KhachLink/Booking.
- AI Voice Adapter v1 (nếu provider audit pass).
- Conversation continuity opt-in (authenticated).
- Shopify app (nếu platform review thuận lợi).

### P3 – Universal Distribution

- Browser Extension (Chrome/Edge), Android Floating.
- Zalo OA / FB Messenger integrations theo API được phép.
- Cross-channel continuity đầy đủ.

### P4 – AI Business Agent

- Voice Agent hoàn chỉnh; L6 Execute workflows; order/booking/CRM automation; multi-agent.

## 14. TESTING & ACCEPTANCE

Kế thừa §14 v1.0 + bổ sung:

| Test | Scenario | Expected |
|---|---|---|
| AT-032 | Cùng assistant trên link + widget | Cùng identity/policy/quota; analytics gộp đúng |
| AT-033 | Cost attribution | Mọi LLM call trace được tenant+assistant+channel+session |
| AT-034 | Tenant vượt budget | Degrade/block theo policy; tenant khác không ảnh hưởng |
| AT-035 | Platform anomaly spike | Circuit breaker kích hoạt; alert |
| AT-036 | Knowledge auto-builder | Profile hữu ích từ input tối thiểu; merchant confirm bắt buộc |
| AT-037 | Handoff | Refuse/escalation → lead tạo đúng, context consented |
| AT-038 | SessionTransferToken | Single-use, expired → reject; không merge anonymous trái phép |
| AT-025 | Regression KhachLink/Gateway/ShopERP/Voice | Pass |

## 15. DEVIN IMPLEMENTATION INSTRUCTIONS

Kế thừa §15 v1.0 + bổ sung:

1. **Đọc §2 evidence trước** — nhiều capability đã tồn tại; đừng đề xuất build lại.
2. P0 audit theo §13 deliverables — đặc biệt `ASSISTANT_HOST_DECISION` và `KHACHLINK_CHAT_RELATION`.
3. Mọi FR → EXISTING/MODIFY/MISSING/CONFLICT trước khi code.
4. Thiết kế interface P3/P4 (voice adapter, surface abstraction, transfer token) ngay từ P1.
5. Không silently remove requirement; không mock production behavior.

## 16. RISKS

Kế thừa §16 v1.0 + bổ sung:

| Risk | Mitigation |
|---|---|
| Scope Universal quá lớn cho 1 team | Phase gates nghiêm; mỗi phase ship độc lập; interface-first cho phase sau |
| Knowledge quality kém → assistant trả lời sai → mất niềm tin | Auto-builder confidence check + merchant confirm + freshness/stale warnings |
| Cost abuse từ anonymous surface | 3-level control + attribution + circuit breaker (P1 MUST) |
| "Universal" bị hiểu là tích hợp mọi nền tảng ngay | Truyền thông rõ: universal = một core nhiều phương thức, theo phase |
| AI Voice provider lock-in | Provider adapter interface; audit trước khi commit |
| Phân mảnh config theo kênh | ADR-004: một assistant config chung, surface chỉ override hiển thị |

## 17. OPEN DECISIONS FOR PO

| ID | Decision | Recommendation |
|---|---|---|
| PO-001 | Pilot shop | Shop Vạn An có knowledge sạch + owner nhiệt tình |
| PO-002 | Link domain | Subdomain Vạn An kiểm soát |
| PO-008 | Pricing | Subscription + usage cap; rate card approve trước production |
| PO-016 (mới) | Assistant free hay paid trong pilot | Free quota nhỏ để đo adoption |
| PO-017 (mới) | Knowledge auto-builder: crawl website có auto không | Opt-in, best-effort, sau KB-001→005 chạy tốt |
| PO-018 (mới) | Handoff đích đến đầu tiên | KhachLink inbox/lead — vì đã có sẵn storefront context |
| PO-019 (mới) | AI Voice provider | Chờ `VOICE_PROVIDER_AUDIT.md` |

## 18. FINAL BASELINE

**Vạn An AI Assistant = lớp giao tiếp AI thống nhất của hệ sinh thái: một Assistant per tenant, một AI Core, nhiều Surface, zero-code onboarding, capability tăng dần từ trả lời → hành động nghiệp vụ.**

- P1 chứng minh: merchant không biết code bật được assistant riêng, khách chat được qua link/QR/widget, chi phí đo và kiểm soát được.
- P4 hướng tới: mỗi SME có trợ lý số tiếp cận khách hàng, tư vấn, tiếp nhận nhu cầu và **kết nối trực tiếp vào vận hành kinh doanh** — đây là khác biệt với chatbot thông thường.

**Universal không phải "cài được ở mọi nơi". Universal là "một bộ não, mọi điểm chạm, từng kênh một cách tích hợp phù hợp".**

---

**END OF SRS – VANAN UNIVERSAL AI ASSISTANT & ZERO-CODE INTEGRATION PLATFORM v2.0**
