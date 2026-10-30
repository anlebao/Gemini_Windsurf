# PHASE 0 AUDIT — SHOPERP ACCOUNTING ENGINE (SRS v1.2)

> **Nguồn:** `docs/specs/SRS_ShopERP_Accounting_Engine_v1.2.md` (§15 + §18.13).
> **Mode:** READ-ONLY audit — không sửa code/schema/production. Dừng sau báo cáo, chờ PO duyệt (Gate 0→1).
> **Ngày:** 2026-10-30 · **Branch:** main @ `e3b39323`
> **PO decisions đã chốt (2026-10-30):** D1 SRS áp dụng **mọi regime** (HKD TT152 · DN TT133/TT99 · HTX TT71 · DNSN TT58) · D2 **auto-post** (không workflow duyệt) · D3 **KEEP 2-layer** `AccountingEntry` (subledger/business-event) + `JournalEntry` (GL) · D4 multi-branch/kho/quỹ **DEFER**.

---

## 1. ARCHITECTURE ASSESSMENT

### 1.1. Kiến trúc kế toán hiện tại

```
Orders (KhachLink→Gateway→NATS→ShopERP, PG = source of truth)
   │ payment-confirm → OrderService.GenerateAccountingEntriesAsync
   │   · idempotent: ExistsByReferenceAsync(tenant, order.Id.ToString())
   │   · entries: revenue 511/512 (net−cọc) · DepositOffset 3387→511
   │              · VAT 3331 (EntryType=Revenue) · COGS 632/611/612
   ▼
AccountingEntry  (IMMUTABLE append-only — PG, Gateway)
   │ fields: Amount · EntryType{Revenue,Expense,TaxPayment,Adjustment,DepositOffset}
   │         VatRate · TransactionDate · BookType · PeriodY/M
   │         ReferenceType/ReferenceId · AccountCode · Vendor · Category · CorrelationId
   │ dedup: CheckDuplicateEntryAsync (5-min window + reference match — code-level)
   │ period guard: PeriodClosingService (PeriodClosingStatusEntity, DB-persisted)
   ▼
ManualEntryJournalBridge  (single source cho JE; fail-safe — lỗi không throw)
   │ ResolveAccounts hardcode: Revenue 5xx/7xx → Nợ 111/Có TK · Expense 6xx → Nợ TK/Có 111
   │                         DepositOffset → Nợ 3387/Có TK · còn lại → null (không JE)
   │ idempotent: ExistsByReferenceAsync("ManualEntry"/"ManualReversal"/"OpeningBalance"/..., entry.Id)
   ▼
JournalEntry + JournalEntryLine (GL — PG)
   · JournalNo unique · EntryDate · Period (record, unmapped — Pattern #5)
   · ReferenceType/ReferenceId · IsReversal/ReversedJournalId
   · AddLine validate: account format, không âm, không cả Nợ lẫn Có — KHÔNG check cân bằng
   · persist: AddToBookAsync → 1 SaveChanges (không atomic với entry SaveChanges)
```

### 1.2. Hai biểu mẫu + điểm phát sinh (baseline UX — đã verify trên code)

| Điểm thao tác | Hiện trạng verify | Phù hợp SRS §18.1 |
|---|---|---|
| Phiếu thu | `RevenueEntry.razor` — select "Loại Phiếu Thu" 3 option (doanh thu / ghi nhận phải thu 131 / thu nợ 131−) + đối tượng + MST tra cứu + DynamicFormFields; HTX in 01-TT | Có select loại phiếu (thêm từ ThuChi MVP — hợp lý, KHÔNG phải dropdown mã nghiệp vụ bắt buộc) |
| Phiếu chi | `ExpenseEntry.razor` — mirror (chi phí / phải trả 331 / trả nợ); HTX in 02-TT | ✓ |
| Orders | `GenerateAccountingEntriesAsync` tại payment-confirm (NATS PaymentConfirmedSubscriber hoặc WalletService) | ✓ — điểm phát sinh duy nhất, booking mirror `BookingAccountingService` |

### 1.3. Thành phần kế toán đã có (inventory)

| Thành phần | File | Trạng thái |
|---|---|---|
| Entries immutable | `1_Shared/Domain.cs:315` `AccountingEntry` | LIVE |
| GL journal | `1_Shared/Domain/JournalEntry.cs` + `JournalTemplate.cs` | JE LIVE · **Template DORMANT** (entity+repo+config tồn tại nhưng posting path không gọi — `EnhancedJournalFactory` không có call site ngoài test) |
| Bridge entries→JE | `Services/Journal/ManualEntryJournalBridge.cs` | LIVE |
| COA | `AccountChartEntity` — **global per Standard** (75 TT99 / 51 TT133 / 89 TT71), KHÔNG per-tenant | LIVE (reference data) |
| Period closing | `PeriodClosingService` + `PeriodClosingStatusEntity` + `PeriodClosing.razor` — validate, close, reopen role-gated (Owner/ChiefAccountant/SystemAdmin) + audit | LIVE |
| AR/AP subledger | `CongNoService` — 131/331 + đối tượng (free string) + FIFO aging + sổ đối tượng + lịch sử thanh toán | LIVE |
| Reversal | `ReversalService` + `AccountingEntry` reversal + bridge JE reversal | LIVE |
| Refund | `RefundOrchestrationService` (order-level, loyalty+reversal orchestration) | LIVE |
| Reconcile | `AccountingReconcileService` R1-R12 + `AccountClassifier` + UI `/admin/reconcile` | LIVE (deploy 2026-10-30) |
| Audit | `AuditTrailService` + `AuditLog` + `AuditLogBackgroundWriter` | LIVE |
| Reports | JournalReport · Ledger (sổ cái) · SubLedger (sổ chi tiết TK) · TrialBalance · B01/B02/B03/B09 ×4 regime · CongNoReport/Ledger · HKDBooks · DnsnBooks · TransactionHistory · OpeningBalance · CashPaymentReview · AccountBalance | LIVE |
| Compliance | `BusinessRuleRegistry` (hardcoded validation rules: VAT/CIT/PIT/PeriodClosing/Standard) | LIVE (validation only — KHÔNG phải posting rule) |
| Payment guard | `PaymentComplianceService` (cảnh báo >77tr + lý do bắt buộc) | LIVE |

