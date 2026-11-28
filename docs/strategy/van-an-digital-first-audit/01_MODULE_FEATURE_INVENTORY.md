# 01 — MODULE / FEATURE INVENTORY

Status taxonomy per brief §17. Evidence = paths/symbols/endpoints/migrations/tests. "RV" = runtime verification recorded in `docs/AI/project_state.md` or task cards.

---

## A. Platform services (deployable units)

| Module | Business capability | Actual implementation | Key features & status | Evidence | Data ownership | Tenant isolation | Test evidence | Gap / risk | Recommendation |
|---|---|---|---|---|---|---|---|---|---|
| `2_Gateway` (VanAn.Gateway) | Public API facade, YARP proxy, order creator, auth, rate limits | ASP.NET Core API + SignalR hubs + YARP; PG source of truth for Orders/Tenants/Users/ShopInstances | Order creation+routing ✅RV · public tenant-ai facade ✅RV · assistant-link public+owner APIs ✅RV · vietqr ✅ · webhooks (e-invoice/payment) code-only · feature-settings effective-flag merge ✅RV | `2_Gateway/Controllers/*` (~70 controllers), `Program.cs`, `docker-compose.gateway.yml` | PG (VanAnDbContext) | `RequireTenantAccess` policy + server-side resolution; `InstanceFeatureGate` host→NavFlags override | Gate/arch tests; RV sweeps per feature | Monolith-adjacent; many controllers; webhook signature verification depth unverified | Keep as facade; extract public-surface resolution into dedicated module when team grows |
| `3_CoreHub` (in-process in Gateway) | Business logic hub: orders, accounting, loyalty, booking, community, notifications, sync | ~200 services, EF Core PG + SQLite business DBs | Orders ✅RV · accounting engine ✅RV · booking ✅RV · loyalty ✅RV · community orders/fund ✅ · membership/HTX ✅RV · notifications (Brevo/ESMS/push) ✅ · sync workers ✅ · `OmnichannelService` **STUB** · `Providers/POS/*` **STUB** | `3_CoreHub/Services/**`, `VanAn.CoreHub.csproj` | PG + per-tenant SQLite | `IMustHaveTenant` filters; `IgnoreQueryFilters` used deliberately for cross-tenant public queries | Core.Tests (thousands), service tests | Largest blast-radius module; stub services mixed with real ones | Complete or remove stubs; consider bounded-context split later |
| `5_WebApps/ShopERP` | Tenant back-office: POS, kitchen, accounting UI, admin consoles | Blazor Server + SQLite (business) + PG (accounting); ~150 pages | POS/kitchen/orders ✅RV · accounting/HKD ✅RV · booking admin ✅ · loyalty admin ✅ · TenantAI admin ✅RV · ATRS admin ✅RV · e-invoice admin ✅code · metering admin ✅ | `5_WebApps/ShopERP/Components/Pages/**` | Tenant SQLite + PG accounting | Tenant-scoped auth, role/permission groups | ShopERP.Tests (333+), Playwright specs | Page count large; some features code-verified only | Keep; incremental hardening |
| `5_WebApps/KhachLink` | Customer-facing PWA storefront + community + wallet + booking + assistant page | Blazor WASM PWA, nginx-served, multi-instance per domain/profile | Store/checkout/orders ✅RV · booking ✅RV · loyalty/wallet ✅RV · community/disputes ✅ · `/a/{slug}` assistant ✅RV · voice/video call UI ✅RV 25/25 · profiles (6) ✅RV | `5_WebApps/KhachLink/Pages/**`, `Components/**`, `nginx.conf` | IndexedDB local + Gateway APIs | Anonymous customer tokens + device-id guest identity; tenant via instance host | E2E specs (174 total repo-wide), RV per feature | WASM cold-start; some pages feature-flagged off per profile | Keep; PWA polish ongoing |
| `5_WebApps/Directory` | Public store directory SSR (timlathay.com etc.) | Blazor SSR separate container, nginx map routing | Store/product search, nearby, SEO JSON-LD ✅RV | `5_WebApps/Directory/Components/Pages/*`, `CatalogService` | Read-only via Gateway | Public data only | E2E `directory-retention`, `w3-2d` specs | Young; audit/growth pages thin | Keep as discovery surface |
| `8_Metering/vanan-metering` | "Phí nền tảng" — wallets, rate cards, topups, usage metering, deny gate | Standalone service, own PG DB, port 5050 internal | Wallet/topup/rate-card ✅RV · `QuotaGate`/`MeteringClient` consumers ✅RV · usage ingest ✅ | `8_Metering/vanan-metering/src/**`, rule `.devin/rules/metering.md` | Own PG | Per-tenant wallet; caller-scoped API keys | Metering tests (63 files in service test projects) | Internal-only; attribution metadata expansion pending (W2a shipped partial) | Keep; extend attribution |
| `9_TenantAI/vanan-ai` | Tenant-scoped AI chat engine: intent, policy, RAG, validation, ingest | Standalone service, PG+pgvector, port 5060 | `/v1/chat`, `/v1/config/widget`, ingest docs+products, ThreatSuite T1–T10, retention cleanup ✅RV | `9_TenantAI/vanan-ai/src/**` | Own PG+pgvector | Caller key + scope + signed HMAC tenant ctx (±5min) | TenantAi tests (200 pass per state), e2e tenantai specs | No file/image ingest originally — W3b/c added OCR+URL extraction upstream | Keep as AI core |
| `10_Voice/vanan-voice` | Human-to-human voice+video calling (WebRTC P2P, TURN) | Standalone service, SignalR hub `/hubs/voice`, port 5070 | Call state machine 14 states, guest calling, video MVP, presence/round-robin ✅RV 25/25 + sweeps | `10_Voice/vanan-voice/src/**`, `docker-compose.voice.yml` | Own SQLite `voice.db` | Per-connection auth; verified tenant context; `VoiceGuestCalling` flag | Voice tests 112+/98/98, e2e voice specs | TURN `max-bps` raise deferred; `turns:` TLS deferred; AI voice absent | Keep; P6 rollout pending Gate 1 |
| `11_Atrs/vanan-atrs` | Account Trust & Recovery: passkeys/WebAuthn, recovery, disputes, RBAC, audit | Standalone service, port 5080, own PG | WebAuthn register/assert, recovery codes, dispute maker-checker, staff grants, security policies, notifications, Prometheus `/metrics` ✅RV multiple sweeps | `11_Atrs/vanan-atrs/src/**`, `docker-compose.atrs.yml` | Own PG | TrustSubject-scoped; staff permission grants; SystemAdmin break-glass | ATRS tests 179/179, RV sweeps 41/41,48/48,32/32,26/26,17/17 | Email/SMS notification channels pending; KhachLink inbox UI residual | Keep; most mature new service |
| `1_Shared` | Domain + contracts single source of truth | `Domain.cs` (7,331 lines, ~173 types) + `Domain/` aggregates + DTOs/Services contracts | All entities, value objects, enums | `1_Shared/Domain.cs`, `1_Shared/Domain/**` | Contract only | `IMustHaveTenant`, single-identity rule | Architecture tests enforce purity | File size/monolith; dual identity historical bugs fixed | Keep; consider splitting Domain.cs gradually |
| `UI.Platform` | Shared Blazor component library | Razor class lib: atomic/composite components, tokens, realtime JS | `VanAnPublicSearchBar`, `AiChatWidget`, `VanAnAlert`, nav, theme | `UI.Platform/Components/**` | n/a | n/a | bUnit tests in app test projects | Component API churn caused prod bugs (missing @using) | Keep; add platform-level render tests |
| `7_Tooling/VanAn.Crawler` | Tenant website/data crawler for onboarding | Worker service calling Gateway + doanhnghiep.vn API | Crawl sources, adapters, workers | `7_Tooling/VanAn.Crawler/**`, `CrawlController` | n/a | Gateway-auth | — | Free-tier external API limits; accuracy unverified (L5 pending) | Keep for onboarding; evaluate quality |
| `4_MobileApps` HRApp/StationApp | MAUI mobile shells | Templates: AppShell + MainPage ~36 lines | **STUB/template** — no real features found | `4_MobileApps/*/MainPage.xaml` | n/a | n/a | none | Not a product; don't count as mobile capability | Clarify scope or archive |
| `apps/ecoNexus.Apps.sln` | Second HRApp copy? | Duplicate MAUI project under `apps/` | **DUPLICATED/UNUSED?** — needs proof before removal | `apps/HRApp`, `apps/ecoNexus.Apps.sln` | n/a | n/a | none | Duplicate of `4_MobileApps/HRApp` | Investigate; likely deprecate |
| `VanAn.Accounting` | Accounting analyzers/source generators | Analyzers + conventions | Compile-time accounting rules | `VanAn.Accounting/**` | n/a | n/a | build-time | — | Keep |

