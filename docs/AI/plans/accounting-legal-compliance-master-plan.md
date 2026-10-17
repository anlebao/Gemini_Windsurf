# MASTER PLAN: HOÀN THIỆN TUÂN THỦ PHÁP LÝ KẾ TOÁN/ERP (P0-P2)

> Created: 2026-10-16 (user directive: "tạo master plan và task card đầy đủ P0, P1, P2")
> Source: Review code 2026-10-16 vs `C:\Users\lebao\Downloads\checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md`
> Branch: `main`
> Status: **✅ USER CHỐT Q1-Q4 (2026-10-16) · ✅ W1 DEPLOYED + RV L1-L3 PASS + EXCEL THEO MẪU TT 99/2025 (2026-10-16/17, commits `8584edd8`→`ff411ff0` — CD ×3 ALL SUCCESS; 2 fix: CI crawler image Pattern #32 + audit async queue tenant Pattern #31) · ⏳ W2 = P0.2-Đ2 Sổ chi tiết + P0.4 (handoff chi tiết tại project_state.md §4)**
> Workflow: `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT)

---

## 1. MỤC TIÊU

Đóng các lỗ hổng tuân thủ pháp lý phát hiện trong review code Vạn An vs Checklist Kiểm soát kế toán/ERP (2026-10-16), chia 3 ưu tiên:

| Ưu tiên | Nội dung | Trạng thái hiện tại |
|---|---|---|
| **P0 (nghiêm trọng — pháp lý)** | ① Kiểm soát thanh toán ≥ 20 triệu không dùng tiền mặt · ② Sổ Cái / Nhật ký chung / Sổ chi tiết TK (DN) · ③ Hóa đơn điện tử điều chỉnh/thay thế · ④ Rà soát chuẩn dữ liệu HĐĐT theo NĐ 254/2026 + TT 91/2026 | ❌ Thiếu hoàn toàn / chưa cập nhật |
| **P1 (quan trọng)** | ① Audit: capture IP/UserAgent + tamper-evident · ② Reopen kỳ: phê duyệt cấp cao + cảnh báo · ③ Cảnh báo âm quỹ/âm kho · ④ Đối chiếu hóa đơn đầu vào + DN bỏ trốn · ⑤ Bổ sung chế độ TT 152/2025 (HKD) + TT 58/2026 vào checklist + Quy chế sử dụng PMKT | ⚠️ Một phần |
| **P2 (nâng cao — tùy chọn)** | ① Chữ ký số ký BCTC/sổ sách · ② Retention 5/10 năm · ③ Giao dịch liên kết (NĐ 255/2026) · ④ IFRS (TT 118/2026) | ❌ Chưa có / khảo sát |

---

## 2. BỐI CẢNH & KHUNG PHÁP LÝ (cập nhật đến 16/10/2026 — đã verify code + luật)

### 2.1 Kết quả review code (bằng chứng đã xác minh)

| Checklist | Phát hiện | Bằng chứng |
|---|---|---|
| 1.1 Audit trail | ✅ Immutable append-only + OldValues/NewValues JSON; ❌ **thiếu IP/UserAgent** cho Create/Update/Delete (chỉ SecurityEvent có IP); ❌ chưa tamper-evident (hash chain/WORM) | `1_Shared/Domain/Audit/AuditLog.cs`, `3_CoreHub/Services/AuditTrailService.cs` |
| 1.2 Khóa sổ | ✅ Guard chặn bút toán kỳ đã đóng (5 services); ❌ **reopen không cần phê duyệt cấp cao**; ⚠️ reopen đảo TOÀN BỘ entries kỳ | `PeriodClosingService.cs`, `AccountingEntryService.cs:151,205`, `ImportService.cs:460`, `OpeningBalanceService.cs:60`, `CongNoService.cs:80`, `BookingAccountingService.cs:162` |
| 1.3 Chế độ kế toán | ✅ **TT 152/2025 (HKD — 7 sổ), TT 133/2016, TT 99/2025 (B01-DN/B09-DN), TT 71/2024 (HTX)** + enum `TT58_2026` | `Domain.cs:4208` `AccountingStandard`, `BalanceSheetService.cs`, `FinancialStatementNotesService.cs`, `AccountChartSeeder.cs` |
| 1.4 Validation | ⚠️ Có Nợ=Có (ManualEntryJournalBridge) + period guard + duplicate detection; ❌ **chưa cảnh báo âm quỹ 111 / âm kho 152,156** | `ManualEntryJournalBridge.cs`, `AccountingEntryService.cs` |
| 2.1-2.5 HĐĐT | ✅ Provider MISA/Viettel (Submit/Status/Cancel/File/HealthCheck) + outbox + UI 7 trang + flag `EInvoice_Auto_Export_Enabled`; ❌ **không có điều chỉnh/thay thế**; ❌ không đăng ký HĐĐT; ❌ không đối chiếu hóa đơn đầu vào; ❌ không kiểm soát 20 triệu | `Services/Providers/EInvoice/`, `Orchestration/EInvoiceOrchestrator.cs`, `ShopERP/Components/Pages/EInvoice/`, `InvoiceManagement.razor` |
| 3.1 Sổ sách BCTC | ✅ BCTC đủ (TrialBalance/BalanceSheet/IncomeStatement/CashFlow/Notes/HKDBooks/CongNoLedger); ❌ **THIẾU Sổ Cái, Sổ Nhật ký chung, Sổ chi tiết TK (112/131/331...) cho DN** — grep ShopERP không có; dữ liệu `JournalEntry` đã sẵn | `HKDBookRepository.cs`, `ShopERP/Components/Pages/Accounting/` |
| 3.3 Chữ ký số/Backup/Retention | ✅ Backup infra (`scripts/backup-db.sh`); ❌ chữ ký số ký BCTC; ❌ retention 5/10 năm không enforce | `scripts/backup-db.sh` |
| 4 Phân quyền | ✅ `UserRole`: Owner/ChiefAccountant/DebtAccountant... + menu gating + SystemAdmin/impersonation + test ma trận | `UserAggregate/UserRole.cs`, `ShopErpMenuService.cs`, `PlatformSystemAdminAccessMatrixTests` |

### 2.2 Khung pháp lý áp dụng (chi tiết: checklist UPDATED_2026 §0)

| Lĩnh vực | Văn bản | Hiệu lực |
|---|---|---|
| Chế độ kế toán DN | **TT 99/2025/TT-BTC** (thay TT 200/2014...) — Điều 28: yêu cầu phần mềm kế toán; Điều 11: Quy chế hạch toán | 01/01/2026 |
| Chế độ kế toán HKD | **TT 152/2025/TT-BTC** (7 loại sổ, S2a/S2b) — **đã áp dụng trong code** | hiện hành |
| Chế độ kế toán DNNVV / siêu nhỏ / HTX | TT 133/2016 · TT 132/2018 · TT 71/2024 (code có `TT58_2026` — cần xác minh phạm vi) | hiện hành |
| Hóa đơn điện tử | **Luật QLT 2025 (108/2025/QH15) Điều 26** + **NĐ 254/2026/NĐ-CP** + **TT 91/2026/TT-BTC** (Điều 8 ngừng/tạm ngừng · Điều 10 điều chỉnh/thay thế) | 01/07/2026 (Điều 26: 01/01/2026) |
| Quản lý thuế / giao dịch liên kết / xử phạt | NĐ 252/2026 · **NĐ 255/2026** (thay NĐ 132/2020) · NĐ 125/2020 sửa bởi 310/2025, 291/2026 | 01/07/2026 |
| IFRS | **TT 118/2026/TT-BTC** | 01/01/2027 |
| Ngưỡng 20 triệu | TT 219/2013 Điều 15.2.b (GTGT) · TT 96/2015 Điều 4 (TNDN) · hướng dẫn TCT gộp hóa đơn cùng ngày | hiện hành |
| Sổ kế toán / lưu trữ | Luật Kế toán 2015 (Điều 24, 25, 27, 41) · NĐ 174/2016 (5/10 năm) | hiện hành |

> ⚠️ **Lưu ý pháp lý:** NĐ 123/2020, NĐ 70/2025, TT 32/2025 đã hết hiệu lực từ 01/07/2026 — mọi tài liệu/UI/comment dẫn chiếu cũ cần rà soát.

---

## 3. PHẠM VI — PHASES + TASK CARDS + DEPENDENCY

| Phase | Nội dung | Layer | Task card | Dep |
|---|---|---|---|---|
| **P0.1** | **Kiểm soát thanh toán ≥ 20 triệu không dùng tiền mặt** — cảnh báo/khóa khi chọn tiền mặt (Order/POS + phiếu chi + hóa đơn) + theo dõi gộp cùng ngày cùng NCC + **Báo cáo rà soát cuối kỳ** phục vụ thanh tra + audit log | Services + UI + Reports | `task_acct_legal_compliance_p0_1_payment_20m.md` | — |
| **P0.2** | **Sổ Cái (per-TK) + Sổ Nhật ký chung + Sổ chi tiết TK (112/131/331...) cho DN** — từ `JournalEntry` + đối chiếu + xuất Excel/PDF + menu · **chia 2 đợt (Q2): Đ1 = Nhật ký chung + Sổ Cái (W1) · Đ2 = Sổ chi tiết TK (W2)** | 3_CoreHub + ShopERP | `task_acct_legal_compliance_p0_2_ledger_reports.md` | — |
| **P0.3** | **Hóa đơn điện tử điều chỉnh/thay thế** — provider method + orchestrator + UI + audit + kê khai đúng kỳ (Điều 10 TT 91/2026) | Providers + Orchestration + UI | `task_acct_legal_compliance_p0_3_einvoice_adjust_replace.md` | P0.4 |
| **P0.4** | **Rà soát chuẩn dữ liệu HĐĐT NĐ 254/2026 + TT 91/2026** — payload MISA/Viettel, ký hiệu/mẫu mới, hóa đơn máy tính tiền (không bắt buộc chữ ký số), chuyển đổi có mã/không mã | Providers + DTOs + config | `task_acct_legal_compliance_p0_4_einvoice_standard_254.md` | — |
| **P1.1** | **Audit: capture IP/UserAgent (Create/Update/Delete) + tamper-evident (hash-chain)** — option A (IP — nhỏ) / B (hash-chain — migration + backfill) | AuditTrailService + Domain | `task_acct_legal_compliance_p1_1_audit_ip_tamper.md` | — |
| **P1.2** | **Reopen kỳ: phê duyệt cấp cao + cảnh báo reversal hàng loạt** — role gate (Owner/ChiefAccountant) + confirm modal (số entries bị đảo) + audit | PeriodClosingService + UI | `task_acct_legal_compliance_p1_2_reopen_approval.md` | — |
| **P1.3** | **Cảnh báo âm quỹ TK 111 / âm kho TK 152,156 tại thời điểm xuất** — tính số dư động từ JournalEntry + stock SQLite + warning UI (config on/off) | Services + UI | `task_acct_legal_compliance_p1_3_negative_balance_alerts.md` | P0.2 |
| **P1.4** | **Theo dõi hóa đơn đầu vào + cảnh báo DN bỏ trốn/ngừng hoạt động** — module hóa đơn đầu vào (thủ công + cờ rủi ro từ danh sách TCT) + báo cáo đối chiếu | Services + UI + Reports | `task_acct_legal_compliance_p1_4_input_invoice_reconciliation.md` | — |
| **P1.5** | **Checklist cập nhật TT 152/2025 + TT 58/2026 + Quy chế sử dụng phần mềm kế toán** (template docs + hiển thị) | Docs + (UI tùy chọn) | `task_acct_legal_compliance_p1_5_regime_checklist_quyche.md` | — |
| **P2.1** | **Chữ ký số ký BCTC/sổ sách** — khung ký (interface + stub + provider USB/HSM tùy chọn) + xuất PDF ký | Services + UI | `task_acct_legal_compliance_p2_1_digital_signature.md` | P0.2 |
| **P2.2** | **Retention 5/10 năm** — chính sách + báo cáo tuổi dữ liệu + cảnh báo admin + hướng dẫn archive (KHÔNG xóa tự động — append-only) | Services + UI + Docs | `task_acct_legal_compliance_p2_2_retention.md` | — |
| **P2.3** | **Giao dịch liên kết (NĐ 255/2026)** — theo dõi bên liên kết + tờ khai mẫu 01 + hồ sơ xác định giá (chỉ khi user xác nhận nhu cầu) | Services + UI | `task_acct_legal_compliance_p2_3_related_party.md` | — |
| **P2.4** | **IFRS (TT 118/2026)** — khảo sát đối tượng + tài liệu roadmap BCTC IFRS // VAS (không code ngay) | Docs | `task_acct_legal_compliance_p2_4_ifrs.md` | — |

**Dependency:** P0.1 → độc lập · P0.2 → độc lập (data `JournalEntry` sẵn) · P0.3 → sau P0.4 (chuẩn dữ liệu) · P1.1/P1.2/P1.4/P1.5 → độc lập · P1.3 → sau P0.2 (dùng sổ cái tính số dư) · P2.1 → sau P0.2 · P2.2-2.4 → độc lập.

---

## 4. CÂU HỎI CẦN USER CHỐT (Step 4 — trước IMPLEMENT)

| # | Câu hỏi | ✅ QUYẾT ĐỊNH (2026-10-16) |
|---|---|---|
| **Q1** | P0.1 ngưỡng 20 triệu: chặn cứng hay cảnh báo? | ✅ **Cảnh báo mạnh + bắt buộc ghi lý do** (không chặn cứng) + báo cáo rà soát cuối kỳ |
| **Q2** | P0.2 phạm vi sổ sách? | ✅ **Chia 2 đợt** — Đợt 1: Nhật ký chung + Sổ Cái (Wave 1); Đợt 2: Sổ chi tiết TK (Wave 2) |
| **Q3** | P1.1 audit: IP capture hay kèm hash-chain? | ✅ **Chỉ IP/UserAgent capture (A)** — hash-chain (B) defer |
| **Q4** | Phạm vi P2? | ✅ **Chỉ tài liệu/khảo sát P2** (không code) — chờ nhu cầu khách hàng |

---

## 5. THIẾT KẾ CHI TIẾT (tóm tắt — chi tiết trong từng task card)

### P0.1 — Kiểm soát 20 triệu
- Domain/config: threshold config per-tenant (default `20_000_000`) + feature flag `Payment_20M_Control_Enabled` (PG).
- Services: `IPaymentComplianceService` (CoreHub): `EvaluatePaymentAsync(order/entry)` → result {Warning, RequiresReason} khi `PaymentMethod ∈ {CASH, COD}` và tổng ≥ ngưỡng; theo dõi **gộp cùng ngày cùng NCC** (nhóm hóa đơn theo Vendor + TransactionDate); `GetCashPaymentReviewAsync(period)` → báo cáo rà soát.
- Hook: OrderService (create/payment) + phiếu chi mua hàng (ExpenseEntry/ManualEntry) + e-invoice issue (P0.3).
- Audit: `AuditLog.ForCreate` + reason vào diễn giải; UI warning modal (UI Platform).

### P0.2 — Sổ Cái / Nhật ký chung / Sổ chi tiết
- Data: `JournalEntry` (HKDBookRepository — đã có theo tenant + period + bookType).
- Services: `IGeneralLedgerService` (CoreHub): `GetJournalAsync(tenant, period)` (Nhật ký chung — thứ tự thời gian, số chứng từ, diễn giải, TK Nợ/Có, số tiền) · `GetLedgerAsync(tenant, period, accountCode)` (Sổ Cái: dư đầu kỳ + phát sinh Nợ/Có + dư cuối kỳ) · `GetSubLedgerAsync(tenant, period, accountCode, doiTuong)` (Sổ chi tiết TK 112/131/331...).
- UI: 3 trang mới `/accounting/journal`, `/accounting/ledger?tk=`, `/accounting/sub-ledger?tk=&doiTuong=` + bộ lọc tháng + In/Excel + menu "Kế toán → Sổ sách" (Owner + ChiefAccountant) + Sitemap + bUnit.
- Validate: tổng Nợ = tổng Có trên từng chứng từ + dư cuối kỳ khớp TrialBalance.

### P0.3 — HĐĐT điều chỉnh/thay thế
- `IEInvoiceProvider` += `AdjustInvoiceAsync(request, reason, adjustType: "adjust"|"replace")` (Viettel/MISA API tương ứng); orchestrator += `AdjustInvoiceAsync`; UI `InvoiceManagement` += action Điều chỉnh/Thay thế (modal lý do + dữ liệu mới) + audit (`AuditActionType.Correction` reuse) + ghi chú kê khai kỳ phát sinh.

### P0.4 — Chuẩn dữ liệu HĐĐT NĐ 254/2026 + TT 91/2026
- Rà soát `MisaDTOs.cs`/`ViettelDTOs.cs` + `EInvoiceRequest` mapping (kỳ hiệu lực cũ NĐ 123/2020 era) → cập nhật theo định dạng/trường mới (hóa đơn máy tính tiền, ký hiệu mẫu, mẫu HĐĐT, hóa đơn không mã); tham số hóa phần cấu hình chuẩn; cập nhật tests (`EInvoiceProviderTests`, `EInvoiceDISmokeTests`); tài liệu mapping table.

### P1.1 — Audit IP + tamper-evident
- A: `AuditTrailService` đọc `HttpContext` → truyền `IpAddress`/`UserAgent` vào mọi factory call (Create/Update/Delete/PeriodClose/Reopen/Correction/Reversal).
- B (tùy chọn): `AuditLog` += `PrevHash`/`Hash` (SHA-256 chuỗi nối dữ liệu + PrevHash) + migration + backfill + test chuỗi bất biến.

### P1.2 — Reopen phê duyệt + cảnh báo
- Role gate: chỉ `Owner`/`ChiefAccountant` (hiện đang kiểm tra — xác minh UI gate `PeriodClosing.razor`); confirm modal hiển thị **số lượng entries sẽ bị đảo** + cảnh báo đỏ; bắt buộc lý do (đã có); audit đã có (PeriodReopen).

### P1.3 — Cảnh báo âm quỹ/âm kho
- Services: `IBalanceAlertService` — số dư 111 theo JournalEntry (PG) + tồn kho 152/156 (SQLite ShopERP) tại thời điểm ghi; hook OrderService/POS + phiếu chi; UI toast cảnh báo (KHÔNG chặn — config).

### P1.4 — Hóa đơn đầu vào + DN bỏ trốn
- Module mới (không entity kế toán): bảng theo dõi hóa đơn đầu vào (MST NCC, số hóa đơn, ký hiệu, ngày, tiền, trạng thái: hợp lệ/rủi ro) + nhập danh sách rủi ro (từ thông báo TCT) + cảnh báo khi tổng ≥ 20 triệu tiền mặt (liên kết P0.1) + báo cáo đối chiếu.

### P1.5 — Checklist + Quy chế
- Cập nhật checklist: thêm TT 152/2025 (HKD — đã trong code) + TT 58/2026 (xác minh phạm vi) + Quy chế sử dụng PMKT (template `docs/legal/` hoặc `docs/operations/`).

### P2.1 — Chữ ký số BCTC
- `IDocumentSigner` (stub + provider USB/HSM tùy chọn) + xuất PDF BCTC (B01-B04) + nút "Ký số" (Owner) + lưu bản ký + audit.

### P2.2 — Retention
- Không xóa dữ liệu tự động (append-only — đã đúng); báo cáo tuổi dữ liệu (min/max CreatedAt theo entity + cảnh báo khi > 5/10 năm); docs hướng dẫn archive/sao lưu 2 nơi (đã có `backup-db.sh`).

### P2.3 — Giao dịch liên kết
- Chỉ khi user xác nhận: entity theo dõi bên liên kết + báo cáo + tờ khai mẫu theo NĐ 255/2026.

### P2.4 — IFRS
- Tài liệu: đối tượng áp dụng (TT 118/2026), gap VAS ↔ IFRS, roadmap tích hợp (không code).

---

## 6. EXECUTION STRATEGY (waves — session-per-phase, precedent Thu chi công nợ/Booking/VA-IIE)

| Wave | Phase | Deliverable | Validation | Push |
|---|---|---|---|---|
| W1 | P0.1 + P0.2-Đ1 (Nhật ký chung + Sổ Cái) | Services + UI + tests (bUnit) | guard + build + Core.Tests + ShopERP.Tests | FAST PUSH → CD → RV L1-L3 |
| W2 | P0.2-Đ2 (Sổ chi tiết) + P0.4 | Sổ chi tiết + DTO mapping HĐĐT | như trên + EInvoice tests | FAST PUSH → CD → RV L1-L3 |
| W3 | P0.3 | Provider adjust/replace + UI | như trên + EInvoice tests | FAST PUSH → CD → RV L1-L4 (sandbox provider) |
| W4 | P1.1(A) + P1.2 + P1.3 | Audit IP + reopen gate + alert | như trên | FAST PUSH → CD → RV L1-L3 |
| W5 | P1.4 + P1.5 + **P2 (chỉ tài liệu — Q4)** | Hóa đơn đầu vào + checklist/quy chế + khảo sát P2 | như trên | FAST PUSH → CD → RV L1-L3 |

- **KHÔNG push** trừ khi user yêu cầu từng wave (governance Git). RV 5 lớp sau mỗi wave deploy.
- Playwright DISABLED trong IMPLEMENT; E2E spec mới cho UI mới (Gate 4) chạy ở RV production.

---

## 7. RỦI RO & MITIGATION

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | **Cảnh báo 20 triệu chặn nhầm nghiệp vụ hợp lệ** (khách quen trả tiền mặt lớn) | Mặc định CẢNH BÁO + bắt buộc lý do (không chặn cứng — Q1); override có audit |
| R2 | **Sổ cái/Nhật ký chung sai số liệu** (JournalEntry chưa đủ? double-entry không đầy đủ cho mọi nghiệp vụ) | Verify nguồn `JournalEntry` phủ toàn bộ nghiệp vụ (Order/phiếu tay/import/công nợ/booking) trước khi build UI; test đối chiếu tổng Nợ=Có + khớp TrialBalance |
| R3 | **Provider API điều chỉnh/thay thế khác nhau** (Viettel vs MISA) | Abstraction qua provider interface + test contract từng provider; fail-safe: hóa đơn điều chỉnh thủ công qua UI + ghi chú |
| R4 | **Chuẩn dữ liệu NĐ 254/2026 phụ thuộc bản cập nhật phía provider/TCT** | Rà soát mapping + cấu hình hóa phần chuẩn; tài liệu mapping; test smoke; cập nhật khi provider nâng cấp |
| R5 | **Reopen đảo toàn bộ entries gây mất dữ liệu nếu nhầm** | Confirm modal hiển thị số lượng entries + cảnh báo đỏ + role gate + audit (P1.2) |
| R6 | **Regress báo cáo kế toán hiện có** | KHÔNG sửa logic IncomeStatement/BalanceSheet/TrialBalance — chỉ thêm service đọc; chạy Core.Tests hồi quy toàn bộ |
| R7 | **Audit hash-chain phá vỡ append-only/migration phức tạp** | Option A trước (IP — không migration); B làm riêng nếu user duyệt — migration + backfill + test chuỗi |
| R8 | **Phạm vi P2 quá rộng** | Q4 chốt phạm vi; P2.3/P2.4 chỉ khảo sát + tài liệu |

---

## 8. TEST STRATEGY

- **Unit/Integration (Core.Tests):** P0.1 (ngưỡng 20M + gộp cùng ngày + báo cáo rà soát + audit) · P0.2 (nhật ký chung/sổ cái/sổ chi tiết: dư đầu kỳ, phát sinh, dư cuối, khớp TrialBalance, isolation tenant) · P0.3 (adjust/replace contract + kê khai kỳ) · P0.4 (mapping mới + DTO) · P1.1 (IP/UserAgent capture + hash chain B) · P1.2 (role gate + lý do bắt buộc) · P1.3 (số dư 111/âm kho) · P1.4 (hóa đơn đầu vào + cờ rủi ro).
- **bUnit (ShopERP.Tests):** các trang mới render + modal cảnh báo + menu/Sitemap.
- **E2E (Gate 4 — chạy ở RV):** spec cho sổ cái + cảnh báo 20M + hóa đơn đầu vào.
- **Validation mỗi batch:** `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests MUST PASS.

---

## 9. SUCCESS CRITERIA (DoD)

- [ ] P0: kiểm soát 20 triệu (cảnh báo + báo cáo rà soát) · Sổ Cái/Nhật ký chung/Sổ chi tiết DN xuất được In/Excel · HĐĐT điều chỉnh/thay thế qua provider + UI · chuẩn dữ liệu mapping theo NĐ 254/2026 + TT 91/2026
- [ ] P1: audit có IP/UserAgent (+ hash-chain nếu duyệt B) · reopen có phê duyệt + cảnh báo · cảnh báo âm quỹ/âm kho · hóa đơn đầu vào + DN bỏ trốn · checklist + Quy chế PMKT
- [ ] P2 (Q4 — chỉ tài liệu): khảo sát chữ ký số + retention + giao dịch liên kết + IFRS (không code — chờ nhu cầu)
- [ ] guard + build + tests PASS mọi batch · Deploy + RV production PASS · task cards đóng từng phase