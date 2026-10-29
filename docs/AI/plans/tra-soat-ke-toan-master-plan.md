# MASTER PLAN: TRA SOÁT SỐ LIỆU KẾ TOÁN (ACCOUNTING RECONCILIATION)

> Created: 2026-10-29 (SRS v1.1 `docs/requirements/van_an_tra_soat_so_lieu_ke_toan_srs_v1.md` — user duyệt + chốt G7/G8/G9/G12)
> Status: **PLAN — chờ approve** (SRS approved 2026-10-29 → master plan + task card → user duyệt → IMPLEMENT)
> Branch: `main`
> Cơ sở thực nghiệm: `6_Testing/rv-scripts/accounting-balance-rv.sh` (C1-C10 — chạy production 2026-10-29, 18 PASS · 0 FAIL sau cleanup 114 journal duplicate)

---

## 1. MỤC TIÊU

Feature **Tra soát số liệu kế toán** — công cụ kiểm soát nội bộ (chỉ SystemAdmin): chạy thủ công theo yêu cầu (tenant + kỳ), kiểm tra toàn diện số liệu kế toán của 1 tenant qua **12 nhóm check R1-R12** (cân bằng journal, trial balance, đồng bộ journals↔entries, balance identity, sổ cái, nhật ký chung, sổ HKD, single-source bridge, deposit 3387, order→entries coverage, cross-store PG↔SQLite, ngày nghiệp vụ), trả báo cáo phân mức **Error / Warning / Info** kèm **gợi ý xử lý**, hoàn toàn **read-only**.

**Nguyên tắc (SRS §3/§6):** KHÔNG migration · KHÔNG entity mới · KHÔNG đụng Domain/AccountingEntry · đọc thuần PG (source of truth) + 1 API đọc SQLite mirror (R11/G8) · port logic C1-C10 đã validate production sang C# service · UI Platform 100%.

---

## 2. HIỆN TRẠNG (khảo sát 2026-10-29 — đã xác minh code + production)

| Thành phần | Hiện trạng | Kết luận |
|---|---|---|
| Nguồn số liệu | PG `JournalEntries`+`JournalEntryLine`+`AccountingEntries`+`Orders`+`Tenants` (source of truth) · SQLite mirror (orders/tenants — entries/journals RỖNG) | ✅ Đọc PG là chính; R11 cần API mirror |
| Logic tra soát | `accounting-balance-rv.sh` C1-C10 đã validate production (18 PASS · bắt 3 lớp defect thật) | ✅ Port sang C# service |
| View→source | Dashboard tài chính=API profit-summary(journal) · Dashboard kế toán=entries · BCTC/sổ HKD/nhật ký/sổ cái=journals | ✅ SRS §2.1 — không đổi |
| API admin pattern | `GatewayAdminApiClientBase` + admin controllers `[Authorize(SystemAdmin)]` | ✅ Tái dùng |
| Audit | `IAuditTrailService` | ✅ Tái dùng (G12 — không lưu lịch sử) |
| Quirk cần xử lý trong code | Dual-identity FK (lines→`JournalEntries."Id"`) · VAT 3331 không journal · offset 3387 (EntryType=5) · legacy Order-ref journals (35 legit pre-bridge — giữ) · TransactionDate -infinity còn sót | ✅ Ghi chú trong service + test |

---

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-29 — SRS v1.1)

| # | Quyết định | Chi tiết |
|---|---|---|
| D1 | Hình thức | **Cả service + API + UI** |
| D2 | Kích hoạt | **Manual on-demand** (tenant + kỳ → chạy ngay; không scheduled, không lưu lịch sử G12) |
| D3 | Xử lý lệch | **Read-only + gợi ý fix** (KHÔNG tự sửa data) |
| D4 | Loại hình | **Tất cả** (HKD TT152 / DN TT133-TT99 / HTX TT71) — chuẩn mực theo Type |
| G7 | Contra 214/229 | **Không special-case** (đẳng thức R4 = hệ quả toán học của R1 — đã chứng minh + verify production chỉ prefix 1-6) — thêm **sub-check Warning: account ngoài `^[1-8]`** (bắt sớm nếu tương lai có kết chuyển 911) |
| G8 | R11 cross-store | **GIỮ + CÓ API** ShopERP `GET /api/shoperp/accounting/mirror-orders?period=` |
| G9 | Phân quyền | **Chỉ SystemAdmin** (UI trong AdminLayout + API admin-only) |
| G12 | Lịch sử | **Không lưu** (chỉ audit log, không bảng mới) |

