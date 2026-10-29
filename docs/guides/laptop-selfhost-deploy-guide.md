# Hướng dẫn triển khai — Chạy toàn bộ Vạn An trên laptop Windows 10/11 và publish ra Internet

> **Đối tượng:** người triển khai (deployer/devops) — không cần server thuê.
> **Mục tiêu:** laptop Windows 10/11 (chỉ WiFi, **không IP tĩnh**) chạy đầy đủ Gateway + ShopERP + KhachLink + Postgres + NATS, public HTTPS cho người dùng khắp nơi truy cập.
> **Công cụ khuyến nghị expose:** **Cloudflare Tunnel** (miễn phí, xuyên NAT/CGNAT, tự HTTPS).
> **Cảnh báo:** phương án laptop chỉ phù hợp demo/UAT/small pilot — laptop phải mở liên tục, WiFi rớt = hệ thống rớt. Production thật nên dùng VPS (GCP) theo `DEPLOYMENT.md`.

---

## 0. Bức tranh tổng thể

```
Người dùng Internet
      │ HTTPS (Cloudflare proxy + tunnel, không cần IP tĩnh/mở port)
      ▼
cloudflared (chạy trong WSL2 trên laptop) ── outbound 443 ──► Cloudflare edge
      │ inbound localhost
      ├── app2.<domain>  → http://localhost:5003  (ShopERP — Blazor admin + order)
      ├── api2.<domain>  → http://localhost:5001  (Gateway — API + order creator + JWT)
      └── api.<domain>   → http://localhost:5002  (KhachLink — PWA khách hàng)

Nội bộ laptop (WSL2 + Docker):
      ShopERP :5003 ─┬─ SQLite per-tenant (file)          ← mirror orders + products
      Gateway  :5001 ─┼─ PostgreSQL :5432 (container)      ← source of truth (orders/accounting/tenants)
      KhachLink:5002 ─┴─ NATS :4222 + 8222 (container)     ← order delivery async
```

Ba app .NET 8 + 2 container infra. Mọi luồng đều **outbound** → laptop sau NAT/CGNAT vẫn chạy được.

**Ports chuẩn:**

| App/Infra | Port | Vai trò |
|---|---|---|
| Gateway | 5001 | API, order creator, JWT mint/validate, source-of-truth PG |
| KhachLink | 5002 | PWA khách (đặt hàng, ví) — **HTTP-only**, mọi call qua Gateway |
| ShopERP | 5003 | Blazor admin/POS/kitchen + SQLite mirror per-tenant |
| PostgreSQL | 5432 | container `vanan-postgres-local`, db `VanAnLocal` |
| NATS | 4222 (+8222 monitoring) | container `vanan-nats-local`, JetStream order events |

---

## Phần 1 — Cài WSL2 + Ubuntu (Windows 10/11)

### 1.1 Yêu cầu phần cứng

- CPU hỗ trợ **VT-x/AMD-V** (bật Virtualization trong BIOS — mặc định hầu hết laptop đã bật).
- RAM ≥ 8GB khuyến nghị (Postgres 768MB + NATS 256MB + 3 app .NET + Windows + Chrome ≈ 5–6GB).
- SSD trống ≥ 30GB (WSL image + Docker images + repo + build artifacts).

### 1.2 Cài WSL2 (1 lệnh)

Mở **PowerShell (Admin)**:

```powershell
wsl --install -d Ubuntu-22.04
```

Khởi động lại máy. Mở **Ubuntu** từ Start Menu → tạo user Linux + password (ghi nhớ — cần cho `sudo`).

> Đã có WSL1? Nâng lên WSL2: `wsl --set-version Ubuntu-22.04 2`
> Kiểm tra: `wsl -l -v` → VERSION phải là `2`.

### 1.3 Cho phép truy cập Windows ↔ Linux

WSL2 tự map: trong Ubuntu gọi Windows qua `localhost` hoạt động với `localhostForwarding` mặc định. File repo nên để **trong filesystem Ubuntu** (`~/repo`) — nhanh hơn 5–10 lần so với `/mnt/c/...`.

---

