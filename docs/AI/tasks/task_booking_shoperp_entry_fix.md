# TASK CARD: Booking — Fix Issue #188 + Luồng đặt lịch từ ShopERP

> **Created:** 2026-10-08 (user directive + 4 quyết định chốt)
> **Master plan:** `docs/AI/plans/booking-shoperp-entry-fix-master-plan.md`
> **Nguồn:** Issue #188 (3 bug) + user yêu cầu luồng đặt lịch ShopERP (Owner/Staff) + SRS v1.1
> **Branch:** `main`
> **Status:** ⏳ PLAN DRAFT — CHỜ USER REVIEW

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** (1) Fix 3 bug issue #188 — không có UI kích hoạt đặt lịch (bug 1) · QR không sinh mã/không xem lại được (bug 2) · không có nơi nhập nguyên liệu (bug 3). (2) Feature mới — luồng đặt lịch từ ShopERP (Owner/Staff tạo thay khách, POS-style).
- **Đã verify (2026-10-08):** bug 1 (8 trang booking không có config page) · bug 2 (không render QR image; link đúng format nhưng 404 vì IsEnabled=false) · bug 3 (0 CRUD Ingredient trong repo; seed F&B chỉ chạy tenant F&B qua ApplyTemplateAsync) · tenant API không có `POST bookings` · `CreateBookingAsync` create-with-staff → STAFF_ASSIGNED ngay (service sẵn) · `QrCodeService` (QRCoder) có sẵn · `UpdateConfigAsync` client có sẵn.
- **4 quyết định user (2026-10-08):** Q1 QR xem lại sau (EncryptedToken — thư giãn §7.2, user approve) · Q2 Ingredient CRUD + nhập/xuất kho · Q3 booking staff tạo auto-CONFIRMED + gán staff · Q4 làm cả Bug 1-3 + Feature 3 session này.

## 2. PHASES

### Phase 1 — Bug 1: UI Cấu hình đặt lịch (`/booking/config`)
- [ ] `BookingConfig.razor` (`StoreManagement`, UI Platform 100%): toggle IsEnabled (warning rõ ràng) + deposit policy (NoDeposit/Fixed/Percentage + số tiền/%) + CancelReschedulePolicy + EinvoiceMode → `GetConfigAsync`/`UpdateConfigAsync` (client có sẵn) → toast
- [ ] `BookingQrChannels.razor`: banner warning khi `!config.IsEnabled` + link tới config
- [ ] NavMenu "Đặt lịch hẹn" += "Cấu hình" + Sitemap `link-booking-config`
- [ ] bUnit: render + toggle + save
- [ ] guard + build + tests PASS → commit

### Phase 2 — Bug 2: QR image + xem lại (Q1)
- [ ] Domain additive: `QRChannel.EncryptedToken` + ctor mới (giữ ctor cũ) — Single-Identity không đổi
- [ ] EF config map EncryptedToken (max 512) + migration PG `AddQrChannelEncryptedToken`
- [ ] Encryption: AES key Gateway env `BOOKING_QR_TOKEN_ENCRYPTION_KEY` (KHÔNG vào repo/DB/log) — encrypt lúc create, decrypt lúc render
- [ ] Gateway tenant API: `GET /api/tenant/booking/qr-channels/{id}` trả `BookingLink` + `QrCodePngBase64` (tái dùng `QrCodeService`) — chỉ tenant sở hữu + active
- [ ] `BookingQrChannels.razor`: mỗi dòng active → QR image + link + Tải/In + Sao chép (reload vẫn thấy — Q1); revoke → ẩn
- [ ] Tests: domain round-trip + service (create → render; revoke → không render) + bUnit QR render
- [ ] guard + build + tests PASS → commit

