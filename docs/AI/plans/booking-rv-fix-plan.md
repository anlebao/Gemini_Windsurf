# FIX PLAN — Booking RV Sweep (2026-10-06, production)

> Nguồn: RV sweep `6_Testing/rv-scripts/booking-rv-sweep.mjs` (60 checks — **60/60 PASS sau các fix**)
> + E2E specs (customer/commission/concurrency/isolation) + UI probe + manual probes.
> Scope: Booking & Staff Scheduling MVP (SRS v1.1) — toàn bộ lỗi phát hiện qua RV production.

---

## 1. ĐÃ FIX (6 lỗi production — deploy qua CD, verified)

| # | Bug (RV phát hiện) | Root cause | Fix (commit) | Verify |
|---|---|---|---|---|
| 1 | **create-with-staff / assign-staff LUÔN 400 trên PG** | `AcquireStaffLockAsync` truyền `CancellationToken` vào params `object[]` của `ExecuteSqlRawAsync` → "no store type mapping for CancellationToken". SQLite tests không bắt (isPostgres=false) | `new object[] {...}, ct` — pattern CommunityAdminController (`c337d034`) | sweep D10/E4 PASS · create+assign 200 |
| 2 | **public availability trả slot quá khứ** → create 400 | GetAvailability không lọc `StartAt > now` | Filter tại PublicBookingController (`c337d034`) | sweep C2 PASS (0 past slots) |
| 3 | **unique index chặn vĩnh viễn slot staff** sau Completed/Cancelled | `IX_Bookings_TenantId_StaffId_StartAt` không filter theo status → booking xong vẫn chiếm row | Migration filtered index `Status IN (1..5)` (`49677f5c`) | sweep E7 complete → complete lại cùng slot OK |
| 4 | **booking API 500 thay vì 404/400 friendly** | controllers chỉ import `VanAn.Shared.Domain` → `catch (NotFoundException/ValidationException)` bind vào DOMAIN exceptions → services exceptions không bắt → 500 | alias services-layer exceptions (`2a076367`) | sweep A4/D5/D6/D9 PASS (404/400 friendly) |
| 5 | **QR tenant A tạo được booking tenant B** (Risk 5) | `CreateBookingAsync` không validate AttributionId ∈ tenant | validation + test isolation (§18.3 #3) (`40a6f01c`) | sweep D7 PASS (400) |
| 6 | **commission không bao giờ tự EARNED** | `FinalizeCommissionForBookingAsync` không có production call site (chỉ test gọi) | wire optional ICommissionService vào BookingService COMPLETED hook (`40a6f01c`) | sweep E8 PASS (Earned + salesman + tax) |

## 2. VẤN ĐỀ CÒN LẠI — CẦN QUYẾT ĐỊNH / FIX TIẾP

### P1 — §9.3 cancel gap (CẦN USER APPROVE — đổi spec state machine)
- **Hiện trạng:** `Booking.Cancel` chỉ cho `PendingConfirmation | Confirmed` (§9.3 MVP đúng spec).
- **Vấn đề thực tế:** create-with-staff (KhachLink Screen 2 chọn staff) → booking **StaffAssigned ngay** → khách KHÔNG hủy được (Status page nút Hủy → 400) + tenant queue Hủy → 400. Đây là **đường đi phổ biến** (khách chọn staff).
- **Đề xuất A (khuyến nghị):** mở rộng `STAFF_ASSIGNED → CANCELLED` trong domain `Booking.Cancel` (+ test + cập nhật SRS §9.3). Impact: public cancel + tenant cancel + sweep cleanup tự sạch.
- **Đề xuất B:** giữ MVP — tenant xử lý qua NoShow (cần assign → no-show); thêm ghi chú UI "đã gán staff không hủy được".
- Effort: A = domain 1 dòng + tests ~3 + SRS note. B = 0 code (chỉ docs).

### P2 — DTO `BookingQueueItemDto` thiếu `OrderId`
- ShopERP queue/chi tiết không hiển thị link Order sau COMPLETED (D2 hook chạy OK — verify PG).
- Fix: thêm `Guid? OrderId` vào DTO + From() + ShopERP queue hiển thị mã đơn (nhỏ).
- Effort: ~30 phút (DTO + razor).

### P3 — Demo data thiếu AddOn
- KhachLink Screen 3 "add-on chips" rỗng (chưa seed AddOn cho tenant demo).
- Fix: seed 2 add-ons (nước uống 20k / phụ thu phòng 50k) qua PG + sweep thêm check create-with-add-on.
- Effort: ~20 phút.

### P4 — KhachLink customer spec timing flake
- `booking-customer.spec.ts` test 2 (full flow) skip do WASM boot > timeout ở vài run (probe UI chạy OK — không phải bug product).
- Fix: tăng timeout 15s→30s + waitForLoadState('networkidle') sau click offering; retry 1 lần nếu slot chưa render.
- Effort: ~15 phút.

### P5 — Offering demo names mất dấu tiếng Việt
- Seed SQL dùng ASCII ("Massage 60 phut", "Cat toc") để tránh encoding — hiển thị kém.
- Fix: update PG bằng chuỗi có dấu (UTF-8) qua psql file script.
- Effort: ~10 phút.

## 3. QUY TRÌNH XÁC NHẬN SAU FIX
1. `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests PASS
2. CD Multi-VPS + migration (nếu có)
3. `node rv-scripts/booking-rv-sweep.mjs` → 60/60 PASS
4. Playwright RV booking specs (customer/commission/concurrency/isolation) PASS
5. Update project_state + task card + đóng

## 4. NGHĨA VỤ TEST THÊM (ghi nợ — không block MVP)
- Commission reversal E2E (refund → reversal entry)
- Rate-limit 429 verify (policy "booking-public" 120/min — config OK, chưa stress test)
- Perf availability p95 production (test context p95 PASS — production đo khi có traffic)