## B. Feature-level inventory (grouped by target capability)

### §5 Digital People & Digital Businesses

| Feature | Status | Evidence | Notes |
|---|---|---|---|
| Customer identity (guest device → OTP email/phone → passkey) | IMPLEMENTED & VERIFIED | `CustomerIdentityController`, `DeviceRegistration*`, ATRS `TrustSubject`, email-OTP RV 2026-11-26, passkey login RV 13/13 | Tiered L0–L3 model approved; full rollout in progress |
| Business/tenant identity + onboarding | IMPLEMENTED & VERIFIED | `TenantRegistrationController`, `MerchantSignup*`, `OnboardingController`, OCR onboarding wizard, crawler | Self-serve + assisted paths |
| Staff/owner/operator roles | IMPLEMENTED & VERIFIED | `DemoUser`, `UserRole`, `PermissionGroup`, `RoleAssignmentService`, ShopERP user mgmt | Plus ATRS `StaffPermissionGrant` for sensitive ops |
| Membership/HTX roles | IMPLEMENTED — CODE REVIEW + partial RV | `Membership/` services, `Member`, `HtxProfile`, `/api/cooperatives` RV data | Young; HTX portal RV'd |
| CTV/salesman/collaborator | IMPLEMENTED — CODE REVIEW + partial RV | `SalesmanService`, `CollaboratorVerification*`, `SalesmanQR/Store` pages, commission calculator RV'd 2026-09-17 | Referral commission base + self-referral block verified |
| Consent/privacy prefs | PARTIALLY IMPLEMENTED | `ConsentRecord` (ATRS + Membership), `CustomerContextDismissal` | No unified consent center; retention policy per-service |
| Tenant lifecycle (suspend/migrate/close) | PARTIALLY IMPLEMENTED | `TenantStatus`, `TenantConversionService`, claims queue | Migration/closure workflows thin |
| Business profile/storefront config | IMPLEMENTED & VERIFIED | `ShopConfig`, `BusinessProfile`, `TenantStore*` controllers, storefront images/vertical migrations | Per-tenant branding, social links, hours |

