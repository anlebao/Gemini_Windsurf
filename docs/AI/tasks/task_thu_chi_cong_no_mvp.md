# TASK CARD: THU CHI & CÔNG NỢ (PHẢI THU / PHẢI TRẢ) MVP

> Created: 2026-10-06 (user directive: "chỉ cần thu chi, báo cáo công nợ, phải thu phải trả, theo dõi phải trả phải thu" + chốt Q1-Q5)
> Source SRS: `docs/requirements/van_an_thu_chi_cong_no_srs_v1.md` (v1.1 — user chốt Q1-Q5 2026-10-06)
> Master plan: `docs/AI/plans/thu-chi-cong-no-master-plan.md` (ACTIVE — chờ approve)
> Branch: `main`
> Status: **PLAN READY — chờ user approve → Session P1 Services**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** mở rộng module kế toán hiện tại đáp ứng 4 nhu cầu: (1) thu chi (đã có) · (2) phải thu (khách nợ) · (3) phải trả (nợ người bán) · (4) theo dõi từng đối tượng (phát sinh → đã thu/trả → còn lại) + **tuổi nợ** (Q4) + **truy ngược lịch sử thanh toán** (Q5) + **tra MST → tên công ty** (Q3).
- **Nguyên tắc (SRS §4 G1-G13 — kịch bản đơn giản nhất):** KHÔNG entity/bảng/migration mới — tận dụng `AccountingEntry` (immutable) + `AccountCode 131/331` + `Vendor` + `ReferenceType` · nhập tay qua phiếu (không đụng Order) · bán chịu không ghi 511 · sửa sai = reversal.
- **Đã xác minh code (2026-10-06):** `AccountingEntryService` server **không chặn** 131/331 + Amount âm (chỉ UI whitelist 5xx/7xx + 6xx) · `BusinessInfoApiClient` + Gateway `GET /api/v1/business-info/{mst}` **đã có** (phiếu thu/chi đang dùng) · `AccountingEntry.Vendor/ReferenceType/ReferenceId` sẵn sàng.
- **Điều kiện trigger — ĐÃ THỎA:** SRS v1.1 hoàn tất + Q1-Q5 user chốt (2026-10-06).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** ANALYZE (hiện tại — plan + task card chờ duyệt) → IMPLEMENT (sau user approve)
- **Skills (max 3):** `domain-integrity-validation` (AccountingEntry immutable — KHÔNG đụng Domain.cs) + `pattern-based-fixing` (regress báo cáo) + `test-system-upgrade` (test matrix)
- **Session strategy (session-per-phase — precedent Booking/VA-IIE):**
  - Session 1 (P1): Services core + FIFO aging + tests → guard/build/Core.Tests PASS → commit
  - Session 2 (P2): UI phiếu thu/chi 3 loại + 2 trang + menu + bUnit → guard/ShopERP.Tests PASS → commit
  - Session 3 (P3): E2E spec + full test matrix → guard/build/tests PASS
  - Session 4 (P4): PUSH → CD Multi-VPS (KHÔNG migration) → RV L1-L5 → đóng

## 3. SCOPE (4 phases per master plan §4)

### Phase 1 — Services core + tests (task `task_thu_chi_cong_no_phase1_services.md`) ⏳

