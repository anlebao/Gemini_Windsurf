# TASK CARD: KẾ TOÁN — NHẬP LIỆU & SỔ SÁCH ĐẦY ĐỦ (Import Excel · Số dư đầu kỳ · Ngày nghiệp vụ · Phiếu tay → Sổ HKD/BCTC)

> Created: 2026-10-07 (user directive: tenant mới bắt đầu dùng kế toán phải nhập được dữ liệu cũ từ Excel; chốt xử lý #1-#4)
> Master plan: `docs/AI/plans/ke-toan-nhap-lieu-master-plan.md` (Q1-Q5 user chốt + chiến lược §6 approved 2026-10-07)
> Branch: `main`
> Status: **✅ COMPLETE (2026-10-08) — P1-P6 DONE · deploy + RV PASS · full test matrix PASS · đóng master plan + task card**

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

### Phase 2 — #4 JournalEntry cho phiếu tay (task `task_ke_toan_nhap_lieu_phase2_journal_entry.md`) ✅ DONE 2026-10-07

- [x] **P2.1 `ManualEntryJournalBridge`** (`3_CoreHub/Services/Journal/` — tách riêng, rollback an toàn): phiếu thu doanh thu (5xx/7xx) → **Nợ 111 / Có {TK}** · chi phí (6xx) → **Nợ {TK} / Có 111** · EntryDate = TransactionDate (đúng kỳ nghiệp vụ) · ReferenceType "ManualEntry" + ReferenceId = AccountingEntry.Id · `AddToBookAsync(S2b_HKD)` (template B01-B09 đọc theo EntryDate — đủ cho cả bộ sổ) · **idempotent** (ExistsByReference trước khi tạo) · **fail-safe** (lỗi JE không throw — phiếu vẫn lưu, log warning)
- [x] **P2.2 Công nợ:** KHÔNG tạo JE ([G6] — ResolveAccounts trả null cho Adjustment 131/331) — test chốt PASS
- [x] **P2.3 Reversal đồng bộ:** `CreateReversalForAsync` — tìm JE gốc (GetByReference "ManualEntry") → nếu có → JE reversal (IsReversal + ReversedJournalId + đảo Nợ/Có + ReferenceType "ManualReversal"); idempotent; hook ở **cả `AccountingEntryService.CreateReversalEntryAsync` + `ReversalService.CreateReversalEntryAsync`** (optional ctor param — không vỡ call site)
- [x] **P2.4 Fail-safe kép:** bridge fail-safe nội bộ + hook try/catch (bridge lạ throw → phiếu vẫn lưu)
- [x] **P2.5 `IBackfillJournalEntriesService`:** Preview (dry-run đếm eligible: Revenue/Expense + 5xx/6xx/7xx + chưa đảo + chưa bị đảo + chưa có JE) · Run (tạo qua bridge + đối chiếu) · **KHÔNG tự startup** — endpoint Gateway `POST /api/accounting/backfill-journal-entries?dryRun=true` (tenant-scoped; dryRun mặc định TRUE — an toàn)
- [x] **P2.6 DI:** CoreHub + ShopERP + Gateway Program.cs (bridge + backfill)
- [x] **P2.7 Tests (+15):** `ManualEntryJournalBridgeTests` (12 — revenue/expense/công nợ [G6]/reversal skip/idempotent/fail-safe/reversal JE đảo Nợ-Có/hooks AccountingEntryService + ReversalService) + `BackfillJournalEntriesServiceTests` (3 — preview dry-run/run đối chiếu/loại đã có JE + phiếu đã đảo) + update `AccountingEntriesControllerTests` (mock backfill) → **Core.Tests 2147 PASS (+15)** · ShopERP.Tests 163 PASS
- [x] **Validation:** build VanAn.sln 0 errors · guard ALL PASSED · commit `fa07b31a` → **push Đợt 2 (CD #37643335425 SUCCESS) + RV L1-L4 PASS:** L1 markers (`ManualEntryJournalBridge`/`BackfillJournalEntriesService` ×2 trong CoreHub.dll) · L2 backfill endpoint live (dry-run → 65 eligible) · L3 **backfill run 65/65 + SQL đối chiếu 65 JE == 65 eligible (tenant 1)** · L4 **phiếu thu mới qua Gateway API → JE `ManualEntry` tự sinh đúng ngày nghiệp vụ** + **reversal → JE `ManualReversal` (IsReversal=true) đồng bộ** + Sổ HKD page mở (test data đã dọn — net zero)

### Phase 3 — #2 Số dư đầu kỳ (task `task_ke_toan_nhap_lieu_phase3_opening_balance.md`) 🔄 CODE DONE — chờ guard + push Đợt 3 + CD + RV

- [x] **P3.1 `IOpeningBalanceService`** (`3_CoreHub/Services/CongNo/OpeningBalanceService.cs`): `SaveAsync`/`GetAsync` — validate TK (3 số, chặn 5xx/6xx/7xx/8xx — kết quả KD không có số dư đầu kỳ) · không âm · **ΣNợ == ΣCó** (chênh → throw kèm số chênh + gợi ý dòng 421 — Q2) · **period-closing guard** (kỳ TRƯỚC mốc bắt đầu phải Open) · **khai 1 lần/kỳ** (chặn khai lại — sửa = đảo bút toán cũ rồi khai lại) · multi-tenancy (repo filter — lesson a21f97f2)
- [x] **P3.2 Tạo dữ liệu:** (a) 1 AccountingEntry tổng (Adjustment, "Số dư đầu kỳ {MM}/{yyyy}", TransactionDate = **ngày cuối kỳ trước** — đầu kỳ của báo cáo) + 1 JournalEntry **"OpeningBalance"** (ReferenceType + ReferenceId = entry; Nợ TK dư Nợ / Có TK dư Có — từng line) → BalanceSheet/B01 · (b) **công nợ cũ theo đối tượng** (131/331 per khách/người bán — nguồn Excel) qua `ICongNoService` (phiếu + Vendor, ngày khai báo) → báo cáo công nợ Đầu kỳ + tuổi nợ đúng — **KHÔNG JE [Q1/G6]**
- [x] **P3.3 Bridge mở rộng:** `ManualEntryJournalBridge.CreateReversalForAsync` hỗ trợ JE gốc **"OpeningBalance"** (reversal → "OpeningBalanceReversal", đảo Nợ/Có, idempotent) — cho phép sửa đầu kỳ qua đảo
- [x] **P3.4 UI `/accounting/opening-balance`** (`OpeningBalance.razor` — UI Platform 100%): chọn kỳ bắt đầu · phần 1 "Số Dư Các Tài Khoản" (bảng TK + Nợ/Có + thêm/xoá dòng + tổng + cảnh báo chênh + nút **"Bù vào 421"**) · phần 2 "Công Nợ Cũ Theo Đối Tượng" (TK 131/331 + đối tượng + tiền) · nút Lưu + "Xem đã khai" · NavMenu "Kế toán → **Số Dư Đầu Kỳ**" + Sitemap `link-accounting-opening-balance` · bUnit +5
- [x] **P3.5 Tests (+9 Core / +5 bUnit):** `OpeningBalanceServiceTests` (8 — hợp lệ tạo entry+JE 3 lines · công nợ 131/331 qua CongNoService không JE · ΣNợ≠ΣCó chặn + gợi ý 421 · TK 511 chặn · kỳ trước đóng chặn · khai lại chặn · isolation tenant · Get trả dữ liệu) + bridge OpeningBalance reversal (1) + `OpeningBalancePageTests` (5 — render 2 phần · thêm dòng · chênh + nút bù 421 · lưu gọi service) → **Core.Tests 2156 PASS (+9)** · ShopERP.Tests 168 PASS (+5)
- [x] **Validation đã chạy:** build VanAn.sln 0 errors · Core.Tests 2156 · ShopERP.Tests 168
- [ ] **Còn lại (session mới):** guard-check.ps1 full → commit → **push Đợt 3 + CD + RV L1-L4** (khai đầu kỳ thật trên tenant demo → đối chiếu BalanceSheet/B01 + công nợ Đầu kỳ)

### Phase 4 — #1 Import Excel (task `task_ke_toan_nhap_lieu_phase4_import_excel.md`) ⏳

> **PREP:** EPPlus (đã có) · `IAccountingService`/`ICongNoService` (đầu vào import) · P2 (JournalEntry tự sinh) · mẫu cột khớp UI phiếu.

- [x] **P4.1 Mẫu file:** xlsx + CSV — cột `Ngày | Loại phiếu | Tài khoản | Số tiền | Đối tượng | MST | Diễn giải | Số chứng từ` (Loại phiếu khớp 6 giá trị UI)
- [x] **P4.2 `IImportService.ImportAsync`:** parse + validate từng dòng (ngày/TK/tiền/đối tượng/kỳ chưa đóng) → **dry-run bảng lỗi** (Q4) → 0 lỗi mới lưu (qua services — giữ immutable + guard + dấu)
- [x] **P4.3 UI `/accounting/import`:** tải mẫu + upload + bảng kết quả/lỗi · NavMenu + Sitemap + bUnit
- [x] **P4.4 Tests:** import hợp lệ tạo đúng phiếu + JE · dòng lỗi liệt kê + không ghi gì (dry-run) · xlsx == csv · isolation
- [x] **Validation:** guard ALL PASSED (18:22) + build 1 warning + Core.Tests 8/8 mới (full gate PASS) + ShopERP.Tests 172/172 → **commit `877901c4` (KHÔNG push)**

### Phase 5 — E2E + hardening ✅

- [x] **P5.1 E2E `accounting-import.spec.ts`** (self-gating + storageState production pattern — commit 2026-10-08): 4 tests — render /accounting/import (tải mẫu + upload + 8 cột) · Sitemap link · lỗi dòng (Q4 — bảng lỗi + KHÔNG nút Lưu) · flow: upload CSV 2 phiếu công nợ 131 → preview 0 lỗi → Lưu 2/2 → báo cáo công nợ 2 đối tượng → cleanup đảo bút toán (G10 — flow dùng CHỈ công nợ 131 Tang>0 để cleanup được; JE + [G6] verify trong Core.Tests)
- [x] **P5.2 Full test matrix:** guard ALL PASSED · build 1 warning · Core.Tests **2164 PASS** (15m21s — full gồm Performance/Integration) · ShopERP.Tests 172 · Architecture 41 → commit

### Phase 6 — Deploy + RV ✅

- [x] **P6.1 PUSH** (FAST PUSH pattern #11) — Đợt 4 `55989664..f6cb4aba` (`877901c4`+`f6cb4aba`) + verify sha == HEAD
- [x] **P6.2 CD Multi-VPS** #37775223372 SUCCESS (11 jobs + smoke — KHÔNG migration mới)
- [x] **P6.3 RV L1/L2:** markers (`ImportService` ×2 CoreHub.dll · ×1 ShopERP.dll + route `accounting/import` ×1) + health 200 ×2 + route 302
- [x] **P6.4 RV L3/L4:** flow thật (Vạn An Test 0dfab177): upload CSV 4 dòng (thu 511 · chi 642 · công nợ 131 · trả nợ 331) → preview 0 lỗi → lưu 4/4 → cong-no "RV Khách Import" 3.000.000 ✓ → history ✓ · PG verify: entries đúng loại/dấu [G9]/TransactionDate + **JE chỉ thu/chi — công nợ KHÔNG JE [G6]** · demo data giữ
- [x] **P6.5 ĐÓNG:** project_state + master plan (✅ COMPLETE) + task card (✅ COMPLETE) → báo user

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
