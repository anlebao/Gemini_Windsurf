# Task P11 — Package Catalog (sysadmin-defined) + Purchase + UI

**Status:** Approved — user chốt 2026-11-19 (sau P10)
**Depends on:** P10 (credit lots + expiry), P7/P8/P9 (topup + Tingee)
**Mode:** IMPLEMENT + TDD

## Mục tiêu

Sysadmin (Vạn An) định nghĩa **gói cước nền tảng** — mỗi gói = giá VND cố định +
số credits cố định + thời hạn dùng. Tenant mua gói qua flow topup hiện có
(Tingee checkout / dynamic QR / CK tay); khi confirm, credits vào **lot có hạn**
(P10). Nạp tự do không hết hạn nhưng đắt hơn (pricing differentiation).

Kèm theo: trang storefront "tạm ngưng phục vụ" hướng dẫn chủ shop nạp phí +
banner CTA trong ShopERP.

## Quyết định đã chốt

| # | Quyết định | Lựa chọn |
|---|---|---|
| D1 | Ai định nghĩa gói | **Sysadmin platform-level** — `sysadmin` caller only; tenant không tạo/sửa |
| D2 | Catalog scope | Global (không per-tenant) — 1 bảng `CreditPackages` |
| D3 | Giá/credits | Mỗi gói `PriceVnd` + `Credits` riêng — **không** dùng `VndPerCredit` (gói càng dài càng rẻ/credit) |
| D4 | Validity | `ExpiresAtUtc = confirmedAtUtc + ValidityDays` — tính từ lúc credit, không phải ngày dương lịch |
| D5 | Nạp tự do | `PackageCode = null` → lot `ExpiresAtUtc=null`, credits = `paidAmount/VndPerCredit` như cũ |
| D6 | Thanh toán | Tái dùng nguyên P8 `/pay` + P9 `/qr` + webhook confirm — chỉ đổi cách tính credits khi có package |
| D7 | Package lifecycle | `IsActive` soft-disable; gói đã mua không bị ảnh hưởng khi gói bị tắt |

## Domain model

```csharp
public sealed class CreditPackage
{
    public Guid Id;
    public string Code;          // "DAY","WEEK","MONTH","YEAR" — unique
    public string Name;          // "Gói ngày", "Gói tuần"...
    public decimal PriceVnd;     // giá bán cố định
    public decimal Credits;      // credits được cấp
    public int ValidityDays;     // 1, 7, 30, 365...
    public int SortOrder;
    public bool IsActive;
}
```

`TopupRequest` thêm `string? PackageCode` (max 32) — null = nạp tự do.

## Thay đổi

### Domain
- `CreditPackage.cs` mới (pure).
- `TopupRequest.PackageCode` + factory param optional.

### Infra
- `MeteringDbContext`: `DbSet<CreditPackage>` + `DbSet` config (unique `Code`,
  `PriceVnd`/`Credits` decimal).
- `PackageService` (mới, nhỏ): `ListActiveAsync`, `GetByCodeAsync`,
  admin `UpsertAsync`/`DeactivateAsync` — caller `sysadmin` only.
- `TopupService`:
  - `CreateAsync` nhận `packageCode` → validate gói active → `AmountVnd = package.PriceVnd`
    (ghi đè amount client gửi — giá do sysadmin quyết), `CreditsGranted = package.Credits`.
  - `ConfirmPaidCoreAsync`: nếu có package → tạo lot `ExpiresAtUtc = now + ValidityDays`,
    credits = `package.Credits` (partial payment? — quyết: gói trả thiếu →
    **không confirm**, yêu cầu trả đủ; nạp tự do vẫn partial theo paidAmount).
    Ghi rõ trong card khi implement: đề xuất partial → reject/hold pending.
  - `CreatePayLinkAsync`/`CreateQrAsync`: amount = `AmountVnd` đã là giá gói.
- `Program.cs`:
  - `GET /v1/packages` — public cho caller đã auth (tenant xem gói mua).
  - `GET/PUT /v1/admin/packages`, `POST /v1/admin/packages/{id}/deactivate` — sysadmin.
  - `POST /v1/topups` body thêm `packageCode`.
- Seed: migration/seed 4 gói mẫu (giá/credits do sysadmin chỉnh sau):
  `DAY(1d), WEEK(7d), MONTH(30d), YEAR(365d)` — giá trị seed đánh dấu "cần chỉnh".
- Schema upgrade idempotent như P9.

### Contracts
- `TopupCreateRequest` + `PackageCode`.
- `TopupDto` + `PackageCode`, `PackageName` (hiển thị).
- `PackageDto { code, name, priceVnd, credits, validityDays }`.
- `WalletDto.Lots` đã có từ P10 — thêm `PackageCode`/`PackageName` vào `CreditLotDto`.

### ShopERP
- `MeteringDtos` + `MeteringPackageDto`; `MeteringApiClient.GetPackagesAsync`.
- `PhiNenTang.razor`: block "Gói cước" phía trên form nạp tự do — chips/cards
  hiển thị tên, giá VND, credits, "dùng trong X ngày"; chọn gói → tạo topup
  `packageCode` → flow thanh toán y hệt (QR động / checkout).
- Form nạp tự do giữ nguyên + note "không hết hạn, đơn giá cao hơn".
- Admin: `MeteringTopups.razor` hoặc trang mới `/admin/metering/packages` —
  SystemAdmin CRUD gói (code, tên, giá, credits, validity, active).
- `Store.razor` (KhachLink) suspended block: thêm "Chủ cửa hàng vui lòng đăng nhập
  ShopERP → Ví phí nền tảng để nạp lại" + link `/login` (hoặc domain ShopERP).
- ShopERP banner: khi `alertLevel=depleted` → CTA "Nạp phí để mở lại" → `/wallet/phi-nen-tang`.

### E2E (Gate 4)
- `metering-wallet.spec.ts`: package picker hiển thị + tạo topup theo gói.
- Store suspended page: text hướng dẫn hiển thị (mock hoặc tolerate).

## Test plan (TDD)

`tests/VanAn.Metering.Tests/PackageTests.cs`:
1. `Create_WithPackage_SetsPriceAndPending`
2. `Create_WithInactivePackage_Fails`
3. `Create_WithUnknownPackage_Fails`
4. `Confirm_Package_GrantsPackageCredits_NotVndPerCredit`
5. `Confirm_Package_CreatesExpiringLot` (ExpiresAtUtc = confirmedAt + validity)
6. `Confirm_Package_PartialPayment_StaysPending` (đề xuất — chốt khi implement)
7. `Package_Expiry_FEFO_WithCustomTopup` (integration với P10)
8. `Admin_UpsertPackage_SysadminOnly` — tenant caller bị 403
9. `GetPackages_ReturnsActiveOnly`
10. Regression P7–P10.

## Không làm

- Giảm giá/khuyến mãi theo tenant, combo gói, gia hạn tự động.
- Đổi `VndPerCredit` semantics cho nạp tự do.
