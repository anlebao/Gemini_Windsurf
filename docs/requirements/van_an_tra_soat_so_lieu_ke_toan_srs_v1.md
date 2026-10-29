# SRS — TRA SOÁT SỐ LIỆU KẾ TOÁN (ACCOUNTING RECONCILIATION) v1.0

> **Ngày:** 2026-10-29 · **Trạng thái:** DRAFT v1.0 — user chốt 4 điểm (2026-10-29): **Cả service + API + UI** ✓ · **Manual on-demand** ✓ · **Read-only + gợi ý fix (KHÔNG tự sửa data)** ✓ · **Tất cả loại hình (HKD TT152 / DN TT133-TT99 / HTX TT71)** ✓
> **Nền tảng:** Module kế toán hiện tại (PG source of truth) — đọc thuần, KHÔNG migration, KHÔNG đụng Domain/AccountingEntry
> **Nguyên tắc:** "những điều chưa rõ → giả định kịch bản ĐƠN GIẢN NHẤT" (mọi giả định đánh dấu **[G#]**)
> **Cơ sở thực nghiệm:** `6_Testing/rv-scripts/accounting-balance-rv.sh` (C1-C10, chạy production 2026-10-29 — bắt được 3 lớp defect thật: double-revenue 114 journal, thiếu migration DepositPaidAmount, TransactionDate -infinity)

---

## 1. BỐI CẢNH & MỤC TIÊU

**Yêu cầu người dùng:** *"kiểm tra số liệu kế toán của 1 tenant có cân bằng, đúng đắn, đồng bộ giữa các báo cáo/sổ sách không"* — phát hiện sớm sai lệch trước khi nộp thuế / chốt kỳ / kiểm toán.

**Vấn đề hiện tại:**
1. Số liệu kế toán nằm ở **2 nguồn** (PG `JournalEntries`/`AccountingEntries` — source of truth; SQLite mirror ShopERP) và nhiều view đọc khác nhau → lệch âm thầm (bằng chứng: double-revenue 114 journal 5 tenant 2026-10-29 chỉ lộ khi RV thủ công).
2. Không có công cụ tự kiểm tra: mỗi lần nghi ngờ phải chạy script SQL thủ công.
3. Rủi ro pháp lý: số liệu lệch → báo cáo thuế/kiểm toán sai (TT 152/2025, TT 133/2016, TT 99/2025, TT 71/2024 đều yêu cầu sổ sách trung thực, cân đối).

**Mục tiêu SRS:** feature **Tra soát số liệu kế toán** — chạy thủ công theo yêu cầu (tenant + kỳ), kiểm tra toàn diện số liệu của tenant, trả báo cáo mức độ **Error / Warning / Info** kèm **gợi ý xử lý**, hoàn toàn **read-only** (không bao giờ ghi/sửa data).

---

## 2. HIỆN TRẠNG & NGUỒN SỐ LIỆU (khảo sát 2026-10-29)

### 2.1 Map view → nguồn dữ liệu (đã xác minh trong code)

| View | Trang / API | Nguồn đọc | Ghi chú |
|---|---|---|---|
| Dashboard tài chính | `/financial` → `GET /api/financial/profit-summary` | IncomeStatementService → **JournalEntries** | Revenue = credit 5xx/7xx |
| Dashboard kế toán | `/accounting` (AccountingIndex) | **AccountingEntries** (EntryType=1/2 sum) | MonthlyRevenue **gồm cả VAT 3331** (entry Revenue type) |
| Báo cáo tài chính | IncomeStatement · BalanceSheet · TrialBalance | **JournalEntries + Lines** | A = L + E (net income plug 420) |
| Sổ HKD (B01-B09) | `/accounting/hkd-books` | HKDBookService → **JournalEntries** | S1a/S2b/S3a = **toàn bộ journal kỳ** (không filter book type) [G1] |
| Nhật ký chung | `/accounting/journal` (JournalReport) | **JournalEntries + Lines** | GeneralLedgerService.GetJournalAsync |
| Sổ cái | `/accounting/ledger` (Ledger) | **JournalEntries + Lines** | GetLedgerAsync: opening (trước kỳ) + movement (kỳ) |

### 2.2 Bảng dữ liệu chính (PG)

- `JournalEntries` — PK **`Id`** (BaseEntity.Id) ⚠️ cột `JournalEntryId` (business key) tồn tại song song (**dual-identity**); `ReferenceType` ('Order'/'ManualEntry'/'OpeningBalance'/'ManualReversal'), `ReferenceId`, `EntryDate`, `IsReversal`, `ReversedJournalId`
- `JournalEntryLine` — FK **`JournalEntryId` → `JournalEntries."Id"`** (KHÔNG phải cột JournalEntryId!) [G2 — bài học 2026-10-29]
- `AccountingEntries` — `EntryType` (1 Revenue / 2 Expense / 3 TaxPayment / 4 Adjustment / **5 DepositOffset**), `AccountCode`, `Reference` (orderId / `{order}-DEPOSIT-OFFSET` / `Booking:{id}:...`), `TransactionDate`, `AccountingBookType`
- `Orders` — `PaymentStatus`, `DepositPaidAmount`, `SourceType`, `Status`
- `Tenants` — `Type` (1 HKD / 2-4 DN / 5 HTX), `AccountingStandard` (TT99_2025 / TT133_2016 / TT58_2026 / TT71_2024)
- SQLite mirror (ShopERP `vanan_shoperp.db`) — `Orders` (mirror), `Tenants` (mirror) — **AccountingEntries/JournalEntries RỖNG** [G3]

### 2.3 Lớp defect thực tế đã gặp (2026-08 → 2026-10) — nguồn case test

| # | Defect | Biểu hiện | Fix đã áp dụng |
|---|---|---|---|
| D1 | **Double-revenue**: 2 đường journal (trực tiếp ref 'Order' + bridge ref 'ManualEntry') | Journal revenue = 2× entries; sổ HKD 200.000 thay vì 100.000 (114 journal / 5 tenant) | Bỏ đường trực tiếp + xóa 114 journal duplicate (backup) |
| D2 | **Thiếu migration** property Domain mới (`DepositPaidAmount`) | SaveChanges "column does not exist" → order hook vỡ | Migration PG+SQLite |
| D3 | **TransactionDate -infinity / 01-01-0001** (backfill gap) | Ngày nghiệp vụ sai, OrderBy(TransactionDate) vỡ | Backfill (1 lần) — còn sót 1 số row cũ [G4] |
| D4 | Legacy pre-bridge `Order`-ref journals (trước 2026-10-16) | Không double (journal duy nhất) — GIỮ, không xóa | — |
| D5 | VAT 3331 entry (EntryType=Revenue) **không tạo journal** (bridge chỉ map 5xx/7xx, 6xx) | Journal revenue ≠ entries Revenue nếu không trừ VAT | Tra soát phải biết semantic này |
| D6 | Offset cọc 3387 (EntryType=5) — journal Nợ 3387/Có 511 nhưng **không có receipt journal** (Nợ 111/Có 3387 khi thu cọc) | 3387 residual âm = cọc đã offset chưa có journal thu | Defer (ghi nhận — tra soát báo INFO) |
| D7 | Contra account (214/229) normal-credit | Balance identity lệch nếu tính 2xx thuần debit | Tra soát xử lý riêng hoặc cảnh báo |

---

## 3. PHẠM VI

### 3.1 Trong phạm vi (MVP — user chốt)

| # | Yêu cầu | Giải pháp |
|---|---|---|
| 1 | Chạy tra soát theo yêu cầu | Service `IAccountingReconcileService` (CoreHub) + nút UI chọn tenant+kỳ |
| 2 | Kiểm tra toàn diện 1 tenant | 12 nhóm check R1-R12 (mục 5) |
| 3 | Kết quả phân mức | Error / Warning / Info + số liệu + gợi ý fix |
| 4 | Read-only tuyệt đối | Không ghi bảng nào; kết quả dựng tại runtime |
| 5 | API | `GET /api/tenant/accounting/reconcile?period=` (owner/accountant) + `GET /api/admin/accounting/reconcile?tenantId=&period=` (SystemAdmin) |
| 6 | UI | `/accounting/reconcile` (trong AccountingLayout, UI Platform 100%) |
| 7 | Tất cả loại hình | HKD (TT152) / DN (TT133/TT99) / HTX (TT71) — chuẩn mực theo Type (mục 7) |

### 3.2 Ngoài phạm vi (ghi nợ — KHÔNG làm MVP)

- Tự sửa data (kể cả nút fix) — chỉ gợi ý hướng xử lý [G5]
- Scheduled/background job + lịch sử kết quả (chỉ manual on-demand)
- Tra soát cross-VPS / multi-tenant một lượt (chỉ 1 tenant/lần)
- Backfill dữ liệu cũ (TransactionDate, receipt journal 3387…)
- Xuất PDF/Excel kết quả (chỉ xem trên UI + API JSON) [G6]
- Kiểm tra tính hợp lệ nghiệp vụ (hóa đơn, kỳ đóng sổ…) — chỉ kiểm tra số liệu

---

## 4. THUẬT NGỮ

| Thuật ngữ | Định nghĩa |
|---|---|
| Tra soát (reconcile) | Kiểm tra số liệu kế toán 1 tenant đối chiếu giữa các nguồn/view + bất biến kế toán |
| Journal | `JournalEntries` + `JournalEntryLine` — bút toán (cân đối Nợ/Có) |
| Entry | `AccountingEntries` — ghi nhận doanh thu/chi phí/điều chỉnh (nguồn của dashboard kế toán) |
| Bridge | `ManualEntryJournalBridge` — tạo Journal từ mọi Entry (single-source journal từ 2026-10-29) |
| Kỳ (period) | `YYYY-MM`, lọc theo `EntryDate` (journal) / `TransactionDate` (entry) |
| Offset cọc | EntryType=5 `DepositOffset` — Nợ 3387/Có 511 tại thanh toán cuối |

---

## 5. YÊU CẦU CHỨC NĂNG (FR)

### FR-1 — Chạy tra soát (manual on-demand)

- FR-1.1 Người dùng chọn: tenant (mặc định tenant hiện tại; SystemAdmin chọn bất kỳ) + kỳ `YYYY-MM` → bấm "Tra soát".
- FR-1.2 Chạy đủ 12 nhóm check R1-R12 (mục 5.1) cho tenant+kỳ.
- FR-1.3 Kết quả trả về ngay (không lưu trữ) — cấu trúc: `summary {error, warning, info, pass}` + `checks[]` + `suggestions[]`.
- FR-1.4 Timeout & fail-safe: 1 nhóm check lỗi exception → đánh dấu check đó `error=không chạy được` (log), KHÔNG chặn các check khác.

### 5.1 Nhóm check (R1-R12) — chuẩn hóa từ RV script + bổ sung

| # | Check | Logic | Mức vi phạm |
|---|---|---|---|
| R1 | **Journal cân bằng** | Mỗi journal kỳ: `Σdebit == Σcredit` (tolerance 0.01) | Error nếu >0 journal lệch |
| R2 | **Trial balance kỳ** | `Σdebit == Σcredit` toàn kỳ | Error nếu lệch |
| R3 | **Revenue/Expense đồng bộ journals ↔ entries** | `Σ credit 5xx/7xx (journal) == Σ entries Revenue(5/7) + Σ offset(5/7)`; `Σ debit 6xx/8xx (journal) == Σ entries Expense(6/8)` (VAT 3331 trừ riêng — semantic D5) | Error nếu lệch > 0.01 |
| R4 | **Balance identity** | `Tài sản(1xx+2xx) == Nợ(3xx) + Vốn(4xx) + Lãi(5/7 − 6/8)` (toàn lịch sử — gồm OpeningBalance) | Error nếu lệch; Warning nếu chỉ lệch do contra 214/229 [G7] |
| R5 | **Sổ cái** | Per account (111, 112, 131, 156, 331, 3387, 421, 511, 632, 3331): `closing == opening + movement` (replicate GetLedgerAsync) + hiển thị bảng | Info (số liệu để đối chiếu UI) |
| R6 | **Nhật ký chung** | count + ΣNợ + ΣCó kỳ (đúng query JournalReport) | Error nếu ΣNợ ≠ ΣCó |
| R7 | **Sổ HKD** (chỉ tenant Type=1) | Books S1a/S2b/S3a = toàn bộ journal kỳ; revenue 5xx/7xx khớp R3 | Error nếu không khớp; Warning nếu tenant HKD không có journal kỳ |
| R8 | **Single-source bridge** | (a) `journal ref 'Order'` tạo sau 2026-10-29 = 0 (double-revenue regression); (b) mỗi order paid kỳ có đúng **2-3 journal** (revenue+COGS [+offset]) | Error (a)/(b) vi phạm |
| R9 | **Deposit 3387 residual** | `Σ credit − Σ debit` TK 3387 (toàn lịch sử) | Info nếu ≠ 0 (cọc chưa thanh toán cuối / thiếu receipt journal — semantic D6) |
| R10 | **Order → entries coverage** | Mỗi order paid kỳ có đủ entries: 511 (hoặc 512 HTX nội) + 3331 (nếu VAT>0) + 632 (nếu COGS>0) + offset (nếu DepositPaidAmount>0) | Error nếu thiếu entries |
| R11 | **Cross-store PG ↔ SQLite mirror** | Order paid kỳ: có trong SQLite mirror (SourceType/Status/PaymentStatus khớp) | Warning nếu lệch (NATS sync chưa kịp / lỗi) [G8] |
| R12 | **Ngày nghiệp vụ** | Entries có `TransactionDate` hợp lệ (không -infinity / 01-01-0001); journal `EntryDate` trong kỳ khớp entries kỳ | Warning nếu có row xấu (defect D3) |

### FR-2 — Kết quả & mức độ

- FR-2.1 Mỗi check trả: `code` (R1..R12), `level` (error|warning|info|pass), `summary` (chuỗi số liệu chính), `details` (bảng/số liệu chi tiết).
- FR-2.2 Mỗi error/warning kèm `suggestion` (gợi ý hướng xử lý, ví dụ: "Xóa journal ref 'Order' trùng — backup trước, xem order 01a18615/01a185f0", "Chạy backfill TransactionDate", "Kiểm tra NATS sync").
- FR-2.3 Không ghi dữ liệu — kết quả thuần runtime (read-only) [FR-2.3 = ràng buộc cứng, KHÔNG được thay đổi].

### FR-3 — API

- FR-3.1 `GET /api/tenant/accounting/reconcile?period=YYYY-MM` — tenant từ claim (`tenant_id`), role: Owner / ChiefAccountant / Accountant (theo role hiện có — cần xác nhận danh sách chính xác khi implement [G9]).
- FR-3.2 `GET /api/admin/accounting/reconcile?tenantId={guid}&period=YYYY-MM` — SystemAdmin, bất kỳ tenant.
- FR-3.3 Response: `{ period, tenantId, ranAt, summary: {error, warning, info, pass}, checks: [...], suggestions: [...] }` — JSON.
- FR-3.4 Lỗi đầu vào: period sai format → 400; tenant không tồn tại → 404.

### FR-4 — UI `/accounting/reconcile`

- FR-4.1 Trong `AccountingLayout` (phủ policy kế toán hiện có), NavMenu mục "Tra soát số liệu".
- FR-4.2 Form: tenant (chỉ SystemAdmin thấy selector — Owner/accountant cố định tenant hiện tại) + kỳ (mặc định kỳ hiện tại) + nút "Tra soát".
- FR-4.3 Kết quả: 4 thẻ tổng (Error/Warning/Info/Pass) + bảng từng check (level badge + summary + expand chi tiết + suggestion) + nút "Chạy lại".
- FR-4.4 UI Platform 100% (VanACard/VanAAlert/VanAnDataGrid/VanAMetricsCard…) — Gate 4: UI thay đổi → viết E2E spec tại `6_Testing/e2e-tests/`.

### FR-5 — Phân quyền & đa tenant

- FR-5.1 Chỉ truy cập trong tenant hiện tại (Owner/accountant) — `TenantId` từ claims/tenant provider, KHÔNG cross-tenant.
- FR-5.2 SystemAdmin: mọi tenant (admin API) — đúng pattern Gateway admin API hiện có.
- FR-5.3 Audit: log mỗi lần tra soát (tenant, kỳ, user, thời gian, summary) — tái dùng `IAuditTrailService`.

---

## 6. YÊU CẦU PHI CHỨC NĂNG (NFR)

| # | Yêu cầu | Chỉ tiêu |
|---|---|---|
| NFR-1 | **Read-only tuyệt đối** | Service KHÔNG gọi AddAsync/UpdateAsync/Delete/SaveChanges trên bất kỳ DbContext nào; chỉ SELECT. Guard test: kiểm tra không có `AddAsync`/`SaveChangesAsync` trong service [G10] |
| NFR-2 | Hiệu năng | 1 tenant+kỳ (≤ 1000 journal): < 5s; tối ưu bằng query gộp (1-2 query/check), index sẵn có |
| NFR-3 | Multi-tenancy | Mọi query filter `TenantId`; không rò cross-tenant (test isolation bắt buộc) |
| NFR-4 | Fail-safe | 1 check lỗi → không chặn check khác; không throw ra UI (trả level error) |
| NFR-5 | Độ chính xác | Tolerance tiền tệ 0.01 VND; nguồn số liệu = PG (source of truth); không đọc SQLite cho số liệu kế toán (trừ R11 cross-check) |
| NFR-6 | Bảo mật | API admin: `[Authorize(SystemAdmin)]`; API tenant: claim tenant_id; không lộ dữ liệu tenant khác |

---

## 7. CHUẨN MỰC KẾ TOÁN THEO LOẠI HÌNH (kiến thức ngoài — áp dụng cho R3/R4/R7/R10)

| Loại hình | Chuẩn | Doanh thu | Giá vốn | Sổ sách |
|---|---|---|---|---|
| HKD (Type=1) | TT 152/2025/TT-BTC | 511 (ngoài) — hạch toán đơn giản, sổ B01-B09 | 632 | Sổ HKD S1a/S2b/S3a (báo cáo doanh thu thuần, không bắt buộc bảng cân đối đầy đủ) |
| DN vừa/nhỏ (Type=2-3) | TT 133/2016 | 511 | 632 | BCTC đầy đủ (A=L+E) |
| DN lớn (Type=4) | TT 99/2025 | 511/512/515/521 | 632/635 | BCTC đầy đủ |
| HTX (Type=5) | TT 71/2024 | **512 (nội bộ) / 511 (ngoài)** | **612 (nội) / 611 (ngoài)** — KHÔNG dùng 632 | B01-HTX/B02-HTX (tách nội/ngoại) |

Áp dụng trong tra soát:
- R3: revenue account theo Type — HTX nội bộ dùng 512/612; prefix check 5xx/7xx + 6xx vẫn đúng cho mọi loại [G11].
- R4: HTX áp dụng tương tự (nội/ngoại gộp vào nhóm 5/7, 6/8).
- R7: chỉ tenant Type=1 (HKD) chạy; HTX/DN dùng R4/R6 (BCTC).

---

## 8. CASE TEST (từ defect thật — chuẩn bị cho task card + test)

| # | Case | Kỳ vọng |
|---|---|---|
| T1 | Tenant có journal lệch Nợ/Có (chèn journal không cân bằng qua test) | R1 Error + suggestion |
| T2 | Order paid kỳ có journal ref 'Order' post-fix (mô phỏng double) | R8a Error |
| T3 | Order paid thiếu entries 632 (COGS) | R10 Error |
| T4 | Deposit order paid đủ offset → R3/R8b/R9 khớp (journal = entries + offset) | Pass |
| T5 | Entry TransactionDate = 01-01-0001 (seed test) | R12 Warning |
| T6 | Tenant HKD không journal kỳ | R7 Warning (không phải Error) |
| T7 | Cross-tenant: user A không xem được kết quả tenant B | 403/không lộ (R11 isolation) |
| T8 | Tenant HTX nội bộ (512/612) | R3/R4 pass với prefix 5/6 |
| T9 | Contra 214 (khấu hao) trong balance identity | R4 Warning (không Error) hoặc xử lý đúng [G7] |
| T10 | Fail-safe: 1 check query lỗi (mock exception) | Check đó error, các check khác chạy tiếp |

---

## 9. RÀNG BUỘC KỸ THUẬT & KIẾN TRÚC

- **Layers:** Service `IAccountingReconcileService` + `AccountingReconcileService` trong `3_CoreHub/Services/Reconcile/` (đọc `IAccountingDbContext` + `ITenantProvider`); Gateway controller `AccountingReconcileController` (`2_Gateway/Controllers/`); ShopERP UI page + `ReconcileApiClient` (pattern `GatewayAdminApiClientBase`/tenant API client hiện có).
- **KHÔNG** migration mới, KHÔNG entity mới, KHÔNG đụng Domain (`AccountingEntry` immutable — đọc thuần).
- Tận dụng logic đã validate từ `accounting-balance-rv.sh` (C1-C10) — port sang C# service, giữ nguyên tolerance 0.01.
- Query: 1-2 truy vấn gộp/check (SQL qua EF LINQ — tránh raw SQL cross-provider); dùng `IgnoreQueryFilters` KHÔNG cần (query theo tenant id tường minh).
- Kiến trúc test: unit test service (mock IAccountingDbContext với data seed) + bUnit page + E2E production spec (pattern `playwright-rv-*.config.ts`).

---

## 10. TIÊU CHÍ CHẤP NHẬN (ACCEPTANCE)

1. Chạy tra soát tenant HKD + tenant DN (có data kỳ 2026-10) → 12 check chạy đủ, không crash, kết quả khớp `accounting-balance-rv.sh` (cùng tenant+kỳ).
2. Tất cả case test T1-T10 pass.
3. Guard-check + build 0 lỗi + Core.Tests pass (test mới + regression).
4. API 2 endpoint hoạt động với phân quyền đúng (tenant → claim; admin → SystemAdmin).
5. UI render đúng 4 thẻ + bảng check + suggestion; E2E spec production pass (sweep-first).
6. Read-only: verify không có write nào lên PG trong quá trình chạy (check bằng DB trigger/log hoặc code review + test).

---

## 11. NGUỒN THAM KHẢO & ĐỊNH HƯỚNG TRIỂN KHAI

- **Nguồn nội bộ:** `6_Testing/rv-scripts/accounting-balance-rv.sh` (logic đã validate production) · `3_CoreHub/Services/HKDBookService.cs` · `GeneralLedgerService.cs` · `IncomeStatementService.cs` · `BalanceSheetService.cs` · `TrialBalanceService.cs` · `ManualEntryJournalBridge.cs` · governance.md (Known Error Patterns #1-#8) · checklist kiểm soát PM kế toán ERP.
- **Nguồn ngoài:** TT 152/2025/TT-BTC (HKD) · TT 133/2016 (DN nhỏ/vừa) · TT 99/2025 (DN lớn) · TT 71/2024 (HTX) · nguyên tắc kép (A = Nợ + Vốn, trial balance) · Thông tư 103/2016/TT-BTC (điều kiện PM kế toán: bút toán cân đối, sổ khớp, không sửa/xóa dữ liệu ghi sổ).
- **Triển khai dự kiến:** master plan + task card → P1 Service + tests → P2 API + controller → P3 UI page + E2E → P4 deploy + RV production (sweep-first, đối chiếu kết quả với script).

---

## 12. CÂU HỎI MỞ (cần chốt trước implement)

1. **Danh sách role** được phép chạy tra soát (Owner/ChiefAccountant/Accountant/SystemAdmin — khớp role nào hiện có)? [G9]
2. Xử lý **contra account 214/229** trong R4: cảnh báo (Warning) hay loại trừ đúng chuẩn ngay từ đầu? [G7]
3. R11 cross-store: có cần **thêm API đọc SQLite mirror** (ShopERP) hay bỏ R11 khỏi MVP (chỉ PG)? [G8]
4. Kết quả có cần **lưu lịch sử tra soát** (bảng mới) để so sánh giữa các lần chạy? (hiện đã chốt manual on-demand — không lưu) [G12]