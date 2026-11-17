# Task ATRS-16: Guest Device Identity (buyer không email/SĐT) + Recovery Rate Cap

> **Nguồn:** SRS traceability review 2026-11-17 (GAP #3a/#1) · **Mode:** IMPLEMENT (user duyệt "let go")
> **SRS coverage:** FR-001 (DeviceCluster định danh), FR-005 (installation/device id), FR-011 (giới hạn request), FR-002 lazy-create
> **Phụ thuộc:** ATRS-2 (TrustSubject/TrustSubjectLink), ATRS-8 (RecoveryRequest), W5d (notification)

## 1. OBJECTIVE

Cho phép định danh + xác thực buyer **không email, không SĐT** ở tầng thiết bị (L0/L1):
guest `X-Customer-Device-Id` → TrustSubject (DeviceCluster link) lazy-create → có thể
submit recovery (code-based / manual review) + nhận in-app notification. Kèm giới hạn
tần suất recovery/ngày (đóng một phần FR-011 "giới hạn số request trong khoảng thời gian").

## 2. THIẾT KẾ

### 2.1 Guest device → TrustSubject
- `TrustProfileService.GetOrCreateForDeviceAsync(string deviceId, Guid? tenantId, ct)`:
  - `identityId = StableGuid(deviceId)` = SHA-256(deviceId) → 16 bytes → Guid (deterministic, KHÔNG migration).
  - resolve `(DeviceCluster, identityId)` → có → trả subject; chưa → lazy-create subject + link + profile (pattern `GetOrCreateForCustomerAsync`, race-safe DbUpdateException re-read).
- `AtrsCustomerContextService.AuthenticateAsync`: token path giữ nguyên; khi KHÔNG có token hợp lệ nhưng có `X-Customer-Device-Id` (format: 8–128 chars, `[A-Za-z0-9._-]`) → guest path → `AtrsCustomerTokenStatus.GuestDevice`.
- `AtrsCustomerAuthResult` += `IsGuest` (định tuyến/gating + metrics sau).
- **KHÔNG đổi** các endpoint: controller hiện tại chỉ cần `TrustSubjectId != null` — guest có subject nên dùng được recovery/notifications/dispute (ledger trả rỗng vì không có Customer link — an toàn).

### 2.2 Recovery daily cap (anti-abuse, FR-011)
- `RecoveryCaseManager.SubmitAsync`: đếm recovery requests của subject kể từ đầu ngày (UTC); ≥ `Atrs:Recovery:MaxPerDay` (mặc định 3) → `InvalidOperationException("rate-limited")` → controller trả **429**.
- Áp cho MỌI subject (không chỉ guest) — chống spam đồng nhất; customer có contact path vẫn bị cap nhưng ngưỡng hợp lý.

### 2.3 Giới hạn trung thực (ghi rõ — KHÔNG hứa quá)
- Device id = tín hiệu, KHÔNG nhân thân pháp lý (FR-004/005). Guest mất device + mất code → manual review (tiered ③).
- FR-001 account lock/delete sync: **Phase B** (cần event bridge Gateway→atrs) — không làm slice này (ghi residual).

## 3. FILES
- `VanAn.Atrs.Infra/TrustProfileService.cs` (+interface) — GetOrCreateForDeviceAsync
- `VanAn.Atrs.Api/Auth/AtrsCustomerContextService.cs` + `AtrsCustomerTokenHelper.cs` (enum GuestDevice + IsGuest)
- `VanAn.Atrs.Infra/RecoveryCaseManager.cs` — daily cap (+ IOptions<RecoveryLimits>)
- `VanAn.Atrs.Api/Program.cs` — bind `Atrs:Recovery`; controllers Submit catch → 429
- Tests: TrustProfileService device-idempotency · RecoveryCaseManager cap

## 4. TESTS
- [ ] Cùng deviceId → cùng subject; khác deviceId → khác subject (deterministic hash)
- [ ] Recovery submit vượt MaxPerDay → 429 (rate-limited)
- [ ] Guest path không ảnh hưởng token path (regression)

## 5. RV (sweep prod)
- [ ] Guest (chỉ X-Customer-Device-Id) submit recovery → 200 + notification in-app
- [ ] Submit thứ 4 trong ngày → 429
- [ ] Anti-enum: guest không thấy subject khác

## 6. STATUS
- **2026-11-17: CODE DONE + DEPLOYED + RV PASS** (chi tiết project_state).
- **Phase B (residual):** FR-001 account lock/delete → ATRS sync (event bridge).