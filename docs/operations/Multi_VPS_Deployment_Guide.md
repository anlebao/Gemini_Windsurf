# Multi-VPS Deployment Guide — VanAn Option C (3-VPS Split)

> **Mục đích:** Hướng dẫn deploy VanAn ecosystem trên 3 VPS Google Cloud (Gateway VPS + KhachLink VPS + ShopERP VPS) theo kiến trúc Option C.
>
> **Prerequisites:** Đã hoàn thành `GCP_Registration_StepByStep.md` (3 VPS đã tạo, VPC + firewall OK, ping giữa các VPS thành công).
>
> **Cập nhật:** 2026-08-07

---

## 0. Thông tin VPS của bạn

| VPS | External IP | Internal IP | Spec | Vai trò |
|---|---|---|---|---|
| vanan-gateway | 136.85.94.119 | 10.148.0.2 | e2-small, Debian 12 | PostgreSQL + NATS + Gateway + nginx (4 containers) |
| vanan-khachlink | TBD | 10.148.0.4 | e2-small, Debian 12 | KhachLink + Seq + Certbot (3 containers) |
| vanan-shop-a | 34.177.89.248 | 10.148.0.3 | e2-small, Debian 12 | ShopERP (per-tenant SQLite + NATS subscriber) |

**ShopInstance ID cho vanan-shop-a:** `9e94f876-27bd-4a16-a85b-b5f42620bc6e`
(đã generate — dùng cho cả .env.shoperp + ShopInstance record trong Gateway PG)

---

## 1. Kiến trúc tổng quan (3-VPS Split)

```
                    ┌─────────────────────────────────────────────┐
                    │           vanan-gateway VPS                 │
                    │           (10.148.0.2)                      │
                    │                                             │
   Internet ───────┤  nginx (80/443)                              │
                    │    ├─ api.*  → Gateway (local)              │
                    │    ├─ diemthuong.* → KhachLink (REMOTE)     │
                    │    └─ app.* + www.* → ShopERP (REMOTE)      │
                    │                                             │
                    │  PostgreSQL (5432 exposed for VPC)          │
                    │  NATS (4222 exposed for VPC)                │
                    │  Gateway API (internal)                     │
                    └──────────────┬──────────────────────────────┘
                                   │ VPC internal (free egress)
                                   │ nats://10.148.0.2:4222
                                   │ postgres:10.148.0.2:5432
                    ┌──────────────▼──────────────────────────────┐
                    │           vanan-shop-a VPS                  │
                    │           (10.148.0.3)                      │
                    │                                             │
                    │  ShopERP (port 80)                          │
                    │    ├─ SQLite per-tenant (/app/keys/)        │
                    │    ├─ NATS subscriber (order.created.*)     │
                    │    └─ Accounting → remote PostgreSQL        │
                    │                                             │
                    │  No nginx, no PostgreSQL, no NATS server    │
                    └─────────────────────────────────────────────┘

                    ┌─────────────────────────────────────────────┐
                    │           vanan-khachlink VPS               │
                    │           (10.148.0.4)                      │
                    │                                             │
                    │  KhachLink (port 80)                        │
                    │    └─ Calls Gateway API via VPC             │
                    │  Seq (port 5341)                            │
                    │    └─ Receives Serilog logs from Gateway    │
                    │  Certbot (SSL renewal)                      │
                    │                                             │
                    │  No nginx, no PostgreSQL, no NATS server    │
                    └─────────────────────────────────────────────┘
```

---

## 2. Files đã tạo cho multi-VPS (3-VPS split)

| File | Vai trò |
|---|---|
| `.github/workflows/cd-multivps.yml` | **GitHub Actions workflow — build + deploy 3 VPS** |
| `docker-compose.gateway.yml` | Compose cho Gateway VPS (postgres + nats + gateway + nginx) |
| `docker-compose.khachlink.yml` | Compose cho KhachLink VPS (khachlink + seq + certbot) |
| `docker-compose.shoperp.yml` | Compose cho ShopERP VPS (shoperp only, remote NATS/PG) |
| `nginx/templates/vanan.multivps.conf.template` | nginx config proxy app.*/www.* → ShopERP, diemthuong.* → KhachLink |
| `nginx/docker-entrypoint.multivps.sh` | Entrypoint substitute `VANAN_DOMAIN` + `SHOPERP_REMOTE_HOST` + `KHACHLINK_REMOTE_HOST` |
| `.env.gateway.example` | Template env vars cho Gateway VPS |
| `.env.khachlink.example` | Template env vars cho KhachLink VPS |
| `.env.shoperp.example` | Template env vars cho ShopERP VPS |
| `scripts/deploy-gateway.sh` | Bootstrap script cho Gateway VPS (manual deploy) |
| `scripts/deploy-khachlink.sh` | Bootstrap script cho KhachLink VPS (manual deploy) |
| `scripts/deploy-shoperp.sh` | Bootstrap script cho ShopERP VPS (manual deploy) |