### §6 Digital Presence

| Feature | Status | Evidence |
|---|---|---|
| Public storefront per tenant | IMPLEMENTED & VERIFIED | KhachLink `Store.razor`, instance routing, profiles |
| Store directory/search (TimLaThay) | IMPLEMENTED & VERIFIED | Directory SSR, `ITenantSearchService` single-source, normalized+fuzzy+did-you-mean RV'd |
| QR entry points | IMPLEMENTED & VERIFIED | `QrCodeService`, `Scan.razor`, `Claim.razor`, table-number QR, assistant QR |
| SEO/metadata/JSON-LD | IMPLEMENTED & VERIFIED | head-meta endpoint + JSON-LD RV'd W3-2 |
| Tenant-controlled content publish workflow | PARTIALLY IMPLEMENTED | Product `PendingReview`/`NeedsReview` flows, knowledge approval; no general CMS |
| Cross-channel info sync | PARTIALLY IMPLEMENTED | `ShopConfig` single source; social links outbound only; no push-to-platform sync |
| Custom domain per tenant | IMPLEMENTED — CODE REVIEW | `TenantDomain`, `DomainRegistrar`, `DynamicCorsService`, `KhachLinkInstances` |

### §7 Van An AI Interaction Layer

| Feature | Status | Evidence |
|---|---|---|
| Customer-facing AI chat (anonymous) | IMPLEMENTED & VERIFIED | `PublicTenantAiController` + `TenantAiPublicHttpService` + `AiChatWidget`; session cap, rate limit, idempotency, degrade |
| Tenant-grounded answers (RAG) | IMPLEMENTED & VERIFIED | pgvector retriever, products batch sync, docs ingest, usage permissions, AnswerValidator |
| Assistant entity + AI Link/QR (`/a/{slug}`) | IMPLEMENTED & VERIFIED | `AssistantLink` entity+migration `20261122072851`, `AssistantLinkController`, `PublicAssistantLinkController`, `Assistant.razor`, E2E |
| Knowledge onboarding | IMPLEMENTED — escalating | W3a text extract ✅RV · W3b menu OCR→knowledge ✅deployed · W3c URL crawl SSRF-guarded ✅deployed · approval workflow ✅ |
| AI widget embed (iframe) + channel attribution | IMPLEMENTED — deployed, depth partial | W2a commit `8cefb48a`; external SDK depth unverified |
| Human handoff (AI→staff) | PARTIALLY IMPLEMENTED | Handoff chip + ShopInbox; W4 business actions pending; context carry-over partial |
| Voice/video human calls | IMPLEMENTED & VERIFIED | `10_Voice` + `VoiceCallPanel`/`RealtimeChatPanel`; guest calling; video MVP RV 25/25 |
| AI voice (speech-to-speech) | PARTIALLY IMPLEMENTED | W3d = mic input + TTS read-aloud (browser APIs), NOT realtime S2S; Gemini Live candidate documented |
| Workflow builder (no-code) | MISSING | No workflow engine found; SmartEntry parse ≠ workflow |
| AI action execution (book/order) | MISSING | Read-only Q&A today; W4 planned |
| Multilingual AI | UNKNOWN | Vietnamese primary; no evidence of multi-lang tuning |
| Abuse prevention | IMPLEMENTED & VERIFIED | Rate limits, session caps, QuotaGate Deny, ThreatSuite, attribution metadata (W2a) |

