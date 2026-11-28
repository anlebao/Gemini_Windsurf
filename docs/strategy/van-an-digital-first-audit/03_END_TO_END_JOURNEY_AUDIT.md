# 03 — END-TO-END JOURNEY AUDIT

Journeys traced against actual code paths. "RV" = runtime-verified evidence recorded in `docs/AI/project_state.md` / task cards (deployed commit + Playwright/API sweep). Status per journey: ✅ verified end-to-end · 🟡 works with gaps · ❌ broken/missing steps.

---

## J1 — Customer discovers a nearby shop and opens its digital presence

**Status: ✅ VERIFIED**

- **Entry:** Directory SSR (`timlathay.com`, `diemthuong2.khachvip.online`) or KhachLink `/stores` (StoreFinder) or QR/deep link.
- **Frontend:** `5_WebApps/Directory/Components/Pages/Home.razor` (search modes store/product, `VanAnPublicSearchBar`, `VanAnDidYouMeanChip`) → `StoreFinder.razor`; `5_WebApps/KhachLink/Pages/StoreFinder.razor`, `Store.razor`.
- **API path:** `GET /api/tenants/search` (Gateway `TenantSearchController` → `ITenantSearchService` single-source: normalized contains→token→fuzzy + fold map + DidYouMean) → tenant rows; `GET /api/public/tenants/{slug}` store-info → `ShopConfig`.
- **Data stores:** Gateway PG `Tenants`, `KhachLinkInstances`, `ShopFeatureSettings`; ShopERP SQLite product catalog via YARP forward for browse.
- **External deps:** none for search; maps tiles external.
- **Auth/authz:** Anonymous public read; rate-limited search.
- **Tenant boundary:** Server-side resolution; `InstanceFeatureGate` can hide sections per portal domain.
- **Success:** Search → results → store page with products/services/AI/voice entries (contextual entries shipped W3-1/W3-2c/W3-2d; RV prod PASS on multiple specs).
- **Failure:** Empty results + did-you-mean chip; AI degraded flag; instance kill-switch returns empty gated sections.
- **Tests/evidence:** `w3-2c/w3-2d` search specs, `directory-retention.spec.ts`, `contextual-ai-entries.spec.ts` — prod PASS.
- **Missing steps:** Sponsored placement (none); freshness SLA; ranking explainability.

## J2 — Customer asks shop AI a product/service question, gets tenant-grounded answer

**Status: ✅ VERIFIED**

- **Entry:** `AiChatWidget` on storefront, `/a/{slug}` assistant page, or iframe embed.
- **Frontend:** `UI.Platform/Components/Ai/AiChatWidget.razor` → `IAiChatClient` → `TenantAiPublicHttpService`.
- **API path:** `POST /api/public/tenant-ai/chat` (`PublicTenantAiController`) → resolve tenant (host/validated tenantId/assistant slug) → sign ctx (HMAC) → `vanan-ai POST /v1/chat` → IntentRouter→PolicyEngine→KnowledgeRetriever (pgvector)→AnswerValidator→LLM (Gemini) → product cards/structured answer.
- **Data stores:** vanan-ai PG+pgvector (knowledge chunks, conversations); Gateway PG (tenant resolution, feature flag); ShopERP SQLite (product sync source via `ProductsBatchRequest`).
- **External deps:** Google Gemini API (`generativelanguage.googleapis.com`).
- **Auth/authz:** Anonymous + `tenant-ai-public` rate limit + session cap 20 msg/h + idempotency `messageId` + QuotaGate wallet check.
- **Tenant boundary:** Signed tenant ctx server-side; `RequireTenantContext` upstream; usage permissions per classification/channel.
- **Success:** Grounded answer + product cards; degraded structured-only when quota/AI down (`ai.chat.degraded` MeterOnly).
- **Failure:** Refused → handoff chip; AI down → `aiEnabled:false`, storefront unaffected.
- **Tests/evidence:** ThreatSuite T1–T10, TenantAi tests 200 pass, tenantai RV specs, W0 audit docs.
- **Missing steps:** File/image knowledge ingest now partially covered (W3b OCR, W3c URL crawl); availability-aware answers (DP-04); multilingual.

## J3 — Customer moves from AI to a real staff member without losing context

**Status: 🟡 PARTIAL — handoff exists, context carry-over incomplete**

