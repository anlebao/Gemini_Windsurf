# METERING "PHÍ NỀN TẢNG" — ARCHITECTURE & INTEGRATION RULES

> Khi nào đọc: bất kỳ task nào đụng `8_Metering/`, `Metering*`, `IMeteringClient`,
> `/v1/wallets`, `/v1/topups`, `rate-cards`, "phí nền tảng", deny/banner/top-up.
> Tiến độ từng phase nằm ở `docs/AI/project_state.md` + task cards
> `docs/AI/tasks/phi_nen_tang_metering/task_p*.md`. File này = kiến thức BỀN.

## 1. Boundary (HARD)

- `8_Metering/vanan-metering` là **solution + service độc lập** (.NET 8 Minimal API,
  EF Core + SQLite GĐ-1, PG ở P13). Entity metering KHÔNG vào `1_Shared/Domain.cs`.
- Domain metering (`src/VanAn.Metering.Domain`) pure: no EF Core, no DbContext.
- `CreditLedgerEntry` + `AuditLog` + `UsageEvent` + `RateCardVersion` = **append-only**;
  sửa sai = reversal/Adjust entry, TUYỆT ĐỐI không UPDATE/DELETE history.
- Mọi mutation admin phải ghi `AuditLogs` trong cùng SaveChanges với thay đổi.

## 2. API contract (caller-facing)

- Auth mọi endpoint (trừ `/v1/health`): headers `X-Caller-Id` + `X-Metering-Key`.
  Key lưu SHA-256 hash trong `CallerKeys`. Admin = `CallerKey.IsAdmin`.
  Admin-guard trả `Forbidden()` JSON 403 — **KHÔNG dùng `Results.Forbid()`**
  (không có auth scheme registered → throw → 500).
- Ingest: `POST /v1/usage/events` (+ `Idempotency-Key` header bắt buộc, dedupe
  `(CallerId,Key)` unique index → replay trả event cũ, không deduct 2 lần).
  Batch: `POST /v1/usage/events:batch` — **route là `{verb}` match segment
  `events:batch`** (`:` illegal trong route template; middleware rewrite KHÔNG
  hoạt động vì WebApplication chạy routing trước user middleware).
  Body = **bare JSON array** `UsageBatchItem[]`, ≤500.
- Wallet: `GET /v1/wallets/{t}` · `GET .../check?serviceCode=&estimate=`
  (deny=true CHỈ khi version head `Enforcement=Deny` && balance<required) ·
  `POST .../grants` (admin) · `GET .../ledger`.
- Topup: `POST /v1/topups` (Idempotency-Key, ref `TOPUP-XXXXXXXX`) ·
  `GET /v1/topups` · `POST .../confirm|reject` (admin) · `GET/PUT /v1/topup-config`.
  Status string lowercase ("pending|confirmed|rejected|expired") — dùng
  `StatusName()` helper, KHÔNG `ToLowerInvariant` (CA1308 + PascalCase leak).
- Rate cards: `GET/POST/PATCH/DELETE /v1/rate-cards` + `GET .../resolve` —
  append-only chain, PATCH tạo version mới (không update in place).

## 3. Semantics phải giữ nguyên

- Deduct **tại ingest trong CÙNG transaction** với UsageEvent (relational tx);
  `applied = min(required, balance)`; dư → `UnpaidCredits`; unpriced → no deduct.
- Check API resolve rate ở `nowUtc`; ingest resolve ở `occurredAtUtc`.
- Alerts: % còn lại trên `GrantedTotal` — ≤50% Warn50, ≤10% Warn90, 0 Depleted;
  unique `(TenantId, Level, CycleKey)`; `CycleKey` đổi mỗi grant/topup → refire.
- Topup: confirm → grant + cycle reset, **idempotent replay**; concurrent confirm
  chặn bằng unique partial index `CreditLedgerEntries.TopupRequestId IS NOT NULL`.
- Wallet tenantId = opaque claim — KHÔNG validate tenant tồn tại.
- Topup lazy expiry: Pending quá TTL (`Metering:TopupTtlHours`, default 72) →
  Expired khi đọc, không có background job.

## 4. SQLite pitfalls (đã trả giá — KHÔNG lặp lại)

- **EF SQLite lưu decimal dạng TEXT** → mọi raw SQL so sánh/tính toán trên cột
  decimal PHẢI `CAST(... AS NUMERIC)` cả 2 vế (`'100' >= '60'` lexicographic
  = FALSE → deduct trả 0 âm thầm). Khi chuyển PG (P13): bỏ CAST, dùng numeric thật.
- `EnsureCreated` không alter DB có sẵn → mọi bảng/cột mới phải có idempotent
  DDL trong `EnsureP7Schema` (Program.cs) — pattern này dùng lại cho mọi phase sau
  tới khi có EF migrations (P13).