## Phần 2 — Cài tools trong Ubuntu

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y git curl unzip ca-certificates gnupg
```

### 2.1 .NET 8 SDK

```bash
# Microsoft package feed (Ubuntu 22.04)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-sdk-8.0
dotnet --version        # expect 8.0.4xx
```

### 2.2 Docker — Docker Desktop (WSL2 backend) — KHUYẾN NGHỊ

1. Tải Docker Desktop for Windows: https://www.docker.com/products/docker-desktop
2. Cài → Settings → **General** → bật *Use the WSL 2 based engine*.
3. Settings → **Resources → WSL Integration** → bật cho **Ubuntu-22.04**.
4. Trong Ubuntu: `docker info` → hoạt động (không cần cài docker trong Ubuntu).

> **Giảm RAM Docker:** Settings → Resources → giới hạn ~4GB cho 3 infra container + build.

### 2.3 Node.js 20 (chỉ cần nếu chạy Playwright test)

```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs
```

---

## Phần 3 — Clone repo + build

```bash
mkdir -p ~/repo && cd ~/repo
git clone https://github.com/anlebao/Gemini_Windsurf.git
cd Gemini_Windsurf
dotnet restore VanAn.sln
dotnet build VanAn.sln          # lần đầu ~5–10 phút
```

> Repo là **private** — cần PAT (Personal Access Token) hoặc `gh auth login` trước khi clone.

---

## Phần 4 — Chạy hệ thống local

Có 2 chế độ — chọn theo mục đích.

### 4.1 Chế độ nhanh — SQLite-only (demo nhỏ, không Docker)

Script có sẵn `scripts/run-dev.ps1` (chạy từ **Windows PowerShell** trong repo trên ổ Windows):

```powershell
cd C:\path\to\Gemini_Windsurf
.\scripts\run-dev.ps1            # Gateway + ShopERP + KhachLink, SQLite tự seed
.\scripts\run-dev.ps1 -NoKhachLink   # bỏ KhachLink
```

- ShopERP dùng `vanan_shoperp.db` (tự tạo + seed lần đầu).
- Gateway chạy monolithic mode.
- Không cần Docker — nhẹ nhất, phù hợp test chức năng nhanh.

### 4.2 Chế độ đầy đủ — Postgres + NATS (giống production — KHUYẾN NGHỊ cho publish)

**Bước 1 — infra containers:**

```powershell
.\scripts\start-local-infra.ps1
```

Script dựng `docker-compose.infra.yml`: postgres (`vanan-postgres-local`, 5432, db `VanAnLocal`, user `vanan_dev` / `VanAnLocal@2026`, RAM 768MB) + nats (`vanan-nats-local`, 4222 + 8222, JetStream, 256MB) rồi chờ healthy.

**Bước 2 — 3 apps:**

```powershell
.\scripts\start-apps.ps1
```

Script set env + `dotnet run` từng app:

- `ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=VanAnLocal;Username=vanan_dev;Password=VanAnLocal@2026`
- `NATS__Url=nats://localhost:4222` · `GATEWAY_URL=http://localhost:5001`
- Gateway `:5001` · ShopERP `:5003` · KhachLink `:5002`

> **Chạy trong WSL thay PowerShell?** Cùng env vars, dùng `dotnet run --project 2_Gateway`... trong bash, hoặc giữ PowerShell (apps không cần Linux — chỉ infra cần Docker mà Docker Desktop đã bridge).

**Bước 3 — verify smoke:**

```bash
curl http://localhost:5001/health        # Gateway → 200
curl http://localhost:5003/health        # ShopERP → 200
```

Mở browser:
- `http://localhost:5003` → ShopERP — đăng nhập SystemAdmin `sysadmin@vanan.vn` / `2026@vanan` (seed mặc định — **đổi trước khi public!**)
- `http://localhost:5002` → KhachLink
- `http://localhost:5001/api/admin/accounting/reconcile?tenantId=…&period=…` → API tra soát (cần SystemAdmin Bearer)

Stop: `.\scripts\stop-local.ps1`.

---

## Phần 5 — Publish ra Internet (không IP tĩnh, không mở port)

### 5.1 Tại sao Cloudflare Tunnel

| Cách | Yêu cầu | Vấn đề trên laptop WiFi |
|---|---|---|
| Port-forward + DDNS | Router cho mở port + IP public | **Hầu hết ISP VN cho WiFi dùng CGNAT → không thể nhận kết nối inbound, DDNS vô dụng** |
| ngrok | — | Free tier domain đổi mỗi lần + trang cảnh báo trung gian |
| Tailscale Funnel | Tailscale | Giới hạn 3 node share + domain `*.ts.net` |
| **Cloudflare Tunnel** | Domain trỏ nameserver về Cloudflare | **Outbound-only, xuyên mọi NAT, HTTPS tự động, domain riêng ổn định, miễn phí** ✅ |

### 5.2 Điều kiện: 1 domain

