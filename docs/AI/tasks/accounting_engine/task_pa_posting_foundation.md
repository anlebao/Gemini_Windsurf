# TASK CARD A — POSTING FOUNDATION (structured key · PostingStatus+retry · JE balance · unique index · DN accrual JE)

> Status: 🔵 CODE DONE (2026-10-30) — chờ verification chain (sln build + guard + full sweep) → push → RV
> Điều chỉnh thiết kế khi implement: `PostingStatus` TÁCH thành `EntryPostingStatusEntity` (PK=EntryId, 1 row/entry) — giữ AccountingEntry 100% immutable (hard-stop, không thêm mutable column). Retry worker chỉ đăng ký CoreHub host (single PG writer — không cần 3 hosts) + thêm forward `IAccountingDbContext→VanAnDbContext` tại host đó. EnsureBalanced gọi ở `HKDBookRepository.AddAsync/AddToBookAsync` — skip purpose null/`opening-balance`. Flag `AccrualReceivableJE` qua `IShopFeatureSettingsService` (per-tenant toggle có sẵn); accrual chỉ SME/Large/HTX — HKD+SuperSmall skip sớm.
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.A) · Audit: `docs/AI/plans/accounting-engine-srs-v12-phase0-audit.md`
> Gap đóng: **G-C1 · G-C2 · G-C3 · G-C4** (4 CRITICAL) + chuẩn hóa `JournalService` reversal
> ⚠️ **Domain changes** (AccountingEntry + JournalEntry fields/method) → **cần Gate 5 approval trước IMPLEMENT**

---

## 1. MỤC TIÊU

Chốt nền móng posting: idempotency ở persistence layer (reversal-aware), JE cân bằng enforce, divergence observable+tự vá (KHÔNG hard-transaction), DN ghi nhận doanh thu khi bán chịu.

**KHÔNG làm trong phase này:** multi-payment per order (`OrderPayments` → Phase C) · hard-transaction entry↔JE (R2 — không bao giờ) · bảng allocation.

## 2. THIẾT KẾ

### 2.1. Structured source key (M1 — additive)

- `AccountingEntry += SourceEventType` (string?), `SourceEventId` (string?), `PostingPurpose` (string?).
- `JournalEntry += PostingPurpose` (string?).
- Purpose vocabulary: `revenue` `vat` `cogs` `deposit-offset` `receivable` `receivable-payment` `payable` `payable-payment` `manual-receipt` `manual-expense` `opening-balance` `reversal` `closing` `other`.
- Set tại factory/service call sites (OrderService: revenue/vat/cogs/deposit-offset · CongNoService: receivable/payable/payment · bridge: reversal/opening).
- **Migration backfill (SQL):** entries — derive theo Reference pattern + EntryType + AccountCode (order-ref + 511→revenue · 3331→vat · 632/611/612→cogs · DepositOffset→deposit-offset · ref "Manual*"/"-DEPOSIT-OFFSET" tương ứng · null→other); JE — ManualReversal/OpeningBalanceReversal→reversal · OpeningBalance→opening-balance · còn lại→original.

### 2.2. PostingStatus + retry (M2 — R2)

- `AccountingEntry.PostingStatus` int: `Pending=0/Posted=1/Skipped=2/Failed=3` (+ `PostingStatusReason` string? ngắn).
- Bridge `CreateForAccountingEntryAsync`: thành công → Posted · ResolveAccounts=null → Skipped("not-journalable") · exception → Failed (giữ fail-safe return, NHƯNG status ghi nhận — không còn silent).
- `JournalPostingRetryWorker` (BackgroundService): mỗi 60s query Failed/Pending > 30s → retry bridge → max 5 lần → để Failed + reconcile R-check bắt. DI cả 3 hosts.
- Backfill status: Posted nếu `ExistsByReference` JE · Skipped nếu Adjustment/non-journalable · Pending còn lại.

### 2.3. JE balance enforce (G-C2)

- `JournalEntry.EnsureBalanced()` — ΣDr==ΣCr (tol 0.01), throw `ValidationException`.
- Gọi trong `HKDBookRepository.AddToBookAsync`/`AddAsync` trước save — **skip khi `PostingPurpose=="opening-balance"`** (single-line by design) hoặc null (legacy path).
- Domain method mới → Gate 5.

### 2.4. Unique index (M3 — R1)