### §8 Omnichannel & Channel Integration

| Feature | Status | Evidence |
|---|---|---|
| Website/PWA/QR entry | IMPLEMENTED & VERIFIED | KhachLink PWA, Directory, QR flows |
| Facebook Lead Ads webhook | IMPLEMENTED — CODE REVIEW | `FacebookLeadService.ProcessFacebookWebhookAsync`, `Lead` entity; endpoint wiring/signature verify unverified |
| Social outbound links | IMPLEMENTED (trivial) | `SocialHub.razor` FB/TikTok links |
| Messenger/WhatsApp/Telegram/Zalo customer chat | MISSING | Only config fields (`VaIIeTenantConfig.Telegram*/Zalo*`) for internal alerts; no channel adapters |
| Unified inbox | PARTIALLY IMPLEMENTED | `ShopInbox` covers in-app chat + voice; not cross-channel |
| Push notifications (Web Push) | IMPLEMENTED & VERIFIED | `PushNotificationService` (VAPID), subscriptions, delivery records, NATS-driven |
| Email (Brevo) | IMPLEMENTED & VERIFIED | `BrevoEmailService`, OTP email login RV'd |
| SMS (ESMS) | IMPLEMENTED — CODE REVIEW | `EsmsNotificationService` real REST client; delivery at scale unverified |
| ZNS (Zalo Notification Service) | MISSING | No implementation |
| Webhooks inbound (e-invoice/payment) | IMPLEMENTED — CODE REVIEW | `WebhookController` AllowAnonymous provider routes; signature verification depth unverified |
| Email/SMS/push to customer lifecycle | PARTIALLY IMPLEMENTED | `CompositeNotificationService` (push channel = logged no-op in Wave 1 comment — verify current) |

### §9 Discovery — TimLaThay

| Feature | Status | Evidence |
|---|---|---|
| Store/product search nearby | IMPLEMENTED & VERIFIED | `ITenantSearchService` normalized/fuzzy/did-you-mean; Directory + StoreFinder |
| Map/location discovery | IMPLEMENTED & VERIFIED | `LeafetMap`/`GoogleMaps` components, geolocation nearby |
| Ranking transparency / quality signals | PARTIALLY IMPLEMENTED | Verification badges exist (tenant verified events); ranking = recency/text match |
| Sponsored placement | MISSING | `FeaturedProducts`/`PromoCampaign` exist but no paid-placement disclosure model |
| Data freshness | PARTIALLY IMPLEMENTED | Tenant profile sync events; no freshness SLA monitoring |

### §10 Commerce & Service Fulfillment