### 1.4. Thành phần KHÔNG có

- AccountingRule engine (event→rule→posting plan; versioning; effective dates; conditions)
- COA per-tenant + TK chi tiết (parent/child) + status + effective dates
- Voucher numbering (số chứng từ tuần tự theo loại/tenant)
- PaymentAllocation entity (phân bổ explicit per invoice) + unapplied balance tracking
- Bank accounts (112 detail), internal transfer (CK01-05), cash balance check
- Purchase docs (PO/GRN/hóa đơn mua), inventory GL-integrated subledger
- Reconciliation work-items (chênh lệch có trạng thái/người xử lý)
- Attachments trên chứng từ · đa tiền tệ (DEFER) · approval workflow (DEFER theo D2)
- Closing entries tự động (kết chuyển 911 KT07-09)

---

## 2. ACCOUNTING FLOW AUDIT (truy vết thực tế)

### F1. Bán hàng POS (thu tiền ngay) — EXISTING

`Order create → (no entry) → paid → GenerateAccountingEntriesAsync → entries{rev net 511, VAT 3331, COGS 632, offset 3387 nếu cọc} → bridge → JE×n`. Idempotent by orderRef. **Deviation vs SRS:** PaymentReceived = RevenueRecognized cùng instant (cash-basis POS — chấp nhận được cho retail immediate-sale; nhưng cần event-level key cho multi-payment — xem G-C1).

### F2. Booking có cọc — EXISTING (đúng §18.2)

Cọc → 3387 (không doanh thu) → thanh toán cuối → revenue = net−cọc (cash) + offset 3387→511. **Đã verify production 2026-10-29.**

### F3. Phiếu thu — EXISTING (PARTIAL)

3 loại: doanh thu → `CreateRevenueEntryAsync` + JE Nợ111/Có5xx · bán chịu → `CreateReceivableAsync` (131+, Adjustment, CashBankBook, KHÔNG JE) · thu nợ → receivable (−, FIFO theo đối tượng string). **Deviation:** thu nợ tạo entry Adjustment — không có JE Nợ111/Có131 (bridge ResolveAccounts → null cho Adjustment). Sổ chi tiết công nợ đúng; **sổ cái thiếu bút toán thu nợ** → G-H4.

### F4. Phiếu chi — mirror F3 (PARTIAL, cùng vấn đề JE thiếu cho 331−)

### F5. Bán chịu cho DN (TT133/TT99) — **INCORRECT (G-C4)**

Q1 (2026-10-06, tiền-SRS): "bán chịu không ghi 511" → `CreateReceivableAsync` = Adjustment, không JE doanh thu. Đúng cho HKD (kê khai) nhưng **sai cho DN accrual**: bán chịu phải Nợ 131/Có 511 + thuế. Regime-aware logic hiện không phân biệt → SRS BH02/FR-SAL-002 vi phạm với DN.

### F6. Mua hàng — MISSING (chỉ phiếu chi tay + phải trả 331)

Không PO/GRN/hóa đơn mua/trả hàng/CKTM/chi phí mua. FR-PUR-* phần lớn MISSING.

### F7. Kho — PARTIAL (ngoài GL)

Ingredients stock IN/OUT + audit (ShopERP SQLite) — không link GL; COGS per-order qua `CalculateCogsAmount` (fixed recipe cost), không theo phương pháp giá xuất kho configurable. FR-SUB-003/FR-RECNC-004 MISSING phần kế toán.

### F8. Đảo/reversal — EXISTING

Entry reversal (ReversalEntryId) + JE reversal (ManualReversal + IsReversal + ReversedJournalId) + reopen period = reversal pattern (không mở kỳ trực tiếp). Đúng §18.8 phần full-reversal; partial reversal MISSING.

### F9. Kỳ kế toán — PARTIAL→EXISTING core

`PeriodClosingStatusEntity` per tenant+period; guard trong `Create*EntryAsync` chặn kỳ đóng; reopen role-gated + audit + preview. Thiếu: closing entries 911 + checklist sâu. **JE-level không tự check kỳ** (đi qua entry guard — transitively covered, nhưng `AddToBookAsync` có thể gọi trực tiếp bypass).

### F10. Idempotency — PARTIAL (G-C1/G-C2)

- Entries: `ExistsByReferenceAsync(tenant, orderRef)` — orderRef = `order.Id.ToString()` → **OrderId-as-key** (SRS §18.3 cấm). Payment thứ 2 cùng order → **bị skip** (vi §18.3.1.2/3).
- JE: `ExistsByReferenceAsync(tenant, refType, entry.Id)` — check-then-act, **race window** (2 request đồng thời → 2 JE; không unique constraint trên (TenantId, ReferenceType, ReferenceId)).
- Fail-safe bridge: entry lưu OK, JE lỗi → nuốt exception → entry không JE (reconcile R-check phát hiện, không tự vá).

---

## 3. REQUIREMENTS TRACEABILITY MATRIX

Nhãn: **KEEP** · **FIX** (sai/lỗi) · **EXTEND** (nền có, thiếu logic) · **ADD** (chưa có) · **ADD-UX** · **DEFER** · **UNKNOWN**

