# TASK CARD: KẾ TOÁN — NHẬP LIỆU & SỔ SÁCH ĐẦY ĐỦ (Import Excel · Số dư đầu kỳ · Ngày nghiệp vụ · Phiếu tay → Sổ HKD/BCTC)

> Created: 2026-10-07 (user directive: tenant mới bắt đầu dùng kế toán phải nhập được dữ liệu cũ từ Excel; chốt xử lý #1-#4)
> Master plan: `docs/AI/plans/ke-toan-nhap-lieu-master-plan.md` (Q1-Q5 user chốt + chiến lược §6 approved 2026-10-07)
> Branch: `main`
> Status: **✅ P1 (#3) DONE — ⏳ P2 (#4 JournalEntry) kế tiếp**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** để 1 tenant bắt đầu dùng phần mềm kế toán với dữ liệu cũ (đang quản lý bằng Excel): (1) nhập dữ liệu cũ hàng loạt từ Excel · (2) khai báo số dư đầu kỳ · (3) các màn theo dõi hiển thị đúng ngày nghiệp vụ · (4) phiếu nhập tay phải vào đầy đủ Sổ HKD B01-B09 + Báo cáo tài chính (DN).
- **Đã verify (2026-10-07):** 4 gap — xem master plan §1-2 (repo lọc CreatedAt · không có opening balance · không có import · B01-B09/BCTC đọc JournalEntry riêng).
- **Điều kiện trigger:** user chốt "có, xử lý luôn #3 #4" + "lập master plan và task card trước, đợi review" — đang chờ approve.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) — hiện tại **ANALYZE (plan) — chờ review**.
- **Execution Mode:** chuyển IMPLEMENT sau khi user approve plan.
- **Skills:** `domain-integrity-validation` (AccountingEntry immutable + [G6]) · `test-system-upgrade` · `pattern-based-fixing`.
- **Session strategy (session-per-phase — precedent Công nợ):**
  - S1 = P1 (#3) · S2 = P2 (#4) · S3 = P3 (#2) · S4 = P4 (#1) · S5 = P5 (E2E) · S6 = P6 (Deploy+RV)

## 3. SCOPE (6 phases)

### Phase 1 — #3 Ngày nghiệp vụ (task `task_ke_toan_nhap_lieu_phase1_transaction_date.md`) ✅ DONE 2026-10-07

- [x] **P1.1 Repo:** thêm `GetByTenantAndTransactionDateRangeAsync` + `GetByTenantAndTransactionDatePeriodAsync` (filter TransactionDate, sort TransactionDate DESC + CreatedAt DESC — giữ UX "mới nhất trước") — **GIỮ method cũ** (duplicate-check + OrderService không đổi)
- [x] **P1.2 Service:** chuyển nội bộ `GetEntriesByDateRangeAsync` (TransactionHistory) + `GetEntriesByTenantAndPeriodAsync` (AccountBalance + dashboard) + `GetTodayRevenueAsync`/`GetRevenueByDateRangeAsync` sang repo TransactionDate — KHÔNG đổi UI (method names ổn định); verify call sites: tất cả là hiển thị + controller tìm theo Id (an toàn)
- [x] **P1.3 Migration `BackfillAccountingEntryTransactionDate`:** data fix — `UPDATE TransactionDate = CreatedAt WHERE TransactionDate = '0001-01-01'` (rows tồn tại trước khi cột có — nếu không, lịch sử sẽ ẩn phiếu cũ sau khi chuyển sang TransactionDate) — schema-neutral, Down no-op
- [x] **P1.4 Tests (+2):** `GetEntriesByDateRangeAsync_ShouldUseTransactionDateRepo` + `GetTodayRevenueAsync_ShouldUseTransactionDateRepo_AndSumRevenueOnly` + cập nhật test period sang repo mới (Verify old method Never) — **duplicate SC4/SC5/SC6 giữ nguyên** (vẫn mock repo CreatedAt — đúng semantics chống double-click 5') → **Core.Tests 2132 PASS (+2)** · ShopERP.Tests 163 PASS
- [x] **Validation:** build VanAn.sln 0 errors · guard ALL PASSED · commit → **push Đợt 1 + CD + RV L1/L2**

### Phase 2 — #4 JournalEntry cho phiếu tay (task `task_ke_toan_nhap_lieu_phase2_journal_entry.md`) ⏳

> **PREP:** pattern `OrderService.CreateRevenueEntryAsync` (L541 — Nợ 111/Có 511, AddLine, AddToBookAsync S2b) + `JournalService` (cân bằng Nợ=Có) + `HKDBookGenerationService.GetJournalEntriesAsync` (template đọc mọi JE theo EntryDate) + `AccountingEntryService`/`CongNoService` nơi hook.

- [ ] **P2.1 Service:** hook tạo JournalEntry khi lập phiếu — thu doanh thu (5xx/7xx): `Nợ 111 / Có {TK}` · chi phí (6xx): `Nợ {TK} / Có 111` · EntryDate=TransactionDate · ReferenceType="ManualEntry", ReferenceId=AccountingEntry.Id · `AddToBookAsync(S2b_HKD)`
- [ ] **P2.2 Công nợ:** KHÔNG tạo JE (Q1/G6) — test chốt
- [ ] **P2.3 Reversal:** đảo phiếu tay → JE reversal (đảo Nợ/Có) — lưu ý JournalService.CreateReversalEntryAsync chặn khác kỳ → tạo trực tiếp (pattern) hoặc điều chỉnh
- [ ] **P2.4 Fail-safe:** lỗi tạo JE không chặn phiếu (log + ghi chú)
- [ ] **P2.5 Tests:** phiếu → JE cân bằng + vào B01/B02/BCTC; công nợ không tạo JE ([G6]); reversal đồng bộ; regress Core.Tests toàn bộ
- [ ] **Validation:** guard + build + Core.Tests PASS → commit (KHÔNG push)

### Phase 3 — #2 Số dư đầu kỳ (task `task_ke_toan_nhap_lieu_phase3_opening_balance.md`) ⏳

> **PREP:** danh mục TK hiện có (AccountChart) · pattern BalanceSheet/B01 (số dư TK) · JournalEntry (P2) · period-closing guard.

- [ ] **P3.1 `IOpeningBalanceService`:** `SaveOpeningBalancesAsync(tenantId, year, month, lines[])` — validate TK/không âm/ΣNợ=ΣCó (bù 421 — Q2) · kỳ trước mốc phải Open · idempotent (1 lần/kỳ)
- [ ] **P3.2 Tạo dữ liệu:** AccountingEntry tổng (Adjustment, "Số dư đầu kỳ", TransactionDate = ngày đầu kỳ − 1) + JournalEntry "Số dư đầu kỳ" (Nợ TK dư Nợ / Có TK dư Có)
- [ ] **P3.3 UI `/accounting/opening-balance`:** chọn kỳ + bảng TK (mã/tên/Nợ/Có) + tổng + lưu · NavMenu "Kế toán → Số dư đầu kỳ" + Sitemap + bUnit
- [ ] **P3.4 Tests:** BalanceSheet/B01 số dư đúng · công nợ Đầu kỳ đúng (131/331) · chênh Nợ≠Có bị chặn · khai lại bị chặn · isolation
- [ ] **Validation:** guard + build + Core.Tests + ShopERP.Tests PASS → commit (KHÔNG push)

### Phase 4 — #1 Import Excel (task `task_ke_toan_nhap_lieu_phase4_import_excel.md`) ⏳

> **PREP:** EPPlus (đã có) · `IAccountingService`/`ICongNoService` (đầu vào import) · P2 (JournalEntry tự sinh) · mẫu cột khớp UI phiếu.

- [ ] **P4.1 Mẫu file:** xlsx + CSV — cột `Ngày | Loại phiếu | Tài khoản | Số tiền | Đối tượng | MST | Diễn giải | Số chứng từ` (Loại phiếu khớp 6 giá trị UI)
- [ ] **P4.2 `IImportService.ImportAsync`:** parse + validate từng dòng (ngày/TK/tiền/đối tượng/kỳ chưa đóng) → **dry-run bảng lỗi** (Q4) → 0 lỗi mới lưu (qua services — giữ immutable + guard + dấu)
- [ ] **P4.3 UI `/accounting/import`:** tải mẫu + upload + bảng kết quả/lỗi · NavMenu + Sitemap + bUnit
- [ ] **P4.4 Tests:** import hợp lệ tạo đúng phiếu + JE · dòng lỗi liệt kê + không ghi gì (dry-run) · xlsx == csv · isolation
- [ ] **Validation:** guard + build + Core.Tests + ShopERP.Tests PASS → commit (KHÔNG push)

### Phase 5 — E2E + hardening (task `task_ke_toan_nhap_lieu_phase5_e2e.md`) ⏳

- [ ] **P5.1 E2E `accounting-import.spec.ts`** (self-gating + storageState production pattern): khai số dư đầu kỳ → import Excel (mẫu) → báo cáo công nợ Đầu kỳ + Sổ HKD B01/B02 + BCTC thấy số liệu · reversal · lỗi dòng
- [ ] **P5.2 Full test matrix:** guard + build + Core.Tests + ShopERP.Tests + Architecture PASS → commit

### Phase 6 — Deploy + RV (task `task_ke_toan_nhap_lieu_phase6_rv.md`) ⏳

- [ ] **P6.1 PUSH** (FAST PUSH pattern #11) + verify sha
- [ ] **P6.2 CD Multi-VPS** (KHÔNG migration mới — chỉ #2 cần cột? KHÔNG — tận dụng AccountingEntry/JournalEntry hiện có) — poll gh run
- [ ] **P6.3 RV L1/L2:** markers (`OpeningBalanceService`/`ImportService`/JournalEntry hook) + health/routes
- [ ] **P6.4 RV L3/L4:** E2E production + flow thật (tenant demo: khai đầu kỳ + import Excel thật → đối chiếu công nợ/B01/BCTC) · L5 manual
- [ ] **P6.5 ĐÓNG:** project_state + master plan + task card → báo user

## 4. QUYẾT ĐỊNH — USER CHỐT (2026-10-07)

| # | Câu hỏi | Quyết định (user đồng ý đề xuất) |
|---|---|---|
| Q1 | Công nợ 131/331 có tạo JournalEntry không? | **KHÔNG** (G6) |
| Q2 | Số dư đầu kỳ Nợ≠Có? | Chặn — cho nhập 421 để bù |
| Q3 | Format import? | xlsx + CSV |
| Q4 | Import lỗi giữa chừng? | Dry-run — 0 lỗi mới lưu |
| Q5 | Phạm vi JournalEntry? | Thu/chi/đầu kỳ — công nợ ngoài |

> Chiến lược implement an toàn: master plan §6 (4 đợt push · backfill JE kiểm soát · idempotency · fail-safe · rollback · test chiến lược) — **chờ user duyệt lần cuối rồi bắt đầu P1.**

## 5. CONSTRAINTS (governance)

- AccountingEntry 100% immutable — mọi sửa sai qua reversal · KHÔNG entity/bảng/migration mới (tận dụng AccountingEntry + JournalEntry + TransactionDate đã map ở Công nợ)
- Domain layer PURE — không đụng Domain.cs (chỉ Services/Repo/UI)
- Multi-tenancy mọi query (lesson a21f97f2)
- KHÔNG sửa logic IncomeStatement/BalanceSheet/TrialBalance/CashFlow/HKD template (chỉ bổ sung nguồn JournalEntry) — [G6] giữ nguyên (công nợ không vào doanh thu/chi phí)
- UI Platform 100% · E2E spec cho UI mới (Gate 4) · Playwright DISABLED khi IMPLEMENT
- `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests MUST PASS trước commit
- Secrets: KHÔNG commit token/secret

## 6. SUCCESS CRITERIA (master plan §9)

- [ ] Lịch sử giao dịch + Số dư tài khoản theo ngày nghiệp vụ (duplicate-check giữ nguyên)
- [ ] Phiếu tay → Sổ HKD B01-B09 + BCTC (DN); công nợ KHÔNG vào ([G6])
- [ ] Số dư đầu kỳ (mọi TK, Nợ=Có [+421]) → BalanceSheet/B01 + công nợ Đầu kỳ đúng
- [ ] Import Excel xlsx+CSV: mẫu · dry-run lỗi theo dòng · 0 lỗi mới lưu · đúng phiếu + JE
- [ ] Reversal phiếu tay đồng bộ JE
- [ ] guard + build + tests PASS mọi batch · E2E · Deploy + RV PASS → đóng

## 7. DEFERRED

- Định khoản thuế GTGT đầu ra/đầu vào theo hóa đơn (phiếu tay chỉ Nợ/Có 1-1 với 111)
- Công nợ vào sổ cái HKD/BCTC (G6)
- Danh bạ đối tượng chuẩn hóa
- Import Excel phức tạp (nhiều sheet/ký hiệu riêng)
