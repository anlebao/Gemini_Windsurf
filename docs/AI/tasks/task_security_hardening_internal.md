# Task SEC-INTERNAL — Hardening bảo mật kênh nội bộ (NATS / credentials / signed ctx)

> **Status:** 🟡 DRAFT 2026-11-07 — chờ user duyệt (phát sinh từ security review sau P10 RV)
> **Gate:** security hardening — **deploy-touching** (secrets + compose + rotate) → cần user điều phối rotate keys
> **Kiến trúc nền:** `docs/Architecture/multi-vps-service-communication.md`
> **Phạm vi:** `docker-compose.gateway.yml` · `docker-compose.shoperp.yml` · `cd-multivps.yml` · `6_Testing/rv-*.cjs` + `.devin/*` + `archive-e2e` · vanan-ai `TenantContextSigner` · ShopERP `TenantAiApiClient` · Gateway `TenantAiForwarder` — **KHÔNG đụng Domain**

## Findings (verified trên production + repo)

| # | Sev | Vấn đề | Bằng chứng |
|---|---|---|---|
| SEC-1 | 🔴 HIGH | **NATS không auth** — `nats:2.10-alpine -js` không user/pass/nkey, publish `0.0.0.0:4222`. Bất kỳ ai reach được :4222 (VPC host hoặc internet nếu firewall hở) đều publish giả `vanan.cloud.order.created.*` / đọc sync traffic. Bảo vệ duy nhất = GCP firewall. | `docker-compose.gateway.yml` :39-50 — no auth flags |
| SEC-2 | 🔴 HIGH | **Password sysadmin production trong repo** — `2026@vanan` hardcode trong file đã commit: `6_Testing/rv-*.cjs` (nhiều file, kể cả mới P10), `.devin/*`, `archive-e2e`, `.env.example`. Repo reader → login → impersonate mọi tenant → full data. | `git grep 2026@vanan` — ~15+ file |
| SEC-3 | 🟠 MED | **Plain HTTP internal** — caller keys + HMAC ctx + tenant data unencrypted trong VPC. GCP mã hóa tầng vật lý nhưng host bị compromise trong VPC MITM được app-layer. | `TenantAi:BaseUrl=http://10.148.0.7:5060` etc. |
| SEC-4 | 🟠 MED | **Caller keys tĩnh, shared, không rotation** — `METERING_CALLERS` / `X-Ai-Key` / `X-Internal-Api-Key` / `CtxSigningSecret` share gateway+shoperp → lộ 1 `.env` forge được ctx mọi tenant (nếu allowlist rộng). | Program.cs DI registrations |
| SEC-5 | 🟠 MED | **HMAC ctx replay ±5ph** — ctx không có nonce/jti → capture 1 ctx replay được 5 phút cho mọi endpoint cùng scope (đọc conversations/docs → exfiltrate). Dedupe chỉ che chat write path. | `TenantContextSigner` expiry ±5min |
| SEC-6 | 🟡 LOW | Impersonation audit — `/api/admin/impersonate/{tenant}` chưa verify có audit trail ai-impersonate-ai-khi-nào. | cần kiểm tra AuditLog coverage |

## Plan theo phase (ưu tiên rủi ro/effort)

### P1 — SEC-2: Rút credential khỏi repo + rotate (độc lập, làm trước)

- [ ] Đổi mọi script `rv-*.cjs` / `.devin/*` / specs sang đọc `SYSADMIN_PASSWORD` từ **env var** (fallback fail-fast với message rõ, KHÔNG default). Pattern: `const PWD = process.env.SYSADMIN_PASSWORD ?? (() => { throw new Error('set SYSADMIN_PASSWORD') })()`.
- [ ] `.env.example` / docs: đổi sang placeholder `__SET_ME__`.
- [ ] **Rotate password sysadmin trên production** (user thực hiện — coi như đã lộ, git history giữ mãi; purge history optional vì đổi mật khẩu đã đủ vô hiệu).
- [ ] Dọn `.devin/` artifacts khỏi repo (junk files, login.json) — thêm `.gitignore` nếu cần.
- [ ] Verify: `git grep "2026@vanan"` → 0 kết quả (trừ changelog note trong card này).

### P2 — SEC-1: NATS authentication

