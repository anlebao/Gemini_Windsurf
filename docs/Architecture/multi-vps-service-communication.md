# Multi-VPS Service Communication — Vạn An Ecosystem

> **Cập nhật:** 2026-11-07 · **Nguồn:** verified từ `docker-compose.*.yml`, `.github/workflows/cd-multivps.yml`, `2_Gateway/Program.cs`, `5_WebApps/ShopERP/Services/TenantAiApiClient.cs`, `2_Gateway/Controllers/PublicTenantAiController.cs`.
> **Mục đích:** mô tả topology deploy + toàn bộ kênh giao tiếp giữa 6 thành phần + cơ chế auth từng kênh. Tham chiếu khi thêm service mới hoặc audit bảo mật.

---

## 1. Topology deploy (production — GCP project `vanan-prod`)

| VPS | Zone | Internal IP | External IP | Chạy | Tag |
|---|---|---|---|---|---|
| `vanan-gateway` | asia-southeast1-a | — | 136.85.94.119 | Gateway :5001 · nginx :80/443 · **NATS :4222** · PostgreSQL | gateway |
| `vanan-shop-a` | asia-southeast1-b | — | 34.177.89.248 | ShopERP :5003 (+ per-tenant SQLite) | shop |
| `vanan-metering` | asia-southeast1-b | 10.148.0.6 | 34.21.219.211 (ephemeral) | Metering :5050 (**internal-only**) | metering |
| `vanan-khachlink` | asia-southeast1-c | — | 136.85.111.51 | KhachLink PWA (nginx static :80) | khachlink |
| `vanan-ai` | asia-southeast1-b | 10.148.0.7 | 34.21.228.114 (ephemeral) | vanan-ai :5060 (**internal-only**) + PG pgvector | tenantai |

**Nguyên tắc:** chỉ gateway + khachlink có public ingress. vanan-ai và metering là **internal-only** (GCP firewall chặn external — đúng threat model D6). Public traffic đều qua nginx trên gateway VPS.

## 2. Sơ đồ luồng

```
Browser ──HTTPS──▶ domains ──▶ nginx (gateway VPS)
                                  │
              ┌───────────────────┼─────────────────────┐
              ▼                   ▼                     ▼
        Gateway :5001       KhachLink :80          ShopERP :5003
        (YARP + PG)         (static PWA)           (shop-a)
              │                   ▲                     ▲
              │    YARP forward   │                     │ NATS 2 chiều
              └───────────────────┘        ┌────────────┘
              │                            │
              │ HTTP internal              │ nats://gw:4222
              ▼                            │
        ┌───────────┐   caller key + HMAC ctx
        │ vanan-ai  │◀──────────────── ShopERP (TenantAiApiClient)
        │  :5060    │◀──────────────── Gateway (TenantAiForwarder)
        └─────┬─────┘
              │ Metering__CallerId/Key
              ▼
        ┌───────────┐
        │ metering  │◀──── Gateway / ShopERP (MeteringApiClient)
        │  :5050    │
        └───────────┘
```

## 3. Từng kênh chi tiết

### 3.1 KhachLink → Gateway — HTTPS public (duy nhất cho browser)

- Blazor WASM trên browser → domain (`diemthuong2.khachvip.online`, `timlathay.com`…) → nginx gateway → `:5001`.
- **HARD STOP:** KhachLink KHÔNG inject DbContext, KHÔNG query DB — chỉ HTTP qua Gateway.
- Public surfaces: `/api/public/tenant-ai/*` (AI chat — anonymous + rate-limit `tenant-ai-public` 60/min + session cap 20/h), `/api/public/booking/*`, catalog, order checkout.
- Tenant resolve **server-side (T6):** `KhachLinkInstance.CustomDomain == Request.Host` → `OwnerTenantId` thắng; client `tenantId` chỉ dùng khi không bound và được validate `Active` — không bao giờ sign raw vào ctx.

### 3.2 Gateway ↔ ShopERP — HTTP internal + YARP forward

- `ShopERP:BaseUrl` (secret `DEPLOY_SHOPERP_REMOTE_HOST` → internal IP) + header `X-Internal-Api-Key`.
- Dùng cho: reconcile mirror providers (orders/inventory, timeout 3s fail-open), seed ShopInstance, product catalog browse forward.
- YARP routes (`2_Gateway/appsettings.json`): `khachlink-route` / `shoperp-route` / `corehub-route` / `fallback-route`.

### 3.3 Gateway → NATS → ShopERP — order delivery (Option C)

- NATS JetStream `nats:2.10-alpine` chạy **trong docker-compose của gateway VPS**, publish `4222:4222`.
- **PG → SQLite:** order mới tạo trên Gateway PG → publish `vanan.cloud.order.created.{shopInstanceId}` → đúng ShopERP VPS subscribe → hiện kitchen/POS (replica, không phải source).
- **SQLite → PG:** order status updates (kitchen/POS) publish ngược → Gateway PG cập nhật.
- Subject khác: `vanan.cloud.tenant.type.changed` (HTX mirror sync), loyalty balance sync.
- ShopERP connect: `NATS_REMOTE_HOST` → `nats://<gw>:4222`; CD pre-flight `test_port` trước deploy.
- ⚠️ NATS hiện **không auth** — xem `docs/AI/tasks/task_security_hardening_internal.md` SEC-1.

