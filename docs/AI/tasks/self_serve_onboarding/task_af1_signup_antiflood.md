# Task AF1: Anti-Flood Hardening Cho Merchant Signup (Self-Serve Onboarding)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — bổ sung FR-23 (đề xuất)
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §8 (risk: spam signup)
> **Mode:** ANALYZE → IMPLEMENT (chờ user duyệt card này)
> **Prerequisite:** P0-P3 DONE (`3c4d7384`) — endpoint `/api/v1/merchant-signups` đã live production
> **Layer:** 3_CoreHub (`MerchantSignupService`) + 2_Gateway (controller/config) + 6_Tests
> **Nguồn:** review anti-abuse 2026-11-10 — rate-limit hiện tại per-IP only, botnet bypass được; cùng SĐT submit nhiều lần không bị chặn; payload không cap.

## 1. OBJECTIVE

Chặn bot flood ở tầng application (bổ sung sau Turnstile + rate-limit per-IP đã có):

1. **Per-phone cooldown** — cùng `ContactPhone` chỉ tạo tối đa 1 Pending self-serve / 24h, bất kể IP.
2. **Global daily cap** — tổng self-serve signup / ngày ≤ `MerchantSignup:DailySignupCap` (default 200) → chặn botnet nhiều IP.
3. **Payload caps** — giới hạn `PhotoUrls`/`ProductLines`/độ dài field → chống DB/bandwidth bloat.
4. **Phone whitelist** — chỉ nhận SĐT VN hợp lệ (10 số, prefix di động/noi địa), chặn dải ảo.
5. **(Optional) Cleanup job** — Pending self-serve >30 ngày → auto-reject (ghi nợ đã có trong master plan §8).

## 2. FILES