---

## 4. SCOPE (phases + task cards)

| Phase | Nội dung | Layer | Task card | Trạng thái |
|---|---|---|---|---|
| **0** | **Chuẩn hoá phân loại tài khoản cho R4 (G7 — user directive 2026-10-29)** — `AccountClassifier` (helper thuần): regex hợp lệ `^[1-9]\d{0,2}$` + phân nhóm prefix (1-2 asset · 3 liab · 4 equity · 5/7/9 revenue/temp · 6/8 expense) · **xử lý 9xx/911 kết chuyển** (hiện production chỉ prefix 1-6 — an toàn; khi xuất hiện → R4 Warning + suggestion mở rộng NET gộp 9xx — read-only, không tự sửa) · **xử lý malformed/rỗng AccountNumber** (Warning + liệt kê) · tests T9/T13 | 3_CoreHub | `task_tra_soat_ke_toan_phase0_account_classifier.md` | ⏳ |
| **1** | **Service core `IAccountingReconcileService`** — R1-R10 + R12 (PG-only) + R11 qua abstraction `IMirrorOrderProvider` (null → skip Info) + port C1-C10 logic + dùng `AccountClassifier` (P0) cho R3/R4/R7/R10 + models (`ReconcileResult/ReconcileCheck/ReconcileLevel`) + tests T1-T10 (trừ T7/T11 API/UI) | 3_CoreHub | `task_tra_soat_ke_toan_phase1_service.md` | ⏳ |
| **2** | **ShopERP mirror API (G8)** — `MirrorOrdersController` `GET /api/shoperp/accounting/mirror-orders?period=` (đọc SQLite mirror: orders + tenants theo kỳ) + `IMirrorOrderProvider` HTTP implementation ở Gateway + tests | 5_WebApps/ShopERP + 2_Gateway | `task_tra_soat_ke_toan_phase2_mirror.md` | ⏳ |
| **3** | **Gateway API** — `AccountingReconcileController` `GET /api/admin/accounting/reconcile?tenantId=&period=` ([Authorize SystemAdmin] · validate period/tenant · audit log) + DI wiring + tests | 2_Gateway | `task_tra_soat_ke_toan_phase3_api.md` | ⏳ |
| **4** | **UI** — `/admin/reconcile` (AdminLayout, UI Platform 100%): tenant+kỳ picker · nút Tra soát · 4 thẻ tổng (Error/Warning/Info/Pass) · bảng check (level badge + expand + suggestion) · NavMenu "Tra soát số liệu" + Sitemap · bUnit + E2E spec `accounting-reconcile.spec.ts` | 5_WebApps/ShopERP | `task_tra_soat_ke_toan_phase4_ui.md` | ⏳ |
| **5** | **Deploy + RV production** — push → CD Multi-VPS (KHÔNG migration) · L1 markers · L2 health/routes · L3 API reconcile (SystemAdmin, tenant thật: Cafe Tân Quy + Test HKD, đối chiếu kết quả với `accounting-balance-rv.sh` cùng tenant+kỳ — sweep-first) · L4 UI E2E production · L5 manual | production | `task_tra_soat_ke_toan_phase5_rv.md` | ⏳ |

**Dependency:** P0 → P1 → P2 (R11) → P3 → P4 → P5. P2 có thể chạy song song sau khi P1 chốt contract `IMirrorOrderProvider` + DTO mirror-orders. P0 là tiền đề của P1 (R3/R4/R7/R10 dùng `AccountClassifier`).

---

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Models (KHÔNG entity/bảng mới — DTOs thuần)