Mua domain rẻ (~100–200k/năm, ví dụ `vanan-demo.site`) trên Cloudflare Registrar/GoDaddy/Namecheap → vào Cloudflare Dashboard → **Add site** → đổi 2 nameserver của domain về Cloudflare → đợi active (~5–30 phút).

### 5.3 Cài cloudflared trong Ubuntu

```bash
wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
sudo dpkg -i cloudflared-linux-amd64.deb
cloudflared --version
```

### 5.4 Tạo tunnel + config

```bash
cloudflared tunnel login            # mở browser → chọn domain vừa add
cloudflared tunnel create vanan-laptop
# → nhớ Tunnel ID + file ~/.cloudflared/<id>.json vừa tạo
```

Tạo `~/.cloudflared/config.yml`:

```yaml
tunnel: <TUNNEL_ID>
credentials-file: /home/<user>/.cloudflared/<TUNNEL_ID>.json

ingress:
  # ShopERP — admin/owner/POS (app2)
  - hostname: app2.vanan-demo.site
    service: http://localhost:5003
  # Gateway — API (api2)
  - hostname: api2.vanan-demo.site
    service: http://localhost:5001
  # KhachLink — PWA khách (api)
  - hostname: api.vanan-demo.site
    service: http://localhost:5002
  # bắt buộc: rule cuối 404
  - service: http_status:404
```

> Không cần map `localhost` thêm — WSL2/Docker Desktop forward `localhost` vào Windows host nơi `dotnet run` đang listen.

Trỏ DNS về tunnel (tự tạo CNAME):

```bash
cloudflared tunnel route dns vanan-laptop app2.vanan-demo.site
cloudflared tunnel route dns vanan-laptop api2.vanan-demo.site
cloudflared tunnel route dns vanan-laptop api.vanan-demo.site
```

### 5.5 Chạy tunnel như service (auto-start)

```bash
sudo cloudflared service install        # systemd trong WSL2
sudo systemctl enable --now cloudflared
cloudflared tunnel info vanan-laptop    # kiểm tra connections
```

> WSL2 + systemd: nếu `systemctl` báo lỗi → thêm `/etc/wsl.conf` `[boot] systemd=true` rồi `wsl --shutdown` và mở lại Ubuntu.
> Auto-start sau khi Windows reboot: tạo Scheduled Task (Task Scheduler) chạy `wsl -d Ubuntu-22.04 -e sudo systemctl start cloudflared` khi logon (hoặc để `wsl --shutdown` → service tự chạy lại khi WSL boot bởi task `wsl -e exit`).

### 5.6 Chỉnh config app cho domain public

Gateway/ShopERP cần biết domain public để sinh URL đúng (callback, CORS, redirect):

```bash
# .env.gateway (hoặc env trước khi start-apps)
VANAN_DOMAIN=vanan-demo.site
Gateway__PublicBaseUrl=https://api2.vanan-demo.site
ShopERP__BaseUrl=http://localhost:5003         # nội bộ laptop — cùng máy
```

Với self-host **1 máy** khác production multi-VPS ở chỗ: `ShopERP:BaseUrl` (Gateway→ShopERP internal, vd mirror-orders/loyalty) trỏ `http://localhost:5003` thay vì IP VPS khác. CORS trên Gateway thêm origin `https://api.vanan-demo.site` + `https://app2.vanan-demo.site` nếu code đọc allowed-origins từ config.

### 5.7 Verify end-to-end

```bash
curl https://api2.vanan-demo.site/health          # 200 — qua Cloudflare→tunnel→Gateway
curl https://app2.vanan-demo.site/health          # 200 — ShopERP
```

Mở `https://app2.vanan-demo.site` trên điện thoại 4G → login → đặt hàng thử → kiểm tra đơn vào ShopERP.

---

## Phần 6 — Bảo mật trước khi public (BẮT BUỘC)

1. **Đổi mật khẩu seed:** `sysadmin@vanan.vn` / `2026@vanan`, postgres `vanan_dev`/`VanAnLocal@2026` — đổi trong `.env` + đổi mật khẩu user sau khi login.
2. **Secrets:** `JWT_SECRET_KEY`, `POSTGRES_PASSWORD` — sinh mới ≥32 ký tự, đặt trong `.env` (file này **đã gitignore — tuyệt đối không commit**). `DevToken:Secret` **không set** (endpoint tự 404).
3. **Internal API key:** `InternalLoyalty__ApiKey` (Gateway↔ShopERP) — đổi thay default, giống nhau 2 phía.
4. **Windows Firewall:** không cần mở inbound nào (tunnel outbound-only) — vẫn nên giữ firewall bật; chỉ `localhost` mới gọi được các app.
5. **Cloudflare Zero Trust (tùy chọn):** gắn Access policy (Google login) trước `app2.vanan-demo.site` để chỉ team vào được trang admin trong thời gian demo.
6. **Rate limit/WAF:** bật Cloudflare WAF mặc định + Bot Fight Mode.