| Feature | Status | Evidence |
|---|---|---|
| Product catalog + cart/checkout | IMPLEMENTED & VERIFIED | Products, Cart, Checkout, orders PG→SQLite routed delivery |
| Order lifecycle + kitchen/POS | IMPLEMENTED & VERIFIED | OrderFlow, Kitchen Display, status history, NATS sync both directions |
| Booking lifecycle | IMPLEMENTED & VERIFIED | `BookingService`, offerings/staff/schedules/deposits/commission, public booking pages, `NeedsReview`/`IsSellable` flows |
| Payment acceptance | PARTIALLY IMPLEMENTED | VietQR generation ✅; bank webhook `api/webhooks/payment` code-only; **no VNPay/MoMo/ZaloPay** (`RefundOrchestrationService` comment) |
| Refund/cancel/dispute | PARTIALLY IMPLEMENTED | `RefundOrchestrationService` (accrual, no money movement), order cancel, ATRS dispute cases (loyalty points), `DisputeSubmit` pages |
| ShopERP integration | IMPLEMENTED & VERIFIED | NATS routed delivery + sync subscribers; per-tenant SQLite replica |
| Inventory/availability | IMPLEMENTED — CODE REVIEW + partial | Stock movements, recipes, forecast, alerts; POS-side availability to public AI unverified |
| Idempotency/audit | IMPLEMENTED & VERIFIED | Outbox events, idempotency keys (booking, metering, orders), immutable `AccountingEntry` |
| E-invoice | IMPLEMENTED — CODE REVIEW | Viettel + Misa real providers, webhook receiver, batch processor, admin pages; live-tenant usage unknown |

### §11 Relationship & Customer Intelligence

| Feature | Status | Evidence |
|---|---|---|
| Customer profiles + consent | IMPLEMENTED & VERIFIED | Customer entity, device identity, merge service, context endpoint `/api/customer-context/me` |
| CRM timeline/segmentation | PARTIALLY IMPLEMENTED | `CustomerSegmentationService`, `CustomerService`, recommendations; no unified timeline UI verified |
| Loyalty points/tiers/vouchers | IMPLEMENTED & VERIFIED | Ledger, tiers, budgets, redemption catalog, missions, alliance wallet |
| Loyalty Alliance (cross-tenant) | IMPLEMENTED — CODE REVIEW + partial RV | `AllianceWalletService`, `InternalLoyaltyController`, idempotency unique migration; SRS + spec docs |
| Campaigns/promo | IMPLEMENTED & VERIFIED | `PromoCampaignService`, `SocialCampaignService`, admin + customer pages |
| Support cases | PARTIALLY IMPLEMENTED | Chat + disputes + recovery cover parts; no generic ticketing system |
| Data export/deletion | PARTIALLY IMPLEMENTED | Account recovery/ATRS; GDPR-style export/delete workflow not found |

### §12 Ecosystem & Marketplace

| Feature | Status | Evidence |
|---|---|---|
| HTX/cooperative portal | IMPLEMENTED & VERIFIED | `CooperativesController`, `HoptacxaPortal.razor`, membership reuse, `/api/cooperatives` RV data |
| Community delivery marketplace | IMPLEMENTED — CODE REVIEW | `CommunityOrderService` (Haversine nearby orders, shipper accept), `CommunityFundService`, OwnerPanel/ShopInbox/FraudFlags pages |
| Group-buy / mua chung | MISSING (deferred) | Explicitly deferred M2 in project_state |
| Job marketplace `/jobs` | PARTIALLY IMPLEMENTED | JobMarket profile exists; `/jobs` page deferred M1 (`ShowJobs=false`) |
| CTV/referral network | IMPLEMENTED — CODE REVIEW | Salesman QR/store, verification, commission engine |
| Marketplace listing lifecycle | PARTIALLY IMPLEMENTED | Catalog/discovery exist; no listing→accept→fulfill→settle→dispute chain for third-party sellers |
| Charity/community fund | IMPLEMENTED — CODE REVIEW | `CommunityFundService` + admin page |
| Cross-tenant referrals | PARTIALLY IMPLEMENTED | Referral config per product, attribution sessions |

### §13 Digital Operations & Business Management

