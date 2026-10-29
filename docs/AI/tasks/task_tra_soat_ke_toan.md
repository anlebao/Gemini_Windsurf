# TASK CARD: TRA SOÁT SỐ LIỆU KẾ TOÁN (ACCOUNTING RECONCILIATION)

> Created: 2026-10-29 (SRS v1.1 approved — user duyệt "Duyệt SRS, go next" + chốt D1-D4/G7-G12)
> Source SRS: `docs/requirements/van_an_tra_soat_so_lieu_ke_toan_srs_v1.md` (v1.1)
> Master plan: `docs/AI/plans/tra-soat-ke-toan-master-plan.md` (ACTIVE — **APPROVED 2026-10-29**)
> Branch: `main`
> Status: **✅ DONE (2026-10-29 — 1 phiên liên tục P0→P5 hoàn tất: P0 `b4525c40` · P1 `20cdf541` 11/11 · P2 `55fdcd38` 13 tests · P3 `c0f41f01` 8 tests · P4 `4aec9930` bUnit+E2E · P5 `fd00e374` deploy+RV — sweep-first 17/0, API khớp 100%, E2E pass. L5 manual: user.)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** feature **Tra soát số liệu kế toán** — SystemAdmin chọn tenant + kỳ → chạy **12 nhóm check R1-R12** (read-only) → báo cáo phân mức Error/Warning/Info + gợi ý fix. Kiểm soát nội bộ: phát hiện sớm lệch số liệu (bằng chứng: 114 journal duplicate 2026-10-29 chỉ lộ khi RV thủ công).
- **Nguyên tắc (SRS §3/§6 — user chốt):** Cả service+API+UI · manual on-demand · read-only + gợi ý (KHÔNG tự sửa) · tất cả loại hình (HKD/DN/HTX) · chỉ SystemAdmin (G9) · không lưu lịch sử (G12) · R11 giữ + API mirror (G8) · contra 214/229 không special-case + Warning account ngoài 1-8 (G7).
- **Đã xác minh (2026-10-29):** `accounting-balance-rv.sh` C1-C10 chạy production 18 PASS (sau cleanup) — logic tra soát đã được chứng minh đúng · nguồn dữ liệu map (SRS §2.1) · quirk dual-identity FK · VAT 3331/offset 3387/legacy journals semantics.
- **Điều kiện trigger — ĐÃ THỎA:** SRS v1.1 approved (2026-10-29) → master plan + task card → chờ user approve để IMPLEMENT.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** IMPLEMENT (sau khi user approve master plan + task card)
- **Skills (max 3):** `domain-integrity-validation` (read-only — KHÔNG đụng Domain/AccountingEntry) + `pattern-based-fixing` (port C1-C10 — tránh lệch logic) + `test-system-upgrade` (test matrix T1-T12)
- **Session strategy — CHIẾN LƯỢC TỐI ƯU (user 2026-10-29 — nhanh, đủ):** **1 phiên liên tục P0→P4** (checkpoint build+tests+commit SAU MỖI phase — giữ phase isolation, không trộn layer) → cuối phiên **P5 push + CD + RV** (sweep-first). Lý do an toàn: feature **read-only, KHÔNG migration/entity/Domain mới** → rủi ro thấp, không cần session-per-phase. **Checkpoint bắt buộc sau P1** (service core — nền tảng mọi check): nếu build/tests đỏ → dừng fix trước khi sang P2.
  - Checkpoint 1 (P0): AccountClassifier + tests → guard/build/Core.Tests PASS → commit
  - Checkpoint 2 (P1): Service core + tests → guard/build/Core.Tests PASS → commit
  - Checkpoint 3 (P2): ShopERP mirror API + Gateway provider → guard/tests PASS → commit
  - Checkpoint 4 (P3): Gateway reconcile API → guard/tests PASS → commit
  - Checkpoint 5 (P4): UI page + bUnit + E2E spec → guard/ShopERP.Tests PASS → commit
  - Cuối phiên (P5): PUSH → CD → RV production (đối chiếu `accounting-balance-rv.sh` cùng tenant+kỳ) → L5 manual
  - *Alternative (nếu phiên quá dài): tách 4 phiên — S1: P0+P1 · S2: P2+P3 · S3: P4 · S4: P5.*