- Race/unique-index test PHẢI dùng SQLite shared-cache in-memory
  (`Mode=Memory;Cache=Shared`) — EF InMemory KHÔNG enforce unique index.
- EF InMemory `BeginTransaction` throw → guard `db.Database.IsRelational()`.
- `docker cp` file `.db` ra ngoài đọc STALE — phải copy cả `-wal`/`-shm` (WAL chưa checkpoint).

## 5. Client conventions (per-host, KHÔNG shared DLL — D2)

- Contract chung: `1_Shared/Domain/Common/IMeteringClient.cs` + `MeteringUsageEvent`;
  `MeteringBalanceCheck(Balance, Required, Deny, AlertLevel?)`.
- Mỗi host có `Services/MeteringApiClient.cs` RIÊNG (ShopERP/Gateway/Directory):
  timeout 3s · circuit breaker 5-fail/60s · **fail-open**: mọi method trả
  false/null khi lỗi, self-disable khi thiếu `Metering:BaseUrl/CallerId/CallerKey`.
- `BaseAddress` BẮT BUỘC trailing `/` (URI relative `v1/...` mất segment cuối) —
  normalize trong ctor VÀ trong env values.
- `CheckBalanceAsync` cache 30s/(tenant,code); null = "allow" (fail-open G9).
- Admin ops (ShopERP): `MeteringRateCardApiClient` riêng — caller `sysadmin`,
  **throw lỗi thật** để UI hiển thị 400/404/409 (khác fail-safe usage client);
  user actor truyền qua `AuditNote "actor:{email} | ..."`.
- ShopERP inject concrete `MeteringApiClient` qua `AddTransient` cast từ
  `IMeteringClient` (wallet/ledger/topup methods ngoài interface).

## 6. ShopERP UI conventions

- `/wallet/phi-nen-tang` (tenant), `/admin/metering/rate-cards` +
  `/admin/metering/topups` (policy `SystemAdmin` + `_isSystemAdmin` guard 2 lớp).
- `MeteringWalletBanner` trong `MainLayout` (Authorized) — poll wallet 60s.
- Deny UX = `<MeteringDenyGate ServiceCode="feature.X">` wrap nội dung trang;
  deny → alert "Hết hạn mức" + link nạp. ServiceCode phải KHỚP rate-card code.
- Mọi trang UI mới → NavMenu (`ShopErpMenuService`) + Sitemap + E2E spec
  `6_Testing/e2e-tests/` (Gate 4) + bUnit nếu testable.

## 7. Deploy & ops

- VPS `vanan-metering` (asia-southeast1-b · int `10.148.0.6` · ext `34.87.121.66`);
  port 5050 external BỊ CHẶN (tag `metering` không firewall rule — đúng design).
- Public path duy nhất: `https://app2.khachvip.online/admin/metering/api/` →
  nginx gateway → internal `10.148.0.6:5050` (secret `METERING_REMOTE_HOST`).
- Deploy dir `/opt/vanan-metering` · `docker-compose.metering.yml` · named volume
  `/app/data/metering.db` (WAL mode — đọc DB phải copy 3 file).
- Callers seed qua env: `Metering__BootstrapCaller` / `Metering__BootstrapAdmin` /
  `Metering__BootstrapCallers` (list `id:key` phân cách **`;`** — `,` KHÔNG hoạt động).
  Caller hiện có: `shoperp-a`, `gateway`, `directory`, `sysadmin` (admin).
- Secrets GH: `METERING_CALLER`, `METERING_ADMIN`, `METERING_CALLERS`,
  `VPS_METERING_*`, `METERING_REMOTE_HOST`. KHÔNG bao giờ in giá trị —
  extract qua `gcloud compute ssh` + python, chỉ hiện length/tên biến.
- ShopERP env: `Metering__BaseUrl` (trailing `/`), `Metering__CallerId/CallerKey`,
  `Metering__AdminCallerId/AdminKey` (extract từ secrets trong deploy job).

## 8. Known deviations / debt (không "fix" bừa)

- `GET /v1/topups` chưa scope tenant server-side (GĐ-1 caller=ShopERP truyền
  tenantId claim) — hardening P8+, KHÔNG coi là bug GĐ-1.
- Chat deny chưa có UI (ShopInbox read-only; chat input ở KhachLink) — deny
  server-side/UI KhachLink = phase sau.
- PATCH rate-card `label=null` giữ giá trị cũ (không clear được) — chấp nhận GĐ-1.
- Patch head `at > head.EffectiveFrom` — test seed phải để EffectiveFrom < now.
- Pre-PG dedupe key `feature:{code}:{tenant}:{yyyy-MM}` (monthly), `booking:{id}`.
- Balance REAL/NUMERIC cast sẽ bỏ khi P13 chuyển PostgreSQL.