### 3.1. COA — `FR-COA-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-COA-001 COA theo chế độ | PARTIAL | `AccountChartEntity` global/Standard (75/51/89) + `AccountChartSeeder` + `AccountChartService` | EXTEND — giữ global chart, thêm lớp tenant |
| FR-COA-002 COA độc lập per tenant | MISSING | Chart global, không tenant-scoped entity | ADD — `TenantAccountChart` (override: thêm TK chi tiết, disable) |
| FR-COA-003 mã/tên/loại/dư Nợ-Có/status/detail | PARTIAL | `AccountChartEntity`: code/name/Type/IsNormalCredit có; **không status, không detail-tracking attrs** | EXTEND |
| FR-COA-004 TK tổng hợp + chi tiết | MISSING | Flat; entries dùng 4-số (6421/6422) nhưng không hierarchy | ADD `ParentCode` + tree resolve |
| FR-COA-005 không xóa TK đã phát sinh | MISSING | Không có tenant COA để xóa | theo COA-002 (rule: chỉ deactivate) |
| FR-COA-006 hiệu lực + lịch sử | MISSING | `CreatedAt` only | ADD effectiveFrom/audit |

### 3.2. Chứng từ — `FR-VCH-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-VCH-001 voucher type cấu hình | PARTIAL | `AccountingEntryType` 5 giá trị (enum code); select 3 loại trên form | EXTEND — BusinessEventCode field (string) thay enum mở rộng |
| FR-VCH-002 1 form nhiều nghiệp vụ | PARTIAL | RevenueEntry/ExpenseEntry 3 loại mỗi bên | KEEP UX + EXTEND backend mapping |
| FR-VCH-003 loại/PTTT/đối tượng/CT nguồn/TK tiền | PARTIAL | EntryType + Vendor(string) + AccountCode + Reference có; **thiếu PaymentMethod, TK tiền đích (luôn 111 implicit)** | EXTEND `PaymentMethod` + `CashAccountCode` nullable |
| FR-VCH-004 số chứng từ duy nhất | MISSING | `SoPhieu` = reference string ad-hoc (in phiếu TT71), không sequence | ADD `VoucherNumber` per (tenant,type,year) + unique index |
| FR-VCH-005 trạng thái CT | PARTIAL | Auto-post (D2): Posted implicit; IsReversal có; không Draft | KEEP theo D2 — chỉ cần Posted/Reversed (đã có) |
| FR-VCH-006 người tạo/duyệt/ghi sổ | PARTIAL | `AuditTrailService.LogCreateAsync` actor+time; không field trên entry | KEEP (audit log đủ theo auto-post) |
| FR-VCH-007 đính kèm | MISSING | Không attachment entity cho CT kế toán | ADD `Attachment` (link R2/storage hiện có) — P1/P2 |

### 3.3. Thu — `FR-REC-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-REC-001 form thu thống nhất | EXISTING | `RevenueEntry.razor` | KEEP |
| FR-REC-002 suy luận loại từ nguồn | PARTIAL | Order → auto; phiếu tay → user chọn 1 trong 3 (hợp lý theo §18.4.1) | KEEP + EXTEND inference cho linked receipts |
| FR-REC-003 PTTT cash/CK/QR | PARTIAL | Order có payment method; phiếu tay không field PTTT | EXTEND `PaymentMethod` |
| FR-REC-004 thu nợ chọn nhiều HĐ + số tiền | PARTIAL | CongNo FIFO auto theo đối tượng — không explicit per-invoice | EXTEND `PaymentAllocation` |
| FR-REC-005 thu 1 phần/nhiều lần/gộp/thừa | PARTIAL | FIFO multi-pay OK; **overpay → chưa verify unapplied balance** | ADD allocation + unapplied (§18.7) |
| FR-REC-006 ứng trước theo KH/HĐ/đơn | PARTIAL | Booking deposit 3387 ✓ per order; generic advance 131 qua CongNo | EXTEND — `AdvanceType` + object link |
| FR-REC-007 liên kết đơn, chặn DT lần 2 | EXISTING | orderRef idempotency + entry ReferenceId | KEEP (nâng cấp key — G-C1) |
| FR-REC-008 thu chưa rõ → chờ phân loại | MISSING | Không pending-classify state | ADD `UnclassifiedReceipt` handling (hoặc rule fallback — P1) |
| FR-REC-009 validate TK/ngày/đối tượng/kỳ | EXISTING | Period guard + dup check + required đối tượng (công nợ) | KEEP |
| FR-REC-010 CT đã ghi sổ không sửa/xóa | EXISTING | `AccountingEntry` immutable + reversal | KEEP |

### 3.4. Chi — `FR-PAY-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-PAY-001 form chi thống nhất | EXISTING | `ExpenseEntry.razor` | KEEP |
| FR-PAY-002 trả 1 phần/toàn bộ nhiều khoản | PARTIAL | CongNo FIFO | EXTEND allocation như FR-REC-004 |
| FR-PAY-003 ứng trước NCC theo đối tượng | PARTIAL | 331 detail qua CongNo (+Vendor) | EXTEND advance semantics |
| FR-PAY-004 hoàn tiền liên kết gốc | PARTIAL | `RefundOrchestrationService` (order) | EXTEND link cho phiếu tay |
| FR-PAY-005 tiền chi ≠ chi phí | EXISTING | `CreateDebt`/Adjustment tách; ResolveAccounts null cho non-expense | KEEP |
| FR-PAY-006 kiểm số dư quỹ | PARTIAL | `PaymentComplianceService` (cảnh báo 77tr); **không balance-check 111** | EXTEND cash-balance guard (warn-level) |
| FR-PAY-007 chi đã ghi sổ → đảo/điều chỉnh | EXISTING | reversal pattern | KEEP |

