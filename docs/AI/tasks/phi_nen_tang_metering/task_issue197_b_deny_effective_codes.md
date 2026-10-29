# Task Card B — Issue #197: Topup tên tenant + Deny hiệu lực + Master switch + 4 service code mới (+ Deploy + RV)

> **Status:** CHỜ DUYỆT (2026-10-29)
> **Phase:** Item 1 + Item 2 + §4 master switch + §5 service codes (master plan) · **Gate:** build + guard + tests + CD Multi-VPS + RV L1-L4
> **Căn cứ:** Issue #197 · master plan `phi-nen-tang-metering-issue197-upgrade-master-plan.md`
> **Quyết định:** Item 1 = Option A (UI join — mặc định, user chưa chọn B) · deny 3 lớp · master switch OFF semantics (đề xuất: không chặn + không trừ credit, usage vẫn ghi) · codes: `feature.loyalty` `feature.accounting` `feature.ocr` `feature.voice-note`

---

## 1. Mục tiêu

(1) `/admin/metering/topups` hiện **tên tenant**. (2) Deny **có hiệu lực thật**: sửa bất khớp gate↔rate-card (data production) + server-side deny guard (booking/voice-note/ocr) + mở rộng UI gates (booking/accounting/loyalty). (3) **Master switch on/off metering toàn hệ thống** (SystemAdmin). (4) Đăng ký + ingest + gate cho 4 service code mới.

## 2. Thay đổi

### 2.1 Item 1 — Topup tên tenant (Option A)
| File | Action |
|---|---|
| `Components/Pages/Admin/MeteringTopups.razor` | load `ITenantManagementService.ListTenantsAsync()` → `Dictionary<Guid,string>` → cột Tenant = Name + tooltip full id · fallback GUID prefix (mirror thiếu row) · 0 đụng metering service/DB |

### 2.2 Master switch toàn hệ thống (mới — master plan §4)
| File | Action |
|---|---|
| 8_Metering Domain `EnforcementConfig.cs` (MỚI) | singleton 1 row: Id · Enabled (default true) · UpdatedBy · UpdatedAtUtc — pattern TopupConfig |
| 8_Metering Infra `EnforcementConfigService.cs` (MỚI) | `GetAsync` (row thiếu → enabled=true) · `SetAsync` (admin, AuditLog cùng SaveChanges) |
| 8_Metering `Program.cs` + `MeteringDbContext.cs` | `GET/PUT /v1/config/enforcement` (PUT admin → 403 JSON) · bảng idempotent `EnsureP7Schema` · check → deny=false khi OFF · ingest → ghi usage KHÔNG deduct khi OFF |
| `WalletService.CheckAsync` + `UsageService` | đọc toggle trong request path |
| ShopERP `MeteringRateCardApiClient.cs` | += `Get/SetEnforcementAsync` (admin key) |
| ShopERP `Admin/MeteringRateCards.razor` | toggle switch ở header + trạng thái + "Cập nhật bởi" + hướng dẫn "Tắt = không chặn, không trừ credit (usage vẫn ghi)" |

### 2.3 Item 2 — Deny hiệu lực
| File | Action |
|---|---|
| ShopERP `Services/MeteringDenyGuard.cs` (MỚI) | `CheckOrThrowAsync(tenantId, serviceCode, estimate?)` — fail-open (transport error → pass + log, G9) · deny → throw `MeteringDeniedException` |
| `3_CoreHub/Services/Booking/BookingService.cs` | `CreateBookingAsync` += guard `feature.booking` (trước persist — phủ KhachLink public + staff) |
| `3_CoreHub/Services/OrderService.cs` | `UpdateOrderVoiceNoteAsync` += guard `feature.voice-note` |
| Gateway `Services/TenantOcrOnboardingService.cs` (hoặc controller) | guard `feature.ocr` (per-action — tenant tạo qua OCR) |
| API layer (ShopERP/Gateway) | catch `MeteringDeniedException` → JSON 402 + UI alert "Hết hạn mức phí nền tảng" + link nạp |
| ShopERP booking pages `/booking/*` | += `MeteringDenyGate ServiceCode="feature.booking"` (UX) |
| Accounting pages (AccountingIndex + page nhập liệu/report chính) | += `MeteringDenyGate ServiceCode="feature.accounting"` (chốt danh sách khi implement — KHÔNG 20 page) |
| `Loyalty/LoyaltyDashboard.razor` | += `MeteringDenyGate ServiceCode="feature.loyalty"` |