---

## 3. Quy trình deploy (7 bước)

> **2 cách deploy:**
> - **Cách A — GitHub CD (khuyến nghị):** push code lên `main` → workflow tự build images + deploy lên 2 Google VPS. Xem §3.A.
> - **Cách B — Manual SSH:** SSH vào từng VPS, chạy script. Xem §3.B.
>
> **Branch strategy (tách Oracle vs Google):**
> | Branch | Workflow | Deploy lên |
> |---|---|---|
> | `main` | `cd-multivps.yml` | Google Cloud 2 VPS (Gateway + ShopERP) |
> | `oracle-prod` | `cd.yml` | Oracle VPS (single-VPS, luồng cũ) |
>
> → Push lên `main` → chỉ Google. Push lên `oracle-prod` → chỉ Oracle. Không bao giờ deploy chéo.
>
> **Cách A dễ hơn sau khi setup xong** — chỉ cần push code. **Cách B cho lần đầu** để debug.

### 3.A — Deploy qua GitHub CD (cd-multivps.yml)

#### Bước A.1 — Setup GitHub Secrets (CHỈ 1 LẦN)

Vào repo GitHub → **Settings → Secrets and variables → Actions → New repository secret**:

| Secret name | Value | Ghi chú |
|---|---|---|
| `VPS_GATEWAY_HOST` | `136.85.94.119` | External IP vanan-gateway |
| `VPS_GATEWAY_USER` | `lebaoan81` | Username SSH gateway |
| `VPS_GATEWAY_SSH_PRIVATE_KEY` | `<SSH private key>` | Xem §3.A.2 cách tạo |
| `VPS_SHOP_HOST` | `34.177.89.248` | External IP vanan-shop-a |
| `VPS_SHOP_USER` | `lebaoan81` | Username SSH shop-a |
| `VPS_SHOP_SSH_PRIVATE_KEY` | `<SSH private key>` | Có thể dùng cùng key với gateway |
| `SHOPERP_REMOTE_HOST` | `10.148.0.3` | VPC internal IP vanan-shop-a |
| `GATEWAY_REMOTE_HOST` | `10.148.0.2` | VPC internal IP vanan-gateway |
| `SHOP_INSTANCE_ID` | `9e94f876-27bd-4a16-a85b-b5f42620bc6e` | Unique cho vanan-shop-a |
| `VANAN_DOMAIN` | `khachvip.online` | Domain thật |
| `POSTGRES_PASSWORD` | `<strong-32-char>` | Strong password |
| `JWT_SECRET_KEY` | `<strong-256-bit>` | JWT signing key |
| `SEQ_ADMIN_PASSWORD` | `<strong>` | Seq log UI password |
| `VAPID_PRIVATE_KEY` | `<vapid-private-key>` | Web Push (xem `.env.example`) |
| `GOOGLE_CLIENT_ID` | `<google-client-id>` | Google OAuth |
| `GOOGLE_CLIENT_SECRET` | `<google-client-secret>` | Google OAuth |
| `INTERNAL_LOYALTY_API_KEY` | `vanan-internal-loyalty-prod-2026` | Service-to-service auth |

#### Bước A.2 — Tạo SSH key cho GitHub Actions

GitHub Actions cần SSH key để login vào VPS. Tạo key riêng (không dùng key cá nhân):