### 3.5. Chuyển tiền nội bộ — `FR-TRF-*` → tất cả MISSING

CK01-05 chưa có (không bank accounts, không transfer doc). **ADD** — phụ thuộc cash/bank account model (G-M1). FR-TRF-005 (không ảnh hưởng tổng tài sản) đạt by-design nếu implement đúng.

### 3.6. Mua hàng — `FR-PUR-*` → phần lớn MISSING

| Req | Status | Action |
|---|---|---|
| FR-PUR-001 tách đơn/nhận/HĐ/TT | MISSING | ADD purchase doc chain (Phase E) |
| FR-PUR-002 trả ngay + công nợ | PARTIAL | ExpenseEntry + 331 CongNo — giữ, link source |
| FR-PUR-003/004 nhận nhiều lần, HĐ trước/sau | MISSING | ADD |
| FR-PUR-005 trả/giảm/CKTM | MISSING | ADD |
| FR-PUR-006 phân bổ chi phí mua | MISSING | ADD |
| FR-PUR-007 chống trùng sync | PARTIAL | reference dedup generic — reuse G-C1 fix |

### 3.7. Bán hàng — `FR-SAL-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-SAL-001 thu ngay/chịu/mixed | PARTIAL | paid + công nợ tay; không mixed-tender | EXTEND multi-payment per order |
| FR-SAL-002 DT theo điều kiện, không chỉ tiền | **INCORRECT (DN)** | F5 — DN bán chịu không ghi 511 | FIX regime-aware (HKD giữ, DN ghi 131/511) |
| FR-SAL-003 COGS theo method cấu hình | PARTIAL | `CalculateCogsAmount` fixed recipe cost | EXTEND costing-policy field |
| FR-SAL-004 trả 1 phần/nhiều lần/thừa | PARTIAL | như FR-REC-005 | ADD allocation |
| FR-SAL-005 trả hàng/giảm giá/CK/hoàn link gốc | PARTIAL | `RefundOrchestrationService` order-level | EXTEND partial + return-doc |
| FR-SAL-006 nhiều lần giao/HĐ/TT per order | MISSING | order paid-once model | ADD (thuộc multi-payment G-C1) |
| FR-SAL-007 trả nợ không DT lần 2 | EXISTING | CongNo = Adjustment | KEEP |
| FR-SAL-008 đối chiếu per order | PARTIAL | Reconcile R2/R3/R10 + drill-down | EXTEND coverage |

### 3.8. Cuối kỳ — `FR-CLS-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-CLS-001 quyền tạo/duyệt điều chỉnh | EXISTING | ReopenApproverRoles + `AccountingAccess` policy | KEEP |
| FR-CLS-002 lịch kỳ + trạng thái | PARTIAL | `PeriodClosingStatusEntity` (derived period, open/closed) | EXTEND calendar (period explicit rows) |
| FR-CLS-003 checklist trước khóa | PARTIAL | `ValidatePeriodAsync` (checks tối thiểu) | EXTEND — reuse Reconcile R-checks làm checklist |
| FR-CLS-004 chặn ghi kỳ khóa | EXISTING | guard `Create*EntryAsync` + reopen log | KEEP (+ add JE-level guard defense) |
| FR-CLS-005 bút toán kết chuyển | MISSING | Không KT07/08/09 | ADD closing-entry generator (911) |
| FR-CLS-006 mở kỳ lưu ai/khi/lý do/phạm vi | EXISTING | `PreviewReopenAsync` + audit + role | KEEP |

### 3.9. Rule Engine — `FR-RULE-*` → phần lớn MISSING (skeleton dormant)

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-RULE-001 chọn rule theo event+điều kiện | MISSING | `ResolveAccounts` hardcode + accounts hardcode `OrderService` | ADD rule resolution layer |
| FR-RULE-002 cấu hình Nợ/Có/điều kiện/công thức | PARTIAL | `JournalTemplate` entity (lines+AmountFormula) — **dormant** | REVIVE hoặc `AccountingRule` mới trên nền template |
| FR-RULE-003 hiệu lực + không hồi tố | MISSING | — | ADD EffectiveFrom + rule snapshot trên JE |
| FR-RULE-004 điều kiện tenant/hàng/đối tượng/PTTT/thuế | MISSING | — | ADD condition model |
| FR-RULE-005 kiểm thiếu TK/trùng/xung đột | MISSING | `AccountCodeValidator` chỉ validate format | EXTEND rule validation |
| FR-RULE-006 preview | MISSING | — | ADD (P1, backend preview API trước) |
| FR-RULE-007 version + audit + duyệt | MISSING | — | ADD versioned rules |
| FR-RULE-008 chặn config mất cân | MISSING | JE không enforce balance | phụ thuộc G-C2 |

### 3.10. Journal Entry — `FR-JE-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-JE-001 mã/ngày/kỳ/nguồn/diễn giải/trạng thái | PARTIAL | JournalNo unique + EntryDate + Period(record) + Ref + Desc; status implicit (auto-post OK) | KEEP + EXTEND source-event fields |
| FR-JE-002 Nợ=Có | PARTIAL | by-construction (bridge 2-line); **không enforce** | FIX — domain check `ΣNợ==ΣCó` khi finalize |
| FR-JE-003 dòng: TK/tiền/chiều/chiều phân tích | PARTIAL | account+Dr+Cr+desc; không object/dimension | EXTEND `ObjectRef` (đối tượng) |
| FR-JE-004 tham chiếu CT/event nguồn | EXISTING | ReferenceType+ReferenceId | KEEP (+ structured — G-C1) |
| FR-JE-005 posting nguyên tử | PARTIAL | entry SaveChanges ≠ JE SaveChanges; fail-safe | FIX — same UoW/transaction hoặc outbox repair |
| FR-JE-006 idempotency key | PARTIAL | ExistsByReference check-then-act, race window | FIX — unique index (Tenant,RefType,RefId) + structured key §18.3.2 |
| FR-JE-007 JE ghi sổ không sửa/xóa | EXISTING | immutable + reversal | KEEP |
| FR-JE-008 đảo toàn phần/1 phần | PARTIAL | full reversal ✓; partial ✗ | EXTEND partial reversal |
| FR-JE-009 diễn giải/đính kèm/liên kết | PARTIAL | desc+ref; attachment ✗ | theo FR-VCH-007 |
| FR-JE-010 đa tiền tệ | MISSING | — | DEFER (P2, chưa bật) |

