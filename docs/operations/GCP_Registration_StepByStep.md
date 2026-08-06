# Hướng dẫn từng bước đăng ký Google Cloud VPS cho VanAn

> **Cách dùng:** Mở file này trên màn hình phụ, mở `console.cloud.google.com` trên browser chính. Làm theo từng bước, đánh dấu `[x]` khi xong. Gặp lỗi hoặc câu hỏi → báo tôi.
>
> **Thời gian ước tính:** 30–45 phút (nếu đã chuẩn bị đủ checklist `GCP_Registration_Checklist.md`).
> **Cập nhật:** 2026-08-06

---

## GIAI ĐOẠN 1 — Đăng ký Free Trial (10 phút)

### Bước 1.1 — Mở trang đăng ký

- [ ] Mở browser, vào `https://console.cloud.google.com/`
- [ ] Nhấn nút **"Get started for free"** (góc trên bên phải) hoặc **"Start free"**
- [ ] Đăng nhập bằng Gmail (đã bật 2-Step Verification)

> 💡 Nếu đã từng đăng ký GCP trước đó với Gmail này → sẽ vào thẳng Console, không thấy nút Free Trial. Dùng Gmail khác.

### Bước 1.2 — Chọn quốc gia + loại tài khoản

- [ ] **Country:** chọn `Vietnam`
- [ ] **Account type:** chọn `Individual` (KHÔNG chọn Business — tránh phải cung cấp giấy phép KD)
- [ ] Tích checkbox đồng ý Terms of Service
- [ ] Nhấn **"Continue"** hoặc **"AGREE AND CONTINUE"**

> ⚠️ Nếu chọn Business → Google sẽ yêu cầu MST + giấy phép kinh doanh + verify qua thư bưu điện. Chọn Individual để đăng ký nhanh, vẫn dùng được đầy đủ tính năng.

### Bước 1.3 — Nhập thông tin thanh toán

Màn hình "Set up billing" — Google hỏi thẻ để verify danh tính.

- [ ] **Name on card:** tên in trên thẻ (khớp với CCCD)
- [ ] **Card number:** 16 chữ số (không có khoảng trắng)
- [ ] **Expiration:** MM/YY
- [ ] **CVV:** 3 chữ số mặt sau
- [ ] **Billing address:** địa chỉ đăng ký ngân hàng (số nhà, đường, phường/xã, quận/huyện, tỉnh/thành)
- [ ] **Postal code:** nhập `700000` (TP.HCM) hoặc `100000` (Hà Nội) — Google không verify kỹ, để trống nếu không bắt buộc
- [ ] Nhấn **"Start my free trial"**

> 💡 Google sẽ hold tạm $1 trên thẻ (hoàn trả trong vài ngày). Có thể cần nhập OTP từ app ngân hàng.

### Bước 1.4 — Xác minh OTP (nếu có)

- [ ] Nhận OTP qua SMS hoặc app ngân hàng
- [ ] Nhập OTP vào màn hình Google
- [ ] Chờ redirect về Console

> ⚠️ Nếu OTP không đến → kiểm tra app ngân hàng có bật "Thông báo giao dịch quốc tế" không. Liên hệ ngân hàng nếu cần.

### Bước 1.5 — Xác nhận đăng ký thành công

Màn hình Console hiện ra với:
- [ ] Banner "Free trial: $300 credit expires in 90 days"
- [ ] Project default tên `My First Project` (sẽ đổi tên ở Bước 2.1)

**✅ Xong Giai đoạn 1. Báo tôi nếu gặp lỗi ở bước nào.**

---

## GIAI ĐOẠN 2 — Tạo project + đặt budget alert (5 phút)

### Bước 2.1 — Tạo project mới

- [ ] Nhấn **project selector** (dropdown góc trên bên trái, hiện "My First Project")
- [ ] Nhấn **"NEW PROJECT"** (góc trên bên phải popup)
- [ ] **Project name:** `vanan-prod`
- [ ] **Organization:** để default (No organization nếu tài khoản cá nhân)
- [ ] **Location:** để default
- [ ] Nhấn **"CREATE"**
- [ ] Chờ 10–20 giây, project sẽ tự chọn