### 2.4 Data production (ops — chỉ sau khi build pass)
- Tạo rate-card `feature.financial` + `feature.inventory` Enforcement=**Deny** (qua admin UI/API)
- Verify check endpoint `deny=true` cho tenant ví 0 (`00000001`, `7C021960`)

### 2.5 Service code mới + ingest hooks
| Code | Hook | File |
|---|---|---|
| `feature.loyalty` | page visit | `LoyaltyDashboard.razor` += `FeatureUsageRecorder` (pattern feature.financial) |
| `feature.accounting` | page visit | Accounting pages += `FeatureUsageRecorder` |
| `feature.ocr` | per-action | Gateway `TenantOcrOnboardingService` — sau persist thành công |
| `feature.voice-note` | per-action | `OrderService.UpdateOrderVoiceNoteAsync` — sau persist |
- `1_Shared/Domain/Common/IMeteringClient.cs` doc comment cập nhật danh sách code
- `store.visit` KHÔNG gate (anonymous) · chat deny (KhachLink) giữ deviation GĐ-2

## 3. Test matrix

| Nhóm | Tests |
|---|---|
| Metering | M7 toggle default true · M8 OFF → check deny=false (dù card Deny + ví 0) · M9 OFF → ingest không deduct, usage vẫn ghi · M10 ON → deny/deduct quay lại · M11 PUT non-admin → 403 · M12 PUT ghi AuditLog + singleton |
| Core.Tests | C1 BookingService deny guard: deny→throw · đủ credit→pass · metering lỗi→fail-open pass · C2 voice-note guard 3 case |
| Gateway | G1 OCR onboarding deny guard (deny → 402, không tạo tenant) |
| ShopERP bUnit | U1 deny gate accounting/loyalty render · U2 topup tên tenant · U3 toggle render + trạng thái |
| E2E (Gate 4) | `metering-issue197.spec.ts`: deny booking thật (ví 0) · topup cột tên tenant · toggle OFF → trang vẫn dùng được |

## 4. Deploy + RV

- Validation: `dotnet build VanAn.sln` 0 err · `dotnet build VanAn.Metering.sln` 0 err · `guard-check.ps1` ALL PASSED · Core.Tests + Metering.Tests PASS
- Push → CD Multi-VPS (gateway + shoperp + metering) → RV:
  - L1: markers production (MeteringDenyGuard · cột scope metering · bảng EnforcementConfig) + data production verify (deny=true)
  - L2: health + API qua `app2.khachvip.online/admin/metering/api/` (GET/PUT config)
  - L3: Playwright — deny feature.financial thật · deny booking · topup tên tenant · toggle OFF/ON
  - L4/L5: manual user

## 5. File plan tổng

8_Metering: `EnforcementConfig.cs` + `EnforcementConfigService.cs` + `Program.cs` + `MeteringDbContext.cs` + `WalletService.cs` + `UsageService.cs` + tests · ShopERP: `MeteringTopups.razor` · `MeteringDenyGuard.cs` (mới) · `MeteringRateCards.razor` · `MeteringRateCardApiClient.cs` · booking/accounting/loyalty pages · `3_CoreHub`: `BookingService.cs` · `OrderService.cs` · Gateway OCR service · `IMeteringClient.cs` (doc) · E2E spec