---

## Phần 7 — Giữ hệ thống chạy ổn định trên laptop

| Vấn đề | Cách xử lý |
|---|---|
| Laptop sleep → hệ thống chết | Settings → Power → **Never sleep khi cắm sạc** · `powercfg /change standby-timeout-ac 0` |
| Gập nắp laptop | Power Options → "when closing lid" → **Do nothing** |
| Windows update tự reboot | Windows Update → pause 5 tuần / đặt active hours |
| App crash | Chạy `start-apps.ps1` trong tmux/`nssm` service, hoặc wrap `dotnet run` trong vòng retry |
| Docker tự start | Docker Desktop → Settings → **Start when you log in** + bật `docker compose` restart policy `unless-stopped` |
| WiFi đổi mạng/IP | Tunnel outbound → không ảnh hưởng; cloudflared tự reconnect |
| Kiểm tra từ xa | `https://api2.<domain>/health` + UptimeRobot ping 5 phút (miễn phí) |

---

## Phần 8 — Troubleshooting nhanh

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| `wsl --install` lỗi | Virtualization tắt trong BIOS | Bật VT-x/AMD-V trong BIOS |
| `docker info` lỗi trong Ubuntu | WSL integration chưa bật | Docker Desktop → Resources → WSL Integration → bật Ubuntu-22.04 |
| `cloudflared` không vào được | systemd tắt | `/etc/wsl.conf` `[boot] systemd=true` |
| app2 → 502 | cloudflared chưa chạy / app chưa up | `cloudflared tunnel info` + `curl localhost:5003/health` |
| ShopERP load chậm lần đầu | Blazor cold start + seed DB | Đợi 10–20s lần đầu |
| POST order fail cross-app | `ShopERP:BaseUrl`/`GATEWAY_URL` sai | Đặt `localhost:5003`/`localhost:5001` cho self-host 1 máy |
| Public domain 522 | Tunnel rớt | `sudo systemctl restart cloudflared` |
| Repo clone trên `/mnt/c` chậm | IO Windows↔Linux | Di chuyển repo sang `~/repo` (ext4 của WSL) |

---

## 9. Checklist triển khai hoàn chỉnh

- [ ] WSL2 + Ubuntu-22.04 · `wsl -l -v` → VERSION 2
- [ ] `dotnet --version` ≥ 8.0.400 · `docker info` OK · `git` OK
- [ ] Clone `~/repo/Gemini_Windsurf` · `dotnet build VanAn.sln` 0 error
- [ ] `start-local-infra.ps1` → postgres + nats healthy
- [ ] `start-apps.ps1` → 3 app lên · `curl localhost:5001|5003/health` 200
- [ ] Đổi mật khẩu sysadmin + `.env` secrets mới (không commit)
- [ ] Domain trỏ Cloudflare → tunnel create + config.yml 3 hostname + `route dns`
- [ ] `sudo cloudflared service install` + systemd enable
- [ ] Verify `https://api2.<domain>/health` · `https://app2.<domain>` từ điện thoại 4G
- [ ] Power settings: never sleep · lid = do nothing · Docker start on login
- [ ] Uptime monitor `api2/health` · (tùy chọn) Cloudflare Access trước `app2`

---

## 10. Khi nào chuyển sang VPS

Chuyển sang VPS (GCP/AWS — theo `DEPLOYMENT.md` + `docker-compose.*.yml` production) khi:

- Người dùng thật > ~20 đồng thời · laptop không thể mở 24/7 · cần uptime 99%+.
- Multi-tenant production nghiêm túc (kiến trúc chuẩn đã là multi-VPS: gateway + shop-a/b/c + khachlink + metering).

Laptop self-host giữ vai trò: **demo bán hàng, UAT cho khách, dev environment di động, DR backup**.

---

*Tài liệu liên quan: `DEPLOYMENT.md` (VPS production) · `docker-compose.infra.yml` (local infra) · `scripts/start-local-infra.ps1` / `start-apps.ps1` / `run-dev.ps1` (launcher) · `docs/user-guide/Tra_Soat_So_Lieu_Ke_Toan_Guide.md` (feature guide).*