```bash
# Trên local machine
ssh-keygen -t ed25519 -C "github-actions-vanan-deploy" -f ~/.ssh/vanan_github_deploy
# KHÔNG nhập passphrase (để trống — GitHub Actions không thể nhập passphrase)

# Copy PUBLIC key lên Gateway VPS
cat ~/.ssh/vanan_github_deploy.pub | ssh lebaoan81@136.85.94.119 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"

# Copy PUBLIC key lên ShopERP VPS
cat ~/.ssh/vanan_github_deploy.pub | ssh lebaoan81@34.177.89.248 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"

# Copy PRIVATE key content → paste vào GitHub Secret VPS_GATEWAY_SSH_PRIVATE_KEY + VPS_SHOP_SSH_PRIVATE_KEY
cat ~/.ssh/vanan_github_deploy
# Copy toàn bộ nội dung (từ -----BEGIN ... -----END) paste vào GitHub Secret
```

> ⚠️ Private key phải bao gồm cả dòng `-----BEGIN OPENSSH PRIVATE KEY-----` và `-----END OPENSSH PRIVATE KEY-----`.

#### Bước A.3 — Tạo GitHub Environments (tùy chọn nhưng khuyến nghị)

Vào repo → **Settings → Environments → New environment**:
- `production-gateway` — cho job deploy-gateway
- `production-shoperp` — cho job deploy-shoperp

Có thể thêm protection rules (require approval, restrict to branches) để an toàn hơn.

#### Bước A.4 — Trigger deploy

**Cách 1 — Manual trigger (khuyến nghị cho lần đầu):**
1. Vào repo GitHub → **Actions** tab
2. Chọn workflow **"CD Multi-VPS"** (cd-multivps.yml)
3. Nhấn **"Run workflow"**
4. Chọn branch `main`
5. (Tùy chọn) Đổi `image_tag` hoặc tick `skip_build` nếu đã build rồi
6. Nhấn **"Run workflow"** (xanh)

**Cách 2 — Auto trigger (đã bật mặc định):**
`cd-multivps.yml` đã cấu hình `push: branches: [main]` → mỗi push lên `main` sẽ tự build + deploy lên Google Cloud 2 VPS.
→ Để deploy lên Oracle, push lên branch `oracle-prod` (trigger `cd.yml`).
→ KHÔNG bao giờ push cùng code lên cả 2 branch cùng lúc nếu không muốn deploy chéo.

#### Bước A.5 — Theo dõi deploy

Vào **Actions** tab → click run mới nhất:
- **Job 1 Build & Push** — build 3 images (gateway, shoperp, khachlink) → push GHCR
- **Job 2 Pre-Deployment Validation** — validate compose files + check secrets
- **Job 3 Deploy to Gateway VPS** — SSH vào gateway, copy files, docker compose up
- **Job 4 Deploy to ShopERP VPS** — SSH vào shop-a, test NATS+PG connectivity, docker compose up
- **Job 5 Post-Deploy Smoke Test** — curl health endpoints

Nếu job 4 fail ở bước "Testing NATS connectivity" → kiểm tra firewall rule `allow-nats-internal`.
Nếu fail ở "Testing PostgreSQL connectivity" → tạo firewall rule `allow-postgres-internal` (xem §3.B Bước 1).

---

### 3.B — Deploy Manual SSH (cho lần đầu hoặc debug)

### Bước 1 — Thêm firewall rule cho PostgreSQL (CHƯA có)

Rule `allow-nats-internal` đã có, nhưng PostgreSQL port 5432 chưa mở. Tạo thêm:

```bash
# Chạy trên local (có gcloud CLI) hoặc Cloud Shell
gcloud compute firewall-rules create allow-postgres-internal \
  --direction=INGRESS \
  --priority=900 \
  --network=vanan-vpc \
  --action=ALLOW \
  --rules=tcp:5432 \
  --source-tags=shop-erp \
  --target-tags=gateway
```

> ⚠️ Nếu bạn dùng default network (không phải `vanan-vpc`), thay `--network=default`.
> Kiểm tra: `gcloud compute networks list`

### Bước 2 — Clone repo + chuẩn bị files trên Gateway VPS (manual)

SSH vào vanan-gateway:
```bash
# Từ local
ssh lebaoan81@136.85.94.119
```

Trên Gateway VPS:
```bash
# Cài git
sudo apt update && sudo apt install -y git

# Clone repo
cd /opt
sudo mkdir -p vanan && sudo chown $USER:$USER vanan
cd vanan
git clone https://github.com/anlebao/VanAn.git .  # thay bằng repo URL thật

# Tạo .env.gateway
cp .env.gateway.example .env.gateway
nano .env.gateway
```