```
ReconcileResult {
  Period (string "YYYY-MM"), TenantId (Guid), TenantName, RanAt (UtcNow),
  Summary { ErrorCount, WarningCount, InfoCount, PassCount, TotalChecks },
  Checks: [ ReconcileCheck ], Suggestions: [ string ]
}
ReconcileCheck { Code ("R1".."R12"), Name, Level (error|warning|info|pass),
                 Summary (string — số liệu chính), Details (string — bảng/trace) }
```

### 5.1a Phase 0 — `AccountClassifier` (chuẩn hoá phân loại tài khoản — user directive 2026-10-29)

- **Regex hợp lệ:** `^[1-9]\d{0,2}$` (1-3 chữ số, không bắt đầu 0) — đúng danh mục tài khoản VN (1xx-9xx). Khớp regex nhưng ngoài prefix 1-8 → nhóm riêng.
- **Phân nhóm:** `1-2` Asset · `3` Liability · `4` Equity · `5/7` Revenue · `6/8` Expense · `9` Temporary (kết chuyển — hiện không dùng).
- **Xử lý:**
  - Account hợp lệ + thuộc 1-8 → tính bình thường vào R3/R4/R7/R10.
  - Account **9xx** (911 kết chuyển — hiện KHÔNG có trong production, chỉ prefix 1-6): R4 → **Warning + liệt kê** + suggestion "mở rộng NET gộp 9xx khi hệ thống làm closing entries" — read-only, không tự sửa (SRS G7/§4.1).
  - Account **malformed/rỗng** (null, "", "ABC", "01", "1111"): R4 → **Warning + liệt kê** (cùng sub-check) — suggestion "kiểm tra dữ liệu AccountNumber".
- **Dùng chung:** R3 (5/7, 6/8), R4 (1-2/3/4/5-9), R7 (5/7), R10 (511/512/612/632/3331/3387 — theo danh sách cố định).

### 5.2 Service — luồng chạy (R1-R12, fail-safe từng check)

- `RunAsync(tenantId, period, ct)` → với mỗi check: try/catch riêng → lỗi exception = check đó `error` (log), KHÔNG chặn check khác (NFR-4).
- Tất cả query EF LINQ qua `IAccountingDbContext` (PG) — filter `TenantId` tường minh, tolerance 0.01.
- Semantic đã xác minh (áp dụng trong code):
  - R3: revenue journal = credit 5xx/7xx; entries Revenue (EntryType=1, AccountCode 5/7) + **offset cọc (EntryType=5, AccountCode 5/7)**; VAT 3331 trừ riêng (không journal).
  - R4: A(1-2) == L(3) + E(4) + [5/7 cred−deb − 6/8 deb−cred] (toàn lịch sử — gồm OpeningBalance) + sub-check account ngoài `^[1-8]` → Warning.
  - R8a: journal `ReferenceType='Order'` AND `EntryDate >= 2026-10-29` → Error. R8b: per paid order count journal ∈ {2,3}.
  - R9: 3387 `Σ credit − Σ debit` (toàn lịch sử) → Info.
  - R11: gọi `IMirrorOrderProvider.GetMirrorOrdersAsync(period)` → so từng order paid: tồn tại + SourceType/Status/PaymentStatus/DepositPaidAmount khớp. Provider null/HTTP lỗi → R11 `info` "không chạy được".

### 5.3 API (P3)

- `GET /api/admin/accounting/reconcile?tenantId={guid}&period=YYYY-MM` — `[Authorize(AuthenticationSchemes = JwtBearer, Roles = SystemAdmin)]` (pattern admin controller hiện có) · period parse (YYYY-MM, kỳ hợp lệ) · tenant tồn tại (404) · audit `IAuditTrailService` (tenant, kỳ, user, summary).

### 5.4 Mirror API (P2 — G8)

- ShopERP: `GET /api/shoperp/accounting/mirror-orders?period=YYYY-MM` — đọc `ShopERPDbContext.Orders` + `Tenants` mirror (filter kỳ theo `CreatedAt`/`UpdatedAt` — cần chốt cột) → DTO `MirrorOrderDto[] { OrderId, SourceType, Status, PaymentStatus, DepositPaidAmount, TotalAmount, TenantName? }` · [Authorize] (caller = Gateway — pattern internal API hiện có).
- Gateway: `IMirrorOrderProvider` HTTP impl (base URL ShopERP, fail-open → null → R11 skip Info).

