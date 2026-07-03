# TASK CARD — VAS Wave 1: Data Audit + Seed

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0 merged to main
> **Branch:** `feature/vas-wave1-data-audit-seed`
> **Estimated sessions:** 1

## Objective
Populate JournalEntries with sample double-entry data + opening balances (dùng fixed writer từ W0).

## Prerequisites (verify before code)
- [ ] W0 merged to main (writer fix available)
- [ ] Verify IVanAnDbContext path: `3_CoreHub/Infrastructure/IVanAnDbContext.cs`
- [ ] Verify JournalEntry entity: `1_Shared/Domain/JournalEntry.cs`
- [ ] Verify AccountingEntry entity: `1_Shared/Domain.cs` line 287
- [ ] Check existing seed scripts in `scripts/` folder

## Files to Create/Modify
| File | Action | Purpose |
|------|--------|---------|
| `scripts/seed-vas-sample.ps1` (or C# console) | CREATE | Seed script |
| `3_CoreHub/Infrastructure/Seed/VasSampleDataSeeder.cs` | CREATE (optional) | C# seeder class |

## Detailed Task List

### W1-T1: Create seed script
- 1 tenant DN vừa (TT 133), TenantType=Enterprise
- Opening balance entries (debit/credit cân):
  - 111 (Tiền mặt), 112 (Tiền gửi NH), 156 (Hàng hóa), 211 (TSCĐ)
  - 311 (Vốn CSH), 331 (NCC), 3331 (VAT)
- ~20 journal entries covering:
  - Bán hàng CASH: debit 111, credit 511 (net), credit 3331 (VAT)
  - Bán hàng VIETQR: debit 112, credit 511, credit 3331
  - COGS: debit 632, credit 156
  - CP bán hàng: debit 641, credit 111
  - CP QLDN: debit 642, credit 111
  - Thu tiền công nợ: debit 111, credit 131
  - Trả NCC: debit 331, credit 111
  - Khấu hao: debit 642, credit 211
  - Lương: debit 641, credit 334
  - Discount: debit 521, credit 111
  - Shipping: debit 111, credit 515
- Multi-period: 2 tháng liên tiếp (test period filter)
- Multi-payment-method: cả CASH (111) và VIETQR (112)

### W1-T2: Verify seed
- Query JournalEntries count > 0
- Verify debit=credit cân per entry
- Verify VAT tách đúng (511 net, 3331 VAT riêng)
- Verify opening balance entries tồn tại

### W1-T3: Optional — verify writer output
- Run 1 order qua ConfirmPaymentAsync (fixed W0)
- Compare generated entry với seed pattern
- Confirm writer logic matches seed structure

### W1-T4: Build + guard pass

## Verification
- [ ] JournalEntries table có > 20 rows
- [ ] Mỗi entry debit total == credit total
- [ ] Opening balance entries tồn tại
- [ ] Multi-period data (2 tháng)
- [ ] Build pass + guard pass

## Rollback
- Delete seeded data via script (provide cleanup script)
- Hoặc recreate dev DB từ migration

## Open Questions
- Q1: Seed via PowerShell script hay C# console app?
- Q2: Store seed data trong migration hay script riêng?
- Q3: Opening balance — separate table hay JournalEntries với special flag?
