# Task P1 — Skeleton `vanan-metering`

> **Status:** ✅ DONE (2026-10-23) — build 0/0, smoke test PASS · chờ push để CI build image
> **Phase:** P1 (GĐ-1) — master plan `docs/AI/plans/phi-nen-tang-metering-master-plan.md`

## Delivered

```
8_Metering/vanan-metering/
  VanAn.Metering.sln              # solution riêng — KHÔNG vào VanAn.sln
  Dockerfile                      # pattern crawler: SDK 8.0 → aspnet 8.0, port 5050, VOLUME /app/data
  src/
    VanAn.Metering.Api/           # minimal API — health + auth middleware + bootstrap seed
    VanAn.Metering.Contracts/     # DTO contract (UsageEventRequest/Response, ApiError)
    VanAn.Metering.Domain/        # UsageEvent + CallerKey (single identity, [G13])
    VanAn.Metering.Infra/         # MeteringDbContext (EF Core Sqlite 8.0.8 CPM)
```

| Thành phần | Chi tiết |
|---|---|
| `/v1/health` | No-auth — docker HEALTHCHECK + CD smoke |
| Auth middleware | `X-Caller-Id` + `X-Metering-Key` → SHA-256 hash match trên `CallerKeys` (SRS [G3]/NFR-8); `/v1/*` trừ health đều gated |
| `/v1/auth/check` | Probe verify pipeline trước khi P3 ingest có |
| `UsageEvent` | Đủ cột SRS §7 + unique index `(CallerId, IdempotencyKey)` + index (TenantId/ServiceCode, OccurredAtUtc) |
| `CallerKey` | unique CallerId, KeyHash SHA-256 |
| Bootstrap | env `Metering__BootstrapCaller="callerId:apiKey"` → seed 1 credential khi DB trống (CallerKey CRUD = P7+) |
| DB | `Metering:DbPath` (default `data/metering.db`, container `/app/data/metering.db`); `EnsureCreated` P1 — migration thật ở P13 |

## CI wiring

- `deployment-impact.yml`: app `metering` → `8_Metering/**` (thay vì rơi vào `unknown: full`)
- `cd-multivps.yml`: matrix `metering` → `8_Metering/vanan-metering/Dockerfile` → `ghcr.io/anlebao/vanan-metering`; thêm vào fail-closed fallback deploy_set (bài học Pattern #32 — image phải được build trong mọi full deploy)
- Deploy job/compose: **P5** (P1 chỉ build image)

## Verify

- `dotnet build VanAn.Metering.sln -c Release` → **0 warnings / 0 errors**
- Smoke local (`dotnet run`, port 5050): health 200 · no-auth 401 · key đúng 200 `{callerId:"shoperp-a"}` · key sai 401 · SQLite tạo bảng + unique indexes đúng · bootstrap seed OK
- Docker image build: chưa chạy local (daemon off) — CI build-push-action sẽ verify trên push

## Deviation / lưu ý

- 4 `.csproj` mới = **deviation đã phê O4** (rule "no new .csproj" — metering là service độc lập theo chỉ đạo user). Architecture tests không bị ảnh hưởng (check theo path cụ thể).
- `VanAn.Metering.sln` tách khỏi `VanAn.sln` → `dotnet build VanAn.sln` + guard-check không động vào metering (đúng ý đồ độc lập; CI build image qua Dockerfile riêng).