- **Entry:** AI `Refused`/low-confidence → "Hỏi chủ quán trực tiếp" chip; or direct chat/voice buttons.
- **Frontend:** `ChatPanel.razor`/`RealtimeChatPanel` (human chat), `VoiceCallPanel` (call), ShopERP `ShopInbox` (staff side).
- **API path:** Human chat = SignalR via Gateway `ChatService`/`RealtimeMessagingService` + `ShopPerCustomerThreads`; voice = `10_Voice` `/hubs/voice`.
- **Data stores:** Conversation threads (PG), voice_calls (voice.db), ShopInbox reads via API clients.
- **Auth/authz:** Customer token or guest device-id; staff JWT tenant-scoped.
- **Success:** Customer reaches staff via chat or voice/video call; guest calling works via device identity + inline passkey upgrade.
- **Failure/gaps:** AI→human **context handoff is a chip/deep-link, not a structured ticket** — staff may not see the AI transcript; no assignment/queue/SLA for AI escalations; W4 planned.
- **Tests/evidence:** voice-chat-e2e 7/7, voice-video-rv 25/25, chat UI unification tasks.
- **Missing steps:** Structured escalation object (conversation ref, transcript summary, customer intent), staff notification routing, follow-up tracking.

## J4 — Customer starts a voice call

**Status: ✅ VERIFIED (human-to-human); AI voice ❌ beyond browser TTS**

- **Entry:** Storefront call buttons (hero/nav/panel), chat panel call button, presence panel.
- **Frontend:** `VoiceCallPanel.razor`, `voice-sdk`, `media` JS; guest → `DeviceTokenValidator` (`X-Customer-Device-Id`); upgrade path inline passkey.
- **API path:** SignalR `/hubs/voice` → `VoiceHub` → `CallService` (14-state machine, idempotent StartCall, collision/busy/offline, ring 30s/negotiation 20s/max 120m) → WebRTC SDP/ICE → Coturn TURN (`/v1/turn/credentials`).
- **Data stores:** `voice.db` SQLite (voice_calls + events, partial unique FR-007).
- **External deps:** Coturn; metering `voice.*`/`video.*` service codes (caller=voice, fail-open G9).
- **Auth/authz:** Per-connection JWT/device token; `PermissionService` (self-call, caller-busy, kill-switch `VoiceFeatureEnabled`, verified context, `VoiceGuestCalling` default ON + per-IP rate limit for device callers).
- **Success:** CONNECTED P2P or TURN-relayed; callbar/scroll lock; reconnect ≤2/10s.
- **Failure:** Busy/offline/deny reasons surfaced; `GUEST_CALL_DISABLED` → quick-login overlay → passkey → auto re-call.
- **Tests/evidence:** Voice tests 112+/98/98; `voice-rv-sweep` 12/12; `voice-call-sim` 3/3; `voice-chat-e2e` 7/7; video RV 25/25.
- **Missing steps:** P2P%/TURN% metrics gate (P5 B3/B4 ≥100 real calls pending); `turns:` TLS 5349 deferred; `video.*` rate cards seed deferred; AI voice S2S absent.

## J5 — Customer starts a video call

**Status: ✅ VERIFIED** — same path as J4 with `CallType=video`, camera toggle (replaceTrack), self-view/PiP, `CallEvent` relay for camera state, metering `video.*`. Evidence: `voice-video-call-master-plan.md`, RV 25/25 (`9c47c47c`..`a0514941`). Deferred: TURN max-bps raise for video, turns TLS.

## J6 — Customer asks about product/service → books or orders → authoritative status

**Status: ✅ VERIFIED for order + booking paths**

- **Order path:** KhachLink `Store.razor`/`Cart`/`Checkout` → `POST` Gateway order creation (PG source of truth, ProductName+VatRate snapshot) → NATS `vanan.cloud.order.created.{shopInstId}` routed by ShopInstanceId → ShopERP SQLite → kitchen/POS display; status sync SQLite→PG via `vanan.shoperp.order.*`. Guest checkout via `X-Customer-Device-Id`; delivery geolocation flows.
- **Booking path:** `BookingOffering`/`BookingTime`/`BookingConfirm` → `PublicBookingController`/`TenantBookingController` → CoreHub `BookingService` (slots unique-filtered, idempotency, conflict guard, deposits, commission) → staff manage via ShopERP `/booking/*` (services/staff/schedules/calendar/queue/deposits/commission). Service catalog `NeedsReview`/`IsSellable` gates.
- **Status truth:** Order status + booking status returned from authoritative services; AI does not fabricate (answers grounded; status via order/booking APIs).
- **Payment step:** VietQR shown at checkout (`QrPaymentModal`, `VietQrController`); confirmation via bank webhook `api/webhooks/payment` or manual — **no gateway capture**.
- **Tests/evidence:** OrderFlow/OrderFlow.Tests, booking migrations+tests, RV entries (booking RV fix plan, order management upgrade), CD smoke.
- **Missing steps:** Real payment capture (CO-07), refund money movement, AI-initiated booking (W4).

## J7 — Shop owner/staff manages customer conversations and follows up

**Status: 🟡 MOSTLY — inbox exists, unified follow-up incomplete**