- [ ] `docker-compose.gateway.yml`: nats command thêm auth — **prefer `nkey` hoặc user/pass** qua env (`NATS_USER`/`NATS_PASSWORD` từ GH secret `NATS_CREDENTIALS`), đọc từ `-config` file hoặc `--user/--pass` flags.
- [ ] `docker-compose.shoperp.yml` + TenantSyncSubscriber + CoreHub NatsSyncWorker: `Nats__Url` → `nats://user:pass@host:4222` (ghép từ env, không hardcode).
- [ ] GH secret mới `NATS_CREDENTIALS` → CD envs cho deploy-gateway + deploy-shoperp (+ metering/khachlink nếu subscribe).
- [ ] Verify sau deploy: publish/subscribe hoạt động (order created flow), connect không creds → `-ERR 'Authorization Violation'`.
- [ ] Bonus trong cùng pass: verify GCP firewall 4222 chỉ mở giữa tag shop-instances (external → closed).

### P3 — SEC-5 + SEC-4: HMAC ctx chặt hơn

- [ ] SEC-5a: giảm window ctx expiry **±5ph → 60-90s** (`TenantContextSigner` + validate phía vanan-ai — clock skew đã được đo, đủ cho call latency).
- [ ] SEC-5b: thêm `jti` (GUID) vào ctx payload → vanan-ai cache used-jti (in-memory TTL > window) → replay cùng jti → 401. Scope: chỉ cần cho GET endpoints đọc data (conversations/documents/profile) — write path đã có idempotency.
- [ ] SEC-4: tách `CtxSigningSecret` per caller nếu effort ≤ 1 ngày (`TenantAi:CtxSigningSecret:gateway|shoperp`); nếu lớn hơn → ghi deviation, giữ shared + siết allowlist: `TenantAi:BootstrapAdminScope`/`IngestScope` liệt kê tenant cụ thể (không `*`).
- [ ] Rotation procedure doc: quy trình đổi key không downtime (dual-accept cũ/mới trong window).

### P4 — SEC-3 (defer-đánh giá) + SEC-6

- [ ] SEC-3: **không implement TLS ngay** — đánh giá + quyết định: (a) accept-risk doc (GCP mã hóa VM↔VM tầng vật lý — passive sniff ngoài VPC không đọc được; residual = app-layer MITM từ host bị compromise), hoặc (b) internal TLS nếu cần compliance. Ghi verdict vào card.
- [ ] SEC-6: verify impersonation ghi `AuditLog` (actor sysadmin + target tenant + timestamp + IP); thiếu thì bổ sung 1 dòng audit.

## Ranh giới (HARD)

- KHÔNG hardcode secret mới vào repo — mọi credential qua GH secrets → env → compose.
- KHÔNG đổi Domain entities / EF configs (SEC-5 chỉ đụng signer+validator).
- KHÔNG đổi auth model tổng thể (HMAC caller-key giữ nguyên — chỉ siết parameters).
- P2 yêu cầu **maintenance window nhỏ**: nats restart → order delivery tạm ngừng vài giây; announce trước.
- Rotate password sysadmin = **user action** — agent chỉ chuẩn bị scripts/docs.

## Tests bắt buộc

- [ ] Core.Tests: ctx expiry 60-90s boundary · jti replay → 401 · unknown-caller vẫn 401
- [ ] ShopERP.Tests: TenantAiApiClient không đổi contract (existing 16/16 giữ xanh)
- [ ] Smoke sau P1: `git grep` clean · RV script chạy được khi `SYSADMIN_PASSWORD` set
- [ ] Smoke sau P2: order tạo trên PG → xuất hiện kitchen ShopERP (RV order flow); nats connect no-creds → denied
- [ ] Smoke sau P3: chat storefront vẫn grounded (replay attack test: capture ctx → reuse sau 90s → 401)

## Acceptance

- [ ] P1: 0 credential trong repo + password rotated + scripts env-based
- [ ] P2: NATS yêu cầu auth trên production + firewall 4222 verified internal-only
- [ ] P3: ctx window ≤90s + jti replay denied + rotation doc
- [ ] P4: verdict SEC-3 ghi nhận + impersonation audit confirmed
- [ ] guard ALL PASSED · build 0 err · không regression ShopERP.Tests/Core.Tests
- [ ] Security section cập nhật vào `multi-vps-service-communication.md` (auth model sau hardening)

## Không được làm

- Không self-signed TLS everywhere ngay (P4 decide-first) · không mTLS/service-mesh
- Không đổi metering append-only semantics · không đụng LLM pipeline
- Không rotate key production mà không backup `.env` hiện tại phía VPS trước

## Delivered

_(điền sau implement từng phase)_