- `IX_JournalEntries_Tenant_Ref_Purpose` UNIQUE `(TenantId, ReferenceType, ReferenceId, PostingPurpose)` — sau khi M1 backfill xong + pre-scan dup report. `ReferenceId IS NULL` rows: index vẫn OK (PG cho phép multi-NULL) nhưng normalize purpose null→'original' trong backfill cho nhất quán.
- Check-then-act giữ làm fast-path; index là safety net — race → DB reject (AT-017/ACC12-005).

### 2.5. DN receivable JE (G-C4 — flag `AccrualReceivableJE`)

- Bridge `ResolveAccounts` thêm tenant regime (inject resolver/tenant lookup — pattern `IsHtxSellingTenantAsync`):
  - DN (TT133/TT99) + HTX: receivable (131+, purpose `receivable`) → **Nợ 131 / Có 511** (HTX: 512 nếu internal — dùng account hint; default 511/512 external)
  - receivable-payment (131−) → Nợ 111 / Có 131
  - payable-payment (331−) → Nợ 331 / Có 111
  - payable-create (331+) → **Skipped("needs-counter-account")** — không đoán TK hàng mua (SRS §18.4.1.5) → Phase B map
  - HKD/DNSN: giữ nguyên — không JE cho Adjustment (cash-basis)
- Purpose phải được set trước (2.1) để phân biệt create vs payment trong cùng 131/331.
- Flag per-tenant `AccrualReceivableJE` — pilot 1 tenant DN trước. Apply-from-date, KHÔNG backfill.

### 2.6. Chuẩn hóa `JournalService.CreateReversalEntryAsync`

- Set `isReversal: true` + `reversedJournalId` (hiện thiếu — JE đảo qua JournalService không đánh dấu) + `PostingPurpose="reversal"`.

## 3. FILES TẠO/SỬA

- Sửa: `1_Shared/Domain.cs` (AccountingEntry fields) · `1_Shared/Domain/JournalEntry.cs` (PostingPurpose + EnsureBalanced) · `JournalEntryConfiguration`/`AccountingEntryConfiguration` · `ManualEntryJournalBridge` (status + regime ResolveAccounts) · `AccountingEntryService` (set status/purpose) · `OrderService`/`BookingAccountingService`/`CongNoService`/`OpeningBalanceService` (set purpose) · `JournalService` (reversal fields) · `HKDBookRepository` (EnsureBalanced call)
- Tạo: `3_CoreHub/Services/Journal/JournalPostingRetryWorker.cs` · migration PG (+ SQLite nếu mirror) · `AccountingEntryConfiguration` update
- Tests: `VanAn.Core.Tests` — bridge status/purpose/retry · index semantics (integration) · balance enforce · DN accrual ResolveAccounts · reversal normalization

## 4. ACCEPTANCE CRITERIA

- [ ] 2 request đồng thời cùng source event → đúng 1 JE (index reject cái thứ 2, log warning) — ACC12-005/AT-017
- [ ] Retry cùng event → không JE trùng — ACC12-004
- [ ] Bridge fail → entry `PostingStatus=Failed` + worker retry → Posted (không silent) — ACC12-014
- [ ] JE lệch Nợ/Có bị reject trước save (trừ opening-balance purpose) — AT-016
- [ ] Reversal JE qua JournalService có IsReversal+ReversedJournalId+purpose — AT-014
- [ ] Tenant DN + flag ON: phiếu bán chịu → JE Nợ131/Có511; thu nợ → Nợ111/Có131 — ACC12-008
- [ ] Tenant HKD + flag OFF: không JE cho Adjustment (zero regression)
- [ ] Không backfill JE cũ; sổ trước ngày bật flag giữ nguyên
- [ ] guard-check ALL PASSED · build 0 errors · Core.Tests sweep PASS

## 5. TEST CASES (tối thiểu)

- T1 ResolveAccounts DN: receivable→(131,511) · payment→(111,131) · payable-payment→(331,111) · payable-create→Skipped
- T2 HTX: receivable→(131,512) internal / 511 external
- T3 HKD/DNSN: Adjustment → null (không JE) — regression
- T4 PostingStatus: Posted/Skipped/Failed paths + retry worker success + max-retry stop
- T5 EnsureBalanced: lệch → throw · cân → pass · opening single-line → skip
- T6 Unique index: duplicate (tenant,reftype,refid,purpose) → DB error; khác purpose → OK
- T7 Backfill derive: seed mẫu → purpose/status đúng từng nhóm
- T8 JournalService reversal: fields đủ; index không chặn (purpose khác)
