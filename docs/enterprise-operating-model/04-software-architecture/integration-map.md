# Integration Map — P3

> Registry: `registries/apis.yaml` (13 entries). `64789992`.

## 1. NATS events (verified subjects)

| Subject | Direction | Publisher → Subscriber |
|---|---|---|
| `vanan.cloud.order.created.{shopInstanceId}` | PG→SQLite | Gateway → OrderSyncSubscriber |
| `vanan.cloud.order.status.changed.{shopInstanceId}` | routed | ShopERP ↔ status consumers |
| `vanan.cloud.order.payment.confirmed.{si}` | PG→SQLite | CoreHub/Gateway → ShopERP (I) |
| `vanan.cloud.tenant.verified` | PG→SQLite | TenantManagement → TenantSyncSubscriber |
| `vanan.cloud.tenant.profile.updated` | PG→SQLite | TenantManagement → TenantSyncSubscriber |
| `vanan.cloud.tenant.type.changed` | PG→SQLite | HtxProfileService → TenantSyncSubscriber |
| `vanan.cloud.loyalty.changed.{deviceId}` | PG→SQLite | AllianceWallet/LoyaltyBalance → LoyaltySyncSubscriber |
| `vanan.cloud.loyalty.config.changed` | PG→SQLite | LoyaltyConfigController → LoyaltySyncSubscriber |
| `vanan.cloud.notification.created` | → Gateway hub | NotificationCenterService → NotificationHubNotifier |
| `vanan.shoperp.*` | SQLite→PG | ShopERP publishers → Gateway/CoreHub (subjects TBD detail) |

Prefix convention (verified): `vanan.cloud.*` = PG→SQLite, `vanan.shoperp.*` = SQLite→PG (`NatsSyncWorker` comments).

## 2. HTTP/API surfaces

| Surface | Auth | Notes |
|---|---|---|
| Public: `/api/public/*` (orders, booking, tenant-ai, metering) | anonymous + rate-limited | pattern per `PublicOrdersController`/`PublicBookingController` |
| `/api/catalog/*`, `/api/products/*` | customer/JWT | two DIFFERENT `recommended` endpoints — see discrepancy D3 |
| Internal cross-VPS | `X-Internal-Api-Key` (CTRL-006) | reconcile/mirror calls ShopERP↔Gateway |
| JWT bearer | tokens issued by ShopERP | secondary scheme both hosts (API-112) |
| vanan-metering `/v1/*` | `X-Caller-Id` + `X-Metering-Key` + `Idempotency-Key` | 4 MeteringApiClient callers |
| vanan-ai `/v1/*` | caller key + scopes + HMAC `X-Tenant-Ctx` | Gateway signs via `TenantAiContextSigner` (CTRL-005) |
| YARP forwards | passthrough | routes/clusters in `2_Gateway/appsettings.json` |

## 3. External integrations

Gemini (LLM+embedding — SYS-003 SmartEntry, SYS-008 vanan-ai) · payment/VietQR (metering top-up, wallet) · e-invoice (`Providers/EInvoice` + EInvoiceWorker/Subscriber) · Zalo (doc-only, I) · Cloudinary/S3 media.

## 4. AuthN/Z mechanisms summary

Tenant isolation (CTRL-002, TenantId on BaseEntity) · internal key (CTRL-006) · JWT (API-112) · anonymous+rate-limit (public controllers) · metering caller keys (CTRL-003) · AI signed ctx (CTRL-005) · DevToken gate (CTRL-007/008).