| File | Change |
|---|---|
| `3_CoreHub/Services/Onboarding/MerchantSignupService.cs` | `ValidateRequest` += caps + phone whitelist · `SubmitAsync` += phone-cooldown + daily-cap check (trước `BuildListing`) |
| `2_Gateway/Controllers/MerchantSignupController.cs` | Map `signup-daily-cap` → 503 generic (pattern `self-serve-closed`) |
| `2_Gateway/Program.cs` | Config keys `MerchantSignup:DailySignupCap`, `MerchantSignup:PhoneCooldownHours`, `MerchantSignup:BlockedPhonePrefixes` (đọc qua `IConfiguration` trong service — không cần sửa Program nếu đã có config section; verify) |
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/MerchantSignupServiceTests.cs` | +test cases (mục 4) |
| `docker-compose.*.yml` | Env mẫu `MerchantSignup__DailySignupCap=200` (optional — default trong code) |
| `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` | Thêm FR-23 anti-flood (1 dòng) — nếu user duyệt |

**KHÔNG đổi:** Domain.cs (Gate 5 — mọi check làm trên `CrawlSource`/`RawJson` hiện có, không cột mới), UI KhachLink (response generic giữ nguyên), Turnstile/honeypot/rate-limit hiện có.

## 3. CHANGES CHI TIẾT

### 3.1 Per-phone cooldown 24h (silent dedup — anti-enumeration)

- Sau `ValidateRequest`, trước `BuildListing`: normalize phone → **một query duy nhất** lấy tenant match gần nhất (dùng cho silent dedup `SignupId`):
  ```csharp
  var phone = NormalizePhone(request.ContactPhone);
  var cooldownHours = configuration.GetValue("MerchantSignup:PhoneCooldownHours", 24);
  var since = DateTime.UtcNow.AddHours(-cooldownHours);
  var existingTenantId = await dbContext.CrawlSources
      .IgnoreQueryFilters().AsNoTracking()
      .Where(c => c.SourceSite.StartsWith("OCR-SELF")
          && c.CrawledAt >= since
          && EF.Functions.Like(c.RawJson, $"%\"ContactPhone\":\"{phone}\"%"))
      .OrderByDescending(c => c.CrawledAt)
      .Select(c => (Guid?)c.TenantId.Value)
      .FirstOrDefaultAsync(ct);
  ```
  - `SourceSite` là **cột thật** trên `CrawlSource` (giá trị `OCR-SELF-GPKD|SIGN|RONG`) — filter được, không cần parse JSON.
  - `RawJson` serialize bằng `JsonSerializer.Serialize(listing)` default → **PascalCase** → pattern `"ContactPhone":"0901..."` đúng casing. Phone đã normalize chỉ còn digits → LIKE-safe (không `%`/`_`).
  - `EF.Functions.Like` trên cột text — PG `LIKE` + SQLite `LIKE` đều OK; volume nhỏ (chỉ row `OCR-SELF-*` gần đây) nên không cần index.
  - `.IgnoreQueryFilters()` BẮT BUỘC — `CrawlSource` là tenant-scoped entity, query global phải bypass filter (pattern `LoadTenantAsync`/`HasNameDuplicateAsync` đã dùng).
- **Silent dedup:** nếu `existingTenantId != null` → KHÔNG tạo tenant, trả `MerchantSignupResult` **generic message giống hệt success** + `SignupId = existingTenantId` (lookup status vẫn cần phone → không lộ thêm gì; UX tốt cho user double-submit). Log `LogInformation` kèm phone đã mask (vd `0901****67`) — không log phone đầy đủ.

### 3.2 Global daily cap

- Trước phone-cooldown: đếm `CrawlSources` `SourceSite.StartsWith("OCR-SELF") && CrawledAt >= utcToday` → `>= DailySignupCap` (default 200) → `throw new InvalidOperationException("signup-daily-cap")`. **Bắt buộc `.IgnoreQueryFilters()`** như 3.1 — count phải global, không theo tenant filter.
- Controller: `catch (InvalidOperationException ex) when (ex.Message == "signup-daily-cap")` → `503 { error: "Hệ thống đang bận, vui lòng thử lại sau." }` (generic — không lộ cap).
- Config key `MerchantSignup:DailySignupCap` (int, default 200 — chỉnh không cần deploy code).

### 3.3 Payload caps (trong `ValidateRequest`)

| Field | Cap | Lỗi |
|---|---|---|
| `PhotoUrls` | ≤ 6 phần tử, mỗi URL ≤ 2048 ký tự | ArgumentException |
| `ProductLines` | ≤ 50 dòng, `Name` ≤ 200 ký tự/dòng | ArgumentException |
| `Address` | ≤ 500 ký tự | ArgumentException |
| `SignName` | ≤ 200 ký tự | ArgumentException |
| `ContactEmail` | ≤ 200 ký tự + format cơ bản (`@`) | ArgumentException |
| `ContactName` | ≤ 120 ký tự | ArgumentException |

- `ProductLines`/`PhotoUrls` null → OK (optional). Ảnh thật đi qua `/api/v1/images/upload` đã rate-limit 10/h/IP riêng — task này chỉ cap số URL nhúng trong payload.

### 3.4 Phone whitelist VN

- Sau `NormalizePhone`: hợp lệ khi `Length == 10 && starts '0' && digits[1] ∈ {'2','3','5','7','8','9'}` (02=Hà Nội, 03x/05x/07x/08x/09x di động).
- Blocklist prefix ảo: config `MerchantSignup:BlockedPhonePrefixes` default `"099,059"` → phone starts with bất kỳ prefix nào → reject.
- Reject = `ArgumentException` → 400 (lỗi nhập liệu thật, không cần giấu — phone không phải enumeration vector vì đã public trong form).
- **Ghi chú:** `ContactPhone` hiện cũng là SĐT liên hệ cho Case A/B — siết mobile-only có thể loại SĐT bàn; nếu user muốn nới → thêm config `MerchantSignup:AllowLandline=true` (khi đó chỉ check length+prefix `0`).

### 3.5 (Optional) Cleanup job Pending >30d

- `BackgroundService` trong **Gateway** (owner của PG + `VanAnDbContext`): chạy 1 lần/ngày → `Tenants` `Status==Pending` + có `CrawlSource` `OCR-SELF-*` + `CreatedAt < utcNow-30d` → `tenant.Deactivate("self-serve-pending-timeout")` + SaveChanges (pattern `RejectPendingAsync`).
- Skip nếu chưa duyệt trong session này — ghi nợ tiếp trong master plan §8.

## 4. TESTS (Core.Tests — viết trước, TDD)

File `MerchantSignupServiceTests.cs` — pattern hiện có (in-memory/SQLite DbContext + mocks):

- [ ] Submit phone trùng trong 24h (2 `CrawlSource` `OCR-SELF-*` cùng `ContactPhone`) → lần 2 trả generic success, **không tạo Tenant/CrawlSource mới**, SignupId = tenant cũ.
- [ ] Phone trùng nhưng row >24h → tạo bình thường.
- [ ] Phone khác nhau cùng IP → cả 2 tạo (rate-limit per-IP là tầng nginx/app riêng, không liên quan).
- [ ] `CrawledAt` của row match không phải `OCR-SELF-*` (crawl thường) → KHÔNG tính cooldown.
- [ ] Daily cap: seed N row `OCR-SELF-*` hôm nay = cap → submit → `InvalidOperationException("signup-daily-cap")`; row hôm qua không tính.
- [ ] `PhotoUrls` 7 phần tử → ArgumentException; 6 → OK.
- [ ] `ProductLines` 51 dòng → ArgumentException; `Name` 201 ký tự → ArgumentException.
- [ ] Phone `12345` / `+84` sai format / `099xxxxxxx` (blocked prefix) → ArgumentException; `0901234567`, `0912345678` → OK.
- [ ] Regression: toàn bộ test P0 hiện có vẫn PASS (generic response, honeypot path không đổi).

## 5. ACCEPTANCE CRITERIA

- [ ] 4 test nhóm trên PASS + toàn bộ `MerchantSignupServiceTests` cũ PASS
- [ ] `dotnet build VanAn.sln` + `guard-check.ps1` PASS
- [ ] Manual verify (dev): submit 2 lần cùng SĐT → chỉ 1 tenant Pending trong DB
- [ ] Không regression `/onboard` UI (response message giống hệt — silent dedup không lộ)
- [ ] (Nếu làm 3.5) Cleanup job chạy đúng, có log

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
.\scripts\guard-check.ps1
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~MerchantSignup"
# Deploy sau: RV nhẹ — submit trùng SĐT trên production → 1 Pending duy nhất
```

