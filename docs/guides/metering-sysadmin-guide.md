# Hướng dẫn sử dụng Module Phí nền tảng (Metering) — cho SystemAdmin

> **Phạm vi:** quản trị rate-card, master switch, top-up, ví credit — `vanan-metering` (Issue #197 nâng cấp 2026-10-29: rate-card theo nhóm tenant + master switch + deny hiệu lực).
> **Tài liệu triển khai server:** xem `docs/guides/metering-deploy-guide.md`.

---

## 1. Tổng quan mô hình

Metering ("Phí nền tảng") tính phí theo **credit**:

- Mỗi tenant có một **ví credit** (`/wallet/phi-nen-tang` — chủ shop tự xem + nạp).
- Mỗi tính năng có một **rate-card**: `định mức credit × số lần dùng` → trừ vào ví khi có usage.
- Rate-card có **Enforcement**:
  - **MeterOnly** — chỉ đo, KHÔNG chặn (mặc định).
  - **Deny** — khi ví không đủ → **chặn tính năng** (HTTP 402 + màn hình "Hết hạn mức phí nền tảng" + link nạp).
- **Master switch** bật/tắt toàn hệ thống: TẮT → không chặn + không trừ credit (usage vẫn ghi).

**Trang SystemAdmin:**
| Trang | URL | Chức năng |
|---|---|---|
| Rate Card + Master switch | `/admin/metering/rate-cards` | Quản lý định mức, phạm vi, bật/tắt hệ thống |
| Top-up | `/admin/metering/topups` | Duyệt nạp tiền, cấu hình VietQR |

---

## 2. Rate Card (`/admin/metering/rate-cards`)

### 2.1 Các trường

| Trường | Ý nghĩa |
|---|---|
| **ServiceCode** | Mã tính năng (vd `feature.financial`). Mỗi ServiceCode + Phạm vi có **1 chain** phiên bản. |
| **Đơn vị đo** | `visit` · `message` · `byte` · `booking` · `module-month` · `onboarding` … |
| **Định mức credit** | Số credit cho 1 đơn vị (vd 30.000/booking, 1/module-month). |
| **Thực thi** | `MeterOnly` (chỉ đo) hoặc `Deny` (chặn khi hết ví). |
| **Phạm vi** | `Global` · `Theo loại hình tenant` · `Theo trạng thái verify` · `Override 1 tenant` (xem §2.2). |
| **Hiệu lực từ** | Trống = áp dụng ngay. PATCH tạo **phiên bản mới** (append-only, version cũ tự đóng). |
| **Nhãn / Ghi chú audit** | Mô tả + lý do thay đổi (ghi kèm email người sửa). |

**Quy tắc append-only:** sửa = tạo phiên bản mới, KHÔNG bao giờ ghi đè lịch sử. "Ngưng" = đóng chain (không xoá).

### 2.2 Phạm vi (Issue #197 — rate-card theo nhóm tenant)

| Phạm vi | Giá trị | Ghi chú |
|---|---|---|
| **Global** | — | Áp dụng mọi tenant (mặc định). |
| **Theo loại hình tenant** | `HKD` · `Enterprise_SuperSmall` · `Enterprise_SME` · `Enterprise_Large` · `HTX` | Tách theo size (quyết định user 2026-10-29). |
| **Theo trạng thái verify** | `verified` (Active) · `unverified` (Pending) | |
| **Override 1 tenant** | chọn tenant từ danh sách | **Ưu tiên cao nhất** — thắng mọi nhóm. |

**Thứ tự ưu tiên resolve:** `Tenant override → Loại hình → Trạng thái verify → Global` — tìm thấy ở tầng nào dùng tầng đó; không có → unpriced (không trừ, không chặn).

**Ví dụ:** muốn HKD giá 10.000/tháng nhưng tenant "Cafe Tân Quy" được ưu đãi 0:
1. Tạo card `feature.xxx` + Phạm vi **Loại hình = HKD**, định mức 10.000, Deny.
2. Tạo card `feature.xxx` + Phạm vi **Tenant cụ thể = Cafe Tân Quy**, định mức 0, MeterOnly.

### 2.3 Master switch (header trang Rate Card)

- **BẬT (mặc định):** deny + trừ credit hoạt động bình thường.
- **TẮT:** toàn hệ thống **không chặn** + **không trừ credit** — usage **vẫn được ghi** (không mất dữ liệu; bật lại là tính phí tiếp).
- Mọi lần đổi đều ghi **AuditLog** (ai, lúc nào).

> ⚠️ Tắt switch = tắt thu phí toàn bộ. Dùng khi: triển khai thử, sự cố, hoặc chưa muốn áp dụng phí. Đừng quên bật lại.

---

## 3. Top-up (`/admin/metering/topups`)

### 3.1 Duyệt yêu cầu nạp

1. Chủ shop bấm **Nạp phí nền tảng** trên `/wallet/phi-nen-tang` → nhận mã `TOPUP-XXXXXXXX` + QR VietQR → chuyển khoản kèm mã.
2. SystemAdmin vào `/admin/metering/topups` → thấy yêu cầu **Chờ xác nhận** (cột Tenant giờ hiện **tên tenant**).
3. **Xác nhận** → nhập số **credit** đã nhận (admin tự quyết tỷ lệ VND→credit) + ghi chú số GD ngân hàng.
4. Hoặc **Từ chối** (bắt buộc lý do).

- Yêu cầu quá **72h** (cấu hình `Metering:TopupTtlHours`) chưa xác nhận → tự **hết hạn**.
- Xác nhận 2 lần cùng 1 yêu cầu → **không cộng 2** (idempotent).

### 3.2 Cấu hình VietQR (bảng phải)

BankCode (vd `970436`), BankName, AccountNumber, AccountName — hiển thị trên QR của trang nạp tenant. Lưu ý: cấu hình này là **chung toàn hệ thống**.

### 3.3 Cấp credit thủ công (grant)

Dùng API (admin): `POST /v1/wallets/{tenantId}/grants` `{credits, reason}` — cấp hạn mức ban đầu / điều chỉnh (ghi ledger + audit).

---

## 4. Deny — điều gì user tenant thấy

- Khi ví không đủ cho tính năng có **Enforcement=Deny**: trang bị thay bởi alert **"Hết hạn mức phí nền tảng"** (kèm số credit cần / còn lại + nút **Nạp credit để tiếp tục**).
- Server-side guard (booking, ghi chú giọng nói) trả **HTTP 402** + link nạp.
- Banner ví (`MeteringWalletBanner`, poll 60s) cảnh báo sớm: còn ≤50% (warn50), ≤10% (warn90), 0 (depleted).

**Các tính năng đang có gate/guard:**
| ServiceCode | Tính năng | Loại chặn |
|---|---|---|
| `feature.booking` | Đặt lịch (tạo booking) | server guard + UI |
| `feature.accounting` | Module kế toán (mọi trang `/accounting/*`) | UI layout |
| `feature.financial` | Phân tích tài chính | UI |
| `feature.inventory` | Kho hàng | UI |
| `feature.loyalty` | Thống kê điểm thưởng | UI |
| `feature.voice-note` | Ghi chú giọng nói | server guard |
| `chat.message` / `chat.bytes` | Chat | (server-side — KhachLink chưa có UI deny GĐ-2) |
| `store.visit` | Lượt truy cập storefront | **không gate** (anonymous — đúng design) |
| `feature.ocr` | OCR onboarding | **chỉ ghi usage** (tenant mới chưa có ví → đặt MeterOnly) |

---

## 5. Registry Service Code (kèm hook nguồn)

| Code | Đơn vị | Nguồn usage | Ghi chú |
|---|---|---|---|
| `store.visit` | visit | KhachLink truy cập (Gateway public beacon) | bot UA bị lọc |
| `chat.message` | message | gửi tin chat | kèm `chat.bytes` (dung lượng) |
| `chat.bytes` | byte | gửi tin chat | |
| `feature.booking` | booking | tạo booking thành công | |
| `feature.financial` | module-month | mở trang phân tích tài chính | 1 event/tháng/tenant (dedupe idempotency) |
| `feature.inventory` | module-month | mở trang kho hàng | |
| `feature.accounting` | module-month | mở module kế toán | |
| `feature.loyalty` | module-month | mở thống kê điểm thưởng | |
| `feature.ocr` | onboarding | tạo tenant qua OCR | đặt MeterOnly |
| `feature.voice-note` | note | lưu ghi chú giọng nói trên đơn | |
| `directory.search` / `directory.redirect` | search/redirect | Directory (tra cứu tenant) | |

---

## 6. Xử lý sự cố thường gặp

| Hiện tượng | Nguyên nhân thường gặp | Xử lý |
|---|---|---|
| Deny không chặn dù ví hết | (1) chưa có rate-card cho code đó → unpriced · (2) Enforcement đang MeterOnly · (3) master switch TẮT | Kiểm tra `/admin/metering/rate-cards`: card tồn tại? `Deny`? Switch **Đang bật**? |
| Tenant nạp nhưng không thấy credit | Admin chưa **Xác nhận** top-up (chưa nhập credits) | Vào `/admin/metering/topups` → xác nhận |
| Không thấy QR khi tenant nạp | Chưa cấu hình VietQR (bank/account) | `/admin/metering/topups` → cấu hình VietQR |
| Tenant bị chặn nhầm khi đang khuyến mãi | Card Deny + ví 0 | Master switch TẮT tạm thời, hoặc grant credit, hoặc PATCH card → MeterOnly |
| Muốn 1 tenant riêng giá khác | — | Tạo card Phạm vi **Tenant cụ thể** (ưu tiên cao nhất) |
| Test nhanh một card | — | API `GET /v1/rate-cards/resolve?serviceCode=X&tenantId=...` kèm headers `X-Tenant-Type`/`X-Tenant-Status` |

**Ghi nhớ append-only:** không xoá card — dùng "Ngưng" hoặc PATCH tạo phiên bản mới. Mọi thay đổi đều có audit.