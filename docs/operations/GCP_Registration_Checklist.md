# Checklist chuẩn bị đăng ký Google Cloud VPS cho VanAn

> **Mục đích:** Danh sách thông tin bạn CẦN CÓ SẴN trước khi mở browser đăng ký Google Cloud. **KHÔNG ghi giá trị thật vào file này** — chỉ là checklist để bạn biết cần chuẩn bị gì. Nhập trực tiếp trên Google Cloud Console.
>
> **Cập nhật:** 2026-08-06

---

## 1. Tài khoản Google (Gmail)

- [ ] **Email Gmail** (khuyến nghị dùng tài khoản cá nhân chính chủ, không dùng tài khoản shared/work tạm)
- [ ] **Password Gmail** (đảm bảo đã bật 2-Step Verification — Google yêu cầu cho Free Trial)
- [ ] **Số điện thoại recovery** (để nhận OTP xác minh đăng ký)
- [ ] **Email recovery** (khuyến nghị)

> ⚠️ KHÔNG lưu password vào file nào trong workspace. Dùng password manager (Bitwarden, 1Password, KeePass) hoặc nhớ.

---

## 2. Thẻ thanh toán quốc tế

Google yêu cầu thẻ để xác minh danh tính (hold $1 tạm thời, hoàn lại sau). Free Trial tặng $300/90 ngày, sau đó chuyển sang Always Free tier.

- [ ] **Loại thẻ:** VISA / Mastercard / JCB / Amex (Debit hoặc Credit đều được)
- [ ] **Thẻ đã kích hoạt thanh toán online/quốc tế** (liên hệ ngân hàng hoặc check app ngân hàng)
- [ ] **Số dư tối thiểu $2** (~50.000 VNĐ) cho verify hold
- [ ] **Thông tin thẻ cần có sẵn khi nhập:**
  - [ ] Số thẻ (16 chữ số)
  - [ ] Tên chủ thẻ (đúng như in trên thẻ)
  - [ ] Ngày hết hạn (MM/YY)
  - [ ] CVV/CVC (3 chữ số mặt sau)
  - [ ] Địa chỉ billing (trùng với địa chỉ đăng ký ngân hàng)

> ⚠️ KHÔNG lưu số thẻ + CVV vào file. Nhập trực tiếp trên console.cloud.google.com (HTTPS, encrypted).

> 💡 **Thẻ VN hoạt động tốt:** Vietcombank Visa/Mastercard Debit, Techcombank Visa, MB Bank Mastercard, TPBank Visa. Cần đã bật "Thanh toán quốc tế" trong app ngân hàng.

---

## 3. Thông tin cá nhân / doanh nghiệp

Google hỏi khi đăng ký Free Trial:

- [ ] **Quốc gia:** Vietnam
- [ ] **Loại tài khoản:** Individual (Cá nhân) — KHÔNG chọn Business (tránh phải cung cấp giấy phép kinh doanh)
- [ ] **Tên đầy đủ** (khớp với CCCD/CMND)
- [ ] **Địa chỉ** (khớp với địa chỉ thẻ ngân hàng — Google cross-check)
- [ ] **Số điện thoại** (nhận OTP SMS xác minh)
- [ ] **Mã bưu chính** (VN: nhập 700000 cho TP.HCM, 100000 cho Hà Nội, hoặc để trống nếu không bắt buộc)

---

## 4. Quyết định cấu hình VPS (chuẩn bị TRƯỚC khi tạo VM)

Dựa trên `docs/operations/ShopInstance_Capacity_Handbook.md` §3.1, chọn spec phù hợp vai trò VPS:

### 4.1. VPS Gateway (PG source of truth — CHỈ 1 VPS)

- [ ] **Region:** `asia-southeast1` (Singapore) — ping thấp về VN
- [ ] **Zone:** `asia-southeast1-a` (hoặc bất kỳ zone nào trong region)
- [ ] **Machine type:** `e2-small` (2GB RAM, 0.5 vCPU) — tối thiểu cho Gateway + PostgreSQL
- [ ] **Boot disk:** Debian 12 (Bookworm) — nhẹ, miễn phí bản quyền
- [ ] **Boot disk size:** 30GB (free tier)
- [ ] **Boot disk type:** Standard persistent disk (free) — upgrade SSD sau nếu chậm
- [ ] **Allow HTTP/HTTPS traffic:** ✅ (Gateway public API)
- [ ] **Service account:** Compute Engine default (đủ cho MVP)
- [ ] **Firewall tags:** `gateway` (dùng cho VPC rule sau)

### 4.2. VPS ShopERP-A (per-tenant SQLite + NATS subscriber)

- [ ] **Region:** `asia-southeast1` (Singapore) — **BẮT BUỘC cùng region với Gateway** (NATS egress free)
- [ ] **Zone:** `asia-southeast1-b` (khác zone với Gateway để chống sập cùng lúc)
- [ ] **Machine type:** `e2-small` (2GB RAM) — cho 25–40 tenant loại vừa (theo §3.1)
- [ ] **Boot disk:** Debian 12
- [ ] **Boot disk size:** 30GB
- [ ] **Allow HTTP/HTTPS traffic:** ✅ (KhachLink + ShopERP admin UI)
- [ ] **Firewall tags:** `shop-erp`

### 4.3. (Tùy chọn) VPS ShopERP-B — khi ShopERP-A đầy

- [ ] Cùng cấu hình ShopERP-A, zone `asia-southeast1-c`
- [ ] Chỉ tạo khi ShopERP-A đạt ~80% capacity theo §3.1