| Feature | Status | Evidence |
|---|---|---|
| POS + receipts/payments records | IMPLEMENTED & VERIFIED | POS pages, order payments, cash/bank books |
| Accounting (HKD, TT152/TT71/TT58 regimes) | IMPLEMENTED & VERIFIED | Journal, ledger, trial balance, financial statements, period closing, regime-lock `615d85ac` RV 34/34 |
| Accounting immutability | IMPLEMENTED & VERIFIED | Append-only + reversal pattern, arch tests |
| E-invoice ops | IMPLEMENTED — CODE REVIEW | see §10 |
| Staff/role management | IMPLEMENTED & VERIFIED | Permission groups, user mgmt, ATRS grants |
| Procurement/purchase | IMPLEMENTED — CODE REVIEW | Purchase receipts, stock movements, supplier ordering partial |
| Reporting/reconciliation | IMPLEMENTED & VERIFIED | Financial reports, reconcile admin, loyalty reconcile, settlement admin |
| Offline operation | PARTIALLY IMPLEMENTED | Edge compose + SQLite local; `OmnichannelService` offline queue is in-memory stub |
| Smart Entry (NL voucher input) | IMPLEMENTED — CODE REVIEW | `SmartEntryDialogService`, `VoucherTextParseService`, `LlmEnhanceVoucherTextParseService`, Voucher Framework registry |

### §14 Digital Infrastructure & Shared Platform

| Feature | Status | Evidence |
|---|---|---|
| Gateway + service comms | IMPLEMENTED & VERIFIED | YARP, SignalR, internal API-key endpoints |
| Identity/auth/session | IMPLEMENTED & VERIFIED | JWT HS256, customer tokens (DataProtection), device tokens, passkeys, token versioning |
| Tenant provisioning | IMPLEMENTED & VERIFIED | Registration→onboarding→activation flows, claims, pending queues |
| PostgreSQL + SQLite stores | IMPLEMENTED & VERIFIED | PG (CoreHub/Metering/TenantAI/Voice/ATRS) + per-tenant SQLite (ShopERP) |
| Sync Worker + conflict | IMPLEMENTED & VERIFIED | NATS outbox both directions; conflict handling basic |
| Event-driven processing | IMPLEMENTED & VERIFIED | NATS subjects `vanan.cloud.*`/`vanan.shoperp.*`, subscribers |
| Notification infra | IMPLEMENTED & VERIFIED | Composite (Brevo/ESMS/push) + NotificationCenter + TenantNotificationHub |
| File/object storage | IMPLEMENTED & VERIFIED | Cloudinary images, R2 storage (`R2StorageController`, cleanup job) |
| Search/retrieval | IMPLEMENTED & VERIFIED | `ITenantSearchService` + pgvector in TenantAI |
| AI provider abstraction | PARTIALLY IMPLEMENTED | Gemini chat+embedding concrete; `IAiVoiceSessionAdapter` planned not built; no multi-provider LLM router |
| Metering/quotas/billing | IMPLEMENTED & VERIFIED | Full metering service + wallet + rate cards + topups + deny gate |
| Observability | PARTIALLY IMPLEMENTED | Serilog, `/health`(+detail), ATRS `/metrics` Prometheus; no tracing/APM, no centralized dashboards verified |
| Backup/DR | PARTIALLY IMPLEMENTED | `scripts/backup-db.sh` SQLite 7-day; manual pg_dump verified once (ATRS W6a); no scheduled PG backup found |
| Security controls | IMPLEMENTED & VERIFIED (broad) | Rate limits, signed ctx, HMAC, internal API keys, DataProtection, secrets via env/GH secrets |
| CI/CD + IaC | IMPLEMENTED & VERIFIED | `cd-multivps.yml` 6 images→4-5 VPS + smoke; `ci-full.ps1`; compose files per service |
| Hybrid hosting (GCP + self-managed option) | IMPLEMENTED — single-site today | All prod on GCP `vanan-prod`; Proxmox path documented not deployed |
| Capacity/load testing | PARTIALLY IMPLEMENTED | `VanAn.Load.Tests`, `6_Testing/load-tests` exist; results not reviewed; P6 voice gates pending |
| Secrets management | PARTIALLY IMPLEMENTED | Env vars + GH secrets + VPS `.env`; no vault/KMS; `DevToken__Secret` governed window pattern |