### Bước 2.2 — Đặt budget alert (BẮT BUỘC)

- [ ] Mở menu góc trái (3 dấu gạch ngang) → **Billing**
- [ ] Chọn **"Budgets & alerts"** ở menu trái
- [ ] Nhấn **"Create budget"**
- [ ] **Section 1 — Scope:**
  - Name: `vanan-monthly-budget`
  - Projects: chọn `vanan-prod`
  - Services: All services
  - Nhấn **"Next"**
- [ ] **Section 2 — Amount:**
  - Budget type: `Specified amount`
  - Amount: `10` (USD/tháng)
  - Nhấn **"Next"**
- [ ] **Section 3 — Actions:**
  - Tích "Alert when spend reaches 50% of budget"
  - Tích "Alert when spend reaches 90% of budget"
  - Tích "Alert when spend reaches 100% of budget"
  - Email recipients: Gmail của bạn (mặc định)
  - Nhấn **"Create"**

> ⚠️ KHÔNG bỏ qua bước này. Free Trial $300 hết sau 90 ngày → tự động tính phí thật. Budget alert là ph cứu cánh duy nhất.

**✅ Xong Giai đoạn 2.**

---

## GIAI ĐOẠN 3 — Kích hoạt Compute Engine (2 phút)

### Bước 3.1 — Mở Compute Engine

- [ ] Mở menu trái → **Compute Engine** → **VM instances**
- [ ] Chờ 1–2 phút (lần đầu kích hoạt, có progress bar ở góc dưới)

> 💡 Nếu hiện "Compute Engine API is being enabled" → chờ xong. KHÔNG cần làm gì thêm.

### Bước 3.2 — Kiểm tra region/zone

- [ ] Ở cột phải màn hình VM instances, có dropdown **Region/Zone**
- [ ] Chọn **Region: `asia-southeast1` (Singapore)** — ping thấp về VN
- [ ] Chọn **Zone: `asia-southeast1-a`** (cho Gateway)

**✅ Xong Giai đoạn 3.**

---

## GIAI ĐOẠN 4 — Tạo VPC + Subnet (5 phút)

> 💡 **Vị trí menu VPC (UI Google Cloud mới 2026):**
> - Menu trái (3 gạch ngang) → cuộn xuống section **"Networking"** → **VPC network**
> - HOẶC gõ trực tiếp URL: `https://console.cloud.google.com/networking/networks/list`
> - Nếu không thấy → dùng thanh search ở đầu Console, gõ "VPC network" → chọn kết quả đầu tiên

### Bước 4.1 — Tạo VPC

- [ ] Mở menu trái → cuộn xuống section **Networking** → **VPC network** → **VPC networks**
  - (Hoặc gõ "VPC network" vào thanh search đầu Console)
- [ ] Nhấn **"CREATE VPC NETWORK"** (hoặc **"Create VPC Network"** — nút trên cùng)
- [ ] **Name:** `vanan-vpc`
- [ ] **Description:** `VanAn ecosystem VPC — Gateway + ShopERP multi-VPS`
- [ ] **Subnet creation mode:** chọn **"Custom"**
- [ ] **Subnet 1:**
  - Name: `vanan-subnet-gateway`
  - Region: `asia-southeast1`
  - IPv4 range: `10.10.0.0/16`
  - Nhấn **"Add subnet"**
- [ ] **Subnet 2:**
  - Name: `vanan-subnet-shop-a`
  - Region: `asia-southeast1`
  - IPv4 range: `10.20.0.0/16`
- [ ] **Firewall rules section:** để trống (tao riêng ở Bước 6)
- [ ] **Dynamic routing mode:** `Regional`
- [ ] Nhấn **"CREATE"**
- [ ] Chờ 10–20 giây

> 💡 Cùng region `asia-southeast1` → egress nội bộ MIỄN PHÍ. NATS Gateway↔ShopERP không tốn tiền.

### Bước 4.2 — Verify VPC

- [ ] Vào lại **VPC networks** → thấy `vanan-vpc` với 2 subnet
- [ ] Click vào `vanan-vpc` → thấy 2 subnet ở tab "Subnets"

**✅ Xong Giai đoạn 4.**

---

