# Task AI-P0b — Auth: Caller Scopes + Signed Tenant Context

> **Status:** ✅ DONE (code) 2026-11-06 — build 0/0, **18/18 tests PASS** · còn phần CP1 deploy (VPS `vanan-ai` provision — user action)
> **Gate:** P1-G1 Foundation — SRS v1.1 [D7] · threat T6/T12 · **→ checkpoint CP1 (deploy + smoke RV)**
> **Phụ thuộc:** P0 skeleton

## Objective

Hoàn thiện auth 3 lớp của `vanan-ai`: API key → caller scope → HMAC-signed tenant context. Đây là lớp chống tenant-spoofing — test fail ở đây = STOP, không qua gate.

## Deliverables

| Thành phần | Spec |
|---|---|
| `CallerScopes` | Parse `scopes_json`: `public-chat`, `ingest`, `admin`, `health`. Endpoint→scope map cứng trong middleware/endpoint filter |
| `TenantScope` | `tenant_scope_json`: `["*"]` (chỉ `gateway`) hoặc `[tenantGuid,...]` (`shoperp-*`). Caller gọi tenant ngoài scope → **403** |
| `SignedTenantContext` | `X-Tenant-Ctx` = base64url(`{tenantId, callerId, iat, exp, requestId}`) · `X-Tenant-Ctx-Sig` = base64url(HMAC-SHA256(ctx, `TenantAi__CtxSigningSecret`)). Verify: sig → `exp` (skew ±5ph, quy ước metering G5) → `callerId` khớp `X-Caller-Id` → tenant ∈ scope |
| `TenantContextSigner` | Shared logic ký ctx — impl trong `VanAn.TenantAi.Contracts` (pure C#) để P4 copy pattern sang `2_Gateway/Services/TenantAiContextSigner.cs` (không shared DLL — per-host convention) |
| Middleware order | `key` → `scope` → `ctx` (khi endpoint yêu cầu tenant). Thiếu ctx trên tenant endpoint = 401; sig sai/expired = 401; scope fail = 403 |
| Audit log | Mọi request log `callerId, tenantId?, endpoint, result` (structured) — phục vụ điều tra blast radius |

## Scope matrix (enforce + test)

| Caller | Scopes | Tenant scope |
|---|---|---|
| `gateway` | `public-chat` | `*` (phục vụ mọi tenant by design — mitigation = signed ctx + audit) |
| `shoperp-a` | `ingest`, `admin` | tenant(s) trên shop-a VPS |
| `sysadmin` (sau) | `admin` | `*` — thêm khi cần, không tạo sẵn |

## Tests bắt buộc (gate evidence)

- [ ] no-key 401 · bad-key 401 · `/v1/health` 200 không key
- [ ] `gateway` key → `/v1/admin/*` = 403 · `gateway` → `/v1/knowledge/*` = 403 (T12)
- [ ] `shoperp-a` key → tenant ngoài `tenant_scope` = 403 (T6 partial)
- [ ] ctx: sig sai 401 · expired (iat>5ph) 401 · callerId mismatch 401 · thiếu ctx trên tenant endpoint 401
- [ ] ctx hợp lệ → request đi qua, `HttpContext` có `TenantId` claim đúng
- [ ] Tamper test: sửa `tenantId` trong ctx payload (giữ sig cũ) → 401

## CP1 — Deploy + Smoke RV (sau card này)

- Provision VPS `vanan-ai` (GCP asia-southeast1): docker + `/opt/vanan-ai` + user `deploy` + secrets env
- nginx gateway: `location /ai/api/ → http://<internal>:5060/`
- Smoke: health qua domain 200 · external `:5060` blocked · no-auth 401 · bad-key 401 · good-key + ctx probe 200
- Secrets set: `TenantAi__BootstrapCaller` (gateway + shoperp-a keys), `TenantAi__CtxSigningSecret`, `VPS_TENANTAI_*`

## Acceptance

- [x] Tất cả auth tests xanh (15/15 auth + 3 skeleton)
- [ ] CP1 smoke pass — chờ VPS `vanan-ai` provision (user action) + push

## Delivered (2026-11-06)

```
Contracts/TenantContext.cs        # TenantContextPayload + TenantContextSigner
                                  #   base64url(json) + HMAC-SHA256 sig, FixedTimeEquals,
                                  #   CreatePayload(ttl=120s) — P4 copy pattern sang Gateway
Api/Auth/EndpointAuthMetadata.cs  # RequireScopeAttribute + RequireTenantContextAttribute
                                  #   + ScopeNames + endpoint extensions (.RequireScope/.RequireTenantContext)
Api/Auth/TenantAiAuthMiddleware.cs# 3 lớp: key(constant-time) → scope(metadata) → signed ctx
                                  #   (sig→exp±5ph→callerId→tenant-scope) + audit log mọi /v1 req
Program.cs                        # UseTenantAiAuth() · BootstrapCaller(public-chat, *)
                                  #   + BootstrapIngest(ingest+admin, BootstrapIngestScope guids)
                                  #   + /v1/auth/check + /v1/auth/tenant-check probes
                                  #   + 4 route stubs đúng scope+ctx (501): chat/widget/ingest/admin
tests/                            # TenantAiApiFactory (WAF + InMemory + seed via DI scope)
                                  #   + AuthTests 15 cases: 401/403 matrix, forge/expiry/mismatch/tamper
```

**Scope matrix (verified tests):** `gateway`→`public-chat`,`*` tenant · `shoperp-a`→`ingest`+`admin`, tenant allowlist `[TenantA]`. Route stubs: `/v1/chat`+`/v1/config/widget` (public-chat+ctx) · `/v1/knowledge/products:batch` (ingest+ctx) · `/v1/admin/tenants/{t}/profile` (admin+ctx).

**Ghi nhận khi fix:** (1) `X-Tenant-Ctx-Sig` ký trên ctx string → tampered payload tự động fail sig (không cần re-serialize). (2) `WebApplicationFactory` + bootstrap env config **không reliable** cho seeding → factory seed trực tiếp qua DI scope sau `Server` build; `UseSetting` cho `CtxSigningSecret`. (3) `IsRelational()` guard cho `CREATE EXTENSION` (InMemory tests).