- [ ] **P1.1 Xác minh nền:** IncomeStatement/TrialBalance filter theo EntryType hay AccountCode → entry 131/331 KHÔNG lọt vào doanh thu/chi phí [G6]; quyết EntryType cho phiếu công nợ (Revenue/Expense vs Adjustment — nếu filter theo EntryType → dùng Adjustment). Kiểm tra cách `CheckDuplicateEntryAsync` ảnh hưởng phiếu công nợ (cùng amount+account+type trong 5' → chặn trùng; công nợ cùng số tiền lặp lại hợp lệ? → tinh chỉnh reference-aware nếu cần).
- [ ] **P1.2 `ICongNoService`/`CongNoService`** (3_CoreHub/Services/CongNo/ hoặc mở rộng AccountingEntryService — chốt tại P1):
  - `CreateReceivableAsync(tenantId, period, amount, doiTuong, mst?, description, transactionDate, bool isPayment)` — 131 dương (bán chịu) / âm (thu nợ)
  - `CreatePayableAsync(...)` — 331 dương (mua chịu) / âm (trả nợ)
  - `GetCongNoReportAsync(tenantId, year, month)` → `CongNoReportDto` (2 khối 131/331; mỗi đối tượng: Đầu kỳ · PS tăng · Đã thu/trả · Cuối kỳ · **Tuổi nợ <30/30-60/60-90/>90 — FIFO động** [G12])
  - `GetDoiTuongLedgerAsync(tenantId, accountCode, vendor, year, month)` — sổ cộng dồn
  - `GetKhoanNoPaymentsAsync(tenantId, khoanNoEntryId)` — lịch sử thanh toán 1 khoản (FIFO động) [FR-8.1]
  - Mọi query filter TenantId (lesson a21f97f2) · tuổi nợ dựa TransactionDate (+CreatedAt tie-break) [G12]
- [ ] **P1.3 Reversal:** đảo phiếu công nợ (reversal hiện có) → số dư tự khớp [G10]
- [ ] **P1.4 Tests (Core.Tests):** kịch bản A (bán chịu 5tr + thu 2tr → cuối kỳ 3tr) · B (mua chịu/trả nợ) · C (reversal) · D (FIFO tuổi nợ: 2 khoản 01/07 + 15/08, trả 20/09 → >90: 3tr · 30-60: 3tr) · D2 (lịch sử thanh toán khoản) · isolation tenant · IncomeStatement không tính 131/331 (regress) → Core.Tests PASS

### Phase 2 — UI (task `task_thu_chi_cong_no_phase2_ui.md`) ⏳

- [ ] **P2.1 Phiếu thu** (`RevenueEntry.razor`): dropdown "Loại phiếu" {Thu doanh thu · Ghi nhận phải thu · Thu tiền khách trả nợ} → loại công nợ: hiện ô **Đối tượng (bắt buộc)** + ô **MST + nút "Tra cứu"** (reuse `BusinessInfoApiClient` — điền tên vào Đối tượng [G11]) + whitelist account 131 + diễn giải gợi ý kèm MST → gọi `ICongNoService.CreateReceivableAsync`
- [ ] **P2.2 Phiếu chi** (`ExpenseEntry.razor`): tương tự — {Chi phí · Ghi nhận phải trả · Trả tiền người bán} → 331
- [ ] **P2.3 Trang `/accounting/cong-no`** (UI Platform 100%): bộ lọc tháng + bảng 2 khối 131/331 (mockup SRS FR-7: Đầu kỳ · PS tăng · Đã thu/trả · Cuối kỳ · Tuổi nợ) + lọc "Tất cả/Còn nợ/Hết nợ" + **In / Xuất Excel** (EPPlus pattern) + click đối tượng → sổ chi tiết
- [ ] **P2.4 Trang `/accounting/cong-no/{loai}/{doiTuong}`**: sổ chi tiết cộng dồn (FR-8) + click 1 khoản nợ → **modal "Lịch sử thanh toán khoản nợ"** (FR-8.1) + nút **Đảo bút toán** mỗi dòng [G10]
- [ ] **P2.5 NavMenu "Kế toán → Công nợ"** (Owner) + Sitemap + **bUnit** (phiếu thu 3 loại + đối tượng bắt buộc + tra MST điền tên + báo cáo render + sổ render) → ShopERP.Tests PASS

### Phase 3 — E2E + hardening (task `task_thu_chi_cong_no_phase3_e2e.md`) ⏳

- [ ] **P3.1 E2E spec `cong-no.spec.ts`** (Gate 4): phiếu thu "Ghi nhận phải thu" + tra MST → báo cáo công nợ (số dư + tuổi nợ) → sổ chi tiết → lịch sử thanh toán — self-gating pattern
- [ ] **P3.2 Full test matrix:** guard-check + build VanAn.sln + Core.Tests + ShopERP.Tests + Architecture PASS → commit (KHÔNG push trừ khi user yêu cầu)

### Phase 4 — Deploy + RV (task `task_thu_chi_cong_no_phase4_rv.md`) ⏳

- [ ] **P4.1 PUSH** (FAST PUSH `env -u GH_TOKEN -u GITHUB_TOKEN git push --no-verify` — pattern #11) + verify sha (`git ls-remote`/gh api — keyring valid)
- [ ] **P4.2 CD Multi-VPS** (KHÔNG migration — không entity mới) — poll `gh run list --json status,conclusion`
- [ ] **P4.3 RV L1/L2:** markers (ShopERP.dll `CongNoService`/`CongNo` + Gateway dll nếu đổi) · `/health` 200 · route probe
- [ ] **P4.4 RV L3/L4:** E2E production `cong-no.spec.ts` + flow thật (phiếu thu công nợ trên tenant demo → báo cáo → tuổi nợ → lịch sử thanh toán) · L5 manual (user)
- [ ] **P4.5 ĐÓNG:** project_state + master plan + task card → thông báo user

## 4. USER DECISIONS — ĐÃ CHỐT (2026-10-06, SRS v1.1 §10)

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Bán chịu ghi đồng thời doanh thu? | KHÔNG — phiếu công nợ chỉ 131; ghi doanh thu khi thu tiền |
| Q2 | Tự động công nợ từ đơn hàng? | KHÔNG — nhập tay qua phiếu |
| Q3 | Đối tượng nhập thế nào? | Tên tự nhập + tùy chọn tra MST → tự điền tên công ty (reuse có sẵn) |
| Q4 | Tuổi nợ? | CÓ — <30/30-60/60-90/>90 ngày, FIFO động |
| Q5 | Truy ngược lịch sử thanh toán nợ? | CÓ — từng khoản: các lần thu/trả + còn lại |

## 5. CONSTRAINTS (governance)

- **AccountingEntry 100% immutable** — KHÔNG đụng Domain.cs (feature này chỉ Services + UI + đọc) — mọi sửa sai qua reversal
- KHÔNG thêm entity/bảng/migration — tận dụng AccountCode 131/331 + Vendor + ReferenceType
- Multi-tenancy mọi query (TenantId filter — lesson a21f97f2)
- KHÔNG sửa logic IncomeStatement/BalanceSheet/TrialBalance/CashFlow (chỉ verify không regress + service đọc mới)
- UI Platform 100% (Gate 5) · E2E spec cho UI mới (Gate 4) · Playwright DISABLED trong IMPLEMENT
- `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests MUST PASS trước commit
- Tra MST: reuse `BusinessInfoApiClient` — KHÔNG gọi API ngoài mới; xử lý 404/429/502 thân thiện (pattern có sẵn)
- Secrets: KHÔNG commit token/secret

## 6. SUCCESS CRITERIA (SRS §9 DoD)

- [ ] Phiếu thu/chi 3 loại + đối tượng bắt buộc + tra MST điền tên
- [ ] AccountingEntry 131/331 + Vendor + ReferenceType đúng dấu — KHÔNG entity/migration mới
- [ ] IncomeStatement/BalanceSheet không regress (test hồi quy PASS)
- [ ] Báo cáo công nợ: đầu kỳ/phát sinh/cuối kỳ + tuổi nợ FIFO đúng (kịch bản A/B/C/D)
- [ ] Sổ chi tiết cộng dồn + truy ngược lịch sử thanh toán + đảo bút toán
- [ ] Menu "Công nợ" + Sitemap + bUnit + E2E spec
- [ ] guard-check + build + tests PASS mọi batch · Deploy + RV production PASS → đóng

## 7. DEFERRED (ghi nợ — SRS §3.2 [G13])

- Tự động công nợ từ Order/POS/checkout · Danh bạ khách/cung cấp · Hạn thanh toán/quá hạn/lãi · Hợp đồng/hóa đơn/đối chiếu · Ngoại tệ/công nợ nội bộ/tạm ứng 141/138 · Tự động đối trừ với đơn hàng
