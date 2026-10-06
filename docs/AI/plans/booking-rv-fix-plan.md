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

### ✅ P1 — §9.3 cancel gap (DONE 2026-10-06 — user approve mở rộng state machine, `fc203395`)
- `STAFF_ASSIGNED → CANCELLED` thêm vào `Booking.Cancel` (CheckedIn/InService vẫn không hủy).
- Tests: domain + service (CheckedIn vẫn reject) + SRS §9.3 note + queue ShopERP bỏ nút Hủy InService.

### ✅ P2 — DTO `BookingQueueItemDto` thêm `OrderId` (DONE `fc203395`)
- Gateway DTO + ShopERP client + queue hiển thị "🧾 Đơn: ..." cho Completed (`booking-order-link`).

### ✅ P3 — Seed AddOn demo (DONE — PG seed + sweep D11 create-with-add-on)
- Nước uống 20k / Phụ thu phòng VIP 50k (tenant A).

### ✅ P4 — Customer spec timing (DONE `fc203395`)
- WASM cold boot timeout 15s→30s + networkidle sau click.

### ✅ P5 — Offering names có dấu (DONE — PG update)
- "Massage 60 phút", "Massage 90 phút", "Cắt tóc nam".

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