## GIAI ĐOẠN 5 — Tạo VM Gateway (10 phút)

### Bước 5.1 — Mở form tạo VM

- [ ] Menu trái → **Compute Engine** → **VM instances**
- [ ] Nhấn **"CREATE INSTANCE"**

### Bước 5.2 — Cấu hình cơ bản

- [ ] **Name:** `vanan-gateway`
- [ ] **Region:** `asia-southeast1`
- [ ] **Zone:** `asia-southeast1-a`
- [ ] **Machine configuration:**
  - Series: `E2`
  - Machine type: `e2-small` (2 vCPU, 2GB RAM — vCPU thực 0.5)
- [ ] **Boot disk:** nhấn **"Change"**
  - Operating system: **Debian**
  - Version: **Debian GNU/Linux 12 (bookworm)**
  - Boot disk type: **Standard persistent disk** (free tier)
  - Size: **30 GB**
  - Nhấn **"Select"**
- [ ] **Service account:** Compute Engine default service account
- [ ] **Access scopes:** Allow default access

### Bước 5.3 — Network config

- [ ] Mở rộng section **"Networking, disks, security, scheduling"**
- [ ] Tab **Networking:**
  - Network: `vanan-vpc`
  - Subnetwork: `vanan-subnet-gateway`
  - External IP: **Ephemeral** (sau có domain sẽ reserve static)
- [ ] **Network tags:** `gateway` (quan trọng — firewall rule sẽ target tag này)

### Bước 5.4 — Firewall (tích sẵn)

- [ ] Tích **"Allow HTTP traffic"**
- [ ] Tích **"Allow HTTPS traffic"**

### Bước 5.5 — Tạo VM

- [ ] Cuộn xuống, kiểm tra ước tính chi phí (góc dưới bên phải) — phải hiện "Free trial credit applies"
- [ ] Nhấn **"CREATE"**
- [ ] Chờ 1–2 phút, status chuyển sang dấu tích xanh

### Bước 5.6 — Ghi lại IP

- [ ] Ở cột **External IP** của `vanan-gateway` → ghi lại (sẽ dùng cho SSH + domain sau)

**✅ Xong Giai đoạn 5. Báo tôi IP nếu muốn tôi viết script deploy ngay.**

---External IP 136.85.94.119

## GIAI ĐOẠN 6 — Tạo VM ShopERP-A (5 phút)

Lặp tương tự Bước 5, khác:

- [ ] **Name:** `vanan-shop-a`
- [ ] **Zone:** `asia-southeast1-b` (khác zone với Gateway — chống sập cùng lúc)
- [ ] **Machine type:** `e2-small` (2GB RAM cho 25–40 tenant, theo sổ tay §3.1)
- [ ] **Boot disk:** Debian 12, 30GB Standard
- [ ] **Network:** `vanan-vpc`
- [ ] **Subnetwork:** `vanan-subnet-shop-a`
- [ ] **Network tags:** `shop-erp`
- [ ] **Allow HTTP/HTTPS:** ✅
- [ ] Nhấn **"CREATE"**
- [ ] Ghi lại External IP của `vanan-shop-a`
External IP  : 34.177.89.248
**✅ Xong Giai đoạn 6.**

---

## GIAI ĐOẠN 7 — Tạo Firewall rules (10 phút)

### Bước 7.1 — Mở form firewall

- [ ] Menu trái → section **Networking** → **VPC network** → **Firewall**
  - (Hoặc gõ "Firewall" vào thanh search đầu Console → chọn "Firewall" trong VPC network)
- [ ] Nhấn **"CREATE FIREWALL RULE"** (hoặc **"Create Firewall Rule"**)

### Bước 7.2 — Rule 1: SSH chỉ cho IP của bạn

- [ ] **Name:** `allow-ssh-admin`
- [ ] **Network:** `vanan-vpc`
- [ ] **Direction of traffic:** `Ingress`
- [ ] **Priority:** `1000`
- [ ] **Action on match:** `Allow`
- [ ] **Targets:** `Specified target tags`
- [ ] **Target tags:** `gateway`, `shop-erp` (gõ từng tag, Enter)
- [ ] **Source IPv4 ranges:** `<IP-của-bạn>/32` (xem IP: mở tab khác vào `https://ifconfig.me`)
- [ ] **Protocols and ports:** tích **TCP**, gõ `22`
- [ ] Nhấn **"CREATE"**

