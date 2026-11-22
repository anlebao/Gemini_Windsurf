# MASTER PLAN: VANAN UNIVERSAL AI ASSISTANT PLATFORM

> Created: 2026-11-22 — based on `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md`
> Status: **PROPOSED — chờ PO approve trước IMPLEMENT**
> Mode: `newfeaturebuild.md` ANALYZE → IMPLEMENT
> Supersedes định hướng: `SRS_VanAn_Shop_AI_Assistant_v1.0.md` (đã bị v2.0 thay thế)

---

## 1. NGUYÊN TẮC THỰC THI: MỖI WAVE = MỘT SẢN PHẨM DÙNG ĐƯỢC

Phản biện 22/11/2026 yêu cầu rõ: **không cắt tầm nhìn để giảm complexity; mỗi wave phải tạo ra giá trị sản phẩm thật có thể demo/bán được**, không phải "lát cắt kỹ thuật" (xong backend mà chưa ai dùng được).

Do đó roadmap được thiết kế lại thành **W0–W6 theo giá trị giao được**, không theo layer kỹ thuật:

| Wave | Sản phẩm giao được (demo-able) | Giá trị kinh doanh ngay |
|---|---|---|
| **W0 — Evidence Audit** | Bản đồ "cái gì đã có / còn thiếu" + quyết định kiến trúc có bằng chứng | Tránh build trùng; quyết định đúng ngay từ đầu |
| **W1 — AI Link/QR Assistant** | Shop owner bật trợ lý → nhận link/QR → khách chat text trên điện thoại | **Bán được ngay**: "trợ lý AI riêng cho shop, dùng bằng link" |
| **W2 — Website Widget** | Cùng assistant đó hiện trên website của shop (đã verify domain) | Upsell: shop có web được trải nghiệm embedded |
| **W3 — Zero-Code Knowledge Onboarding** | Wizard 5 phút: upload menu/ảnh/bảng giá → AI tự dựng knowledge → merchant confirm | Giảm onboarding friction = tăng self-serve ratio |
| **W4 — Business Actions (Handoff→Booking)** | AI chuyển khách sang nhân viên / tạo yêu cầu đặt lịch / lead capture | **Moat**: khác biệt với chatbot thường — nối vào ShopERP/KhachLink/Booking |
| **W5 — Universal Distribution** | Browser Extension, Android floating, Zalo/FB integrations theo API | Mở rộng điểm chạm |
| **W6 — AI Voice & Business Agent** | Nói chuyện với AI bằng giọng Việt; AI thực hiện nghiệp vụ được cấp quyền | Tầm nhìn dài hạn |

**Quy tắc:** W(n+1) không bắt đầu trước khi W(n) pass gate. W1–W3 có thể ship độc lập; W4 phụ thuộc nền session/identity của W1–W2; W5/W6 phụ thuộc interface đã thiết kế từ W1.

---

## 2. HIỆN TRẠNG ĐÃ VERIFY (2026-11-22) — nền móng có sẵn

| Thành phần | Evidence | Ý nghĩa cho plan |
|---|---|---|
| Public anonymous chat | `2_Gateway/Controllers/PublicTenantAiController.cs` — rate limit, session cap 20/h, idempotency, server-side tenant resolution | W1 tái dùng facade pattern; chỉ cần thêm resolution key (slug) |
| Widget UI | `UI.Platform/Components/Ai/AiChatWidget.razor` + `IAiChatClient` (products, refused, confidence, degraded) | W1 hosted page + W2 SDK tái dùng component/contract |
| KhachLink integration | `TenantAiPublicHttpService`, `StoreSearchBar` semantic search | W1 link page = generalize storefront context sang standalone |
| Handoff chip | AI-P8: `Refused` → "Hỏi chủ quán trực tiếp" | W4 nâng cấp thành lead/ticket thật |
| TenantAI engine | IntentRouter, PolicyEngine, Retriever, AnswerValidator, UsagePermissionFilter, QuotaGate, MeteringClient, IngestSanitizer, ThreatSuite T1–T10 | W1–W4 dùng nguyên, không sửa engine |
| Metering | `8_Metering/vanan-metering` + MeteringClient | Cost control + billing sẵn cơ chế |
| Voice human-call | `10_Voice/vanan-voice` + video MVP deployed | W6 AI voice = adapter riêng, không đụng |
| **MISSING** | Assistant entity/config/publish, AI Link/QR, external JS SDK, domain verification, Knowledge Auto-Builder, AI voice, extension, Android | Đây là phần build thật |

---

## 3. QUYẾT ĐỊNH KIẾN TRÚC CẦN CHỐT Ở W0