### 5.5 UI (P4 — UI Platform 100%)

- Page `/admin/reconcile` trong `AdminLayout` — form (tenant select từ `TenantApiClient.ListAllAsync()` + period picker) · nút "Tra soát" → gọi API → 4 `VanAMetricsCard` (Error/Warning/Info/Pass) + `VanAnDataGrid` các check (badge level màu, expand detail, suggestion) · nút "Chạy lại" · `ReconcileAdminApiClient` (pattern `GatewayAdminApiClientBase`).
- NavMenu (admin) "Tra soát số liệu" + Sitemap + E2E spec.

---

## 6. CASE TEST (SRS §8 — T1-T10, ánh xạ phase)

| # | Case | Phase |
|---|---|---|
| T1 | Journal lệch Nợ/Có → R1 Error | P1 |
| T2 | Order paid có journal ref 'Order' post-fix → R8a Error | P1 |
| T3 | Order paid thiếu entries COGS → R10 Error | P1 |
| T4 | Deposit order paid đủ offset → R3/R8b/R9 khớp | P1 |
| T5 | Entry TransactionDate 01-01-0001 → R12 Warning | P1 |
| T6 | Tenant HKD không journal kỳ → R7 Warning | P1 |
| T7 | Cross-tenant isolation (user A không thấy tenant B) | P1/P3 |
| T8 | HTX nội bộ (512/612) → R3/R4 pass | P1 |
| T9 | Account **9xx (911 kết chuyển)** seed → R4 **Warning** + liệt kê (không Error) | **P0**/P1 |
| T10 | Fail-safe: 1 check exception → check error, còn lại chạy | P1 |
| T11 | API: period sai 400 · tenant 404 · non-admin 403 | P3 |
| T12 | UI render 4 thẻ + bảng + suggestion · E2E production | P4/P5 |
| T13 | Account **malformed/rỗng** ("", "ABC", "01", "1111") seed → R4 **Warning** + liệt kê | **P0**/P1 |

---

## 7. RỦI RO & GIẢM THIỂU

| Rủi ro | Giảm thiểu |
|---|---|
| Sai semantic (VAT/offset/contra) → false PASS/FAIL | Port trực tiếp logic đã validate production (C1-C10) + test T4/T8/T9 |
| R11 phụ thuộc ShopERP HTTP (NATS/sync lag) | Fail-open: HTTP lỗi → R11 Info "không chạy được", không chặn các check khác |
| Query chậm trên journal lớn | Gộp query/check (≤3 query/check), index sẵn có (EntryDate, TenantId) — NFR-2 <5s |
| Lệch giữa script RV và service | P5 RV đối chiếu cùng tenant+kỳ (sweep-first) — tolerance 0.01 |

---

## 8. VALIDATION GATES (mỗi phase)

- guard-check.ps1 + `dotnet build VanAn.sln` 0 errors + Core.Tests (test mới + regression) + ShopERP.Tests (P4) MUST PASS.
- Read-only guard: unit test đảm bảo service không gọi `AddAsync/UpdateAsync/SaveChangesAsync` trên DbContext (NFR-1 [G10]).
- Playwright: DISABLED trong IMPLEMENT (steps 1-6) — chỉ P5 RV production (spec `accounting-reconcile.spec.ts` + config `playwright-rv-rc.config.ts`).

---

## 9. NGUỒN THAM KHẢO

- SRS v1.1: `docs/requirements/van_an_tra_soat_so_lieu_ke_toan_srs_v1.md`
- Logic đã validate: `6_Testing/rv-scripts/accounting-balance-rv.sh` (C1-C10)
- Pattern admin API: `2_Gateway/Controllers/` (admin controllers) + `GatewayAdminApiClientBase`
- Pattern service read-only: `3_CoreHub/Services/GeneralLedger/GeneralLedgerService.cs` · `TrialBalanceService.cs` · `HKDBookService.cs`
- Chuẩn mực: TT 152/2025 · TT 133/2016 · TT 99/2025 · TT 71/2024 · TT 103/2016 (điều kiện PM kế toán)