- **Frontend:** ShopERP `Community/ShopInbox.razor` (chat threads), `Orders/*`, `Booking/*` queues, `Notifications.razor`, voice presence/incoming call UX.
- **API path:** ShopERP API clients (`ShopInboxApiClient`, `OrderFlowApiClient`, `BookingTenantApiClient`, `VoiceAdminApiClient`) → Gateway → CoreHub services; NATS events for realtime.
- **Data stores:** PG threads/orders/notifications; tenant SQLite mirrors.
- **Auth/authz:** Staff JWT + permission groups; voice staff targeting tenant-scoped.
- **Success:** Owner sees conversations, accepts calls, manages orders/bookings, gets push/email notifications.
- **Gaps:** No unified customer-360 timeline (chat+orders+bookings+loyalty in one view); AI-escalation queue absent; notification preferences per channel partial.
- **Tests/evidence:** ShopInbox/RealtimeChatPanel tests, voice presence RV 19/19.

## J8 — Tenant onboarded → isolated identity/data/config/AI context

**Status: ✅ VERIFIED**

- **Entry:** Self-serve registration (`Register`/`Onboard`/`MerchantSignup`), assisted onboarding, crawler (`VanAn.Crawler` + `CrawlController`), OCR wizard (`OcrOnboarding*`), directory claim (`Claim.razor`, `TenantClaimController`).
- **API path:** `TenantRegistrationController`/`TenantOnboardingController`/`OnboardingController` → tenant provisioning (PG `Tenants`, `TenantSettings`, `KhachLinkInstances`) → feature flags (`ShopFeatureSettings` sync PG↔SQLite via NATS) → optional TenantAI knowledge ingest (docs/products) → assistant link generation.
- **Auth/authz:** SystemAdmin approval queues (`TenantPending`, claims), tenant-scoped admin JWT.
- **Tenant boundary:** `TenantId` filter all layers; per-tenant SQLite; per-tenant AI knowledge; per-tenant wallet; instance feature gate.
- **Success:** Tenant gets storefront domain/instance, admin access, isolated data, AI assistant link/QR.
- **Failure:** Pending/rejected states, duplicate detection, antiflood (AF1 pending approval).
- **Tests/evidence:** onboarding RV specs (`playwright-rv-onboarding`), tenantai-admin sweep, claims queue admin.
- **Missing steps:** Automated corpus quality gate; domain auto-SSL ops partially manual (certbot runbook).

## J9 — Customer interacts via external channel → correct tenant/customer/conversation

**Status: ❌–🟡 CHANNELS ESSENTIALLY ABSENT (except FB Lead Ads ingestion)**

- **What exists:** `FacebookLeadService.ProcessFacebookWebhookAsync` parses FB Lead Ads payloads → `Lead` entity → `LeadManagementService` (CRM-ish). Webhook endpoint wiring + signature verification **unverified**.
- **What does not exist:** Messenger/WhatsApp/Telegram/Zalo/TikTok customer messaging, unified conversation identity, channel mapping, unified inbox. `OmnichannelService` is an in-memory stub.
- **Consequence:** Vision's "omnichannel, not channel-dependent" is currently **first-party-channel-only** (VanAn web surfaces). This is the single largest journey gap.
- **Evidence:** grep for messenger/whatsapp/telegram/zalo → only `VaIIeTenantConfig` alert fields + enum values; `SocialHub.razor` = outbound links.

## J10 — Business event processed across ShopERP/CoreHub/accounting

**Status: ✅ VERIFIED**

- **Path examples:** Order confirmed → inventory deduction + kitchen status + loyalty earn (`vanan.shoperp.order.confirmed`) → NATS → PG order update → accounting event → `AccountingEntry` (immutable) → journal/ledger → e-invoice batch (if configured) → notifications (push/email/SMS) → metering usage where applicable.
- **Accounting engine:** PostingFoundation → Journal/Voucher → HKD books per regime (regime-lock RV 34/34) → period closing → financial statements; reversal-only corrections.
- **Idempotency:** Outbox event ids, booking idempotency keys, metering idempotency, duplicate-entry guards (reference-based fix 2026-09-18).
- **Tests/evidence:** Core.Tests (thousands), accounting engine SRS compliance tasks, RV regime-lock, NATS subject fixes verified in prod repair.
- **Gaps:** E-invoice live-tenant verification unknown; POS provider stubs if tenants expect KiotViet/Sapo sync; refund = accrual only.

---

## Journey scorecard

| Journey | Status | Blocking gap |
|---|---|---|
| J1 Discovery/presence | ✅ | — |
| J2 AI grounded answer | ✅ | Corpus coverage per tenant |
| J3 AI→human handoff | 🟡 | Structured escalation + context |
| J4 Voice call | ✅ | AI voice absent; P2P metrics pending |
| J5 Video call | ✅ | — |
| J6 Order/booking | ✅ | Payment capture |
| J7 Staff conversation mgmt | 🟡 | Unified timeline/inbox |
| J8 Tenant onboarding | ✅ | — |
| J9 External channel | ❌ | No channel adapters |
| J10 Business event→accounting | ✅ | E-invoice live verify |
