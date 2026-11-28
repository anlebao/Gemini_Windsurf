# 00 — EXECUTIVE ASSESSMENT

**Audit:** Vạn An Digital-First, Human-Centered — Codebase Capability Gap Assessment
**Date:** 2026-11-27 · **Brief:** `docs/requirements/VAN_AN_DIGITAL_FIRST_VISION_AND_CODEBASE_AUDIT_BRIEF_v1.0.md` v1.0
**Method:** Static code inspection + repository documentation + prior verified audit/runtime evidence (`docs/AI/project_state.md`, `docs/AI/tasks/universal_ai_assistant/w0_audit/*`, task cards, RV reports). No production system was modified; no destructive commands run. Where runtime verification evidence exists (deployed commit + RV PASS recorded in project_state), it is cited; where only code exists, status is "code review only".

---

## 1. Overall alignment verdict

Vạn An is **substantially aligned with the Digital-first, Human-connected vision — and is considerably further along than a typical MVP**. The codebase already contains a multi-tenant commerce + accounting + communication platform with several genuinely differentiated assets: a tenant-isolated AI chat engine with metering/quota enforcement, a production human voice+video call service, an account trust & recovery service (passkeys, disputes, RBAC), a multi-VPS routing architecture, and a loyalty/membership/community layer.

The dominant pattern of the codebase is **"deep vertical execution in commerce/accounting, thin horizontal execution in channels/distribution"**: the system can take a customer's order, book an appointment, place a voice/video call, earn loyalty points, and post accounting entries — but it cannot yet *meet the customer on the channels where the customer already is* (no Messenger/WhatsApp/Zalo/Telegram customer-facing integration), has **no real payment gateway** (VietQR + manual/webhook confirmation only), and its AI is text-chat + browser TTS, not conversational voice.

**Estimated position (qualitative, not a score):**
- Digital presence & storefront: **strong** (verified end-to-end on production).
- Customer interaction (chat/voice/video/handoff): **strong and verified** — best-in-class relative to regional SMB SaaS.
- AI interaction layer: **real and metered**, but surface area still narrow (storefront + assistant link + early widget embed; no external channel distribution).
- Commerce fulfillment: **strong** for order/booking/accounting; **weak** on payment acceptance and refunds.
- Ecosystem/marketplace/discovery: **partially built** (TimLaThay directory, HTX portal, community delivery, CTV/salesman, loyalty alliance) — several modules are real but young.
- Shared infrastructure: **strong core** (Gateway/CoreHub/Metering/TenantAI/Voice/ATRS), with observability/backup/DR being the weakest infrastructure area.

## 2. What already exists that directly supports the vision

| Vision pillar | Verified asset | Evidence |
|---|---|---|
| Digital presence for every business | Multi-profile KhachLink storefronts (FullCommerce, Directory, Logistics, JobMarket, Reseller, Hoptacxa) + Directory SSR (timlathay.com) + QR claim + SEO/JSON-LD | `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkProfile.cs`, `5_WebApps/Directory/`, `5_WebApps/KhachLink/Pages/Store.razor`, RV logs in `docs/AI/project_state.md` §3 |
| Customer↔shop communication | Human chat (SignalR), ShopInbox staff UI, AI chat widget (anonymous, grounded, metered), AI Link/QR hosted page `/a/{slug}`, handoff chip, voice + video calls with guest calling | `PublicTenantAiController`, `AiChatWidget.razor`, `Assistant.razor`, `10_Voice/` (RV 25/25 video MVP), project_state voice/AILink entries |
| AI that acts within tenant boundaries | `vanan-ai` service: IntentRouter/PolicyEngine/KnowledgeRetriever/AnswerValidator/ThreatSuite T1–T10, pgvector RAG, usage-permission filtering, signed tenant context (HMAC), per-tenant rate card + Deny gate | `9_TenantAI/vanan-ai/`, `docs/AI/tasks/universal_ai_assistant/w0_audit/*` |
| Commerce & fulfillment | POS, orders lifecycle, kitchen display, booking (services/staff/schedules/deposits/commission), vouchers framework, e-invoice (Viettel + Misa), accounting engine (HKD books, immutable ledger, period closing) | `5_WebApps/ShopERP/Components/Pages/**`, `3_CoreHub/Services/Booking/`, `3_CoreHub/Services/Providers/EInvoice/` |
| Relationship & loyalty | Points ledger, tiers, alliance wallet, redemption catalog, missions, CTV/salesman verification, membership/HTX infrastructure, community fund, disputes | `Loyalty*`, `AllianceWalletService`, `Membership/`, `Community*` services + pages |
| Identity & trust | Tiered identity (device→OTP→passkey), ATRS service (WebAuthn, recovery, disputes, RBAC, audit explorer, security policies), MFA TOTP for admins | `11_Atrs/vanan-atrs/` (deployed, RV sweeps 41/41, 48/48, 32/32, 26/26, 17/17) |
| Digital infrastructure | Multi-VPS routing (ShopInstances + NATS routed delivery), metering service (wallets/rate-cards/topups), outbox sync, per-tenant SQLite + PG accounting | `docker-compose.*.yml`, `NatsSyncWorker`, `8_Metering/` |