### 3.11. Sổ chi tiết & đối chiếu — `FR-SUB-*`, `FR-RECNC-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-SUB-001 sổ chi tiết TM/TGNH | PARTIAL | `SubLedger.razor` per TK + CashBankBook; không bank-account entity | EXTEND bank accounts + sổ quỹ/TGNH report |
| FR-SUB-002 AR/AP theo đối tượng+CT | EXISTING | CongNo 131/331 + đối tượng + ledger + lịch sử | KEEP (+ link đối tượng → Customer/Vendor entity — G-M5) |
| FR-SUB-003 kho SL+GT | PARTIAL | ingredients stock+audit (SQLite); không integrate GL | EXTEND — inventory valuation + reconcile hook |
| FR-SUB-004 chi tiết ↔ tổng hợp | PARTIAL | Reconcile R1-R12 (entries↔JE↔orders↔balance) | EXTEND coverage thêm subledgers |
| FR-RECNC-001 sao kê NH | MISSING | — | ADD (Phase F, sau bank accounts) |
| FR-RECNC-002 phát hiện trùng/lệch | PARTIAL | Reconcile R-checks + 5-min dup | EXTEND |
| FR-RECNC-003 đối chiếu công nợ | PARTIAL | CongNo + OpeningBalanceService; reconcile chưa cover AR/AP sub-ledger vs GL | EXTEND R-check AR/AP |
| FR-RECNC-004 tồn kho ↔ GL | MISSING | — | ADD sau F7 |
| FR-RECNC-005 chênh lệch có trạng thái/người XL | MISSING | Reconcile = suggestions only | ADD ReconciliationItem work-flow |

### 3.12. Báo cáo — `FR-RPT-*`

| Req | Status | Evidence | Action |
|---|---|---|---|
| FR-RPT-001 nhật ký chung | EXISTING | `JournalReport.razor` + filters | KEEP |
| FR-RPT-002 sổ cái | EXISTING | `Ledger.razor` | KEEP |
| FR-RPT-003 sổ quỹ | PARTIAL | SubLedger TK111 + CashBankBook | EXTEND dedicated format |
| FR-RPT-004 sổ TGNH | PARTIAL | như trên, không bank accounts | theo G-M1 |
| FR-RPT-005/006 công nợ thu/trả + tuổi nợ | EXISTING | `CongNoReport` aging <30/60/90/>90 FIFO | KEEP |
| FR-RPT-007 NXT | PARTIAL | `InventoryExcelReport` | EXTEND valuation-linked |
| FR-RPT-008 DT+giá vốn | PARTIAL | `RevenueExcelReport` + dashboard | EXTEND dimensions |
| FR-RPT-009 CDPS | EXISTING | `TrialBalance.razor` | KEEP |
| FR-RPT-010 BCTC theo regime | EXISTING | B01a/B02/B03/B09 × TT99/TT133/TT58/TT71 | KEEP |
| FR-RPT-011 LCTT | EXISTING | `CashFlowStatement` | KEEP |
| FR-RPT-012 thuế/đối chiếu thuế | PARTIAL | `HKDTaxReportingService` (HKD); DN: 3331 tracked by account | EXTEND DN tax view |

### 3.13. NFR

| Req | Status | Evidence | Action |
|---|---|---|---|
| NFR-001 không JE mất cân/thiếu TK | PARTIAL | bridge by-construction; không enforce | FIX (G-C2) |
| NFR-002 JE+sổ nguyên tử | PARTIAL | separate SaveChanges + fail-safe | FIX (G-C3) |
| NFR-003 lặp event ≠ trùng | PARTIAL | ExistsByReference + 5-min; race | FIX (G-C1) |
| NFR-004 cô lập tenant server-side | EXISTING | IMustHaveTenant + filters + policies | KEEP |
| NFR-005 audit không xóa | EXISTING | AuditTrail + AuditLog bg writer | KEEP |
| NFR-006 phân trang/lọc/tổng hợp | PARTIAL | period-scoped; Orders paginated; reports chưa verify tải lớn | EXTEND pagination nếu cần |
| NFR-007 multi-tenant/branch/kho | PARTIAL | tenant ✓; branch/kho | DEFER (D4) |
| NFR-008 backup/phục hồi | UNKNOWN | infra-level, chưa audit | verify ops-level |
| NFR-009 testability | EXISTING | Core.Tests 2481 + ShopERP.Tests + bUnit | KEEP |
| NFR-010 tương thích dữ liệu/API | EXISTING | additive-only + migrations | KEEP |
| NFR-011 correlation ID | PARTIAL | `CorrelationId` trên entry + audit | EXTEND end-to-end trace |
| NFR-012 decimal + làm tròn | EXISTING | `HasPrecision(18,2)`, Money VO | KEEP |

### 3.14. §12 Control rules — trạng thái nhanh