Edit `.env.gateway` — điền giá trị thật:
```bash
VANAN_DOMAIN=khachvip.online          # domain thật của bạn
SHOPERP_REMOTE_HOST=10.148.0.3        # internal IP của vanan-shop-a
POSTGRES_PASSWORD=<strong-32-char-password>
JWT_SECRET_KEY=<strong-256-bit-key>
SEQ_ADMIN_PASSWORD=<strong-password>
VAPID_PRIVATE_KEY=<your-vapid-private-key>
GOOGLE_CLIENT_ID=<your-google-client-id>
GOOGLE_CLIENT_SECRET=<your-google-client-secret>
```

### Bước 3 — Deploy Gateway VPS (manual)

```bash
cd /opt/vanan
chmod +x scripts/deploy-gateway.sh
./scripts/deploy-gateway.sh
```

Script sẽ:
1. Cài Docker + Docker Compose (nếu chưa có)
2. Validate `.env.gateway`
3. Pull images từ GHCR
4. Start postgres + nats + seq + gateway + khachlink + nginx
5. Health check sau 60s

Verify:
```bash
docker compose -f docker-compose.gateway.yml ps
# Tất cả services phải "Up" hoặc "healthy"

# Test Gateway health
curl http://localhost/health
# Hoặc qua nginx (sau khi có SSL):
# curl https://api.khachvip.online/health
```

### Bước 4 — Clone repo + chuẩn bị files trên ShopERP VPS (manual)

SSH vào vanan-shop-a (mở terminal mới):
```bash
ssh lebaoan81@34.177.89.248
```

Trên ShopERP VPS:
```bash
sudo apt update && sudo apt install -y git

cd /opt
sudo mkdir -p vanan && sudo chown $USER:$USER vanan
cd vanan
git clone https://github.com/anlebao/VanAn.git .

# Tạo .env.shoperp
cp .env.shoperp.example .env.shoperp
nano .env.shoperp
```

Edit `.env.shoperp` — điền giá trị thật (PHẢI khớp Gateway):
```bash
VANAN_DOMAIN=khachvip.online
NATS_REMOTE_HOST=10.148.0.2          # internal IP của vanan-gateway
GATEWAY_REMOTE_HOST=10.148.0.2       # internal IP của vanan-gateway
SHOP_INSTANCE_ID=9e94f876-27bd-4a16-a85b-b5f42620bc6e  # unique cho VPS này
POSTGRES_PASSWORD=<MUST MATCH Gateway .env.gateway>
JWT_SECRET_KEY=<MUST MATCH Gateway .env.gateway>
VAPID_PRIVATE_KEY=<MUST MATCH Gateway .env.gateway>
GOOGLE_CLIENT_ID=<MUST MATCH Gateway .env.gateway>
GOOGLE_CLIENT_SECRET=<MUST MATCH Gateway .env.gateway>
```

> ⚠️ **POSTGRES_PASSWORD + JWT_SECRET_KEY + VAPID_PRIVATE_KEY + Google OAuth PHẢI KHỚP** giữa 2 VPS. Nếu không khớp → auth fail, accounting connection fail.

### Bước 5 — Deploy ShopERP VPS (manual)

```bash
cd /opt/vanan
chmod +x scripts/deploy-shoperp.sh
./scripts/deploy-shoperp.sh
```

Script sẽ:
1. Cài Docker + Docker Compose
2. Validate `.env.shoperp`
3. **Test connectivity** đến NATS (10.148.0.2:4222) + PostgreSQL (10.148.0.2:5432) trên Gateway VPS
4. Pull ShopERP image
5. Start ShopERP
6. Health check

Verify:
```bash
docker compose -f docker-compose.shoperp.yml ps
# vanan-shoperp phải "Up" hoặc "healthy"

# Test health
curl http://localhost/health

# Check NATS subscription log
docker logs vanan-shoperp 2>&1 | grep -i "OrderSyncSubscriber"
# Phải thấy: "OrderSyncSubscriber: subscribed to vanan.cloud.order.created.9e94f876-..."
```

### Bước 6 — Tạo ShopInstance record trong Gateway PG