### Phase 3 — Bug 3: Nguyên liệu CRUD + nhập/xuất (Q2)
- [ ] `Ingredients.razor` (`/inventory/ingredients`, `StoreManagement`): grid + modal thêm/sửa (Name/Category/Unit/CurrentStock/MinStockThreshold/PricePerUnit) + xóa (chặn RecipeLine FK + chặn tồn >0 kèm confirm)
- [ ] Nhập kho/Xuất kho (modal số lượng + diễn giải) → `CurrentStock ±= qty` (chặn âm) + `AuditTrailService` "IngredientAdjust"
- [ ] `InventoryDashboard`: EmptyMessage sửa + nút link trang nguyên liệu
- [ ] NavMenu "Kiểm kê" += "Nguyên liệu" + Sitemap `link-inventory-ingredients`
- [ ] bUnit: CRUD + adjust + guard
- [ ] guard + build + tests PASS → commit

### Phase 4 — Feature 3: Đặt lịch nhanh từ ShopERP (Q3/Q4)
- [ ] Gateway `TenantBookingController`: `POST /api/tenant/booking/bookings` (Idempotency-Key bắt buộc; tenantId JWT; body offeringId/addOnIds/startAt/staffId?/customerName?/customerPhone?/customerNote?) → `CreateBookingAsync` (CustomerId=null, tên/SĐT vào note) → staffId!=null: STAFF_ASSIGNED (sẵn) · staffId==null: `ConfirmAsync` → 201 DTO
- [ ] `IBookingTenantApiClient.CreateBookingAsync` (POST + Idempotency-Key header)
- [ ] `BookingCreate.razor` (`/booking/create`, `StaffOrAbove`): POS 2-cột — trái catalog (category chips + offering cards + add-ons) · phải sticky (ngày/giờ slot từ `GetAvailabilityMatrixAsync` union ≥1 staff available · staff filter · khách tên+SĐT · ghi chú + Quick Tags · cọc server-authoritative · tổng · nút Tạo — Idempotency-Key giữ khi retry)
- [ ] Success → toast + code + nút queue + sao chép link khách
- [ ] NavMenu += "Đặt lịch nhanh" (StaffOrAbove) + Sitemap `link-booking-create`
- [ ] E2E spec `booking-shoperp-create.spec.ts` (Gate 4, self-gating — chạy RV)
- [ ] bUnit: render + submit + validation
- [ ] guard + build + tests PASS → commit

### Phase 5 — Deploy + RV + ĐÓNG
- [ ] FAST PUSH Đợt 1 (P1-P3) → CD Multi-VPS (migration PG auto-apply) → RV L1-L4 (markers + health + routes + QR flow thật + ingredient CRUD)
- [ ] FAST PUSH Đợt 2 (P4-P5) → CD Multi-VPS → RV L1-L4 (markers + E2E production + flow thật: bật config → tạo QR → link mở → staff tạo booking → queue → complete)
- [ ] `booking-rv-sweep.mjs` 60/60 + regression cong-no smoke
- [ ] Đóng issue #188 (comment + close) · task card Status → ✅ COMPLETE · master plan ✅ COMPLETE · project_state update

## 3. OPEN QUESTIONS (nếu phát sinh khi implement)
- Encryption: DataProtection sẵn trong Gateway hay cần AES key env? (ưu tiên AES key env — đơn giản, không phụ thuộc key ring)
- Ingredient adjust có cần lịch sử phiếu nhập/xuất không? (MVP: audit log qua AuditTrailService — không entity mới)

## 4. ACCEPTANCE
- User bật/tắt đặt lịch bằng UI (không API) · QR channel hiển thị QR image + link, xem lại được sau reload, quét mở KhachLink catalog thành công · Owner/Staff tạo booking từ ShopERP → queue → confirm/assign → hoàn tất · Tenant F&B hoặc bất kỳ đều thêm/sửa/nhập/xuất nguyên liệu được · guard + build + Core.Tests + ShopERP.Tests PASS · CD SUCCESS ×2 · RV L1-L4 PASS · issue #188 closed
