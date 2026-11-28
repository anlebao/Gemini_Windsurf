# 04 — CURRENT ARCHITECTURE & DATA OWNERSHIP MAP

## 1. Actual service topology (production, GCP `vanan-prod`)

```text
                              ┌─────────────────────────────────────────────┐
   Internet                   │              GCP vanan-prod (5 VPS)          │
 ┌──────────┐   ┌──────────┐  │                                             │
 │ KhachLink│   │ Directory│  │  ┌──────────────┐      ┌──────────────────┐ │
 │ PWA/WASM │   │ SSR      │  │  │ vanan-gateway│      │ vanan-shop-a     │ │
 │ :static  │   │ :SSR     │  │  │ YARP+SignalR │ NATS │ ShopERP Blazor   │ │
 │ nginx    │   │ nginx    │  │  │ +CoreHub(in- │◄────►│ Server + tenant  │ │
 └────┬─────┘   └────┬─────┘  │  │  process)    │      │ SQLite DBs       │ │
      │              │        │  │ PG: Orders,  │      └──────────────────┘ │
      │ HTTPS        │ HTTPS  │  │ Tenants,     │                           │
      ▼              ▼        │  │ Users,       │   ┌──────────────────┐   │
 ┌─────────────────────────┐  │  │ ShopInstances│   │ vanan-metering   │   │
 │        GATEWAY :5001    │◄─┘  │ FeaturedProd │   │ :5050 internal   │   │
 │  /api/* → CoreHub/YARP  │     │ Accounting   │   │ PG: wallets,     │   │
 │  /hubs/* → SignalR      │     │ entries(PG)  │   │ rate-cards,usage │   │
 └───────────┬─────────────┘     └──────┬───────┘   └────────┬─────────┘   │
             │                          │                    │             │
             │ NATS vanan.cloud.*.{}    │ PG VanAnDbContext   │             │
             │ (routed by ShopInstanceId)                    │             │
             ▼                          ▼                    ▼             │
      ┌─────────────┐            ┌──────────────┐    ┌──────────────┐      │
      │ vanan-khach-│            │ vanan-ai     │    │ vanan-voice  │      │
      │ link VPS    │            │ :5060 PG+    │    │ :5070 SQLite │      │
      │ (static/PWA)│            │ pgvector     │    │ voice.db     │      │
      └─────────────┘            └──────────────┘    └──────┬───────┘      │
      ┌─────────────┐                                     │ WebRTC+TURN   │
      │ vanan-atrs  │                                     ▼               │
      │ :5080 PG    │                              ┌──────────────┐        │
      │ trust/      │                              │ Coturn (TURN)│        │
      │ recovery/   │                              └──────────────┘        │
      │ disputes    │                                                     │
      └─────────────┘                                                     │
      External: Google Gemini LLM · Brevo email · ESMS SMS · VietQR img ·  │
                Cloudinary/R2 storage · doanhnghiep.vn (crawler) ·        │
                e-invoice Viettel/Misa APIs · Facebook Lead webhook       │
```

Instances (verified `docs/AI/project_state.md` §1): `vanan-gateway` (asia-southeast1-a), `vanan-shop-a`, `vanan-metering`, `vanan-khachlink`, `vanan-khachlink-20260815-timlathay-com`, plus `vanan-ai`, `vanan-voice`, `vanan-atrs` VPS.

## 2. Data ownership (authoritative sources)