| # | Câu hỏi | Deliverable W0 | Ảnh hưởng |
|---|---|---|---|
| Q1 | Assistant Management nằm đâu? (Gateway module / CoreHub / service mới) | `ASSISTANT_HOST_DECISION.md` | W1 schema + API |
| Q2 | Public facade generalize thế nào? (slug/domain/storefront resolution) | `API_CONTRACT_GAP.md` | W1–W2 endpoints |
| Q3 | Rate card `ai.chat.message` đã seed chưa? Cost attribution schema? | `PRICING_AND_QUOTA_MODEL.md` | W1 metering |
| Q4 | Knowledge ingest hiện nhận loại nguồn nào? Auto-builder build trên gì? | `KNOWLEDGE_PIPELINE_AUDIT.md` | W3 |
| Q5 | Handoff đích đến: KhachLink inbox hay kênh khác? | `HANDOFF_TARGET_DECISION.md` | W4 |
| Q6 | AI voice provider capability (STT/TTS VN, latency, cost)? | `VOICE_PROVIDER_AUDIT.md` | W6 (không block W1–W5) |
| Q7 | Widget JS SDK: Shadow DOM vs iframe — compatibility với storefront hiện có? | ghi trong W2 task | W2 |

---

## 4. WAVE BREAKDOWN

### W0 — Evidence Audit (1–2 tuần, không code feature)

**Sản phẩm giao:** 7 decision documents + capability matrix. Giá trị: mọi wave sau build trên facts, không assumptions.

- Audit TenantAI public contract chi tiết (auth, scopes, signed ctx, metering gate behavior khi deny/fail).
- Audit Gateway facade patterns (`PublicTenantAiController`, `PublicBookingController`) → pattern chuẩn cho public surface mới.
- Audit Account/Identity anonymous session capability.
- Audit CoreHub: chỗ nào chứa tenant/shop config phù hợp cho Assistant entity.
- Audit Metering rate cards hiện có + ingestion contract.
- Audit knowledge ingest pipeline (input types, sanitizer, permissions).
- Audit UI.Platform conventions cho public-facing page (guest, no-login).
- Audit KhachLink chat relation: cái gì share, cái gì khác.

**Gate W0:** PO approve `P0_DECISION_REGISTER.md` — đặc biệt Q1 (host), Q3 (pricing).

### W1 — AI Link/QR Assistant (P1 hero)

**Sản phẩm giao được:** Chủ shop login → bật "Trợ lý AI" (wizard tối giản) → nhận link `v.a.i/{slug}` + QR → khách mở link trên điện thoại chat text với assistant của shop → owner thấy usage. **Demo được ngay, bán được ngay.**

Phạm vi:

- `TenantAssistant` entity + publish states (Draft/Preview/Published/Paused/Suspended) — theo quyết định Q1.
- Public resolution: `slug → assistant → tenant` server-side (mở rộng `ResolveStoreTenantAsync` pattern).
- Hosted assistant page mobile-first (tái dùng `AiChatWidget` logic/contract; guest surface không cần login).
- AI Link management: create/revoke/regenerate + QR PNG/SVG.
- Metering: `ai.chat.message` usage + per-assistant cap (W0 chốt rate card).
- Cost control cấp visitor+tenant tối thiểu (rate limit đã có pattern; thêm per-assistant budget check).
- Owner dashboard tối thiểu: trạng thái, link/QR, usage tháng, pause/resume — đặt trong portal/ShopERP hiện có.
- Knowledge tối thiểu: chọn nguồn TenantAI đã ingest + FAQ thủ công ngắn (Auto-Builder là W3).
- Kill switch + audit events.

**KHÔNG làm ở W1:** external widget, domain verification, auto-builder, handoff thật, voice.

**Gate W1:** 1 shop pilot thật → link hoạt động trên mobile → grounded answer → quota enforced → cross-tenant isolation pass → không secret client-side.

### W2 — Website Widget cho site của shop

**Sản phẩm giao được:** Cùng assistant W1 hiện floating widget trên website shop sở hữu. Khách chat ngay trên trang, không rời site.

Phạm vi:

- `WebsiteDomain` + `WidgetInstance` entities; domain verification: DNS TXT + meta-file + **operator manual review fallback** (SME VN cần).
- External JS widget SDK v1: loader async, Shadow DOM/iframe (quyết ở W2), config fetch public-không-secret, kill switch, unsubscribe cleanup.
- Cùng `assistantRef`/tenant context như link — một assistant, mọi surface (ADR-004).
- Widget chỉ trên domain verified; xóa domain → widget chết, link vẫn sống.
- Installation health check cơ bản (loader reachable, config fetch, runtime init, test request).
- Giữ hosted link page hoạt động song song — widget là surface thứ 2 của cùng assistant.

**Gate W2:** widget chạy trên ≥1 site test thật đã verify; unverified domain reject; widget fail không hỏng host page; CSP/ad-blocker diagnostics.

### W3 — Zero-Code Knowledge Onboarding (Auto-Builder)

**Sản phẩm giao được:** Owner không cần "hiểu RAG" — upload menu/ảnh/bảng giá/FAQ hoặc nhập URL → hệ thống tự dựng knowledge profile → test questions → owner confirm → assistant trả lời tốt ngay.

