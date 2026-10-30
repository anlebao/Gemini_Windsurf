# MASTER PLAN: SHOPERP ACCOUNTING ENGINE (SRS v1.2)

> Created: 2026-10-30 — PO duyệt Gate 0 audit + Gate 1 answers + 5 điều chỉnh phản biện
> Nguồn: `docs/specs/SRS_ShopERP_Accounting_Engine_v1.2.md` · Audit: `docs/AI/plans/accounting-engine-srs-v12-phase0-audit.md`
> Branch: `main` · Workflow: `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT per phase)
> Status: **⏳ PLAN ĐÃ CHỐT — implement tuần tự theo task cards A→F**

---

## 1. MỤC TIÊU

Nâng lõi kế toán ShopERP theo SRS v1.2 — đóng **4 gap CRITICAL + 6 HIGH** từ audit, giữ nguyên UX baseline (2 biểu mẫu + Orders), áp dụng cho **mọi regime** (HKD TT152 · DN TT133/TT99 · HTX TT71 · DNSN TT58).

| Gap | Nội dung | Phase |
|---|---|---|
| G-C1 | Idempotency OrderId-level + race window → structured key + unique index | A |
| G-C2 | JE không enforce ΣNợ=ΣCó | A |
| G-C3 | Entry↔JE không atomic → PostingStatus + retry (KHÔNG hard-txn) | A |
| G-C4 | DN bán chịu không ghi 511 (accrual) — regime-aware | A |
| G-H1 | Không rule engine → `AccountMappingRule` data-map (B1), engine full defer | B |
| G-H2 | COA global-only → TenantAccountChart + hierarchy/effective | B |
| G-H3 | Không voucher numbering / attachments | C |
| G-H4 | Thu/trả nợ không JE · allocation implicit · unapplied balance | C (+A phần receivable) |
| G-H5 | Purchase/inventory docs + GL link (cross-DB) | E |
| G-H6 | Closing entries 911 + checklist + reconcile workflow | F |
| G-M1 | Bank accounts / chuyển tiền nội bộ / sổ quỹ-TGNH | D |

**Ngoài phạm vi (ghi nhận):** multi-branch/kho/quỹ (D4 DEFER) · đa tiền tệ (FR-JE-010 P2) · approval workflow (D2 auto-post) · full AccountingRule formula/condition engine (B2 — đến khi tenant thật cần) · kê khai thuế điện tử · ngân hàng API trực tiếp.

---

## 2. QUYẾT ĐỊNH ĐÃ CHỐT

### 2.1. PO decisions (2026-10-30)

| # | Nội dung | Quyết định |
|---|---|---|
| D1 | Phạm vi regime | **Mọi regime** — HKD TT152, DN TT133/TT99, HTX TT71, DNSN TT58 |
| D2 | Workflow duyệt chứng từ | **Auto-post** — không Draft/Approve; chỉ cần Posted/Reversed distinction |
| D3 | Mô hình dữ liệu | **KEEP 2-layer** — AccountingEntry (subledger/event) + JournalEntry (GL); không BusinessEvent table |
| D4 | Multi-branch/kho/quỹ | **DEFER** |

### 2.2. Gate 1 answers (PO duyệt đề xuất audit — §10 audit doc)

| # | Quyết định |
|---|---|
| Q1 | DN bán chịu → ghi Nợ 131/Có 511 (accrual); HKD/DNSN giữ cash-basis — **regime-aware** |
| Q2 | `AccountMappingRule` entity mới (KHÔNG revive JournalTemplate dormant — thiếu conditions/version); reuse formula/line model sau ở B2 |
| Q3 | StructuredSourceRef trên AccountingEntry — không BusinessEvent table |
| Q4 | Thu/trả nợ → JE (111/131 · 331/111) qua bridge mở rộng |
| Q5 | Giá xuất kho: `CostingPolicy` trên tenant, default **bình quân gia quyền** (chuẩn DN thương mại) — kế toán trưởng đổi được |
| Q6 | Revenue recognition: POS = immediate (giữ); DN credit-sale → ghi nhận khi phát sinh phải thu (Q1) |
| Q7 | Đối tượng công nợ: giữ free-string legacy + thêm nullable `ObjectRef` (link Customer/Vendor) |
| Q8 | Phase E làm sau C/D, có thể tách triển khai riêng |

### 2.3. Điều chỉnh phản biện (5 — PO approved)

R1 index có `PostingPurpose` (reversal-aware) · R2 PostingStatus+retry thay hard-txn · R3 multi-payment → `OrderPayments` Phase C · R4 allocation = link-field không bảng mới · R5 account-map trước, engine full defer.

---

## 3. PHASES + TASK CARDS (theo WAVE PLAN đã duyệt — §5.1)

| Wave | Phase(s) | Nội dung | Task card | Status |
|---|---|---|---|---|
| **W1** | A | Posting Foundation: structured key (M1) · PostingStatus + retry (M2 — `EntryPostingStatusEntity` + `JournalPostingRetryWorker`) · JE `EnsureBalanced` · unique index reversal-aware (M3) · DN/HTX accrual JE (flag `AccrualReceivableJE`) · chuẩn hóa reversal | `task_pa_posting_foundation.md` | ✅ DEPLOYED `b01bb878` (2026-10-30) — còn pilot flag tenant DN |
| **W2** | B + C1 | COA + Account Map: `TenantAccountCharts` + `ParentCode`/`EffectiveFrom` (M4) · `AccountMappingRules` + resolver + seed=hardcode parity (M5) · VAT 3331→JE · **+** C1: `VoucherSequences`/`VoucherNumber` (M6) · `AllocatedToEntryId`/`PaymentMethod`/`CashAccountCode` (M7) · `Attachments` (M10) | `task_pb_coa_account_map.md` + `task_pc_voucher_allocation.md` §C1 | ⏳ NEXT |
| **W3** | D | Cash/Bank: `BankAccounts` + `InternalTransfers` (M9) · sổ quỹ/TGNH · cash-balance warn | `task_pd_cash_bank.md` | ⏳ |
| **W4** | C2 | `OrderPayments` multi-payment (M8) · unapplied/trả thừa · refactor `GenerateForPaymentAsync` | `task_pc_voucher_allocation.md` §C2 | ⏳ |
| **W5** | F | Closing 911 + checklist + `ReconciliationItems` (M10) · sao kê NH | `task_pf_closing_reconcile.md` | ⏳ |
| **W6** | E | PurchaseReceipts + inventory↔GL R13 (cross-DB) — cuối cùng, defer được (Q8) | `task_pe_purchase_inventory.md` | ⏳ |

---

## 4. THIẾT KẾ CHI TIẾT

### 4.A — Posting Foundation

**Structured source key (SRS §18.3.2):**
- `AccountingEntry += SourceEventType/SourceEventId/PostingPurpose` (nullable string). Mapping hiện tại → `SourceEventType`: ReferenceType giữ nguyên; `SourceEventId`: orderRef/entry.Id string; `PostingPurpose`: `revenue`/`vat`/`cogs`/`deposit-offset`/`receivable`/`payable`/`receivable-payment`/`payable-payment`/`manual-receipt`/`manual-expense`/`opening-balance`/`reversal`/`other`.
- `JournalEntry += PostingPurpose` (nullable). Backfill: ManualReversal/OpeningBalanceReversal→`reversal`; OpeningBalance→`opening-balance`; còn lại→`original`.
- Migration SQL derive — KHÔNG sửa Domain cũ (append-only fields, get-only).

**PostingStatus + retry (R2):**
- `AccountingEntry.PostingStatus`: `Pending=0/Posted=1/Skipped=2/Failed=3` (int, default Posted cho backfill có JE).
- Bridge: sau attempt → set status (Posted/Skipped reason/Failed) thay vì return void im lặng. Skipped: adjustment-non-debt (không JE by design).
- `JournalPostingRetryWorker` (BackgroundService): poll Failed/Pending > TTL → retry bridge → cap retries → alert Reconcile. Đăng ký cả 3 hosts như pattern hiện có.

**JE balance enforce (G-C2):**
- `JournalEntry.EnsureBalanced()` — ΣDebit==ΣCredit (tolerance 0.01) — gọi trong `AddToBookAsync`/`AddAsync` trước save **cho JE có PostingPurpose thuộc nhóm posting chuẩn** (skip `opening-balance` — single-line by design).
- Domain change → **Gate 5 approval** (additive method + field trên JournalEntry — không sửa semantics cũ).

**Unique index (R1):** `IX_JournalEntries_Tenant_Ref_Purpose` trên `(TenantId, ReferenceType, ReferenceId, PostingPurpose)` — chạy SAU backfill M1 + pre-scan dup. Reversal có purpose `reversal` ≠ `original` → không xung đột.

**DN receivable JE (G-C4 — feature flag `AccrualReceivableJE`):**
- Bridge `ResolveAccounts` mở rộng: khi tenant DN (TT133/TT99) hoặc HTX:
  - receivable-create (131+, Adjustment, PostingPurpose=receivable) → Nợ 131 / Có 511|512 (HTX)
  - receivable-payment (131−, receivable-payment) → Nợ 111 / Có 131
  - payable-payment (331−) → Nợ 331 / Có 111
  - payable-create (331+): **deferred** — counter-account (156/6xx) không suy luận an toàn → PostingStatus=Skipped("needs-account-map") → Phase B
- HKD/DNSN: giữ nguyên (không JE cho Adjustment) — cash-basis.
- Apply-from-date; KHÔNG backfill JE lịch sử (SRS §18.11).

**Chuẩn hóa reversal:** `JournalService.CreateReversalEntryAsync` set `IsReversal=true` + `ReversedJournalId` + purpose `reversal` (giống domain `CreateReversal`).

### 4.B — COA + Account Map

**`TenantAccountChart` (M4):** per-tenant override — `AccountCode` (chi tiết cho phép: ParentCode phải tồn tại trong global hoặc tenant chart), `IsActive` (deactivate — FR-COA-005: không delete TK đã phát sinh, enforce ở service), `EffectiveFrom`. `AccountChartEntity += ParentCode?/EffectiveFrom?`. Resolver: tenant row > global row.
**`AccountMappingRules` (M5):** `(Id, TenantId?, Regime?, EventCode, DebitAccount, CreditAccount, AmountKind, EffectiveFrom, Version, IsActive)` — TenantId null = global default theo Regime. `AccountMapResolver`: tenant > regime > builtin; cache per-tenant.
**Seed = hardcode hiện tại (zero behavior change — R5):**
`manual-receipt→(111,{acct})` · `manual-expense→({acct},111)` · `deposit-offset→(3387,{acct})` · `order-revenue→(111,511|512-HTX)` · `order-cogs→(632|611/612-HTX,111)` · `order-vat→(111,3331)` **[mới — fix GL missing VAT]** · `receivable→(131,511|512)` · `receivable-payment→(111,131)` · `payable-payment→(331,111)` · `payable→(null-counter,331)→skipped until configured`.
**Switch:** bridge `ResolveAccounts` → `IAccountMapResolver.Resolve(eventCode, tenant, accountHint)`; OrderService hardcode 511/632/3331 → event codes; feature flag `AccountMappingEnabled` per tenant (pilot → all).

### 4.C — Voucher & Allocation

- `VoucherSequences(TenantId, VoucherType, Year, LastNumber)` — atomic increment (row lock); format `PT-{yyyy}-{seq:00000}`/`PC-`. `AccountingEntry.VoucherNumber` — gán khi tạo phiếu (manual + công nợ + thu/trả nợ); order-driven entries: VoucherNumber null (order là chứng từ gốc).
- `AllocatedToEntryId` (nullable Guid) trên payment entries — link thu/trả nợ → khoản gốc; CongNoService set khi FIFO match (1 payment → nhiều allocations = 1 entry/link per khoản? **thiết kế:** payment entry giữ amount tổng; allocation derive từ FIFO như hiện tại + `AllocatedToEntryId` optional khi user chọn khoản cụ thể — không bảng riêng, tránh second truth).
- **Unapplied/trả thừa:** payment vượt nợ → đối tượng balance âm = trả trước (FIFO tự nhiên) — đảm bảo report hiển thị "Trả trước/Khách ứng" thay vì âm lạ + test ACC12-006.
- `OrderPayments` (M8): `(Id, OrderId, TenantId, Amount, Method, PaidAt, SourceEventId)` — mỗi payment = event riêng; `GenerateAccountingEntriesAsync` refactor → per-payment (`SourceEventId=payment.Id`, purpose revenue/vat/cogs/offset theo phần tỉ lệ); backfill 1 synthetic row/paid order. **Lớn nhất của phase — có thể tách C2.**
- `PaymentMethod`/`CashAccountCode` trên phiếu tay (nullable; default 111).
- `Attachments` (M10): `(Id, TenantId, EntityType, EntityId, FileUrl, MimeType, CreatedBy)` — storage R2/Cloudinary reuse.

### 4.D — Cash/Bank

- `BankAccounts(TenantId, AccountCode=112x, BankName, AccountNumber, IsActive)` + `AccountingEntry.CashAccountCode` (M7 — đã có từ C) phân biệt 111/112 khi ghi sổ.
- `InternalTransfers` (CK01-05): `(Id, FromAccountCode, ToAccountCode, Amount, Fee, Status[Draft/InTransit/Received/Discrepancy], Dates, Ref)` → JE Nợ đích/Có nguồn (+ phí riêng Nợ 642/Có nguồn nếu có — FR-TRF-004); InTransit dùng 113.
- Reports: sổ quỹ (SubLedger TK111 + running balance format) + sổ TGNH (per BankAccount) — FR-RPT-003/004; FR-PAY-006 warn khi chi > số dư quỹ (warn, không chặn — theo precedent Q1 cũ).

### 4.E — Purchase & Inventory (có thể tách)

- `PurchaseReceipts` (phiếu nhập mua): link Vendor/331 + JE Nợ(156|6xx map)/Có 331 qua account-map `payable→(?)` — **phải có counter-account rule trước (B)**.
- Inventory GL link: stock valuation summary (SQLite) → reconcile R-check mới (inventory-value vs TK 15x GL) — cross-DB qua mirror/provider pattern (MirrorOrderHttpProvider precedent).
- Trả hàng/giảm giá/CKTM: manual phiếu + link gốc trước (documented manual flow), document-model đầy đủ defer.

### 4.F — Closing & Reconcile+

- Closing entries: `PeriodClosingService` thêm `GenerateClosingEntriesAsync` — kết chuyển 5xx→911, 6xx→911, 911→421 (per regime; HTX 512/612, DNSN N/A vì chế độ sổ) — chạy trong ClosePeriodAsync, JE purpose `closing`.
- Checklist: reuse `IAccountingReconcileService` R-checks làm checklist trước đóng sổ (FR-CLS-003) — cảnh báo thay chặn (precedent).
- `ReconciliationItems` (M10): chênh lệch có Status/AssignedTo/Cause/Resolution — Reconcile service emit items; UI `/admin/reconcile` thêm cột xử lý.
- Sao kê NH (FR-RECNC-001): CSV import bank statement → match entries 112 (manual-match UI tối thiểu) — sau BankAccounts (D).

---

## 5. CHIẾN LƯỢC THỰC THI

### 5.1. WAVE PLAN ĐÃ DUYỆT (PO chốt 2026-10-30 — đây là thứ tự thực thi bắt buộc)

**Nguyên tắc:** phase-sequential theo dependency + rủi ro — KHÔNG làm per-regime (85% là shared infra; regime-priority đặt ở feature flag, không đặt ở build order). Mỗi wave = 1 deploy (FAST PUSH → CD ~12min → RV L1-L3).

| Wave | Lý do vị trí |
|---|---|
| **W1 = A riêng** | Risk cao nhất (migration + index + worker) → deploy một mình cho RV sạch |
| **W2 = B + C1** | B là nền cho C/D/E/F. C1 nhẹ (bảng/field mới, không đụng hot path) → gom chung tiết kiệm 1 CD cycle |
| **W3 = D** | Độc lập với C2; cần trước F (BankAccounts cho sao kê) |
| **W4 = C2** | `OrderPayments` refactor nặng nhất (đụng hot path payment) → để khi nền đã ổn |
| **W5 = F** | Cần map (B) cho closing rules + BankAccounts (D) cho sao kê |
| **W6 = E** | Cross-DB phức tạp nhất — cuối cùng hoặc defer hẳn theo Q8 |

- Có thể nén thêm: nếu W1 RV sạch nhanh → W2 có thể gộp F-closing+checklist (chỉ cần B). Không gộp W1 với gì.
- 5-6 deploys tổng (vs ~12+ nếu per-regime).

### 5.2. Nguyên tắc kỹ thuật

1. **Additive-first 100%** — cột nullable/bảng mới; không drop/rename; không backfill JE lịch sử.
2. **Feature flags** (`VasFeatureFlagService` + `IShopFeatureSettingsService` per-tenant): `AccrualReceivableJE` (A) · `AccountMappingEnabled` (B) · pilot tenant → all.
3. **Zero behavior change tại B** — seed map = hardcode hiện tại; switch resolver phải cho output identical (test T-parity: cùng input → cùng cặp TK).
4. **Domain changes** (mỗi phase có) → Gate 5 approval trước khi IMPLEMENT phase đó.
5. **Mỗi wave = 1-2 commit** + guard-check + build + Core.Tests → FAST PUSH → CD → RV L1-L3.
6. **Không hard-txn entry↔JE** (R2) · unique index chỉ sau backfill + pre-scan (R1) · single source of truth (R4) · UX baseline giữ nguyên — mọi UX change qua proposal PO.

### 5.3. Lessons từ W1 deploy (ghi nhận cho các wave sau)

- **`ReferenceId` alone KHÔNG đủ** — Order JEs chia sẻ `ReferenceId=order.Id` cho revenue+cogs (2 purposes cùng ref). Backfill JE purpose phải **derive từ `JournalEntryLine`** (6xx→cogs, 3387→deposit-offset, else→revenue), KHÔNG gán 'original' trần.
- **Pre-scan DO-block trong migration = đúng thiết kế** — đã bắt đúng collision trước khi index fail (production recovery ~25min thay vì corruption).
- **Seed EntryPostingStatuses phải join cả Order-ref** (prefix match — entry.Reference có thể mang suffix `-DEPOSIT-OFFSET`), không chỉ `ReferenceId=entry.Id`.
- **Bridge cần `HasOrderJournalEntryAsync` guard** — entry order-linked đã có JE refType='Order' → skip, chặn retry worker double-post.
- Apply tay migration khi cần: sửa SQL → `psql -f` → `INSERT "__EFMigrationsHistory"` → restart container; sau đó commit migration đã sửa để repo là source of truth (EF skip vì đã applied).

## 6. RỦI RO & ROLLBACK

| Rủi ro | Giảm thiểu |
|---|---|
| Backfill purpose sai → index fail | Pre-scan dup trước M3; index tạo trong migration riêng sau verify |
| Resolver map sai → JE sai TK | T-parity test + feature flag + seed=hardcode |
| Multi-payment refactor OrderService lớn | Tách C2 riêng; synthetic OrderPayments backfill giữ tương thích |
| Cross-DB Phase E phức tạp | Defer sau D; mirror/provider precedent |
| Performance | Rule map cache; index thay EXISTS; sequence row-lock — <5ms hot path |

## 7. TEST & RV STRATEGY

- Per-phase tests theo SRS §13 AT-xxx + §18.10 ACC12-xxx (ma trận trong từng task card).
- Regression: full Core.Tests + ShopERP.Tests mỗi phase (hiện 2481+).
- RV sau deploy: L1 markers/migration applied · L2 routes/health · L3 Playwright production (impersonate) theo từng regime tenant.
- Pilot flag: bật `AccrualReceivableJE`/`AccountMappingEnabled` cho 1 tenant DN trước, verify R-checks reconcile production → mở rộng.
