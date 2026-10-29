# Hướng dẫn triển khai / cài đặt Server vanan-metering — cho nhân sự triển khai

> **Mục đích:** cài đặt, cấu hình, vận hành service "Phí nền tảng" (`vanan-metering`) — solution + service **độc lập** (`.NET 8 Minimal API`, SQLite GĐ-1 → PostgreSQL GĐ-3/P13).
> **Hướng dẫn sử dụng cho SystemAdmin:** xem `docs/guides/metering-sysadmin-guide.md`.
> **Quy tắc kiến trúc bắt buộc:** `.devin/rules/metering.md`.

---

## 1. Kiến trúc & Network

```
KhachLink/ShopERP/Gateway (callers)
   │  X-Caller-Id + X-Metering-Key (+ X-Tenant-Type/Status)
   ▼
app2.<domain>/admin/metering/api/v1/*        ← nginx gateway (public path DUY NHẤT)
   │  proxy_pass http://<METERING_REMOTE_HOST>:5050/
   ▼
vanan-metering VPS (:5050, INTERNAL ONLY) ── SQLite /app/data/metering.db (WAL)
```

- **VPS riêng:** `vanan-metering` (GCP `vanan-prod`, asia-southeast1-b) — ext `34.87.121.66` · int `10.148.0.6`.
- **Port 5050 KHÔNG mở ra ngoài** (GCP firewall chặn; tag `metering` không có rule — đúng design). Chỉ vào qua nginx gateway.
- **DB:** SQLite trong named volume `/app/data` (WAL mode — đọc DB phải copy **cả 3 file** `.db` + `-wal` + `-shm`, `docker cp` một mình file `.db` sẽ STALE).
- **Chủ sở hữu dữ liệu:** metering service KHÔNG có FK sang bảng Vạn An — `tenantId`/`shopInstanceId` là **opaque caller claims**.

---

## 2. Cài đặt thủ công lần đầu (fallback khi chưa có CD)

### 2.1 Tạo VM (GCP)

```bash
gcloud compute instances create vanan-metering \
  --project vanan-prod --zone asia-southeast1-b \
  --machine-type e2-small --image-family ubuntu-2204-lts --image-project ubuntu-os-cloud \
  --tags metering
# Tag "metering" KHÔNG có firewall rule mở port 5050 — giữ nguyên
```

### 2.2 Docker + thư mục deploy

```bash
# (CD tự làm phần này — xem §4; thủ công chỉ khi cần)
curl -fsSL https://get.docker.com | sh && sudo usermod -aG docker $USER
sudo apt-get update -qq && sudo apt-get install -y docker-compose-plugin
sudo mkdir -p /opt/vanan-metering && sudo chown $USER:$USER /opt/vanan-metering
```

### 2.3 Deploy files + env

Copy `docker-compose.metering.yml` vào `/opt/vanan-metering/` (repo gốc — CD làm tự động).

Tạo `.env.metering` (chmod 600):

```bash
IMAGE_PREFIX=ghcr.io/anlebao
IMAGE_TAG=<sha>
METERING_CALLER=shoperp-a:<api-key>        # caller thường (usage/check/wallet)
METERING_ADMIN=sysadmin:<admin-key>        # caller ADMIN (rate-card/topup/config)
METERING_CALLERS=gateway:<key>;directory:<key>   # các caller khác, phân cách bằng ';'
```

> ⚠️ `;` phân cách list caller — **dấu `,` KHÔNG hoạt động**.
> ⚠️ Không bao giờ commit/echo key — truyền qua env của deploy job / secrets.

### 2.4 Chạy + health

```bash
cd /opt/vanan-metering
docker login ghcr.io -u <actor> --password-stdin
docker compose -f docker-compose.metering.yml --env-file .env.metering pull
docker compose -f docker-compose.metering.yml --env-file .env.metering up -d --remove-orphans --wait
curl -fsS http://localhost:5050/v1/health   # {"status":"ok",...}
```

---

## 3. Env & Secrets (reference)

### 3.1 Env của container (`docker-compose.metering.yml`)

| Biến | Mặc định | Ghi chú |
|---|---|---|
| `Metering__DbPath` | `/app/data/metering.db` | SQLite trong volume |
| `Metering__BootstrapCaller` | — | `id:key` caller thường (seed nếu chưa tồn tại) |
| `Metering__BootstrapAdmin` | — | `id:key` caller ADMIN (`CallerKey.IsAdmin`) |
| `Metering__BootstrapCallers` | — | list `id:key` phân cách **`;`** |
| `Metering__TopupTtlHours` | 72 | Topup Pending hết hạn sau N giờ (lazy-expire khi đọc) |
| `ASPNETCORE_URLS` | `http://+:5050` | |

### 3.2 GitHub Secrets (CD dùng)

| Secret | Dùng cho |
|---|---|
| `METERING_CALLER` / `METERING_ADMIN` / `METERING_CALLERS` | → `Metering__Bootstrap*` khi deploy |
| `VPS_METERING_HOST` / `VPS_METERING_USER` / `VPS_METERING_SSH_PRIVATE_KEY` | SSH tới VPS metering |
| `METERING_REMOTE_HOST` | IP nội bộ `10.148.0.6` — nginx gateway proxy tới :5050 |
| `METERING_*` (ShopERP-side) | `Metering:BaseUrl/CallerId/CallerKey/AdminCallerId/AdminKey` cho ShopERP/Gateway |

