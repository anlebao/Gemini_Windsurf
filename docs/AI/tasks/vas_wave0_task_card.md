# TASK CARD — VAS Wave 0: Order→Accounting Data Flow Fix

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** Master plan v3 approved
> **Branch:** `feature/vas-wave0-order-accounting-writer-fix`
> **Estimated sessions:** 1-2

## Objective
Fix 18 vấn đề Order→Payment→Accounting data flow (3C + 5H + 4M, defer 6M) để writer ghi dữ liệu đúng VAS trước khi seed.

## Prerequisites (verify before code)
- [ ] Read master plan Section 2.4
- [ ] Verify OrderService.cs path exists: `3_CoreHub/Services/OrderService.cs`
- [ ] Verify Domain.cs Order.ConfirmPayment signature
- [ ] Grep `TotalPrice` usage (R8 audit)

## Files to Modify
| File | Changes | Lines (approx) |
|------|---------|-----------------|
| `3_CoreHub/Services/OrderService.cs` | C1-C3, H1-H5, M9 | 106, 120-126, 146-152, 160, 184, 192-193, 202-206, 523 |
| `1_Shared/Domain.cs` | (none — Service-only fix) | - |
| `3_CoreHub/Services/AccountingEntryService.cs` | (verify CreateRevenueEntryAsync signature) | 134, 184 |
| `6_Tests/.../OrderServiceTests.cs` | Update existing + add new assertions | - |

## Detailed Task List

### Critical (bắt buộc)
#### W0-T1 (C2): Truyền PaymentMethod vào ConfirmPayment
- File: `OrderService.cs` line 523
- Old: `order.ConfirmPayment(transactionId);`
- New: `order.ConfirmPayment(transactionId, order.PaymentMethod ?? "CASH");`
- Verify Domain method accepts paymentMethod param (Domain.cs:1038)

#### W0-T2 (C2+H1): Map PaymentMethod → cash account
- File: `OrderService.cs` CreateRevenueEntryAsync (line 177-196)
- Old: `journalEntry.AddLine("111", order.TotalPrice, 0, "Tiền mặt thu từ bán hàng");`
- New: 
  ```csharp
  string cashAccount = (order.PaymentMethod ?? "CASH") switch
  {
      "CASH" => "111",
      "VIETQR" or "CREDIT_CARD" => "112",
      _ => "111" // safe fallback
  };
  journalEntry.AddLine(cashAccount, order.TotalAmount, 0, "Tiền thu từ bán hàng");
  ```
- Define PaymentMethodConstants class (R9)

#### W0-T3 (C3): Tách VAT trong revenue entry
- File: `OrderService.cs` CreateRevenueEntryAsync
- Old: 2 lines (debit 111, credit 511 = TotalPrice)
- New: 3 lines if VAT > 0:
  ```csharp
  decimal netRevenue = order.TotalAmount - order.TotalVatAmount;
  journalEntry.AddLine(cashAccount, order.TotalAmount, 0, "Tiền thu từ bán hàng");
  journalEntry.AddLine("511", 0, netRevenue, "Doanh thu bán hàng (net)");
  if (order.TotalVatAmount > 0)
      journalEntry.AddLine("3331", 0, order.TotalVatAmount, "Thuế GTGT đầu ra");
  ```

#### W0-T4 (C1): Đồng bộ COGS 2 path
- Extract shared method `CalculateCogsAmount(Order)` 
- Both Path A (line 136-143) and Path B (CreateCOGSEntryAsync line 202-206) call it
- Logic: SUM(item.Product.CostPrice ?? UnitPrice*0.7 * item.Quantity), fallback 0 if no items

#### W0-T5 (B3 absorbed): Fix AccountCode 621→632
- File: `OrderService.cs` line 151
- Old: `accountCode: "621"`
- New: `accountCode: "632"`

### High (nên làm)
#### W0-T6 (H4): Dùng OrderDate thay UtcNow
- Line 106: `AccountingPeriod.Create(order.OrderDate.Year, order.OrderDate.Month)`
- Line 184: `DateTime.UtcNow` → `order.OrderDate`

#### W0-T7 (H5): Thêm Order reference vào AccountingEntry path
- Line 120-126, 146-152: thêm `reference: order.Id.ToString()`

#### W0-T8 (H2): Discount entry
- If `order.DiscountAmount > 0`: thêm entry debit 521/credit 111 (or net 511 approach — decide in session)

#### W0-T9 (H3): Shipping entry
- If `order.ShippingFee > 0`: thêm entry debit 111/credit 515

### Medium (làm nếu time)
#### W0-T10 (M9): Bỏ COGS khỏi S2d
- Line 160: xóa `AddToBookAsync(cogsJournalEntry, AccountingBookType.S2d_HKD)`

#### W0-T11 (M6): Reversal khi cancel sau payment
- Tạo reversal JournalEntry + AccountingEntry.CreateReversal

#### W0-T12 (M1): Map Product.Category → TK tồn kho
- 152 (Vật liệu), 153 (Dụng cụ), 155 (Sản phẩm), 156 (Hàng hóa)

### Defer sang wave sau
- M2 (OrderType), M3 (CustomerId), M4 (AR 131), M5 (VAT input 1331), M7 (COGS fallback), M8 (JE↔AE link), M10 (multi-tenancy query — W4)

## Verification
- [ ] W0-V1: Unit test OrderServiceTests — verify JE có 3 line nếu VAT, 2 line nếu không
- [ ] W0-V1: Verify COGS Path A == Path B
- [ ] W0-V1: Verify PaymentMethod truyền đúng
- [ ] W0-V2: `dotnet build VanAn.sln` Release pass
- [ ] W0-V2: `guard-check.ps1` pass
- [ ] W0-V3: Existing OrderServiceTests pass (no regression)

## Rollback
- Git revert commit W0
- Không破坏 Domain (Service-only fix)
- Existing data không ảnh hưởng (chỉ thay đổi writer logic cho order mới)

## Open Questions (resolve in INVESTIGATE phase)
- Q1: Discount — net revenue (giảm 511) hay gross (debit 521)? (R10)
- Q2: Shipping — 515 (Doanh thu HĐTC) hay 641...? 
- Q3: Có cần PaymentMethodConstants class riêng hay dùng string?