## 7. RISKS / NOTES

- **LIKE trên RawJson**: scan row `OCR-SELF-*` trong cửa sổ 24h — volume thấp; nếu bảng CrawlSource lớn → thêm `CrawledAt` index hoặc chuyển phone-hash sang cột riêng (cần Gate 5 — Domain change → STOP báo user).
- **Legit user đổi SĐT nhập sai rồi nhập lại**: cooldown 24h chặn — giảm `PhoneCooldownHours` config xuống 1h nếu phát sinh complaint.
- **Bot rotate SĐT**: per-phone không chặn được — đó là lý do có daily cap + Turnstile; OTP (P4) là lớp cuối nếu cần chắc.
- **`EF.Functions.Like` case-sensitive trên PG**: phone digits-only → không vấn đề; RawJson luôn PascalCase deterministic.
- **Daily cap quá thấp chặn launch spike**: default 200/ngày đủ cho MVP; theo dõi log `signup-daily-cap` sau deploy.
- **Phone cooldown ≠ identity verification**: đây là anti-flood, không chứng minh SĐT thật — đúng tinh thần Zero-Cost Identity (L0-L3); L2 verify thật là việc của P4/ID-P3.
- **Phone whitelist chặn SĐT quốc tế**: merchant người nước ngoài dùng SĐT non-VN sẽ bị 400 — chấp nhận cho MVP (thị trường VN); nếu cần → config `MerchantSignup:AllowInternationalPhone`.
- **Không động Domain/UI/Turnstile** → risk regression thấp; Playwright không cần (không đổi UI).