## 3. SCOPE (6 phases per master plan §4)

### Phase 0 — AccountClassifier (chuẩn hoá phân loại tài khoản — user directive 2026-10-29) ⏳

> **P0 PREP (user directive):** xử lý "Tài khoản ngoài prefix 1-8 — quan trọng nhất là **911 (Xác định kết quả kinh doanh)** khi hệ thống làm bút toán kết chuyển cuối kỳ (Nợ 511/Có 911 · Nợ 911/Có 632): lúc đó 511/632 bị zero, lợi nhuận nằm ở 911 → công thức R4 (chỉ 5/7−6/8) sẽ lệch" + "Tài khoản **malformed/rỗng** trong AccountNumber — cùng sub-check bắt được".
> **Verify production (2026-10-29 — đã làm):** journal chỉ có prefix 1-6 (1:213 lines · 2:3 · 3:25 · 4:2 · 5:113 · 6:96) — **KHÔNG có 9xx/911, KHÔNG có account lạ** → hiện tại an toàn; Phase 0 chuẩn bị xử lý cho tương lai (read-only).
> **Open items P0:** (1) regex chuẩn `^[1-9]\d{0,2}$` — chốt cùng SRS G7; (2) khi 911 xuất hiện: Warning + suggestion "mở rộng NET gộp 9xx" (không tự sửa) — xác nhận mức xử lý; (3) malformed/rỗng liệt kê tối đa bao nhiêu (limit 20 — tránh flood).

- [ ] **P0.1 `AccountClassifier` (helper thuần — `3_CoreHub/Services/Reconcile/`)** — `IsValid(code)` (regex `^[1-9]\d{0,2}$`) · `Classify(code)` → `AccountGroup {Asset, Liability, Equity, Revenue, Expense, Temporary(9xx), Unknown(malformed/rỗng)}` · `Describe(code)` cho suggestion.
- [ ] **P0.2 Xử lý 9xx/911** — hiện không có trong production (verify) · khi xuất hiện: R4 → **Warning + liệt kê** + suggestion "mở rộng NET gộp 9xx khi hệ thống làm closing entries" (SRS G7/§4.1 — read-only, không tự sửa).
- [ ] **P0.3 Xử lý malformed/rỗng** — null/"": `Unknown` · "ABC"/"01"/"11111": `Unknown` → R4 → **Warning + liệt kê** (limit 20) + suggestion "kiểm tra dữ liệu AccountNumber".
- [ ] **P0.4 Tests (T9/T13)** — seed 911 kết chuyển → R4 **Warning** (không Error) + liệt kê 911 · seed ""/"ABC"/"01"/"11111" → Warning + liệt kê · hợp lệ 1xx-9xx → không Warning · HTX 512/612 classify đúng (Revenue/Expense).

### Phase 1 — Service core + tests (`task_tra_soat_ke_toan_phase1_service.md`) ⏳

