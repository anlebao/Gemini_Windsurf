# MASTER PLAN — Booking: Fix Issue #188 + Luồng đặt lịch từ ShopERP (2026-10-08)

> **Trạng thái:** DRAFT — CHỜ USER REVIEW (2026-10-08)
> **Nguồn:** Issue #188 (3 bug) + user directive (luồng đặt lịch mới từ ShopERP — Owner/Staff) + SRS v1.1
> **4 quyết định user chốt (2026-10-08):** Q1 QR — cho xem lại QR sau (lưu mã hóa, thư giãn §7.2) · Q2 Nguyên liệu — CRUD + nhập/xuất kho · Q3 Booking staff tạo — Auto-CONFIRMED + gán staff · Q4 Phạm vi — Bug 1-3 + Feature 3 (session này)
> **Branch:** `main`

---

## 1. MỤC TIÊU

| # | Việc | Root cause (đã verify) | Fix |
|---|---|---|---|
| Bug 1 | KHÔNG có UI kích hoạt đặt lịch (phải gọi API `PUT /api/tenant/booking/config`) | 8 trang booking không có trang config; `UpdateConfigAsync` client có sẵn nhưng không UI gọi. `IsEnabled=false` mặc định → public catalog 404 → khách tưởng "link sai" | Trang `/booking/config` (toggle + deposit policy + cancel + e-invoice) |
| Bug 2 | QR channels không sinh mã QR · link "bị sai" | (a) Không render QR image — chỉ text link, chỉ hiện trong session tạo (token 1 lần §7.2); (b) link format đúng (`{KhachLink:BaseUrl}/booking/{token}` = route KhachLink `/booking/{QrToken}`) nhưng không hoạt động vì chưa enable (Bug 1) → 404 "Cửa hàng không tồn tại..." | **Q1:** Domain additive `QRChannel.EncryptedToken` (AES server key) → render lại QR image + link bất kỳ lúc nào (tenant API + StoreManagement). QR image qua `QrCodeService` (QRCoder có sẵn). Migration PG |
| Bug 3 | Không có nơi nhập nguyên liệu mới; seed F&B không chạy cho tenant không F&B | Toàn repo KHÔNG có CRUD Ingredient (chỉ display/select); seed chỉ qua `OnboardingService.ApplyTemplateAsync` (F&B) | **Q2:** Trang `/inventory/ingredients` — CRUD + nhập/xuất kho (adjust CurrentStock) + ngưỡng. Không entity/migration mới (Ingredient đủ field) |
| Feature 3 | Luồng đặt lịch mới từ ShopERP (Owner/Staff) — tạo thay khách | Tenant API KHÔNG có `POST bookings` (create chỉ qua public API). POS có pattern 2-cột tốt (`/pos`, policy `StaffOrAbove`) | **Q3/Q4:** `POST /api/tenant/booking/bookings` (tenant-scoped, auto-confirm) + client `CreateBookingAsync` + trang `/booking/create` POS-style + E2E |

## 2. PHẠM VI / NGOÀI PHẠM VI

- **Trong:** Bug 1 (config UI) · Bug 2 (QR image + xem lại) · Bug 3 (Ingredient CRUD + nhập/xuất) · Feature 3 (create booking từ ShopERP + endpoint tenant + UI + auto-confirm).
- **Ngoài:** reschedule UI · push/SMS thông báo cho khách · commission salesman cho booking staff tạo (attribution null — staff tự đặt không hoa hồng, đúng §17.1 qualified-only) · ingredient nhập/xuất theo phiếu (entity mới) · get-or-create Customer theo SĐT (MVP: CustomerId=null + tên/SĐT vào CustomerNote).

## 3. THIẾT KẾ

### P1 — Bug 1: `/booking/config` (ShopERP)
- `BookingConfig.razor` (`@page "/booking/config"`, `StoreManagement`, UI Platform 100%):
  - Toggle **"Bật đặt lịch hẹn"** (IsEnabled) — warning rõ ràng: tắt → khách quét QR sẽ thấy "chưa mở đặt lịch".
  - Deposit policy: Không cọc / Cố định (số tiền) / Phần trăm (%), Cancel/Reschedule policy text, E-invoice mode (select enum).
  - Load `GetConfigAsync` → Lưu `UpdateConfigAsync` (đã có trong `IBookingTenantApiClient`) → toast + re-load.
- `BookingQrChannels.razor`: banner nếu `!config.IsEnabled` ("Tính năng đang tắt — khách không đặt được. Bật tại Cấu hình đặt lịch").
- NavMenu "Đặt lịch hẹn" += "Cấu hình" + Sitemap `link-booking-config` + bUnit (render + toggle + save).