| Rule | Status |
|---|---|
| 1 cân bằng | PARTIAL (không enforce — G-C2) |
| 2 thiếu TK/đối tượng | PARTIAL (account format validate; missing-rule → silent null — cần observable error) |
| 3 kỳ khóa | EXISTING (entry-level) |
| 4 trùng source+rule | PARTIAL (G-C1) |
| 5/6 phân bổ vượt | PARTIAL (FIFO tự nhiên giới hạn; overpay→unapplied chưa verify) |
| 7/8/9 tiền≠DT/CP, chuyển tiền | EXISTING (Adjustment/CashBankBook; transfer chưa có → N/A) |
| 10 không xóa lịch sử | EXISTING |
| 11 rule thay đổi không hồi tố | N/A (chưa có rule engine — sẽ enforce khi build) |
| 12 rollback giữa chừng | PARTIAL (fail-safe swallow — G-C3) |

### 3.15. §18.3.1 Idempotency behaviors

| # | Behavior | Status |
|---|---|---|
| 1 | retry cùng event | ✓ ExistsByReference (race window — G-C2) |
| 2 | 2 payment khác nhau cùng order | **✗** — orderRef key chặn payment thứ 2 (G-C1) |
| 3 | partial payment không bị skip | **✗** — cùng nguyên nhân |
| 4 | 2 worker đồng thời | **✗** — check-then-act race, không unique constraint |
| 5 | fail giữa chừng không nửa posting | PARTIAL — fail-safe để entry-thiếu-JE |
| 6 | payload đổi cùng event ID → phát hiện | MISSING |
| 7 | reversal là event riêng | ✓ ManualReversal ref type riêng |

---

## 4. DATA INTEGRITY ASSESSMENT

| Khía cạnh | Đánh giá |
|---|---|
| Duplicate posting | **RỦI RO CAO** — orderId-as-key chặn multi-payment; race window JE (không unique constraint); 5-min heuristic cho phiếu tay |
| Transaction boundary | **RỦI RO** — entry + JE 2 SaveChanges riêng; bridge fail-safe → silent divergence (reconcile phát hiện ex-post) |
| Tenant isolation | TỐT — IMustHaveTenant + query filters + VO compare + server policies |
| Decimal precision | TỐT — decimal 18,2 + Money VO |
| Audit trail | TỐT — AuditTrailService + AuditLog + correlationId |
| Rollback/migration | TỐT — additive-only + EF migrations + backup precedent (jnl_backup_*) |
| Truy vết | TỐT — ReferenceType/Id + CorrelationId chain Order→Entry→JE |

---

## 5. GAP & RISK REGISTER