Phạm vi (theo SRS §6 KB-001→007):

- Knowledge Auto-Builder: thu thập → extract (tái dùng ingest pipeline + sanitizer) → profile → confidence check (test questions, missing/conflict) → merchant confirm → publish.
- Knowledge management UI plain-language: xem/sửa/xóa từng mục.
- Freshness: stale warning khi giá/menu cũ.
- (SHOULD) website crawl best-effort opt-in.

**Gate W3:** owner hoàn thành knowledge setup <15 phút không cần support; assistant trả lời đúng trên test set do owner duyệt.

### W4 — Business Actions: Handoff + Convert/Booking

**Sản phẩm giao được:** AI không chỉ trả lời — chuyển khách sang người thật, tạo lead, hỗ trợ đặt lịch/giữ hàng qua flow hiện có. **Đây là moat.**

Phạm vi:

- L4 Handoff đầy đủ: trigger (refuse lặp, escalation keyword, user request) → handoff packet (consent-scoped context) → notify staff qua kênh shop dùng (KhachLink inbox trước — Q5).
- L3 Capture: lead form trong widget khi cần (tên/SĐT có consent).
- L5 Convert/Booking: "đặt lịch"/"giữ hàng" → deep-link/contract sang Booking/KhachLink approved flow — AI không tự commit.
- Conversation history per shop cho staff review (scoped, audited).
- (Nếu W0 voice audit sớm xong) AI Voice Adapter v1 spike — không block wave này.

**Gate W4:** kịch bản thật — khách hỏi → AI không biết → handoff → staff nhận context → khách được phục vụ; booking request tạo được trong Booking system.

### W5 — Universal Distribution

**Sản phẩm giao được:** Extension Chrome/Edge (user-triggered panel trên site được phép), Android floating (nếu P4 kéo lên), Zalo OA/FB Messenger theo API được phép, conversation continuity opt-in.

Phạm vi theo SRS §7 feasibility matrix + §9.2 continuity (SessionTransferToken).

**Gate W5:** từng kênh pass store/policy review + isolation/consent tests.

### W6 — AI Voice & Business Agent

**Sản phẩm giao được:** nói chuyện với assistant bằng tiếng Việt; AI thực hiện nghiệp vụ được cấp quyền (L6 Execute) có approval/audit.

Phạm vi: `IAiVoiceSessionAdapter` + provider adapter (theo `VOICE_PROVIDER_AUDIT.md`), voice metering rate cards, business workflow execution có policy + audit trail.

**Gate W6:** voice session pass latency/quality bar; execute actions luôn qua approved flow + audit.

---

## 5. DEPENDENCY GRAPH

```text
W0 Audit ──► W1 Link/QR ──► W2 Widget ──► W4 Business Actions ──► W6 Agent
                │              │              ▲
                └──► W3 Auto-Builder ─────────┘   (W3 song song sau W1)
                              │
W5 Distribution ── cần interface W1 (assistant/session/transfer token)
```

- W1 là **critical path** — mọi thứ sau cần assistant entity + public resolution.
- W3 có thể chạy song song W2 (khác team surface được nếu đủ người).
- W5/W6 interface design bắt đầu từ W1 (đừng để P3/P4 phải viết lại).

---

## 6. RISK & CONTROL TỔNG

| Risk | Control |
|---|---|
| Build trùng những gì đã có | W0 audit bắt buộc; §2 evidence table là baseline |
| Scope creep "universal" | Mỗi wave có gate riêng; không mở wave sau khi wave trước chưa pass |
| Cost abuse anonymous | Cost control 3 cấp từ W1 (visitor cap + tenant budget); platform breaker W1 tối thiểu |
| Knowledge kém → mất niềm tin | W3 auto-builder + confirm; pilot chọn shop có data sạch |
| Platform limits (FB/TikTok/Play Store) | Feasibility matrix §7 SRS; không claim vượt giới hạn |
| AI voice provider lock-in | Provider adapter interface; audit trước commit |

---

## 7. TASK CARDS

```
docs/AI/tasks/universal_ai_assistant/
├── task_w0_p0_audit.md              — audit + decision docs
├── task_w1_ai_link_qr.md            — P1 hero: link/QR + chat + metering + dashboard
├── task_w2_website_widget.md        — external JS SDK + domain verification
├── task_w3_knowledge_onboarding.md  — Auto-Builder
├── task_w4_business_actions.md      — handoff + capture + booking integration
├── task_w5_universal_distribution.md— extension/android/mxh channels
└── task_w6_ai_voice_agent.md        — voice adapter + business agent
```

Mỗi task card: objective, user value, scope in/out, concrete tasks, acceptance, dependencies, files likely touched, gate.

---

## 8. NEXT ACTION

1. PO review master plan này + SRS v2.0.
2. Approve → bắt đầu **W0 audit** (task_w0_p0_audit.md) theo `newfeaturebuild.md` ANALYZE.
3. W0 xong → PO approve decision register → IMPLEMENT W1.