---

## 5. Cấu hình mạng (chuẩn bị TRƯỚC)

### 5.1. VPC + Subnet

- [ ] **VPC name:** `vanan-vpc`
- [ ] **Subnet Gateway:** `vanan-subnet-gateway`, range `10.10.0.0/16`, region `asia-southeast1`
- [ ] **Subnet ShopERP-A:** `vanan-subnet-shop-a`, range `10.20.0.0/16`, region `asia-southeast1`
- [ ] **Subnet ShopERP-B (sau):** `vanan-subnet-shop-b`, range `10.30.0.0/16`

> 💡 Cùng region → egress nội bộ MIỄN PHÍ. NATS Gateway↔ShopERP không tốn tiền.

### 5.2. Static IP (để domain trỏ vào)

- [ ] **Gateway external IP:** `vanan-gateway-ip` (Ephemeral OK cho MVP, reserve static khi có domain)
- [ ] **ShopERP-A external IP:** `vanan-shop-a-ip`

### 5.3. Firewall rules (chuẩn bị danh sách — tạo sau khi có VPC)

| Rule name | Direction | Source | Target tag | Ports | Mục đích |
|---|---|---|---|---|---|
| `allow-ssh-admin` | Ingress | `<IP nhà bạn>/32` | `gateway`,`shop-erp` | tcp:22 | SSH quản trị |
| `allow-http-https` | Ingress | `0.0.0.0/0` | `gateway`,`shop-erp` | tcp:80,443 | Web public |
| `allow-nats-internal` | Ingress | `gateway` tag | `shop-erp` | tcp:4222 | NATS Gateway→ShopERP |
| `allow-postgres-internal` | Ingress | `gateway` tag | `gateway` | tcp:5432 | (nếu PG tách VPS sau) |
| `allow-nats-monitor` | Ingress | `<IP nhà bạn>/32` | `shop-erp` | tcp:8222 | NATS monitoring (admin only) |

> ⚠️ KHÔNG mở `0.0.0.0/0` cho port 22 (SSH) hoặc 5432 (PostgreSQL) — đây là lỗi bảo mật phổ biến nhất.

---

## 6. Domain (tùy chọn — cho production)

- [ ] **Domain đã mua** (ví dụ `vanan.cloud` trên Namecheap/Cloudflare/Pavietnam)
- [ ] **DNS records cần trỏ:**
  - `gateway.vanan.cloud` → Gateway external IP
  - `shop-a.vanan.cloud` → ShopERP-A external IP
  - `khachlink.vanan.cloud` → Gateway IP (KhachLink serve từ Gateway hoặc ShopERP)
- [ ] **Cloudflare proxy:** Bật (giấu IP thật + CDN + DDoS protection free)

---

## 7. Budget alert (BẮT BUỘC — tránh tự tính tiền)

- [ ] **Budget name:** `vanan-monthly-budget`
- [ ] **Amount:** $10/tháng (hoặc $5 nếu muốn chặt)
- [ ] **Alert thresholds:** 50%, 90%, 100%
- [ ] **Email nhận alert:** Gmail đã đăng ký

> ⚠️ Free Trial $300 hết sau 90 ngày → tự động chuyển sang Always Free tier (e2-micro 1 VPS ở US). Nếu cấu hình e2-small ở Singapore → **sẽ tính phí**. Phải chủ động downgrade hoặc hủy billing trước ngày hết hạn.

---

## 8. Sau khi đăng ký xong — việc tiếp theo

Khi tài khoản GCP đã active (verify thẻ thành công, vào được Console), quay lại gặp tôi với thông tin:

```
✅ Tài khoản GCP đã tạo
✅ Region chọn: asia-southeast1
✅ Đã tạo project: vanan-prod (hoặc tên bạn đặt)
```

Tôi sẽ:
1. Viết script `gcloud` CLI tạo VPC + VM + firewall (bạn chạy trên Cloud Shell hoặc local gcloud).
2. Hướng dẫn SSH vào VPS + cài NATS + PostgreSQL + .NET 8 runtime.
3. Deploy Gateway + ShopERP từ repo VanAn.
4. Cấu hình NATS routing theo Option C.

---

## 9. Cảnh báo an toàn (ĐỌC KỸ)

| ❌ KHÔNG làm | ✅ NÊN làm |
|---|---|
| Lưu password Gmail / số thẻ / CVV vào file trong workspace | Dùng password manager riêng (Bitwarden, KeePass) |
| Commit file `.env` chứa credentials vào git | Thêm `.env` vào `.gitignore` (đã có sẵn trong repo) |
| Chia sẻ screenshot console có hiện IP/thông tin billing | Che IP + thông tin nhạy cảm trước khi share |
| Mở port 22/5432 cho `0.0.0.0/0` | Chỉ mở cho IP cá nhân (`<IP-của-bạn>/32`) |
| Bật Windows Server (tính phí bản quyền) | Dùng Debian/Ubuntu (miễn phí) |
| Quên hủy billing sau 90 ngày | Đặt calendar reminder + budget alert §7 |
| Dùng 1 VPS cho cả Gateway + ShopERP ở region khác | Cùng region `asia-southeast1` để NATS free egress |

---

> **Bảo trì checklist:** Cập nhật khi (a) Google thay đổi Free Trial policy, (b) thêm VPS role mới, (c) thay đổi region. Phiên bản: 1.0 (2026-08-06).