ShopERP trên vanan-shop-a đã start với `SHOP_INSTANCE_ID=9e94f876-...`, nhưng Gateway PG chưa có record này. Tạo qua API:

```bash
# Trên Gateway VPS — login SystemAdmin trước, rồi:
curl -X POST http://localhost/api/v1/shop-instances \
  -H "Authorization: Bearer <SYSTEM_ADMIN_JWT_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "baseUrl": "http://10.148.0.3:80",
    "label": "VPS-SG-ShopA e2-small",
    "maxTenants": 40,
    "healthCheckUrl": "http://10.148.0.3:80/health"
  }'
```

> 💡 `maxTenants: 40` theo sổ tay capacity (e2-small Paid 1GB/mo). Nếu Free tier → đặt 12.
> 💡 `baseUrl` dùng VPC internal IP (10.148.0.3), KHÔNG dùng external IP.

Hoặc tạo trực tiếp trong PostgreSQL:
```bash
docker exec -it vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "
INSERT INTO \"ShopInstances\" (\"Id\", \"BaseUrl\", \"Label\", \"MaxTenants\", \"IsActive\", \"HealthStatus\", \"CreatedAt\")
VALUES ('9e94f876-27bd-4a16-a85b-b5f42620bc6e', 'http://10.148.0.3:80', 'VPS-SG-ShopA e2-small', 40, true, 'Unknown', NOW());
"
```

Verify:
```bash
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "
SELECT \"Id\", \"Label\", \"MaxTenants\", \"IsActive\", \"HealthStatus\" FROM \"ShopInstances\";
"
```

### Bước 7 — Setup SSL + test end-to-end

#### 7a. SSL certs (nếu có domain)

```bash
cd /opt/vanan
chmod +x scripts/init-ssl.sh
./scripts/init-ssl.sh
# Script sẽ dùng certbot lấy Let's Encrypt cert cho *.khachvip.online
```

#### 7b. Test end-to-end

```bash
# 1. Gateway health
curl https://api.khachvip.online/health

# 2. KhachLink
curl -I https://diemthuong.khachvip.online

# 3. ShopERP (qua gateway nginx proxy)
curl -I https://app.khachvip.online

# 4. NATS routing — tạo order test từ KhachLink, kiểm tra log ShopERP
docker logs vanan-shoperp 2>&1 | grep -i "order.*created"
```

---

## 4. Kiểm tra firewall rules (verify)

```bash
gcloud compute firewall-rules list --filter="network:vanan-vpc"
```

Phải có 5 rules:

| Rule | Source | Target | Ports | Mục đích |
|---|---|---|---|---|
| allow-ssh-admin | `<IP-của-bạn>/32` | gateway, shop-erp | tcp:22 | SSH |
| allow-http-https | `0.0.0.0/0` | gateway, shop-erp | tcp:80,443 | Web |
| allow-nats-internal | `10.148.0.0/24` hoặc tag gateway | shop-erp | tcp:4222 | NATS Gateway→ShopERP |
| allow-nats-monitor-admin | `<IP-của-bạn>/32` | shop-erp | tcp:8222 | NATS monitoring |
| **allow-postgres-internal** | tag shop-erp | gateway | tcp:5432 | PostgreSQL ShopERP→Gateway |

> ⚠️ Nếu bạn dùng default network (không phải `vanan-vpc`), thay `--network=default` trong tất cả lệnh gcloud.

---

## 5. Troubleshooting

### ShopERP không connect được NATS

```bash
# Trên shop-a VPS
nc -zv 10.148.0.2 4222
# Nếu timeout → firewall chặn, kiểm tra allow-nats-internal
# Nếu connection refused → NATS chưa start trên Gateway, kiểm tra:
#   docker logs vanan-nats
```

### ShopERP không connect được PostgreSQL

```bash
# Trên shop-a VPS
nc -zv 10.148.0.2 5432
# Nếu timeout → firewall chặn port 5432, tạo rule allow-postgres-internal (Bước 1)
# Nếu connection refused → PostgreSQL chưa start, kiểm tra:
#   docker logs vanan-postgres
```

### Order không đến ShopERP

