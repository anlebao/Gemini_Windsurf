# TASK CARD: P0.2 — SỔ CÁI + SỔ NHẬT KÝ CHUNG + SỔ CHI TIẾT TK (DN)

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P0.2)
> Branch: `main`
> Status: ✅ **Q2 CHỐT (2026-10-16): chia 2 đợt — Đ1 (Nhật ký chung + Sổ Cái) READY IMPLEMENT (Wave 1) · Đ2 (Sổ chi tiết) Wave 2**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **3.1** — kết xuất **Sổ tổng hợp (Sổ Nhật ký chung + Sổ Cái các TK)** và **Sổ chi tiết TK (112, 131, 331...)** cho DN (TT 133/2016 · TT 99/2025 · TT 71/2024) — bộ sổ chuẩn mực thanh tra luôn yêu cầu.
- **Phát hiện review (2026-10-16):** ShopERP chỉ có `TrialBalance`/`BalanceSheet`/`IncomeStatement`/`CashFlowStatement`/`FinancialStatementNotes`/`HKDBooks`/`CongNoLedger` — **grep "Sổ cái/Nhật ký chung/Sổ quỹ" không có kết quả trong UI ShopERP** → ❌ THIẾU Sổ Cái + Nhật ký chung + Sổ chi tiết TK cho DN.
- **Đã xác minh code:** nguồn dữ liệu `JournalEntry` ĐÃ có (double-entry: `ManualEntryJournalBridge.cs`, `HKDBookRepository.cs` — GetByBookTypeAndPeriodAsync, `BackfillJournalEntriesService`, Order/import/công nợ/booking đều ghi JournalEntry) — chỉ cần báo cáo/UI, KHÔNG cần dữ liệu mới.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 1 cùng P0.1.
- **Execution Mode:** IMPLEMENT (sau khi user chốt Q2).
- **Skills (max 3):** `dynamic-hkd-book-architecture` (pattern sổ) + `accounting-ui-implementation` + `domain-integrity-validation`.

## 3. SCOPE

### 3.0 Pre-validation (bắt buộc trước khi code — R2)
- [ ] Verify `JournalEntry` phủ TOÀN BỘ nghiệp vụ: Order/POS · phiếu thu/chi tay (ManualEntry) · import Excel · công nợ 131/331 · booking/đặt cọc — đối chiếu số lượng entries vs AccountingEntry/Order (test rà soát, không fix data trừ khi phát hiện thiếu).

### 3.1 Services (`3_CoreHub/Services/GeneralLedger/`) — chia 2 đợt (Q2)
**Đợt 1 (Wave 1):**
- [ ] `IGeneralLedgerService`:
  - `GetJournalAsync(tenantId, year, month)` → **Sổ Nhật ký chung**: thứ tự thời gian (TransactionDate + CreatedAt), số chứng từ, diễn giải, TK Nợ, TK Có, số tiền, tài liệu kèm — mỗi JournalEntry (cặp Nợ/Có hiển thị 2 dòng hoặc cột Nợ/Có).
  - `GetLedgerAsync(tenantId, year, month, accountCode)` → **Sổ Cái TK**: dư đầu kỳ · phát sinh Nợ · phát sinh Có · dư cuối kỳ (cộng dồn theo thời gian).
  - `GetBalancesAsync(tenantId, year, month)` → số dư theo TK (dùng cho đối chiếu + cảnh báo P1.3).
**Đợt 2 (Wave 2):**
- [ ] `GetSubLedgerAsync(tenantId, year, month, accountCode, doiTuong?)` → **Sổ chi tiết** (112, 131, 331...): lọc theo đối tượng/khách/vendor nếu có.
- [ ] Đối chiếu: tổng Nợ = tổng Có từng chứng từ + dư cuối kỳ Sổ Cái khớp `TrialBalance`.
- [ ] Repository: mở rộng `IHKDBookRepository` hoặc repository mới `GetByTenantAndPeriodOrderedAsync` (filter TenantId — lesson a21f97f2; theo TransactionDate — lesson ngày nghiệp vụ).
- [ ] DI trong `3_CoreHub/Program.cs`.

### 3.2 UI (ShopERP — UI Platform 100%)
**Đợt 1 (Wave 1):**
- [ ] `/accounting/journal` — Sổ Nhật ký chung (bộ lọc tháng + In + Excel).
- [ ] `/accounting/ledger?tk=111` — Sổ Cái (dropdown chọn TK theo hệ thống tài khoản + tháng + In/Excel).
**Đợt 2 (Wave 2):**
- [ ] `/accounting/sub-ledger?tk=131&doiTuong=` — Sổ chi tiết (filter đối tượng).
- [ ] NavMenu "Kế toán → Sổ sách" (Owner + ChiefAccountant) + Sitemap + AccountingIndex quick actions + bUnit (từng đợt).

### 3.3 Tests
- [ ] Core.Tests: nhật ký chung (thứ tự + đủ entries + tổng Nợ=Có) · sổ cái (dư đầu kỳ, phát sinh, dư cuối; 2 kỳ liên tiếp) · sổ chi tiết 131 theo đối tượng · khớp TrialBalance · isolation tenant.
- [ ] bUnit: 3 trang render + lọc TK/đối tượng + menu.
- [ ] E2E spec (Gate 4 — chạy ở RV): tạo phiếu thu → sổ cái 111 hiển thị → nhật ký chung → sổ chi tiết.

## 4. DECISIONS — ✅ Q2 CHỐT (2026-10-16)

| # | Câu hỏi | ✅ QUYẾT ĐỊNH |
|---|---|---|
| Q2 | Làm cả 3 sổ trong 1 phase hay chia đợt? | ✅ **Chia 2 đợt** — Đợt 1: Nhật ký chung + Sổ Cái (Wave 1); Đợt 2: Sổ chi tiết TK (Wave 2) |

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs · KHÔNG entity/migration mới (đọc từ JournalEntry hiện có) · KHÔNG sửa logic IncomeStatement/BalanceSheet/TrialBalance/CashFlow (chỉ thêm service đọc + test regress) · Multi-tenancy mọi query · UI Platform 100% (Gate 5) · E2E spec cho UI mới (Gate 4) · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS trước commit.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Sổ Nhật ký chung + Sổ Cái + Sổ chi tiết TK kết xuất đúng, In/Excel được
- [ ] Đối chiếu Nợ=Có + khớp TrialBalance (test)
- [ ] Menu + Sitemap + bUnit + E2E spec
- [ ] guard + build + tests PASS · Deploy + RV production PASS → đóng

## 7. DEFERRED

- Sổ chi tiết TSCĐ/khấu hao (bảng tính khấu hao) · phân bổ chi phí trả trước (242) · sổ quỹ tiền mặt theo TT 200 cũ (đã hết hiệu lực — TT 99/2025 mẫu sổ mới) · xuất XML chuẩn eTax (P0.4/P1.4 liên quan)