> ⚠️ **KHÔNG nhập `0.0.0.0/0` cho SSH.** Đây là lỗi bảo mật phổ biến nhất — bot scan 24/7.

### Bước 7.3 — Rule 2: HTTP/HTTPS public

- [ ] Nhấn **"CREATE FIREWALL RULE"**
- [ ] **Name:** `allow-http-https`
- [ ] **Network:** `vanan-vpc`
- [ ] **Direction:** `Ingress`
- [ ] **Priority:** `1000`
- [ ] **Action:** `Allow`
- [ ] **Targets:** `Specified target tags`
- [ ] **Target tags:** `gateway`, `shop-erp`
- [ ] **Source IPv4 ranges:** `0.0.0.0/0`
- [ ] **Protocols:** TCP `80,443`
- [ ] Nhấn **"CREATE"**

### Bước 7.4 — Rule 3: NATS nội bộ Gateway → ShopERP

- [ ] **Name:** `allow-nats-internal`
- [ ] **Network:** `vanan-vpc`
- [ ] **Direction:** `Ingress`
- [ ] **Priority:** `900`
- [ ] **Action:** `Allow`
- [ ] **Targets:** `Specified target tags`
- [ ] **Target tags:** `shop-erp`
- [ ] **Source IPv4 ranges:** `10.10.0.0/16` (chỉ subnet Gateway)
- [ ] **Protocols:** TCP `4222` (NATS client), `8222` (NATS monitoring)
- [ ] Nhấn **"CREATE"**

> 💡 Dùng source range `10.10.0.0/16` thay vì source tag `gateway` — đơn giản hơn cho lần đầu.

### Bước 7.5 — Rule 4 (tùy chọn): NATS monitoring admin only

- [ ] **Name:** `allow-nats-monitor-admin`
- [ ] **Direction:** `Ingress`
- [ ] **Targets:** `shop-erp`
- [ ] **Source IPv4 ranges:** `<IP-của-bạn>/32`
- [ ] **Protocols:** TCP `8222`
- [ ] Nhấn **"CREATE"**

### Bước 7.6 — Verify

- [ ] Vào **Firewall** → thấy 4 rules (allow-ssh-admin, allow-http-https, allow-nats-internal, allow-nats-monitor-admin)
- [ ] Đảm bảo KHÔNG có rule nào cho `0.0.0.0/0` trên port 22 hoặc 5432

**✅ Xong Giai đoạn 7.**

---

## GIAI ĐOẠN 8 — Test SSH (5 phút)

### Bước 8.1 — SSH qua browser

- [ ] Vào **VM instances** → nhấn nút **"SSH"** cạnh `vanan-gateway`
- [ ] Cửa sổ terminal browser mở ra (lần đầu chờ 30–60 giây generate key)
- [ ] Thấy prompt `your_username@vanan-gateway:~$`

### Bước 8.2 — Test connectivity

> ⚠️ **QUAN TRỌNG:** Các lệnh dưới có placeholder `<...>`. **BẮT BUỘC thay bằng giá trị thật** trước khi chạy. KHÔNG gõ nguyên dấu `<` `>` vào terminal — bash sẽ báo syntax error.
>
> **Cách tìm internal IP của `vanan-shop-a`:**
> 1. Mở tab browser khác → Compute Engine → VM instances
> 2. Tìm dòng `vanan-shop-a`
> 3. Ghi lại giá trị cột **"Internal IP"** (ví dụ `10.20.0.2`)
> 4. Dùng IP đó thay cho `<internal-IP-của-vanan-shop-a>` trong lệnh ping

Trong SSH window của `vanan-gateway`:

```bash
# Kiểm tra OS
cat /etc/os-release | grep PRETTY

# Kiểm tra RAM
free -h

# Kiểm tra disk
df -h /

# Ping ShopERP-A (test VPC nội bộ) — THAY 10.20.0.2 BẰNG INTERNAL IP THẬT CỦA vanan-shop-a
ping -c 3 10.20.0.2
```
===>ping -c 3 10.148.0.3
- [ ] Ping thành công (0% packet loss) → VPC internal OK
- [ ] Ping fail → kiểm tra lại subnet + firewall rule `allow-nats-internal`
- [ ] Nếu không biết IP → scan subnet: `sudo apt update && sudo apt install -y nmap && sudo nmap -sn 10.20.0.0/24`