| ID | Gap | Mức | Ảnh hưởng |
|---|---|---|---|
| G-C1 | Idempotency key = OrderId-level → chặn multi/partial payment, refund thứ 2 cùng order; race window (không unique constraint) | **CRITICAL** | Vi phạm §18.3 core; mất bút toán hoặc trùng |
| G-C2 | JE không enforce ΣNợ=ΣCó (domain/DB) | **CRITICAL** | NFR-001/§12.1 — rule engine tương lai có thể tạo JE lệch |
| G-C3 | Entry↔JE không atomic; fail-safe nuốt lỗi → entry không JE | **CRITICAL** | Divergence subledger-GL thầm lặng |
| G-C4 | DN bán chịu không ghi 511 (HKD-semantics áp cho DN) | **CRITICAL** | Sai nghiệp vụ accrual DN TT133/TT99 (FR-SAL-002, BH02) |
| G-H1 | Không AccountingRule engine (accounts hardcode; JournalTemplate dormant) | HIGH | FR-RULE-* toàn bộ; không cấu hình được |
| G-H2 | Không COA per-tenant/chi tiết/status/hiệu lực | HIGH | FR-COA-002..006 |
| G-H3 | Không voucher numbering tuần tự; không attachments | HIGH | FR-VCH-004/007; truy vết chứng từ |
| G-H4 | PaymentAllocation thiếu; thu/trả nợ không tạo JE (Nợ111/Có131) → sổ cái thiếu; unapplied balance chưa có | HIGH | §18.7, FR-REC-004/005, GL incompleteness |
| G-H5 | Purchase/inventory doc flows thiếu (PO/GRN/HĐ mua/trả hàng/CKTM) | HIGH | FR-PUR-*, MH01-10 |
| G-H6 | Closing entries (kết chuyển 911) thiếu; checklist nông | HIGH | FR-CLS-003/005, KT07-10 |
| G-M1 | Không bank accounts (112 detail)/cash-account model → chuyển tiền nội bộ, sổ quỹ/TGNH đúng nghĩa | MEDIUM | FR-TRF-*, FR-RPT-003/004, FR-SUB-001 |
| G-M2 | Sổ quỹ/TGNH dedicated format thiếu | MEDIUM | FR-RPT-003/004 |
| G-M3 | Reconcile chênh lệch = suggestions, không work-item | MEDIUM | FR-RECNC-005 |
| G-M4 | PaymentMethod field thiếu trên phiếu tay | MEDIUM | FR-VCH-003, FR-REC-003 |
| G-M5 | Đối tượng = free string, không link Customer/Vendor entity | MEDIUM | Data quality, dedup đối tượng |
| G-M6 | Rule/COA change audit (khi build) | MEDIUM | NFR-005 coverage |
| G-M7 | Reports pagination chưa verify (NFR-006) | MEDIUM | perf kỳ lớn |
| G-L1 | VAT entry EntryType=Revenue (semantic imperfection — code đã ghi chú) | LOW | phân loại báo cáo |
| G-L2 | JournalEntry dual-identity (`Id` PK vs `JournalEntryId` business key + unique index) — kiểm tra single-identity rule | LOW | consistency |
| G-L3 | `Voucher` (loyalty) va tên với "Voucher" kế toán SRS | LOW | thuật ngữ |
| G-L4 | `AccountingPeriod` record unmapped (Pattern #5 workaround EntryDate) | LOW | tech debt |

---

## 6. TARGET ARCHITECTURE PROPOSAL

```
                    ┌──────────────── KEEP ────────────────┐
Orders/POS ──┐      │ AccountingEntry (subledger+event trace)│
Phiếu thu ───┼─────▶│   + StructuredSourceRef (NEW)           │──┐
Phiếu chi ───┘      │ JournalEntry/Line (GL)                │  │ cùng
Booking ───────────▶│ CongNo (AR/AP) · PeriodClosing ·       │◀─┘ UoW/txn
Inventory ─────────▶│ Reconcile R1-R12 · AuditTrail          │
                    └───────────────────────────────────────┘
        ┌── ADD ─────────────────────────────────────────────────┐
        │ AccountingRule (per-tenant, versioned, effectiveFrom)   │
        │  → revive trên nền JournalTemplate HOẶC entity mới      │
        │ PostingEngine: resolve rule → posting plan → JE atomic  │
        │ IdempotencyKey entity (Tenant,Module,EventId,Type,Purpose)│
        │ VoucherSequence · PaymentAllocation · BankAccount        │
        │ (DEFER: multi-branch/kho/quỹ, đa tiền tệ, approval flow) │
        └────────────────────────────────────────────────────────┘
```

Nguyên tắc: giữ 2-layer (D3); hardcode accounts → migrate dần sang rule có default seed theo regime; idempotency làm trước (nền móng); mọi thay đổi additive-first.

## 7. DATABASE MIGRATION PLAN — v2 sau phản biện (§11)

| # | Change | Type | Backfill | Rollback |
|---|---|---|---|---|
| M1 | `AccountingEntries.SourceEventType/SourceEventId/PostingPurpose` + `JournalEntries.PostingPurpose` (nullable) | add | purpose derive từ RefType/EntryType/AccountCode (order→revenue/vat/cogs/deposit-offset · ManualReversal→reversal · còn lại→original) | giữ cột, không dùng |
| M2 | `AccountingEntries.PostingStatus` (Pending/Posted/Skipped/Failed) | add | 'posted' nếu có JE ref-entry · 'skipped' cho công nợ/Adjustment · 'pending' còn lại | — |
| M3 | Unique index `JournalEntries(TenantId,ReferenceType,ReferenceId,PostingPurpose)` — **SAU M1** (reversal cần purpose riêng — R1) | add index | pre-scan dup bắt buộc | drop index |
| M4 | `TenantAccountCharts` (COA override) + `AccountChartEntity.ParentCode/EffectiveFrom` | new+add | lazy copy global | drop/keep null |
| M5 | `AccountMappingRules` (event-code × regime → TK Nợ/Có, versioned, EffectiveFrom) | new | **seed = hardcode hiện tại per regime** (zero behavior change — R5) | drop |
| M6 | `VoucherSequences` + `AccountingEntries.VoucherNumber` | new+add | null cũ | — |
| M7 | `AccountingEntries.AllocatedToEntryId/PaymentMethod/CashAccountCode` (**link-field — KHÔNG bảng allocation riêng, tránh second truth — R4**) | add | null | — |
| M8 | `OrderPayments` (multi-payment per order — scope thật của G-C1 phần 2 — R3) | new | 1 row synthetic per paid order | drop |
| M9 | `BankAccounts` + `InternalTransfers` | new | 1 cash acct default | drop |
| M10 | `ReconciliationItems` + `Attachments` | new | — | drop |

Mọi migration: expected row counts + integrity check + test trên data đại diện (theo §18.11). **Không hard-transaction entry↔JE** — dùng PostingStatus + retry (R2).

## 8. TEST PLAN

- **Phase A (foundation):** AT-012/017/022 · ACC12-004/005/014 · JE balance reject · concurrent posting · multi-payment per order (ACC12-003).
- **Phase B (rule engine):** FR-RULE-* matrix · ACC12-013/016 · regime-specific default rules (5 regime) · missing-rule → observable error.
- **Phase C (voucher/allocation):** FR-VCH-004 sequence uniqueness · ACC12-006/007 · unapplied balance · AT-005/006.
- **Phase D (cash/bank):** AT-011 · FR-TRF-* · sổ quỹ/TGNH reports.
- **Phase E (purchase/inventory):** AT-007/008 · FR-PUR-* · FR-SUB-003/FR-RECNC-004.
- **Phase F (closing/reconcile):** AT-013 · FR-CLS-003/005 · ACC12-017.
- **Regression toàn phần:** mọi phase — AT-001..024 + ACC12-001..018 theo regime áp dụng (D1: 5 regime).

## 9. PHASED IMPLEMENTATION PLAN (đề xuất — chốt ở Gate 1)

| Phase | Scope (v2 sau phản biện) | Gates | Phụ thuộc |
|---|---|---|---|
| **A — Posting Foundation** | Structured key M1 + PostingStatus M2 + retry worker (KHÔNG hard-txn — R2) · JE balance enforce trên posting path (skip opening/single-line seed theo purpose) · unique index M3 (reversal-aware — R1) · G-C4 DN receivable JE (feature-flag) | ACC12-004/005/014 · AT-013 | — (làm trước mọi thứ) |
| **B — COA + Account Map** | G-H2 TenantAccountChart + ParentCode/EffectiveFrom · **B1 `AccountMappingRule`** (event→TK data-map, seed=hardcode hiện tại — R5) + resolver + bridge/OrderService switch · VAT 3331 → JE (GL completeness) · **B2 full rule engine (conditions/formula/preview) DEFER đến khi tenant thật cần** | FR-RULE-001/002 · FR-COA | A |
| **C — Voucher & Allocation** | G-H3 VoucherNumber · allocation **link-field** `AllocatedToEntryId` (R4) + unapplied/trả thừa (đối tượng âm = trả trước) · **`OrderPayments`** multi-payment (R3 — scope thật) · PaymentMethod · Attachments | AT-005/006 · ACC12-003/006/007 | A,B |
| **D — Cash/Bank** | G-M1 BankAccount (112) + InternalTransfer (CK01-05, 113) + sổ quỹ/TGNH · FR-PAY-006 cash-balance warn | FR-TRF · AT-011 | C |
| **E — Purchase & Inventory** | G-H5 purchase docs + inventory GL link (cross-DB PG↔SQLite — phức tạp nhất, outbox/NATS) + reconcile coverage | AT-007/008 | B,C — có thể tách riêng |
| **F — Closing & Reconcile+** | G-H6 closing entries 911 (kết chuyển 5xx/6xx→911→421) + checklist (reuse Reconcile R-checks) · G-M3 ReconciliationItem workflow · FR-RECNC-001 sao kê NH | AT-013 · ACC12-017 | C,D |

UX: giữ nguyên 2 form + Orders (D-baseline §18.1); mọi UX change → UX Change Proposal → PO duyệt.

## 10. DECISION LOG (cần PO/kế toán trưởng xác nhận tại Gate 1)

| # | Câu hỏi | Đề xuất của audit |
|---|---|---|
| Q1 | DN bán chịu: ghi 131/511 ngay (accrual) đúng TT133/99? — HKD giữ cash-basis? | YES — regime-aware: HKD/DNSN giữ hiện tại; DN ghi nhận DT khi ghi nhận phải thu |
| Q2 | Revive `JournalTemplate` (dormant) làm AccountingRule hay entity mới? | Đánh giá thêm ở Gate 1 — JournalTemplate thiếu conditions/version/effectiveFrom; nghiêng entity `AccountingRule` mới, reuse formula/line model |
| Q3 | AccountingEntry = subledger+event đủ, hay cần BusinessEvent table riêng? | Đủ — StructuredSourceRef trên entry thay bảng riêng (ít schema hơn, đúng §9.1) |
| Q4 | Thu/trả nợ tạo JE Nợ111/Có131 (và ngược lại) — sửa bridge ResolveAccounts mở rộng cho Adjustment có Reference công nợ? | YES — cần distinguish công nợ-adjustment vs adjustment khác (field hoặc ref type) |
| Q5 | Method giá xuất kho cho DN: FIFO / bình quân / đích danh? | Cần kế toán trưởng chốt (§16.3) — hiện fixed recipe cost |
| Q6 | Revenue recognition cho DN: fulfillment vs payment? | POS = immediate (giữ); future B2B credit → theo Q1 |
| Q7 | Đối tượng công nợ: nâng free-string → Customer/Vendor entity link? | Đề xuất YES ở Phase C (giữ string legacy, thêm nullable ObjectId) |
| Q8 | Mức ưu tiên Phase E (purchase/inventory) — cần thiết sớm hay defer? | Theo tải thực tế PO |

---

## 11. ĐIỀU CHỈNH SAU PHẢN BIỆN (approved 2026-10-30 — R1-R5)

| # | Vấn đề phát hiện khi tự phản biện | Điều chỉnh |
|---|---|---|
| R1 | Unique index (Tenant,RefType,RefId) sẽ **chặn reversal** — `JournalService.CreateReversalEntryAsync` copy nguyên RefType+RefId (thậm chí không set IsReversal) | Structured key TRƯỚC (M1), index SAU với `PostingPurpose` discriminator (M3); reversal = purpose riêng |
| R2 | Hard-transaction entry↔JE **đụng fail-safe decision** (phiếu vẫn lưu — chống bug payment-chết-vì-journal đã fix Option B) | KHÔNG gộp transaction. `PostingStatus` + retry worker → divergence observable + tự vá |
| R3 | "Multi-payment per order" là **domain change** (Order paid-once model), không chỉ đổi key | Phase A chỉ fix key/index; `OrderPayments` entity → Phase C (M8) |
| R4 | `PaymentAllocation` bảng = **second source of truth** so với FIFO-derive hiện có | Link-field `AllocatedToEntryId` trên entry (M7); unapplied = đối tượng âm |
| R5 | Full rule engine = **over-engineering** cho 5 mapping hardcode hiện tại | B1 `AccountMappingRule` data-map trước (seed=hardcode → zero change); engine full DEFER đến khi cần |

**Phát hiện thêm trong phản biện:** VAT 3331 entry → `ResolveAccounts` trả null (không bắt đầu 5/7) → **JE thuế không tồn tại trong GL** — BCTC B01 thiếu nợ thuế. Fix trong Phase B (map `order-vat→(111,3331)`). Bonus: `JournalService` reversal không set `IsReversal`/`ReversedJournalId` (domain `CreateReversal` thì có) — cần chuẩn hóa.

---

## ĐIỀU KIỆN KẾT THÚC PHASE 0 — tự đánh giá

- [x] Tất cả FR/NFR có trạng thái (§3)
- [x] Luồng ghi sổ hiện tại đã truy vết (§2)
- [x] Rủi ro dữ liệu + duplicate posting đã đánh giá (§4, G-C1/C2/C3)
- [x] Đề xuất migration + regression (§7, §8)
- [x] Câu hỏi PO liệt kê (§10)
- [x] Không thay đổi code/database

**→ DỪNG — chờ PO duyệt Gate 0 → Gate 1 (backend design approval) trước khi implement.**