| Data | Authoritative store | Replicas/sync | Notes |
|---|---|---|---|
| Orders (new) | Gateway PG `Orders` | → tenant SQLite via NATS `vanan.cloud.order.created.{shopInstId}` | Option C; ShopERP = replica for kitchen/POS |
| Order status (kitchen/POS updates) | ShopERP SQLite | → PG via `vanan.shoperp.order.*` | Bidirectional — conflict surface |
| Accounting entries | PG (CoreHub) + ShopERP PG-accounting per ADR | Immutable append-only; reversal only | Regime-locked per Tenant.Type |
| Tenants/Users/ShopInstances/FeaturedProducts | Gateway PG | → SQLite via `vanan.cloud.user.upserted`, `tenant.*` | Source of truth PG |
| Products/catalog | ShopERP per-tenant SQLite | → TenantAI `ProductsBatchRequest` for AI grounding; → PG FeaturedProducts subset | Client supplies ProductName+VatRate snapshot at checkout |
| Knowledge/RAG | vanan-ai PG+pgvector | Ingested from docs/products/OCR/URL extract | Tenant-scoped, approval-gated |
| Conversations (AI) | vanan-ai PG | Retention cleanup job | SessionId client-gen, tenant-scoped |
| Human chat threads | Gateway/CoreHub PG | ShopInbox reads | `ShopPerCustomerThreads` |
| Voice calls | vanan-voice SQLite `voice.db` | Events table audit | Per-call state machine |
| Trust/recovery/disputes/passkeys | vanan-atrs PG | Notifications in-app | Staff grants RBAC |
| Wallet/rate-cards/usage/metering | vanan-metering PG | QuotaGate reads via API (30s cache) | Append-only ledger semantics |
| Loyalty points/alliance | CoreHub PG (+ SQLite mirror events) | `vanan.shoperp.loyalty.earned`, `vanan.cloud.loyalty.config.changed` | Option B HTTP proxy for alliance |
| Booking | CoreHub PG (+ ShopERP views) | Idempotency keys, slot unique constraints | Commission + deposits |
| Customer identity | Split: Customer (KhachLink/Gateway) vs DemoUser (ShopERP staff) — **intentional 2-system** (Q1 chốt) | UserSyncSubscriber syncs TrustLevel only | Collaborator=Customer+CommunityRole, no backend login |
| Notifications/push subs | CoreHub PG | Web Push VAPID, delivery records | Composite email/SMS/push |
| Files/images | Cloudinary + Cloudflare R2 | `R2StorageController`, cleanup job | — |
| Assistant links | Gateway PG (`AssistantLink`) | Public resolution via slug | W1 entity |

## 3. Service-to-service call map (key flows)

| Flow | Path |
|---|---|
| Public AI chat | KhachLink/Directory/embed → Gateway `PublicTenantAiController`/`PublicAssistantLinkController` → sign ctx → vanan-ai `/v1/chat` → Gemini; usage → vanan-metering ingest |
| Metering gate | Gateway/CoreHub/ShopERP `MeteringApiClient`/`MeteringClient` → vanan-metering `/v1/wallets/{t}/check` (30s cache, fail-open policy per caller) |
| Human voice/video | Client → vanan-voice `/hubs/voice` (SignalR) + `/v1/turn/credentials`; verify context → Gateway `CustomerContext`/tenant endpoints; metering `voice.*`/`video.*` |
| ATRS | KhachLink `SecurityHttpService` → Gateway YARP `/api/v1/account-trust/*` → vanan-atrs; staff → ShopERP `AtrsAdminApiClient` → Gateway → atrs; loyalty adjust → Gateway internal `/points/adjust-customer` → CoreHub |
| Booking | KhachLink → Gateway `PublicBookingController`/`TenantBookingController` → CoreHub `BookingService` (PG); ShopERP staff → `BookingTenantApiClient` → Gateway |
| E-invoice | ShopERP → CoreHub providers (Viettel/Misa HTTP) ; provider callbacks → Gateway `WebhookController /api/webhooks/{provider}` |
| Notifications | CoreHub `CompositeNotificationService` → Brevo (email), ESMS (SMS), Web Push; `NotificationCenterService` + `TenantNotificationHub` in-app; NATS `vanan.cloud.notification.created` |
| Sync | ShopERP/CoreHub `NatsSyncWorker`/`SimpleOutboxProcessor` → NATS subjects → subscribers (`OrderSyncSubscriber`, `TenantSyncSubscriber`, `UserSyncSubscriber`, `LoyaltySyncSubscriber`, `PaymentConfirmedSubscriber`, kitchen) |
| Crawl/onboarding | `VanAn.Crawler` → Gateway API + doanhnghiep.vn → `CrawlController`/`OcrOnboardingController` → tenant data + knowledge ingest → vanan-ai |
| Storefront browse | KhachLink → Gateway catalog/tenant endpoints → YARP forward → ShopERP for product catalog |