### Bước 8.3 — SSH ShopERP-A

- [ ] Mở SSH window cho `vanan-shop-a` (nút SSH cạnh instance)
- [ ] Chạy `ping -c 3 10.10.0.2` (THAY `10.10.0.2` BẰNG internal IP thật của `vanan-gateway` — xem cột Internal IP trong VM instances)
- [ ] Ping OK → 2 VPS nói chuyện được qua VPC
===>ping -c 3 10.148.0.2

**✅ Xong Giai đoạn 8. Hai VPS đã sẵn sàng.**

---

## GIAI ĐOẠN 9 — Báo tôi để deploy

Gửi tôi thông tin sau:

```
✅ Project: vanan-prod
✅ Region: asia-southeast1
✅ VPC: vanan-vpc (2 subnet)
✅ VM vanan-gateway: 136.85.94.119, 10.148.0.2, e2-small, Debian 12
✅ VM vanan-shop-a: 34.177.89.248, 10.148.0.3, e2-small, Debian 12
✅ Firewall: 4 rules (ssh-admin, http-https, nats-internal, nats-monitor-admin)
✅ Budget alert: $10/tháng
✅ SSH test: ping OK giữa 2 VPS
```

Tôi sẽ viết script tiếp theo:
1. Cài .NET 8 runtime + NATS + PostgreSQL trên Gateway.
2. Cài .NET 8 runtime + NATS subscriber trên ShopERP-A.
3. Deploy Gateway + ShopERP từ repo VanAn.
4. Cấu hình NATS routing theo Option C (`vanan.cloud.order.created.{shopInstanceId}`).
5. Tạo ShopInstance record trong Gateway PG cho `vanan-shop-a`.

---

## Troubleshooting thường gặp

| Vấn đề | Nguyên nhân | Khắc phục |
|---|---|---|
| "Free trial not available for this account" | Gmail đã đăng ký GCP trước | Dùng Gmail khác |
| Thẻ bị reject | Chưa bật thanh toán quốc tế | Liên hệ ngân hàng bật "International payment" trong app |
| OTP không đến | App ngân hàng tắt thông báo quốc tế | Bật lại hoặc gọi hotline ngân hàng |
| VM tạo mãi không xong | Region quá tải | Đổi zone (a → b → c) |
| SSH browser không mở | Popup blocker | Tắt popup blocker cho console.cloud.google.com |
| Ping VPC fail | Firewall chặn ICMP | Tạo rule allow ICMP tạm (debug xong xóa) hoặc test bằng `nc -zv <IP> 4222` |
| "Quota exceeded" cho e2-small | Tài khoản mới có quota thấp | Request increase quota hoặc dùng e2-micro trước |
| Bandwidth alert liên tục | VPS serve product images | Tách sang Cloud Storage/CDN (sổ tay §5) |

---

## Cảnh báo cuối cùng

| ❌ Đừng quên | Hậu quả |
|---|---|
| Hủy billing trước ngày 90 | Tự động tính phí thật sau Free Trial |
| Để VPS chạy khi không dùng | VPS tắt vẫn tính phí disk → phải **Delete instance** |
| Mở port 22 cho 0.0.0.0/0 | Bot brute-force SSH 24/7 |
| Dùng Windows Server | Tính phí bản quyền ~$30/tháng |
| Quên budget alert | Có thể tự tính tiền hàng trăm $ |

> Sau 90 ngày Free Trial, nếu KHÔNG muốn trả phí:
> 1. Delete tất cả VM instances (KHÔNG chỉ Stop — phải Delete).
> 2. Vào Billing → Close billing account.
> 3. Hoặc: downgrade VM xuống e2-micro ở US region (Always Free tier).

---

> **Bảo trì:** Cập nhật khi Google đổi UI Console hoặc Free Trial policy. Phiên bản: 1.0 (2026-08-06).