### 3.3 Env phía caller (ShopERP/Gateway/Directory — mỗi host một client riêng, KHÔNG shared DLL)

```
Metering__BaseUrl=https://app2.khachvip.online/admin/metering/api/   # PHẢI có trailing '/'
Metering__CallerId=shoperp-a
Metering__CallerKey=...
Metering__AdminCallerId=sysadmin        # chỉ ShopERP (admin ops: rate-card/topup/config)
Metering__AdminKey=...
```

- Client fail-safe (G9): thiếu config → **self-disable** (no-op, log 1 lần) — staging chạy được không cần metering.
- Timeout 3s · circuit breaker 5-fail/60s · check cache 30s.

---

## 4. Deploy tự động qua CD (`cd-multivps.yml`)

1. Push `main` → **Impact Analysis** tính `deploy_set` (đụng `8_Metering/**` → có `metering`).
2. **Build & Push (metering)** → image `ghcr.io/anlebao/vanan-metering:<sha>`.
3. **Deploy to Metering VPS** (job riêng, độc lập gateway/shoperp):
   - SSH `VPS_METERING_*` → chuẩn bị `/opt/vanan-metering` (mkdir + chown).
   - Copy `docker-compose.metering.yml` → viết `.env.metering` (từ secrets, chmod 600).
   - Pull (retry ×5 — lỗi mạng ghcr CDN) → `up -d --wait` → health probe local `:5050/v1/health`.
4. **Smoke tests** sau deploy (production).

**Lưu ý:** nếu VPS mới (chưa có Docker) — job tự cài Docker idempotent. Nếu chỉ đổi file khác (không đụng `8_Metering/`) — metering KHÔNG nằm trong deploy_set → không redeploy.

---

## 5. nginx Gateway (route công khai)

`nginx/templates/vanan.multivps.conf.template` (§ metering):

```nginx
location ^~ /admin/metering/api/ {
    limit_req zone=api burst=60 nodelay;
    proxy_pass http://${METERING_REMOTE_HOST}:5050/;   # strip /admin/metering/api → /v1/*
    proxy_set_header Host $host;
    proxy_read_timeout 30s;
}
```

- `METERING_REMOTE_HOST` default `127.0.0.1` khi unset → 502 cho riêng path này (không ảnh hưởng app khác).
- Public path DUY NHẤT: `https://app2.khachvip.online/admin/metering/api/`.

---

## 6. API Contract (tóm tắt)

**Auth mọi endpoint trừ `/v1/health`:** headers `X-Caller-Id` + `X-Metering-Key` (SHA-256 hash trong `CallerKeys`). Admin-op yêu cầu caller `IsAdmin` → sai quyền trả **403 JSON** (KHÔNG `Results.Forbid`).

| Method + Path | Mô tả | Quyền |
|---|---|---|
| `GET /v1/health` | health (docker HEALTHCHECK + smoke) | public |
| `POST /v1/usage/events` | ingest 1 event (bắt buộc `Idempotency-Key`) | caller |
| `POST /v1/usage/events:batch` | ingest ≤500 (body = **bare JSON array**) | caller |
| `GET /v1/usage/events?tenantId=&serviceCode=&from=&to=&page=` | lịch sử usage | caller |
| `GET /v1/usage/summary?tenantId=&period=yyyy-MM` | báo cáo shadow (theo rate tại occurredAt) | caller |
| `GET /v1/wallets/{t}` | ví (balance/đã mua/granted/alertLevel) | caller |
| `GET /v1/wallets/{t}/check?serviceCode=&estimate=` | check deny (`deny = Deny-card && balance<required`) | caller |
| `POST /v1/wallets/{t}/grants` | cấp credit thủ công | admin |
| `GET /v1/wallets/{t}/ledger?page=` | sổ cái credit (append-only) | caller |
| `GET/POST/PATCH/DELETE /v1/rate-cards…` | CRUD rate-card (append-only chain) | admin |
| `GET /v1/rate-cards/resolve?serviceCode=&tenantId=` | resolve theo precedence + headers | caller |
| `GET/PUT /v1/config/enforcement` | **master switch** toàn hệ thống (PUT: admin) | GET caller / PUT admin |
| `POST /v1/topups` · `GET /v1/topups` | tạo / list yêu cầu nạp | caller |
| `POST /v1/topups/{id}/confirm|reject` | duyệt nạp (confirm nhập credits) | admin |
| `GET/PUT /v1/topup-config` | cấu hình VietQR (singleton) | admin |

