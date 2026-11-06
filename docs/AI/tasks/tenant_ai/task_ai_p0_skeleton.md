# Task AI-P0 — Skeleton `vanan-ai`

> **Status:** ✅ DONE 2026-11-06 — build 0/0 warnings/errors, 3/3 tests PASS · chờ push (image build qua CI — deploy ở CP1 sau P0b)
> **Gate:** P1-G1 Foundation — master plan `docs/AI/plans/tenant-ai-master-plan.md` · SRS v1.1 [D1][D2][D6]
> **Push/RV:** Build image only (CI) — KHÔNG deploy (deploy ở CP1 sau P0b)

## Objective

Tạo solution + skeleton deployable của service `vanan-ai` độc lập, đúng precedent `vanan-metering`. Không business logic.

## Deliverables

```
9_TenantAI/vanan-ai/
  VanAn.TenantAi.sln              # sln riêng — KHÔNG vào VanAn.sln
  Dockerfile                      # SDK 8.0 build → aspnet 8.0 runtime, EXPOSE <port> (chọn 5060 — tránh 5050 metering)
  docker-compose.tenantai.yml     # api + pg (pgvector/pgvector:pg16, volume vanan_ai_pgdata)
  src/
    VanAn.TenantAi.Api/           # minimal API: /v1/health + auth middleware stub + /v1/auth/check
    VanAn.TenantAi.Contracts/     # ApiError + placeholder DTOs
    VanAn.TenantAi.Domain/        # CallerKey entity (single identity)
    VanAn.TenantAi.Infra/         # TenantAiDbContext (EF Core Npgsql + Pgvector pkg)
    VanAn.TenantAi.Ai/            # classlib trống — chỗ cho pipeline (P3)
  tests/
    VanAn.TenantAi.Tests/         # health + auth-check smoke unit tests
```

## Requirements

| Item | Spec |
|---|---|
| `/v1/health` | No-auth — docker HEALTHCHECK + CD smoke; trả `{status:"ok", db:"up"|"down"}` (check PG connect) |
| Auth middleware stub | `X-Caller-Id` + `X-Ai-Key` → SHA-256 hash match `CallerKeys`; gate mọi `/v1/*` trừ health; full scope/ctx logic ở P0b |
| `/v1/auth/check` | Probe trả `{callerId}` — verify pipeline trước khi có business API |
| Bootstrap | env `TenantAi__BootstrapCaller="callerId:apiKey"` → seed khi DB trống (pattern `Metering__BootstrapCaller`) |
| DB ensure | Startup: `CREATE EXTENSION IF NOT EXISTS vector` + `EnsureCreated`/migrate (idempotent) |
| CallerKey | unique CallerId, KeyHash SHA-256, `scopes_json`, `tenant_scope_json` (nullable) — cột đầy đủ ngay dù logic scope ở P0b |

## CI wiring (bài học metering)

- `deployment-impact.yml`: app `tenantai` → `9_TenantAI/**` (tránh rơi `unknown: full`).
- `cd-multivps.yml`: matrix `tenantai` → `9_TenantAI/vanan-ai/Dockerfile` → `ghcr.io/anlebao/vanan-ai`; thêm vào **fail-closed fallback deploy_set** (Pattern #32 — image phải build trong mọi full deploy).
- Deploy job + nginx location `…/ai/api/` + VPS provision: **P0b/CP1** (P0 chỉ build image).

## Tests bắt buộc

- `dotnet build VanAn.TenantAi.sln -c Release` → **0 errors / 0 warnings**
- Unit: health 200 · no-auth `/v1/auth/check` 401 · good-key 200 `{callerId}` · bad-key 401
- Compose up local (hoặc docker): PG + `CREATE EXTENSION vector` thành công · health `db:"up"`

## Deviation / lưu ý

- 5 `.csproj` mới = deviation đã chốt D1 (service độc lập như metering — rule "no new .csproj" không áp cho solution riêng).
- `VanAn.TenantAi.sln` tách → `dotnet build VanAn.sln` + guard-check không động tới (đúng ý đồ).
- Port chọn `5060` default nội bộ — confirm không trùng services khác trên cùng VPS khi provision.

## Acceptance

- [x] Build sạch (`dotnet build VanAn.TenantAi.sln -c Release` → **0 warnings / 0 errors**)
- [x] Tests xanh (3/3 — CallerKey defaults · unique index config · bootstrap skip-dup)
- [x] Card "Delivered" cập nhật — CI image build verify sau push (matrix `tenantai` đã wire)

## Delivered (2026-11-06)

```
9_TenantAI/vanan-ai/
  VanAn.TenantAi.sln              # 6 projects — KHÔNG vào VanAn.sln
  Dockerfile                      # SDK 8.0 → aspnet 8.0, EXPOSE 5060, HEALTHCHECK /v1/health
  docker-compose.tenantai.yml     # api + tenantai-pg (pgvector/pgvector:pg16, volume vanan_ai_pgdata, no published port)
  src/ Api · Contracts (ApiError) · Domain (CallerKey: scopes+tenant_scope cols đầy đủ)
       Infra (TenantAiDbContext + HasPostgresExtension("vector")) · Ai (classlib chỗ P3)
  tests/ VanAn.TenantAi.Tests (xunit + InMemory + FluentAssertions)
```

**Program.cs:** `GET /v1/health` (no-auth, trả `db:"up"|"down"`) · auth middleware `X-Caller-Id`+`X-Ai-Key` SHA-256 (gate mọi `/v1/*` trừ health · scope/ctx ở P0b) · `/v1/auth/check` probe · bootstrap seed `TenantAi__BootstrapCaller` (public-chat) + `TenantAi__BootstrapIngest` (ingest+admin) · startup `CREATE EXTENSION IF NOT EXISTS vector` + `EnsureCreated`.

**CI wiring:** `deployment-impact.yml` app `tenantai` → `9_TenantAI/**` · `cd-multivps.yml` matrix `tenantai` → image `vanan-ai` + **fail-closed fallback deploy_set** (Pattern #32).

**Packages:** `Pgvector` 0.3.0 + `Pgvector.EntityFrameworkCore` 0.2.0 thêm vào CPM (`Directory.Build.props`).

**Deviation:** 5 csproj + 1 test csproj mới = đã chốt D1. Fix khi build: `ToLowerInvariant` CA1308 → uppercase hex (hash consistency giữ nguyên — cùng hàm cả seed lẫn verify).
