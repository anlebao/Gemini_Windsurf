# TASK CARD F — CLOSING ENTRIES 911 · CHECKLIST ĐÓNG SỔ · RECONCILIATION WORKFLOW · SAO KÊ NH

> Status: 🔵 IN PROGRESS — backend deployed `ea13045a` (CD SUCCESS) + UI done (2026-10-31) — còn RV production
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.F) · Gap đóng: **G-H6 · G-M3 · FR-CLS-003/005 · FR-RECNC-001/005 · KT07-10 · AT-013 · ACC12-017**
> ⚠️ JE closing là bút toán thật ghi vào GL — quy tắc kết chuyển theo từng regime phải được kế toán trưởng duyệt (§16/§18.14 — Q mở: DNSN/HKD không cần kết chuyển 911?).

---

## 1. MỤC TIÊU

Đóng kỳ đủ nghĩa: checklist kiểm tra trước khóa + bút toán kết chuyển tự động đúng chế độ · chênh lệch đối chiếu có workflow xử lý · đối chiếu sao kê ngân hàng (sau BankAccounts Phase D).

## 2. THIẾT KẾ

### 2.1. Closing entries (KT07-09 → purpose `closing`)

- `PeriodClosingService.GenerateClosingEntriesAsync(period, tenant, userId)` — chạy trong `ClosePeriodAsync` SAU ValidatePeriod + TRƯỚC MarkClosed (hoặc job riêng nút "Tạo bút toán kết chuyển" trên PeriodClosing.razor — **UX:** thêm nút = minor UI, chốt với PO):
  - `closing-revenue`: Nợ 511|512|515|711 / Có 911 — Σ phát sinh kỳ per account
  - `closing-expense`: Nợ 911 / Có 632|641|642|635|811 (+HTX 611/612/658)
  - `closing-result`: Nợ 911 / Có 421 (lãi) hoặc Nợ 421 / Có 911 (lỗ)
- Regime-aware: TT99/TT133/TT71 áp; **HKD (TT152) + DNSN (TT58): skip** (chế độ sổ không kết chuyển — xác nhận kế toán trưởng §18.14).
- Qua account-map `closing-*` rules (Phase B nền) hoặc hardcode per regime nếu map chưa có — **quyết định khi IMPLEMENT** (map có sẵn sau B → dùng map).
- Idempotent: purpose `closing` + period-ref `closing:{tenant}:{yyyyMM}` — unique index M3 chặn lặp; đã đóng rồi re-run → skip.
- Reversal khi mở lại kỳ: reopen → đảo closing JEs (purpose `closing-reversal`, link ReversedJournalId) — giữ audit đầy đủ (FR-CLS-006 đã có).

### 2.2. Checklist trước khóa (FR-CLS-003)

- `PeriodClosingService.ValidatePeriodAsync` mở rộng → chạy `IAccountingReconcileService.RunSafeAsync(tenant, period)` + checklist items: JE unbalanced (R1) · entries Failed/Pending posting · công nợ lệch · ngày xấu (R12) · unclassified receipts.
- Kết quả: `errors` (chặn đóng) vs `warnings` (đóng được, ghi audit) — precedent Q1 "cảnh báo không chặn cứng" cho warnings; JE lệch/Failed = error.
- UI PeriodClosing.razor: hiển thị checklist pass/fail trước nút đóng (đọc lại trang hiện có — minor UI change, OK report/ops page).

### 2.3. `ReconciliationItems` workflow (M10 — FR-RECNC-005, G-M3)

- `(Id, TenantId, CheckId[R1-R13], Period, Severity, Description, Status[Open/Investigating/Resolved/Dismissed], AssignedTo?, ResolutionNote?, CreatedAt, ResolvedAt, ResolvedBy)` 
- `AccountingReconcileService`: fail/warn checks → emit/upsert items (dedup per check+period+key); `/admin/reconcile` UI: cột trạng thái + assign + resolve note — minor UI trên trang admin đã có.
- Resolved item khi check pass lần sau → auto-resolve optional.

### 2.4. Sao kê ngân hàng (FR-RECNC-001 — sau D)

- Import CSV sao kê (`Date,Amount,Ref,Desc`) per `BankAccount` → match entries `CashAccountCode=112x` theo Amount+Date±3d+ref → `BankReconciliation` session: matched/unmatched lists → unmatched bank-txn tạo suggestion phiếu (manual xử lý — không auto-post, SRS an toàn).
- Phụ thuộc Phase D BankAccounts; scope tối thiểu: import + match report, KHÔNG auto-create entries.

## 3. FILES TẠO/SỬA

- Tạo: `ReconciliationItem` entity (+config+migration) · `BankStatementImport`/`BankReconciliation` service nhỏ · seeder `closing-*` rules (nếu dùng map)
- Sửa: `PeriodClosingService` (closing entries + checklist) · `PeriodClosing.razor` (checklist display + nút kết chuyển) · `AccountingReconcileService` (emit items + auto-resolve) · `/admin/reconcile` UI (status columns)
- Tests: closing entries per regime · idempotent re-run · reopen reversal · checklist error/warn · items lifecycle · bank match

## 4. ACCEPTANCE CRITERIA

- [x] Đóng kỳ DN → 3 JE kết chuyển đúng TK/cân; HTX 512/611/612/658; HKD/DNSN skip — KT07-09 (backend `ea13045a` + tests 5/5 · RV production pending)
- [x] Re-run closing → không JE trùng; mở kỳ → closing-reversal — AT-013/ACC12-016-tương tự (idempotent purpose+period · reopen → CreateReversal)
- [x] Kỳ có JE lệch/Failed → chặn đóng + lý do; warnings → đóng được + audit — FR-CLS-003 (ValidatePeriodAsync → reconcile checklist; UI hiển thị errors/warnings sẵn có)
- [x] Chênh lệch → item Open → assign → Resolved + audit trail — FR-RECNC-005 (entity+service `ea13045a` · UI `/admin/reconcile` items section 2026-10-31)
- [x] Import sao kê → matched/unmatched đúng; unmatched không tự ghi sổ — FR-RECNC-001 (BankStatementService `ea13045a` · UI BankBook 2026-10-31)
- [x] Nút "Tạo bút toán kết chuyển" trên PeriodClosing.razor (period Open → GenerateClosingEntriesAsync + feedback)
- [x] guard + build + sweep PASS (guard-check ALL PASSED 2026-10-31 · ShopERP.Tests 28/28 targeted)
- [ ] RV production: migration `AddClosingReconcileW5` applied + đóng/gen-closing thật trên tenant DN → verify 3 JEs

## 5. TEST CASES

- T1 DN: 511 doanh thu 100 + 642 chi 40 → closing JEs: (511→911 100), (911→642 40), (911→421 60 lãi)
- T2 HTX: 512/611/612/658 map đúng; lỗ → Nợ 421/Có 911
- T3 HKD/DNSN: ClosePeriod không tạo closing JE
- T4 Idempotent + reopen → reversal closing
- T5 Checklist: Failed-posting → error chặn; unclassified → warn pass
- T6 Items: create→assign→resolve; re-run pass → auto-resolve
- T7 Bank CSV: 10 rows → 8 matched / 2 unmatched (amount±date+ref)