**Headers scoping (Issue #197 — D1=A):**
- `X-Tenant-Type`: `HKD` | `Enterprise_SuperSmall` | `Enterprise_SME` | `Enterprise_Large` | `HTX`
- `X-Tenant-Status`: `verified` (Active) | `unverified` (Pending)
- Optional — thiếu → resolve **Global only** (backward compat). Caller resolve attributes từ PG (Gateway) / SQLite mirror (ShopERP), cache 60s.

**Lỗi chuẩn:** body `{"error":{"code","message","retryable"}}` · 400/401/403/404/409/402 (deny).

---

## 7. Vận hành SQLite (GĐ-1 — chưa EF migrations tới P13)

- **Decimal lưu TEXT** → mọi raw SQL so sánh/tính trên cột decimal PHẢI `CAST(... AS NUMERIC)` cả 2 vế.
- `EnsureCreated` không alter DB cũ → mọi bảng/cột mới qua **idempotent DDL** trong `EnsureP7Schema` (Program.cs) — pattern dùng cho mọi phase tới P13.
- **Backup:** docker volume `metering-data` — copy 3 file:
  ```bash
  CID=$(docker ps --format '{{.ID}}' | head -1)
  docker cp $CID:/app/data/metering.db /tmp/mdb.bak.db
  docker cp $CID:/app/data/metering.db-wal /tmp/mdb.bak.db-wal
  docker cp $CID:/app/data/metering.db-shm /tmp/mdb.bak.db-shm
  ```
- **Append-only:** `CreditLedgerEntry` / `AuditLog` / `UsageEvent` / `RateCardVersion` — sửa sai = reversal/version mới, TUYỆT ĐỐI không UPDATE/DELETE.
- Mọi mutation admin ghi `AuditLogs` **cùng SaveChanges** với thay đổi.

---

## 8. Checklist kiểm tra sau deploy (RV L1-L2)

```bash
# 1. Health qua domain
curl -s https://app2.khachvip.online/admin/metering/api/v1/health        # 200
# 2. No-auth → 401
curl -s -o /dev/null -w '%{http_code}' https://app2.khachvip.online/admin/metering/api/v1/wallets/xxx
# 3. Auth caller (lấy key từ .env.metering trên VPS, KHÔNG echo)
CID=<id> KEY=<key>
curl -s -H "X-Caller-Id: $CID" -H "X-Metering-Key: $KEY" \
  "https://app2.khachvip.online/admin/metering/api/v1/wallets/<tenantId>/check?serviceCode=feature.booking"
# 4. Master switch mặc định ON
curl -s -H "X-Caller-Id: $CID" -H "X-Metering-Key: $KEY" \
  https://app2.khachvip.online/admin/metering/api/v1/config/enforcement   # {"enabled":true,...}
# 5. Schema (trên VPS — container KHÔNG có sqlite3/python3, copy DB ra host rồi check)
gcloud compute ssh vanan-metering --zone asia-southeast1-b --project vanan-prod -- \
  "CID=\$(docker ps --format '{{.ID}}' | head -1) && \
   docker cp \$CID:/app/data/metering.db /tmp/mdb.db && \
   docker cp \$CID:/app/data/metering.db-wal /tmp/mdb.db-wal && \
   docker cp \$CID:/app/data/metering.db-shm /tmp/mdb.db-shm && \
   python3 -c \"import sqlite3;c=sqlite3.connect('/tmp/mdb.db');print([r[1] for r in c.execute('PRAGMA table_info(RateCardVersions)')])\""
```

**RV production đầy đủ (5 lớp):** xem `.devin/rules/runtime-verification.md` — L1 markers + schema · L2 API · L3 Playwright (`playwright-rv-issue197.config.ts`) · L4/L5 manual.

---

## 9. Troubleshooting

| Hiện tượng | Nguyên nhân / xử lý |
|---|---|
| `502` riêng path `/admin/metering/api/` | `METERING_REMOTE_HOST` chưa set (default 127.0.0.1) hoặc VPS metering down — set secret = `10.148.0.6` + redeploy gateway |
| External `:5050` không vào được | Đúng design — firewall chặn. Vào qua nginx gateway |
| `401 missing_credentials` | Thiếu `X-Caller-Id`/`X-Metering-Key` |
| `401 invalid_credentials` | Key sai / caller chưa seed (`BootstrapCaller` đúng định dạng `id:key`) |
| `403 forbidden` | Cần admin caller (`Metering:AdminCallerId/AdminKey`) |
| Ingest trả 0 deduct nhưng có usage | Chưa có rate-card (unpriced) hoặc **master switch TẮT** |
| Deduct sai số âm thầm | Decimal TEXT — raw SQL thiếu `CAST(... AS NUMERIC)` (xem §7) |
| DB đọc trên máy khác lệch số | Copy thiếu `-wal`/`-shm` (WAL chưa checkpoint) |
| CD deploy metering bị skip | Commit không đụng `8_Metering/**` → không nằm trong `deploy_set` (đúng) |

---

## 10. Lộ trình hạ tầng (GĐ sau)

- **P13:** SQLite → PostgreSQL (bỏ CAST decimal, EF migrations, backup/PITR, CallerKey→tenant mapping).
- **P9/P10:** NATS alert push + mirror balance SQLite ShopERP.
- **P17:** thu tiền tự động (Order.SourceType=`credit_topup` → OrderFlow + e-invoice) — thay top-up thủ công.