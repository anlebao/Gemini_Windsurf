# SRS — SHOPERP VOUCHER FRAMEWORK v1.0

> Trạng thái: ✅ Approved (2026-11-02) — PO duyệt hướng registry-driven
> Phạm vi: Tầng UI/route/parse — KHÔNG đổi Domain (AccountingEntry immutable), service cốt lõi, DB
> Liên quan: task PG/G4 (Smart Entry multi) · TT 71/2024/TT-BTC (phiếu HTX) · SRS Accounting Engine v1.2

---

## 1. MỤC ĐÍCH

Chuẩn hoá cách thêm **loại phiếu chứng từ mới** (xuất kho, tạm ứng, thanh lý TSCĐ, và mọi phiếu tương lai) mà **không phá vỡ hoặc làm cồng kềnh hệ thống**. Hiện tại mỗi phiếu = 1 page (~400-600 dòng) + ApplyParse switch + service — thêm phiếu là copy-paste cả khối.

## 2. KIẾN TRÚC (3 lớp)

```
Smart Entry (intent) ──┐
                      ▼
              VoucherTypeRegistry  (DI — khai báo phiếu)
                      │  VoucherTypeDefinition: Code/Name/Route/PrintTemplate/Fields/PostingStrategy
                      ▼
        VoucherEntry.razor  (/vouchers/{code} — PAGE GENERIC DUY NHẤT)
                      │  render form theo schema · ApplyParse generic · preview JE · guard · Lưu · in
                      ▼
         IVoucherPostingStrategy (1 class nhỏ / phiếu — bút toán test được)
```

## 3. L1 — VOUCHER TYPE REGISTRY

```csharp
public sealed record VoucherField(
    string Id,                    // "amount","date","party","account","item","quantity"...
    VoucherFieldType Type,        // Amount|Date|Party|Account|Select|Text|Item|Quantity
    string Label,
    bool Required = false,
    string? SourceParse = null);  // map từ ParsedVoucher (amount/date/doiTuong/paymentMethod...)

public sealed record VoucherTypeDefinition(
    string Code,                  // "advance","stock-issue","asset-disposal","expense"...
    string Name,                  // "Phiếu Tạm Ứng","Phiếu Xuất Kho","Thanh Lý TSCĐ"...
    string Route,                 // "/vouchers/advance"
    string? PrintTemplate,        // "02-TT"|"02-VT"|"01-TSCĐ"|null
    IReadOnlyList<VoucherField> Fields,
    string PostingStrategyKey);   // → IVoucherPostingStrategy

// Đăng ký: builder.Services.AddVoucherType(new VoucherTypeDefinition(...));
// Thêm phiếu mới = 1 registry entry + 1 strategy — KHÔNG sửa page/ApplyParse/SmartEntryBar
```

## 4. L2 — PAGE GENERIC `VoucherEntry.razor`

- Route: `/vouchers/{code}` → tra registry → render form theo `Fields`
- **ApplyParse generic**: `ParsedVoucher` → field theo `SourceParse` (thay switch trong từng page)
- Preview JE (AccountMapResolver + bank accounts + feature flags — reuse)
- Guard giữ nguyên: dup-check 5' · period · balance-warn · preview trước Lưu · voucher number · in phiếu
- Multi-voucher (G4): list phiếu → mỗi phiếu "Áp vào form" → route đúng registry

## 5. L3 — IVoucherPostingStrategy

```csharp
public interface IVoucherPostingStrategy
{
    Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct);
}
```
- 1 class nhỏ (~30-60 dòng) mỗi phiếu: xây AccountingEntry + JE theo nghiệp vụ
- Vẫn là C# rõ ràng, test được — **KHÔNG đẩy business logic vào config** (chống over-engineering)
- Ví dụ `AssetDisposalStrategy`: Nợ 214 + Nợ 811/Có 711 + Nợ 111,112 / Có 211

## 6. SMART ENTRY TÍCH HỢP

- `VoucherIntent → VoucherTypeDefinition` (dictionary trong registry) — hết hardcode route switch
- Intent mới: `advance-out`, `advance-settle`, `stock-out`, `asset-disposal` (+ từ khoá VN + alias)
- Multi giữ nguyên (1 câu → N phiếu, mỗi phiếu route theo intent)

## 7. PHIẾU ĐẦU TIÊN QUA FRAMEWORK (P1-P3)

| Phiếu | Intent | JE chính | TT71 |
|---|---|---|---|
| **Phiếu Tạm Ứng** | advance-out / advance-settle | Nợ 141/Có 111 · Nợ 111/Có 141 (hoàn ứng, đối ứng tạm ứng mở — reuse GetOpenKhoanNoAsync) | 01-TT/02-TT |
| **Phiếu Xuất Kho** | stock-out | SX: Nợ 154/Có 152 · bán: Nợ 632/Có 152 (bình quân gia quyền) + StockMovement | 02-VT |
| **Thanh Lý TSCĐ** | asset-disposal | Nợ 214 + Nợ 811/Có 711 + Nợ 111,112 / Có 211 (cần bảng FixedAsset tối thiểu) | 01-TSCĐ |

## 8. YÊU CẦU PHI CHỨC NĂNG

- NFR1: Thêm phiếu mới ≤ 1 ngày dev (registry + strategy), không sửa file khác
- NFR2: Không migration mới khi thêm phiếu dạng form thuần (chỉ cần khi có entity mới — TSCĐ)
- NFR3: Testability — mỗi strategy có unit test độc lập; guard chung test 1 lần
- NFR4: Backward compat — 5 phiếu cũ giữ nguyên; migrate dần (G3, không bắt buộc)
- NFR5: Multi-voucher + session (G4f) hoạt động với mọi phiếu registry

## 9. GIỚI HẠN (CHỐNG OVER-ENGINEERING)

- Framework chỉ ở UI/route/parse — Domain/service/DB không đổi
- KHÔNG làm "máy cấu hình vạn năng" — chuẩn hoá phần lặp lại (form/route/parse/guard)
- Nghiệp vụ phức tạp (khấu hao định kỳ, tín dụng nội bộ 132/332) không nhét vào framework — làm service riêng, có thể gắn qua strategy