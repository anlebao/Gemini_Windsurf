# TASK CARD — VAS Wave 1: Data Audit + Seed

> **Status:** COMPLETE ✅ | INVESTIGATE → PLAN → IMPLEMENT → VERIFIED
> **Prerequisite:** W0 merged to main (✅ `45250f3`)
> **Branch:** `feature/vas-wave1-data-audit-seed`
> **Estimated sessions:** 1 (actual: 1)

## Objective
Populate JournalEntries + AccountingEntries with sample double-entry data + opening balances (dùng fixed writer từ W0). Fix schema gaps discovered during Data Audit.

## Prerequisites (verify before code)
- [x] W0 merged to main (writer fix available — `45250f3`)
- [x] Verify IVanAnDbContext path: `3_CoreHub/Infrastructure/IVanAnDbContext.cs`
- [x] Verify JournalEntry entity: `1_Shared/Domain/JournalEntry.cs`
- [x] Verify AccountingEntry entity: `1_Shared/Domain.cs` line 287
- [x] Check existing seed scripts in `scripts/` folder (`seed-production-users.ps1`)

## Data Audit Findings (W1-T0 — CRITICAL)
**Schema Gap Discovered:** `JournalEntries` table was missing 3 columns that exist on the `JournalEntry` entity:
- `EntryDate` (DateTime) — W0 writer sets this to `order.OrderDate`, but was **silently dropped** on persist (EF model snapshot didn't map it)
- `ReferenceId` (Guid?) — W0 writer sets this to `order.Id`, but **not persisted**
- `IsReversal` (bool) — **not persisted**

**Root Cause:** `JournalEntryConfiguration.cs` had `EntryDate` mapped (line 27) but the migration was generated before that. `ReferenceId` and `IsReversal` were not in the configuration at all.

**Modeling Defect Found:** `JournalEntryLine` entity lacked an explicit `Id` property — it was a shadow property that SQLite cannot auto-generate for composite keys (`JournalEntryId` + `Id`). This caused `NOT NULL constraint failed: JournalEntryLine.Id` on persist.

**Fix Applied:**
1. Added `ReferenceId` and `IsReversal` to `JournalEntryConfiguration.cs`
2. Added composite index on `(TenantId, EntryDate)` for period-filtered queries
3. Created migration `20260704044449_AddJournalEntryMissingColumns` (adds 3 columns + 1 index)
4. Added explicit `Id` property to `JournalEntryLine` (Domain fix — genuine modeling defect)
5. `JournalEntry.AddLine` now passes sequential `Id` (`_lines.Count + 1`)
6. `JournalEntryConfiguration` updated: `ValueGeneratedNever()` for `Id` (domain-set, not DB-generated)

## Files Created/Modified
| File | Action | Purpose |
|------|--------|---------|
| `3_CoreHub/Infrastructure/Seed/VasSampleDataSeeder.cs` | CREATE | C# seeder class (Enterprise DN vừa TT 133) |
| `3_CoreHub/Infrastructure/Configurations/JournalEntryConfiguration.cs` | MODIFY | Map ReferenceId, IsReversal, Id; add EntryDate index |
| `3_CoreHub/Infrastructure/Migrations/20260704044449_AddJournalEntryMissingColumns.cs` | CREATE | Migration: add EntryDate, ReferenceId, IsReversal columns |
| `1_Shared/Domain/JournalEntry.cs` | MODIFY | Add `Id` property to `JournalEntryLine`; sequential Id in `AddLine` |
| `6_Tests/VanAn.Core.Tests/Seed/VasSampleDataSeederTests.cs` | CREATE | 11 tests: counts, balance, VAT split, opening, multi-period, multi-payment |
| `6_Tests/VanAn.Core.Tests/Accounting/JournalEntryLineTests.cs` | MODIFY | Update constructor calls for new `Id` parameter |
| `6_Tests/VanAn.Core.Tests/Accounting/JournalEntryTests.cs` | MODIFY | Update constructor call for new `Id` parameter |
| `6_Tests/VanAn.Core.Tests/Accounting/TestDomainClasses.cs` | MODIFY | Update mock `JournalEntryLine` class for new `Id` parameter |

## Detailed Task List

### W1-T0: Data Audit (NEW — discovered during investigation)
- [x] Audit `JournalEntries` schema vs entity → found 3 missing columns
- [x] Audit `JournalEntryLine` persistence → found shadow `Id` property not auto-generated
- [x] Fix schema: add migration + configuration + Domain `Id` property

### W1-T1: Create seed data
- [x] 1 tenant DN vừa (TT 133), BusinessType=Company (Enterprise)
- [x] Opening balance entries (debit/credit cân):
  - 111 (Tiền mặt 50M), 112 (Tiền gửi NH 100M), 156 (Hàng hóa 80M), 211 (TSCĐ 200M)
  - 411 (Vốn CSH 350M), 331 (NCC 50M), 3331 (VAT 30M)
- [x] 22 journal entries (31 with COGS splits) covering:
  - Bán hàng CASH: debit 111, credit 511 (net), credit 3331 (VAT)
  - Bán hàng VIETQR: debit 112, credit 511, credit 3331
  - COGS: debit 632, credit 156
  - CP bán hàng: debit 641, credit 111
  - CP QLDN: debit 642, credit 111
  - Thu tiền công nợ: debit 111/112, credit 131
  - Trả NCC: debit 331, credit 111
  - Khấu hao: debit 642, credit 211
  - Lương: debit 641, credit 334
  - Discount: debit 521, credit 111
  - Shipping income: debit 111, credit 515
  - CP điện nước: debit 642, credit 111
- [x] Multi-period: 2026-05 and 2026-06 (2 tháng liên tiếp)
- [x] Multi-payment-method: cả CASH (111) và VIETQR (112)
- [x] Corresponding AccountingEntries for each transaction (511, 3331, 632, 641, 642, etc.)

### W1-T2: Verify seed (15 tests — ALL PASS)
- [x] JournalEntries count >= 20 (actual: 31)
- [x] Each entry debit total == credit total (balanced)
- [x] VAT tách đúng (511 net, 3331 VAT riêng) — `SeedAsync_VatSplitCorrect_511Net_3331Vat`
- [x] Opening balance entries tồn tại (7 lines: 111, 112, 156, 211, 311, 331, 3331)
- [x] Multi-period data (2026-05 and 2026-06)
- [x] Multi-payment-method (CASH 111 + VIETQR 112)
- [x] COGS uses 632 (not 621 — W0 B3 fix verified)
- [x] EntryDate persisted correctly (not default DateTime)
- [x] AccountingEntries have correct AccountCodes
- [x] Idempotent (skip if tenant exists)
- [x] Cleanup method removes all seeded data

### W1-T3: Optional — verify writer output
- [ ] DEFERRED — W0 writer already verified by 10 SC17-SC23 tests in W0

### W1-T4: Build + guard pass
- [x] `dotnet build` — 0 errors
- [x] `guard-check.ps1` — ALL CHECKS PASSED
- [x] Core.Tests — 843/843 PASS (828 original + 15 seeder tests)
- [x] Architecture.Tests — PASS
- [x] Integration.Tests (CircuitBreaker) — PASS

## Verification Results
- [x] JournalEntries table có > 20 rows (31 entries)
- [x] Mỗi entry debit total == credit total
- [x] Opening balance entries tồn tại
- [x] Multi-period data (2 tháng: 2026-05, 2026-06)
- [x] Build pass + guard pass

## Rollback
- `VasSampleDataSeeder.CleanupAsync(db)` — removes all seeded data (tested)
- Hoặc recreate dev DB từ migration (`dotnet ef database update`)

## Open Questions — RESOLVED
- Q1: ~~Seed via PowerShell script hay C# console app?~~ → **C# seeder class in CoreHub** (user-approved)
- Q2: ~~Store seed data trong migration hay script riêng?~~ → **C# seeder class** (not migration — migrations are for schema, not data)
- Q3: ~~Opening balance — separate table hay JournalEntries với special flag?~~ → **JournalEntries with ReferenceType="OpeningBalance"** + corresponding AccountingEntries

## Decisions Made
- D-W1-1: Fix schema + seed both (JournalEntries + AccountingEntries) — user-approved
- D-W1-2: C# seeder in `3_CoreHub/Infrastructure/Seed/` — user-approved
- D-W1-3: `JournalEntryLine.Id` added as explicit Domain property (genuine modeling defect — SQLite can't auto-generate int for composite keys)
- D-W1-4: Dev DB recreated from migrations (old DB was `EnsureCreated` without migration history)
- D-W1-5: **Account code fix — 311 → 411** (Vốn CSH). Bug found during account code review against TT 133/2016/TT-BTC and TT 200/2014/TT-BTC. TK 311 = "Vay ngắn hạn" (QĐ 48) / removed in TT 200 (replaced by 341) / does NOT exist in TT 133. TK 411 = "Nguồn vốn kinh doanh" (correct for Owner's Equity). All other account codes verified correct (111, 112, 131, 156, 211, 331, 3331, 334, 511, 515, 521, 632, 641, 642).

## Account Code Verification (against official chart of accounts)
**Căn cứ pháp lý:**
- **TT 200/2014/TT-BTC** — Hệ thống tài khoản kế toán DN (Bộ Tài chính, 22/12/2014)
  - Nguồn: https://baocaotaichinh.vn/thong-tu-200/3/333.html
  - Nguồn: https://congbao.chinhphu.vn/van-ban/thong-tu-so-200-2014-tt-btc-6697.htm
- **TT 133/2016/TT-BTC** — Hệ thống tài khoản kế toán DN vừa và nhỏ (Bộ Tài chính, 26/08/2016)
  - Nguồn: https://baocaotaichinh.vn/thong-tu-133/index.html
  - Nguồn: https://thuvienphapluat.vn/van-ban/Doanh-nghiep/Circular-133-2016-TT-BTC-accounting-for-small-medium-enterprises-337431.aspx
  - Nguồn TK 642: https://baocaotaichinh.vn/thong-tu-133/6/642.html (6421 + 6422, KHÔNG có 641)
  - Nguồn TK 521: https://easyinvoice.vn/cach-hach-toan-chiet-khau-thuong-mai/ ("TT 133 không có TK 521")
- **TT 99/2025/TT-BTC** — chỉ thay đổi format BCTC, KHÔNG thay đổi hệ thống tài khoản

**5 LỖI KẾ TOÁN SƠ ĐẲNH ĐÃ PHẢN BIỆN VÀ SỬA (2026-07-04):**

| # | Lỗi cũ | Sửa thành | Lý do | Nguồn |
|---|--------|-----------|-------|-------|
| 1 | 311 = "Vốn CSH" | **411** = Nguồn vốn kinh doanh | 311 = Vay ngắn hạn (QĐ48) / bị xóa trong TT 200 / KHÔNG tồn tại trong TT 133 | TT 133 danh mục |
| 2 | Khấu hao: Nợ 642 / Có **211** | Nợ 6422 / Có **214** (Hao mòn lũy kế) | 211 = Nguyên giá (chỉ giảm khi thanh lý). 214 = Hao mòn lũy kế | TT 133 nguyên tắc TSCĐ |
| 3 | Chiết khấu: Nợ **521** / Có 111 | Nợ **511** / Có 111 (ghi giảm doanh thu) | TT 133 ĐÃ KHAI TỬ 521. Ghi giảm trực tiếp 511. Khớp W0 Option A | TT 133 + W0 decision |
| 4 | Phí vận chuyển: Nợ 111 / Có **515** | Nợ 111 / Có **5113** (Doanh thu CCDV) | 515 = Doanh thu HĐ tài chính (lãi ngân hàng, tỷ giá). Phí vận chuyển = doanh thu dịch vụ | TT 133 danh mục 511 |
| 5 | CP bán hàng: Nợ **641** / Có 111 | Nợ **6421** / Có 111 (TT 133 gộp vào 642) | TT 133 KHÔNG CÓ 641. 6421 = CP bán hàng, 6422 = CP QLDN | TT 133 điều 64 |

**Bài học ghi nhận (để không lặp lại):**
1. **KHÔNG chắp vá TT 200 vào TT 133** — phải dùng đúng danh mục tài khoản của TT đang áp dụng. 521, 641 là mã TT 200, không dùng cho tenant TT 133.
2. **Nguyên tắc TSCĐ** — 211 (Nguyên giá) KHÔNG BAO GIỜ giảm khi khấu hao. Phải dùng 214 (Hao mòn lũy kế).
3. **Bản chất nghiệp vụ** — 515 chỉ cho HĐ tài chính (lãi, tỷ giá). Phí vận chuyển = dịch vụ = 5113.
4. **W0 Option A** — chiết khấu ghi giảm 511 (net revenue), KHÔNG dùng 521.
5. **Luôn verify account code với danh mục chính thức** trước khi seed.

**Bảng tài khoản FINAL (TT 133 — DN vừa):**
| TK | Ý nghĩa | Trạng thái |
|----|---------|------------|
| 111 | Tiền mặt | ✅ |
| 112 | Tiền gửi Ngân hàng | ✅ |
| 131 | Phải thu của khách hàng | ✅ |
| 156 | Hàng hoá | ✅ |
| 211 | TSCĐ hữu hình (Nguyên giá) | ✅ |
| 214 | Hao mòn TSCĐ lũy kế | ✅ (fixed from 211 credit) |
| 411 | Nguồn vốn kinh doanh (Vốn CSH) | ✅ (fixed from 311) |
| 331 | Phải trả cho người bán | ✅ |
| 3331 | Thuế GTGT phải nộp (33311 = GTGT đầu ra) | ✅ |
| 334 | Phải trả người lao động | ✅ |
| 511 | Doanh thu bán hàng & CCDV (ghi giảm khi chiết khấu) | ✅ (fixed from 521) |
| 5113 | Doanh thu cung cấp dịch vụ (phí vận chuyển) | ✅ (fixed from 515) |
| 632 | Giá vốn hàng bán | ✅ |
| 6421 | Chi phí bán hàng (TT 133, thay 641) | ✅ (fixed from 641) |
| 6422 | Chi phí QLDN (TT 133) | ✅ (fixed from 642) |

**Tests mới thêm (4 tests — phòng ngừa regression):**
- `SeedAsync_DepreciationUsesAccount214_Not211` — khấu hao phải Có 214, KHÔNG Có 211
- `SeedAsync_DiscountReduces511_Not521_TT133` — chiết khấu Nợ 511, KHÔNG 521
- `SeedAsync_ShippingUses5113_Not515` — phí vận chuyển Có 5113, KHÔNG 515
- `SeedAsync_TT133_NoAccount641_Uses6421` — không dùng 641/521/311, khấu hao không Có 211