### 3.4 Gateway/ShopERP → vanan-ai — HTTP internal `:5060` + HMAC ctx

Auth **3 lớp** (P0b):

1. `X-Ai-Key` caller key — constant-time compare.
2. Endpoint scope metadata — caller phải có scope đúng (`public-chat`/`ingest`/`admin`).
3. `X-Tenant-Ctx` — HMAC-signed tenant context (`TenantContextSigner`, shared `CtxSigningSecret`): payload `{tenantId, callerId, scopes, exp}` ký HMAC-SHA256, window **±5 phút**, callerId phải khớp key, tenant phải nằm allowlist (`TenantAi:BootstrapIngestScope`).

| Caller | Scope | Dùng cho |
|---|---|---|
| `gateway` (`TenantAiForwarder`, timeout 35s > LLM 30s) | `public-chat` | `/v1/chat` · `/v1/config/widget` · `/v1/chat/sessions/{id}/messages` |
| `shoperp` (`TenantAiApiClient`) | `ingest` + `admin` | products:batch · documents ingest/approve/delete · `/v1/admin/*` (P10 admin UI) |

Missing config (`TenantAi:BaseUrl`/`CtxSigningSecret`) → client **self-disable** graceful (widget ẩn `aiEnabled:false`, admin báo "không kết nối").

### 3.5 → metering `:5050` — HTTP internal + caller key

- `MeteringApiClient` ở **Gateway + ShopERP + vanan-ai**: `Metering:BaseUrl` (internal `10.148.0.6`) + `X-Caller-Key` per caller (`METERING_CALLER`, `METERING_ADMIN`, `METERING_CALLERS` list — một secret seed cả 2 phía).
- Semantics: wallet/topup **append-only** — check balance, usage report, quota deny gate.
- vanan-ai → metering: quota check [G7] — metering fail → **degrade không-LLM** (không deny cứng).
- Admin API proxy public: `app2.khachvip.online/admin/metering/api/` → nginx → internal :5050 (admin key).

### 3.6 Storefront AI chat — end-to-end (P4)

```
KhachLink widget (IAiChatClient → TenantAiPublicHttpService)
  → POST /api/public/tenant-ai/chat  (anonymous, rate-limited)
  → Gateway: resolve tenant (T6) → flag AIChat_Enabled → session cap → TenantAiForwarder
  → vanan-ai /v1/chat (signed ctx, caller "gateway")
  → metering quota check → LLM (Gemini) → grounded answer + product cards
```

## 4. Fail-safe degrade matrix (thiết kế cố tình)

| Service down | Hậu quả |
|---|---|
| vanan-ai | Widget storefront ẩn (`aiEnabled:false`); admin pages báo "không kết nối"; sync knowledge fail im lặng — **checkout/POS không ảnh hưởng** (NFR-3) |
| metering | ShopERP fail-open (banner ví không hiện); vanan-ai degrade không-LLM; deny gate fail-safe |
| NATS | Order delivery PG→SQLite ngừng → kitchen/POS không nhận đơn mới (đơn vẫn tạo được trên PG) |
| ShopERP | YARP catalog forward 502; orders vẫn tạo trên PG chờ deliver khi NATS/back online |
| Gateway | **SPOF kép** — mất cả public ingress + NATS + PG → toàn hệ thống ngừng phục vụ |

## 5. Config/secrets map (CD `cd-multivps.yml`)

| Secret/env | Deploy lên | Trỏ tới |
|---|---|---|
| `DEPLOY_SHOPERP_REMOTE_HOST` | gateway | `ShopERP:BaseUrl` internal |
| `DEPLOY_NATS_REMOTE_HOST` | shoperp | `nats://<gw>:4222` |
| `DEPLOY_GATEWAY_REMOTE_HOST` | shoperp | gateway base cho status sync |
| `DEPLOY_TENANTAI_REMOTE_HOST` | gateway + shoperp | `http://10.148.0.7:5060/` |
| `TENANTAI_CTX_SIGNING_SECRET` | gateway + shoperp + vanan-ai | HMAC key chung |
| `TENANTAI_BOOTSTRAP_CALLER` / `INGEST` | vanan-ai | seed caller keys + scopes |
| `DEPLOY_METERING_REMOTE_HOST` | gateway + shoperp + vanan-ai | `http://10.148.0.6:5050/` |
| `METERING_CALLERS` | metering + clients | list `caller:key` — một secret seed 2 phía |

## 6. Lưu ý vận hành đã học

- **External IP GCP là ephemeral** — reboot đổi IP (incident metering 2026-11-06: secret `VPS_METERING_HOST` trỏ IP cũ → CD dial fail). `*_REMOTE_HOST` secrets phải là **internal IP** (không đổi), host SSH secrets cần cập nhật khi reboot nếu dùng ext IP.
- **SPOF:** gateway VPS chứa nginx + Gateway + NATS + PG — chết là chết hết ingress. Chưa có HA.
- Internal calls **plain HTTP** trong VPC — GCP mã hóa ở tầng vật lý nhưng không có app-layer TLS → xem task card security hardening.