```bash
# 1. Kiểm tra Gateway tạo order + outbox
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "
SELECT \"Id\", \"Subject\", \"Status\", \"CreatedAt\" FROM \"OutboxEvents\" ORDER BY \"CreatedAt\" DESC LIMIT 5;
"

# 2. Kiểm tra NATS nhận event
docker logs vanan-nats 2>&1 | tail -20

# 3. Kiểm tra ShopERP subscriber
docker logs vanan-shoperp 2>&1 | grep -i "order.*created\|OrderSyncSubscriber"

# 4. Kiểm tra SHOP_INSTANCE_ID khớp
# Gateway order subject: vanan.cloud.order.created.{shopInstanceId}
# ShopERP subscribes: vanan.cloud.order.created.{SHOP_INSTANCE_ID}
# Nếu không khớp → order đi sai VPS
```

### Auth fail giữa ShopERP và Gateway

```bash
# Kiểm tra JWT_SECRET_KEY khớp
# Trên Gateway:
grep JWT_SECRET_KEY /opt/vanan/.env.gateway
# Trên ShopERP:
grep JWT_SECRET_KEY /opt/vanan/.env.shoperp
# PHẢI GIỐNG NHAU
```

### nginx 502 Bad Gateway cho app.*

```bash
# nginx không reach được remote ShopERP
# Trên Gateway VPS:
curl -I http://10.148.0.3:80
# Nếu timeout → firewall chặn port 80 từ gateway sang shop-a
# Tạo rule:
gcloud compute firewall-rules create allow-shoperp-http-internal \
  --direction=INGRESS --action=ALLOW --rules=tcp:80 \
  --source-tags=gateway --target-tags=shop-erp --network=vanan-vpc
```

---

## 6. Mở rộng — thêm VPS mới (vanan-shop-b)

Khi vanan-shop-a đạt ~80% capacity (xem sổ tay `ShopInstance_Capacity_Handbook.md`):

1. Tạo VM mới `vanan-shop-b` (cùng region `asia-southeast1`, zone `c`).
2. Gắn tag `shop-erp`, gắn vào `vanan-vpc`.
3. Generate NEW ShopInstance Guid:
   ```bash
   python3 -c 'import uuid; print(uuid.uuid4())'
   # Ví dụ: a1b2c3d4-...
   ```
4. Clone repo + tạo `.env.shoperp` với `SHOP_INSTANCE_ID=<new-guid>`.
5. Run `deploy-shoperp.sh`.
6. Tạo ShopInstance record trong Gateway PG với Id=`<new-guid>`.
7. Onboard tenant mới với `shopInstanceId=<new-guid>`.

> ⚠️ KHÔNG bao giờ reuse `SHOP_INSTANCE_ID` giữa các VPS — sẽ gây order routing sai.

---

## 7. Backup

### PostgreSQL (Gateway VPS)

```bash
# Daily backup
docker exec vanan-postgres pg_dump -U vanan_admin VanAnCoreHub | gzip > /opt/vanan/backups/pg-$(date +%Y%m%d).sql.gz

# Cron job (crontab -e):
# 0 2 * * * docker exec vanan-postgres pg_dump -U vanan_admin VanAnCoreHub | gzip > /opt/vanan/backups/pg-$(date +\%Y\%m\%d).sql.gz
```

### SQLite (ShopERP VPS)

```bash
# Backup SQLite volume
docker run --rm -v vanan_shoperp_data:/data -v /opt/vanan/backups:/backup alpine \
  tar czf /backup/shoperp-$(date +%Y%m%d).tar.gz /data
```

---

## 8. Tắt VPS khi không dùng (tránh tính phí)

> ⚠️ VPS **Stop** vẫn tính phí disk. Phải **Delete** mới dừng hẳn.

```bash
# Stop (vẫn tính phí disk ~$0.04/GB/tháng)
gcloud compute instances stop vanan-gateway --zone=asia-southeast1-a
gcloud compute instances stop vanan-shop-a --zone=asia-southeast1-b

# Delete (dừng hẳn — MẤT DATA)
gcloud compute instances delete vanan-gateway --zone=asia-southeast1-a
gcloud compute instances delete vanan-shop-a --zone=asia-southeast1-b
```

> 💡 Trước khi delete, backup PostgreSQL + SQLite (xem §7).

---

> **Bảo trì:** Cập nhật khi (a) thêm VPS mới, (b) thay đổi architecture, (c) Google đổi pricing. Phiên bản: 1.0 (2026-08-06).