## 4. Tenant context propagation

- **Staff/owner:** JWT (HS256 `Jwt:Secret`) with `tenant_id` claim; `RequireTenantAccess` policy; permission groups; token versioning (`TokenVersion`) for revocation.
- **Customer:** `customer_token` (DataProtection-minted via ShopERP `InternalCustomerTokenController`/`CustomerIdentityController`); guest = `X-Customer-Device-Id`/`customerDeviceId` (device-bound identity, ATRS guest TrustSubject via SHA-256 device id).
- **Service-to-service:** internal API key (`X-Internal-Api-Key`/`INTERNAL_LOYALTY_API_KEY`) for trusted hops (e.g., ShopErpTokenIssuer → atrs); vanan-ai = caller key + scope metadata + **HMAC-signed tenant ctx (±5 min)**; metering = caller secrets server-side only.
- **AI public:** server-side resolution (host/slug/validated tenantId) — client tenantId never authoritative.
- **Instance-level:** `X-KhachLink-Domain` header → `KhachLinkInstances` → NavFlags feature gate (kill-switch AND semantics).

## 5. Duplicated / fragmented / unclear ownership findings

| Finding | Severity | Detail |
|---|---|---|
| `OmnichannelService` in-memory stub coexists with real chat/notification services | Medium | `3_CoreHub/Services/OmnichannelService.cs` — dictionaries + TODO; misleading name suggests capability that doesn't exist |
| POS providers (KiotViet/Sapo) return fake success | Medium | `Providers/POS/*` — `Task.FromResult(Success:true)`; if any UI exposes "sync from KiotViet" it silently no-ops |
| `apps/HRApp` duplicates `4_MobileApps/HRApp` | Low | Two MAUI solutions — unclear which is canonical |
| Domain logic in 7,331-line `Domain.cs` AND `1_Shared/Domain/` aggregates | Medium | Two locations for domain types; migrations reference both |
| Notification paths: `CompositeNotificationService` (CoreHub), `NotificationCenterService`, `OutboxNotificationService`, `PushNotificationService`, ATRS `AccountNotification` | Medium | Five overlapping notification mechanisms; channel coverage differs per path |
| Loyalty read/write across CoreHub PG + ShopERP SQLite + Alliance HTTP proxy | Medium | `LoyaltyReadRouter`, sync publishers, reconcile cases — working but complex; single-writer discipline needed |
| `RetryService.cs.bak`, `StateMachineService.cs.bak`, `*.bak` files in Services | Low | Dead files in production tree |
| Circular-dep history managed via `Lazy<T>` (known pattern #6) | Low | Documented; watch for new cycles |

## 6. Architecture vs target capability model — deltas

- **Target:** "modular, interoperable, secure, tenant-aware, evolving without monolith." **Actual:** CoreHub is a modular-monolith-in-process inside Gateway — pragmatic and working, but the largest scaling/ownership risk. Separate services exist exactly where isolation mattered (metering, AI, voice, ATRS) — a healthy pattern to continue.
- **Target:** channel abstraction layer. **Actual:** none — first-party surfaces only.
- **Target:** authoritative business services for AI actions. **Actual:** present (orders/booking/products) but AI is read-only; W4 will need tool/permission layer.
- **Target:** clear bounded contexts. **Actual:** mostly clear by service; inside CoreHub boundaries blur (200 services).