### P2 — Bug 2: QR image + xem lại (Q1)
- **Domain additive (1_Shared/Domain.cs):** `QRChannel.EncryptedToken` (string) — set qua constructor mới `QRChannel(tenantId, qrTokenHash, encryptedToken, salesmanId, campaignId)` (giữ ctor cũ — không vỡ call site; property `protected set` + internal setter cho EF).
- **EF config:** map `EncryptedToken` (max length 512) — **KHÔNG Ignore** (phải lưu). Migration PG `AddQrChannelEncryptedToken`.
- **Encryption:** AES-256-GCM hoặc DataProtection (`IDataProtectionProvider` — Gateway đã có pattern? Nếu chưa, AES key từ env `BOOKING_QR_TOKEN_ENCRYPTION_KEY` trong Gateway config — key KHÔNG vào DB/repo). Encrypt tại `IQRAttributionService.CreateQrChannelAsync` (token→encrypt), decrypt tại render.
- **Gateway tenant API:** `GET /api/tenant/booking/qr-channels/{id}` trả thêm `BookingLink` (decrypt token → `{KhachLink:BaseUrl}/booking/{token}`) + `QrCodePngBase64` (qua `QrCodeService.GenerateQrPngBase64(link)` — kiểm tra signature, QRCoder có sẵn). Chỉ tenant sở hữu + channel active.
- **ShopERP `BookingQrChannels.razor`:** mỗi dòng active → `<img src="data:image/png;base64,...">` + link + nút **Tải/In** (vanAn.downloadFile / window.print) + nút **Sao chép**. Reload vẫn xem được (Q1). Revoke → ẩn QR.
- **Tests:** domain (ctor + encrypt/decrypt round-trip) + service (create → render lại được; revoke → không trả) + bUnit (QR render).

### P3 — Bug 3: `/inventory/ingredients` (Q2)
- `Ingredients.razor` (`@page "/inventory/ingredients"`, `StoreManagement` — dashboard policy; Staff? Giữ StoreManagement như dashboard/forecast hiện tại):
  - Grid: tên/loại/đơn vị/tồn kho/ngưỡng/giá + trạng thái badge (dưới ngưỡng).
  - Modal thêm/sửa: Name, Category (Nguyên liệu/Vật tư/Vật dụng), Unit, CurrentStock, MinStockThreshold, PricePerUnit.
  - Nút **Nhập kho / Xuất kho** (modal số lượng + diễn giải) → `CurrentStock ±= qty` (chặn âm) + audit `AuditTrailService` ("IngredientAdjust" + note).
  - Xóa: chặn khi có `RecipeLine` tham chiếu (FK — message hướng dẫn xóa recipe trước); chặn xóa khi còn tồn kho > 0 (cảnh báo xác nhận).
  - Dùng `IVanAnDbContext` trực tiếp (pattern `InventoryDashboard`) — ShopERP SQLite.
  - NavMenu "Kiểm kê" += "Nguyên liệu" + Sitemap `link-inventory-ingredients` + bUnit.
- `InventoryDashboard`: EmptyMessage sửa → "Chưa có nguyên liệu. Thêm nguyên liệu mới" + nút link.

### P4 — Feature 3: `/booking/create` (Q3/Q4)
- **Gateway `TenantBookingController` — endpoint mới:**
  ```
  POST /api/tenant/booking/bookings
  body: { offeringId, addOnIds: Guid[], startAt (ISO UTC), staffId?, customerName?, customerPhone?, customerNote? }
  Header: Idempotency-Key (bắt buộc — pattern public API §21.1)
  ```
  - tenantId từ JWT claim (mọi query filter — lesson a21f97f2).
  - Validate: offering thuộc tenant + active, startAt tương lai (service đã lo), staffId → `ValidateSlotAsync` (skill + conflict §12 — service đã lo).
  - `CreateBookingAsync(tenantId, cmd, idempotencyKey)` — **customer: CustomerId=null** + `CustomerNote = "[NV] {customerName} · {customerPhone} | {note}"` (khách chưa có tài khoản — MVP).
  - **Auto-CONFIRMED (Q3):** staffId != null → booking đã `STAFF_ASSIGNED` (create-with-staff, service có sẵn — dòng 158-166) · staffId == null → `ConfirmAsync` → `CONFIRMED`. Trả `BookingQueueItemDto` 201.
  - Catch services exceptions → 400/404 friendly (alias pattern RV P6 — controller đã có sẵn alias).