## 3. Most consequential capability gaps

1. **No real payment acceptance.** VietQR URL generation + bank webhook/manual confirmation only. VNPay/MoMo/ZaloPay explicitly absent (`RefundOrchestrationService.cs:20,71`). Refunds are accrual entries, not money movement. *Impact: cannot close the "transact" loop digitally.*
2. **No customer-facing external channel integration.** Facebook "integration" = Lead Ads webhook processing (`FacebookLeadService`) + outbound social links (`SocialHub.razor`); no Messenger/WhatsApp/Telegram/Zalo customer chat, no unified inbox across channels. `OmnichannelService` is an in-memory stub with `TODO: sync to cloud` (`3_CoreHub/Services/OmnichannelService.cs:34`).
3. **AI voice is shallow.** W3d shipped mic + browser TTS read-aloud on `/a/{slug}`; there is no real-time speech-to-speech pipeline. Gemini Live is the documented candidate but not implemented (`VOICE_PROVIDER_AUDIT.md`).
4. **AI actions are read-only.** The assistant answers questions and offers handoff/product cards; it cannot book, order, or mutate state. W4 (Business Actions) is the next planned wave.
5. **POS provider integrations are stubs.** `KiotVietPOSProvider`/`SapoPOSProvider` return `Task.FromResult` success without HTTP calls.
6. **Observability/backup/DR thin.** Serilog + per-service `/health` exist; ATRS has Prometheus `/metrics`. No centralized tracing, no scheduled PG backup automation found (only `scripts/backup-db.sh` for SQLite, 7-day retention, and one-time manual pg_dump verification).

## 4. Largest architectural and operational risks

| Risk | Severity | Evidence |
|---|---|---|
| Monolith concentration: CoreHub hosts ~200 services in-process inside Gateway process | High (scaling) | `3_CoreHub/Services/` (~200 files), `VanAn.sln` |
| Dual-write sync PG↔SQLite via NATS outbox — conflict surface | Medium-High | `NatsSyncWorker`, `OrderSyncSubscriber`, `SimpleOutboxProcessor` |
| Single `Domain.cs` (7,331 lines, ~173 types) as single source of truth | Medium (maintainability) | `1_Shared/Domain.cs` |
| Secrets/config spread across `.env.*`, GH secrets, VPS `.env` files; no secrets manager | Medium | `.env.gateway.example`, `cd-deployment.md` rule |
| POS/e-invoice/payment provider credentials per-tenant unverified at scale | Medium | `Providers/EInvoice/*`, `Providers/POS/*` |
| Backup/restore for PG not automated/scheduled; RPO undefined | High | `scripts/backup-db.sh` (SQLite only) |
| Rate-limit/abuse controls exist per-endpoint but no global anomaly detection | Medium | `tenant-ai-public`, `public-metering` policies |

## 5. Critical unknowns preventing a fully reliable conclusion

- **Production data state**: tenant count, active tenants, usage volumes, metering wallet balances — not audited (no DB access in this pass).
- **Rate-card seeding on prod metering DB**: `ai.chat.message` Deny card existence flagged "verify" in W0 audit; no newer evidence found.
- **Facebook Lead Ads webhook endpoint reachability/signature verification**: service exists; endpoint wiring and `X-Hub-Signature` verification not fully traced.
- **E-invoice provider credentials**: Viettel/Misa providers are real HTTP clients; whether any tenant has live credentials configured is unknown.
- **Load/capacity evidence**: `VanAn.Load.Tests` + `6_Testing/load-tests` exist; results not reviewed.
- **`4_MobileApps` (HRApp/StationApp)**: MAUI shells with ~36-line MainPage — scope/production status unknown; likely templates.

## 6. Verified vs inferred — explicit statement

- **Verified end-to-end (deployed + RV PASS recorded):** storefront chat+AI, assistant link `/a/{slug}`, voice+video calls (human), guest calling, booking flow, loyalty wallet/pages, ATRS trust/recovery/disputes, tenant onboarding, multi-VPS routing, metering top-up, directory search, HTX portal, instance feature gating, email-OTP login.
- **Verified code-only (exists, runtime not re-verified this audit):** e-invoice providers, booking internals, accounting engine depth, crawler/OCR onboarding, financial/inventory intelligence, smart-entry voucher parsing.
- **Inferred/partial:** payment webhook reliability, Facebook lead ingestion in production, backup automation coverage.
- **Confirmed absent (searched, not found):** VNPay/MoMo/ZaloPay, Messenger/WhatsApp/Telegram/Zalo customer channels, ZNS, real POS provider HTTP, AI speech-to-speech voice, workflow builder, external JS widget SDK (iframe embed shipped in W2a — status "deployed" per git log, depth unverified), browser extension, Android assistant app.

See `08_EVIDENCE_INDEX.md` for the full path/commit/test mapping.