> **Session P1 PREP (cùng phiên — sau P0 checkpoint):** Đọc: `docs/AI/project_state.md` (§2 — issue #195 + RV accounting) + SRS v1.1 (§5 FR, §6 NFR, §8 T1-T13) + master plan §5.1-5.2 + `6_Testing/rv-scripts/accounting-balance-rv.sh` (logic C1-C10 — nguồn port) + `3_CoreHub/Services/GeneralLedger/GeneralLedgerService.cs` + `TrialBalanceService.cs` + `HKDBookService.cs` + `3_CoreHub/Program.cs` (DI) + **`AccountClassifier` (P0 — dùng cho R3/R4/R7/R10)**.
> **PHÁT HIỆN KIẾN TRÚC (2026-10-29 — đã verify):** (1) Service đặt `3_CoreHub/Services/Reconcile/` (namespace `VanAn.CoreHub.Services.Reconcile`), đọc `IAccountingDbContext` (PG — source of truth) — KHÔNG đụng Domain/entities; DTOs thuần. (2) R11 qua abstraction `IMirrorOrderProvider` (interface trong CoreHub; HTTP impl ở P2/P3) — null/không config → R11 skip với `info` "không chạy được" (fail-open). (3) Dual-identity: journal lines join theo `JournalEntries."Id"` (KHÔNG cột `JournalEntryId`) — EF navigation tự xử lý đúng; raw query phải đúng cột. (4) Semantic: VAT 3331 EntryType=1 nhưng KHÔNG journal; offset cọc EntryType=5 AccountCode 5/7 → R3 phải cộng; R4 gộp toàn lịch sử (OpeningBalance) + sub-check account ngoài `^[1-8]`.
> **Open items P1 (quyết định tại P1):** (1) cột filter period cho R11 mirror (CreatedAt vs UpdatedAt) — chốt cùng P2; (2) `IgnoreQueryFilters` có cần (IsDeleted) — rà soát khi viết query; (3) log audit mức nào (chỉ summary hay chi tiết check) — G12 không lưu history, audit log vẫn ghi summary.
> **Tái dùng:** `IAccountingDbContext` · `ITenantProvider` · `IAuditTrailService` · `VanAnDbContextTestFactory` (test — global filter lesson → `IgnoreQueryFilters` đúng chỗ) · FluentAssertions/xUnit pattern.
> **Validation cuối session:** guard-check + build VanAn.sln + Core.Tests + ShopERP.Tests MUST PASS → commit (KHÔNG push trừ khi user yêu cầu).

- [ ] **P1.1 Models + contract** — `ReconcileLevel (Error|Warning|Info|Pass)` · `ReconcileCheck {Code, Name, Level, Summary, Details}` · `ReconcileResult {Period, TenantId, TenantName, RanAt, Summary{...}, Checks[], Suggestions[]}` · `IAccountingReconcileService.RunAsync(tenantId, period, ct)` · `IMirrorOrderProvider.GetMirrorOrdersAsync(period, ct)` (DTO `MirrorOrderDto {OrderId, SourceType, Status, PaymentStatus, DepositPaidAmount, TotalAmount}`).
- [ ] **P1.2 Service implementation (R1-R10 + R12 + R11-skip)** — port C1-C10 từ script (tolerance 0.01):
  - R1 journal cân bằng per journal (count lệch) · R2 trial balance kỳ (ΣNợ vs ΣCó)
  - R3 revenue/expense: journal credit 5/7 == entries Revenue(5/7) + offset(5/7) · journal debit 6/8 == entries Expense(6/8) — VAT 3331 trừ riêng (Info hiển thị)
  - R4 balance identity toàn lịch sử + sub-check qua **`AccountClassifier` (P0)**: account ngoài `^[1-8]` / malformed / rỗng → Warning + liệt kê (911 kết chuyển → suggestion mở rộng NET gộp 9xx)
  - R5 sổ cái key accounts (opening/movement/closing — replicate GetLedgerAsync) → Info bảng
  - R6 nhật ký chung count + ΣNợ/ΣCó · R7 sổ HKD (chỉ Type=1) revenue khớp R3
  - R8a journal ref 'Order' sau 2026-10-29 = 0 · R8b mỗi order paid ∈ {2,3} journals
  - R9 3387 residual (Info) · R10 order paid → entries coverage (511/512 HTX nội, 3331 nếu VAT>0, 632, offset nếu cọc)
  - R11 qua `IMirrorOrderProvider` (null → skip Info) · R12 TransactionDate hợp lệ (Warning)
  - Fail-safe: từng check try/catch riêng → error + log (NFR-4) · READ-ONLY (không AddAsync/SaveChanges)
- [ ] **P1.3 DI** — `3_CoreHub/Program.cs` (Scoped service; MirrorOrderProvider optional).
- [ ] **P1.4 Tests (Core.Tests — T1-T8, T10; T9/T13 đã cover ở P0)** — seed journal lệch (T1) · Order-ref post-fix (T2) · thiếu entries COGS (T3) · deposit offset đủ (T4) · TransactionDate 01-01-0001 (T5) · HKD không journal (T6) · HTX 512/612 (T8) · mock check exception → fail-safe (T10) · **read-only guard: service không gọi write trên DbContext** (NFR-1 [G10]) · isolation tenant.

### Phase 2 — ShopERP mirror API (G8) ⏳

> **P2 PREP:** đọc `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` (Orders/Tenants mirror) + pattern internal API + `Program.cs` (auth).
- [ ] **P2.1 `MirrorOrdersController`** — `GET /api/shoperp/accounting/mirror-orders?period=YYYY-MM` → đọc SQLite mirror `Orders` (+ `Tenants` name) filter kỳ (chốt cột P1) → `MirrorOrderDto[]` · authorize (pattern internal API — caller Gateway).
- [ ] **P2.2 Gateway `IMirrorOrderProvider` HTTP impl** — base URL ShopERP (config) · fail-open (lỗi → null) · timeout ngắn (<3s).
- [ ] **P2.3 Tests** — controller (đọc mirror đúng kỳ, auth) + provider (mock HTTP lỗi → null, ok → data).

### Phase 3 — Gateway API ⏳

> **P3 PREP:** đọc 1 admin controller pattern (`[Authorize]` + tenant resolve + audit).
- [ ] **P3.1 `AccountingReconcileController`** — `GET /api/admin/accounting/reconcile?tenantId={guid}&period=YYYY-MM` · `[Authorize(Roles=SystemAdmin)]` · period validate (400) · tenant tồn tại (404) · gọi service (wired MirrorOrderProvider) · audit log (tenant, kỳ, user, summary).
- [ ] **P3.2 Tests** — 400/404/403 · 200 + summary đúng · audit ghi (mock).

### Phase 4 — UI `/admin/reconcile` ⏳

> **P4 PREP:** đọc 1 admin page pattern + `GatewayAdminApiClientBase` + NavMenu/Sitemap.
- [ ] **P4.1 Page `Reconcile.razor`** — AdminLayout · form (tenant select `TenantApiClient.ListAllAsync()` + kỳ) · nút "Tra soát" → `ReconcileAdminApiClient` → 4 `VanAMetricsCard` + `VanAnDataGrid` check (badge level, expand detail, suggestion) + nút "Chạy lại" · loading/empty/error state (UI Platform 100%).
- [ ] **P4.2 NavMenu "Tra soát số liệu" (admin) + Sitemap** · bUnit (render, level badge, suggestion).
- [ ] **P4.3 E2E spec `accounting-reconcile.spec.ts`** (Gate 4 — UI change bắt buộc) — login SystemAdmin → chọn tenant+kỳ → chạy → 4 thẻ + bảng.

### Phase 5 — Deploy + RV production ⏳

- [ ] PUSH → CD Multi-VPS (KHÔNG migration) · L1 markers (CoreHub.dll `AccountingReconcileService` ×N · ShopERP.dll `MirrorOrdersController`) · L2 health + routes.
- [ ] **L3 API reconcile** (SystemAdmin, tenant thật Cafe Tân Quy + Test HKD, kỳ 2026-10) — **đối chiếu kết quả với `accounting-balance-rv.sh` cùng tenant+kỳ (sweep-first — không fix case-by-case)** · R11 mirror khớp.
- [ ] L4 UI E2E production (spec + config `playwright-rv-rc.config.ts` — storageState SystemAdmin) · L5 manual (user).

---

## 4. TÀI LIỆU LIÊN QUAN

- SRS v1.1: `docs/requirements/van_an_tra_soat_so_lieu_ke_toan_srs_v1.md`
- Master plan: `docs/AI/plans/tra-soat-ke-toan-master-plan.md`
- Logic nguồn: `6_Testing/rv-scripts/accounting-balance-rv.sh`
- Governance: `docs/AI/project_state.md` (issue #195 + RV accounting 2026-10-29) · `.devin/rules/governance.md` (Domain protection — KHÔNG đụng Domain.cs) · `.devin/workflows/newfeaturebuild.md`