- **ShopERP `IBookingTenantApiClient`:** `CreateBookingAsync(...)` (POST + Idempotency-Key header).
- **UI `BookingCreate.razor` (`@page "/booking/create"`, `StaffOrAbove` — POS precedent):** layout 2 cột kiểu POS:
  - Trái: category chips + offering cards (tên/giá/thời lượng) + add-on chips → click thêm vào panel phải.
  - Phải (sticky): Offering + add-ons + **Ngày** (date picker 7-14 ngày) + **Giờ** (slot buttons từ `GetAvailabilityMatrixAsync(date, offeringId)` — union slot có ≥1 staff available) + **Staff** ("Bất kỳ ai phù hợp" / staff available theo slot — server-filtered) + **Khách hàng** (tên + SĐT) + **Ghi chú** (text + Quick Tags §15) + **Đặt cọc** (hiển thị theo config — server-authoritative, không cho sửa) + Tổng tiền + nút **Tạo lịch hẹn** (disabled khi thiếu offering/giờ; submit → Idempotency-Key giữ khi retry — pattern KhachLink §29).
  - Success → toast + booking code + nút "Xem trong danh sách chờ" (`/booking/queue`) + nút **Sao chép link khách** (status page).
- NavMenu "Đặt lịch hẹn" += "Đặt lịch nhanh" (StaffOrAbove hiển thị) + Sitemap `link-booking-create` + bUnit.
- **E2E spec `booking-shoperp-create.spec.ts`** (Gate 4 — self-gating, chạy RV production): render + chọn offering/giờ + tạo thành công → queue có booking + cleanup hủy.

### P5 — Hardening + Deploy + RV
- Full test matrix: guard-check + build VanAn.sln + Core.Tests + ShopERP.Tests + Architecture PASS → commit → **FAST PUSH** (pattern #11 `env -u GH_TOKEN -u GITHUB_TOKEN`) → CD Multi-VPS (migration PG `AddQrChannelEncryptedToken` auto-apply) → **RV L1-L4**:
  - L1 markers production (CoreHub.dll `EncryptedToken`/`BookingCreate`? → method/type markers + ShopERP.dll `BookingConfig`/`Ingredients`/`BookingCreate` + route strings).
  - L2 health 200 + routes live (`/booking/config`, `/inventory/ingredients`, `/booking/create`).
  - L3 E2E production (spec mới + regression booking sweep 60/60 + cong-no smoke).
  - L4 flow thật: bật config → tạo QR → QR image hiển thị + link mở được (KhachLink catalog 200) → tạo booking từ ShopERP (staff) → queue → confirm/assign → hoàn tất → đóng issue #188 (comment + close).

## 4. RỦI RO

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | EncryptedToken lộ key → token bị đọc | Key chỉ ở Gateway env, KHÔNG trong repo/DB/log; chỉ trả QR/link qua tenant API + StoreManagement JWT; revoke → không render |
| R2 | Ingredient xóa → FK RecipeLine vỡ | Chặn xóa khi có RecipeLine (message hướng dẫn) — verify bằng query trước delete |
| R3 | Booking staff tạo duplicate (retry) | Idempotency-Key bắt buộc (client giữ key khi retry) + server record (§21.1 có sẵn) |
| R4 | Availability matrix nặng (nhiều staff × slot) | Chỉ lấy theo offeringId đã chọn + giới hạn 14 ngày (pattern §14 hiện có) |
| R5 | Bug 1-3 + Feature 3 = phạm vi lớn 1 session | Session-per-phase (P1→P5), mỗi phase commit riêng + guard PASS; nếu quá dài → deploy P1-P3 trước (Đợt 1), P4-P5 sau (Đợt 2) |

## 5. TRÌNH TỰ

1. P1 (Bug 1 config UI) → guard + tests → commit
2. P2 (Bug 2 QR + domain additive + migration PG) → guard + tests → commit
3. P3 (Bug 3 ingredient CRUD) → guard + tests → commit
4. P4 (Feature 3 endpoint + client + UI + E2E spec) → guard + tests → commit
5. P5 Deploy Đợt 1 (P1-P3) → CD + RV L1-L4 → Deploy Đợt 2 (P4-P5) → CD + RV L1-L4 + sweep → đóng issue #188 + task card + project_state

## 6. VALIDATION
- `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests + Architecture.Tests PASS mọi phase.
- Playwright DISABLED trong IMPLEMENT (playwright.rules) — spec chạy RV production.
- Sau deploy: `booking-rv-sweep.mjs` 60/60 + E2E booking specs + issue #188 closed.
