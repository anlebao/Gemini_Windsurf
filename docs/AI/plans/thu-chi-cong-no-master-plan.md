# MASTER PLAN: THU CHI & CÔNG NỢ (PHẢI THU / PHẢI TRẢ) MVP

> Created: 2026-10-06 (user chốt Q1-Q5 — SRS v1.1 `docs/requirements/van_an_thu_chi_cong_no_srs_v1.md`)
> Status: **✅ APPROVED (2026-10-06, user) — ⏳ Session 1: P1 Services core**
> Branch: `main` · Priority: tiếp nối Booking (Booking P1-P7 DONE)

---

## 1. MỤC TIÊU

Đáp ứng yêu cầu user: *"tôi chỉ cần thu chi, báo cáo công nợ, phải thu phải trả, theo dõi phải trả phải thu thôi"* — mở rộng **tối thiểu** module kế toán hiện tại:

`Phiếu thu/chi (đã có) + 2 loại phiếu công nợ MỚI → AccountingEntry 131/331 + Vendor → Báo cáo công nợ (tổng hợp + tuổi nợ) → Sổ theo dõi (chi tiết + lịch sử thanh toán)`

**Nguyên tắc:** kịch bản ĐƠN GIẢN NHẤT (SRS §4 G1-G13) · **KHÔNG thêm entity/bảng/migration** (tận dụng `AccountingEntry` immutable + `AccountCode` + `Vendor` + `ReferenceType`) · KHÔNG phá báo cáo hiện có (TrialBalance/IncomeStatement/BalanceSheet/CashFlow/B01-B09).

---

## 2. HIỆN TRẠNG (khảo sát 2026-10-06 — đã xác minh code)

| Thành phần | Hiện trạng | Kết luận |
|---|---|---|
| Phiếu thu `RevenueEntry.razor` | Nhập doanh thu (UI chặn account 5xx/7xx) → `AccountingEntryService.CreateRevenueEntryAsync` | ✅ Giữ nguyên + mở rộng |
| Phiếu chi `ExpenseEntry.razor` | Chi phí 6xx + **vendor** + **tra MST có sẵn** (`BusinessInfoApiClient` + Gateway `GET /api/v1/business-info/{mst}`) | ✅ Reuse tra MST |
| `AccountingEntryService` | **Không chặn** account 131/331 · **không chặn** Amount âm (server) | ✅ Không cần sửa validation server |
| `AccountingEntry` | Immutable + `AccountCode` + `Vendor` + `ReferenceType`/`ReferenceId` + Reversal | ✅ Tận dụng 100% |
| IncomeStatement/BalanceSheet | 131/331 đã hiểu (BalanceSheet/CashFlow delta) | ⚠️ P1 verify IncomeStatement không tính entry 131/331 |
| Báo cáo công nợ / tuổi nợ / lịch sử thanh toán | **KHÔNG có** | ❌ MỚI (services + UI) |

---

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-06 — SRS v1.1 §10)

| # | Quyết định | Chi tiết |
|---|---|---|
| Q1 | Bán chịu KHÔNG ghi đồng thời 511 | Phiếu công nợ chỉ theo dõi 131/331; ghi doanh thu khi thu tiền (SRS [G6]) |
| Q2 | Nhập tay qua phiếu | KHÔNG tự động từ Order/POS (tránh đụng OrderService) |
| Q3 | Tra MST → tên công ty | Reuse cơ chế có sẵn; đối tượng = chuỗi tên + MST kèm diễn giải ([G11]) |
| Q4 | CÓ tuổi nợ | <30 · 30-60 · 60-90 · >90 ngày — FIFO động, không lưu mapping ([G12]) |
| Q5 | Truy ngược lịch sử thanh toán nợ | Từ 1 khoản nợ → modal các lần thu/trả + còn lại (FR-8.1) |

---

## 4. SCOPE (phases + task cards)

| Phase | Nội dung | Layer | Task card | Trạng thái |
|---|---|---|---|---|
| **1** | **Services core + tests** — `ICongNoService`/`CongNoService`: tạo phiếu công nợ (131/331 + Vendor + ReferenceType + dấu) · báo cáo tổng hợp (đầu kỳ/phát sinh/cuối kỳ + **tuổi nợ FIFO**) · sổ đối tượng (cộng dồn) · **lịch sử thanh toán khoản nợ** · verify IncomeStatement không tính 131/331 + reversal | 3_CoreHub | `task_thu_chi_cong_no_phase1_services.md` | ⏳ pending |
| **2** | **UI** — Phiếu thu/chi mở rộng 3 loại (doanh thu/chi phí + Ghi nhận phải thu/trả + Thu/Trả nợ) + ô Đối tượng bắt buộc + **nút Tra MST** (reuse) · 2 trang mới `/accounting/cong-no` (báo cáo tổng hợp + tuổi nợ + In/Excel) + `/accounting/cong-no/{loai}/{doiTuong}` (sổ chi tiết + modal lịch sử thanh toán + đảo bút toán) · NavMenu "Công nợ" + Sitemap + bUnit | 5_WebApps/ShopERP | `task_thu_chi_cong_no_phase2_ui.md` | ⏳ pending |
| **3** | **E2E + hardening** — spec công nợ (phiếu công nợ + tra MST → báo cáo + tuổi nợ → sổ + lịch sử) · guard + build + Core.Tests + ShopERP.Tests full | 6_Tests + 6_Testing | `task_thu_chi_cong_no_phase3_e2e.md` | ⏳ pending |
| **4** | **Deploy + RV production** — CD Multi-VPS (KHÔNG migration — không entity mới) · L1 markers · L2 health/routes · L3 E2E production · L4 flow thật (phiếu thu công nợ → báo cáo → tuổi nợ → lịch sử) · L5 manual | production | `task_thu_chi_cong_no_phase4_rv.md` | ⏳ pending |

**Dependency:** 1 → 2 → 3 → 4 (tuần tự; P2 UI chuẩn bị song song sau khi P1 API contract chốt).

---

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Dữ liệu (KHÔNG entity mới — SRS §6)

| Phiếu | EntryType | AccountCode | Vendor | Amount | ReferenceType |
|---|---|---|---|---|---|
| Ghi nhận phải thu (bán chịu) | Revenue (hoặc Adjustment — P1 quyết) | 131 | tên đối tượng | + | RECEIVABLE |
| Thu tiền khách trả nợ | như trên | 131 | tên đối tượng | − | RECEIVABLE_PAYMENT |
| Ghi nhận phải trả (mua chịu) | Expense (hoặc Adjustment) | 331 | tên đối tượng | + | PAYABLE |
| Trả tiền người bán | như trên | 331 | tên đối tượng | − | PAYABLE_PAYMENT |

- Diễn giải gợi ý: `"Bán chịu — {đối tượng} (MST xxxxx)"` / `"Thu tiền khách trả nợ — {đối tượng}"` [G11].
- **P1 verify:** IncomeStatement/TrialBalance filter theo EntryType hay AccountCode — entry 131/331 KHÔNG được lọt vào doanh thu/chi phí [G6]; nếu filter theo EntryType → dùng EntryType=Adjustment cho phiếu công nợ.

### 5.2 Services (Phase 1) — `3_CoreHub/Services/CongNo/` (hoặc mở rộng AccountingEntryService — P1 chốt)

- `ICongNoService`:
  - `CreateReceivableAsync(tenantId, period, amount, doiTuong, mst?, description, transactionDate)` — phiếu dương (bán chịu) hoặc âm (thu nợ) qua param `bool isPayment`
  - `CreatePayableAsync(...)` tương tự cho 331
  - `GetCongNoReportAsync(tenantId, year, month)` → `CongNoReportDto`: khối 131 + khối 331, mỗi đối tượng: Đầu kỳ · Phát sinh tăng · Đã thu/trả · Cuối kỳ · **Tuổi nợ (4 nhóm FIFO)** [G12]
  - `GetDoiTuongLedgerAsync(tenantId, accountCode, vendor, year, month)` → sổ chi tiết cộng dồn (Ngày · Diễn giải · Tăng · Giảm · Số dư)
  - `GetKhoanNoPaymentsAsync(tenantId, khoanNoEntryId)` → **lịch sử thanh toán 1 khoản nợ** (FIFO động) [FR-8.1]
  - FIFO: thanh toán trừ vào khoản phát sinh cũ nhất còn dư — tính động từ chuỗi phiếu (sắp theo TransactionDate + CreatedAt).
- Reversal: tái dùng cơ chế reversal hiện có (đảo phiếu công nợ → số dư tự khớp) [G10].

### 5.3 UI (Phase 2) — ShopERP

- **Phiếu thu** (`RevenueEntry.razor`) + **Phiếu chi** (`ExpenseEntry.razor`): dropdown "Loại phiếu" (3 lựa chọn) → hiện/ẩn ô **"Đối tượng"** (bắt buộc cho loại công nợ) + ô **"MST + nút Tra cứu"** (reuse `BusinessInfoApiClient` — có sẵn) [G11]. Whitelist account theo loại: công nợ phải thu → 131; phải trả → 331.
- **Trang mới** `/accounting/cong-no` (UI Platform 100%): bộ lọc tháng + bảng 2 khối 131/331 (mockup SRS §5.3 FR-7) + nút In/Xuất Excel (EPPlus pattern) + click đối tượng → sổ chi tiết.
- **Sổ chi tiết** `/accounting/cong-no/{loai}/{doiTuong}`: bảng cộng dồn (FR-8) + click 1 khoản nợ → **modal "Lịch sử thanh toán khoản nợ"** (FR-8.1) + nút Đảo bút toán mỗi dòng [G10].
- NavMenu "Kế toán → Công nợ" (Owner) + Sitemap + bUnit.

### 5.4 Tests (Phase 1 + 3)

- **Unit/Integration (P1):** kịch bản A (bán chịu + thu nợ → cuối kỳ đúng) · B (mua chịu + trả nợ) · C (reversal) · D (tuổi nợ FIFO: 2 khoản 01/07 + 15/08, trả 20/09 → >90: 3tr · 30-60: 3tr) · D2 (lịch sử thanh toán khoản) · isolation tenant · IncomeStatement không tính 131/331.
- **bUnit (P2):** phiếu thu 3 loại render + đối tượng bắt buộc · tra MST điền tên · trang báo cáo render · sổ chi tiết render.
- **E2E (P3):** spec `cong-no.spec.ts` (phiếu thu công nợ → báo cáo → tuổi nợ → sổ → lịch sử).

---

## 6. EXECUTION STRATEGY (session-per-phase — precedent Booking/VA-IIE)

| Session | Phase | Deliverable | Validation |
|---|---|---|---|
| S1 | P1 | Services + FIFO aging + tests | guard + build + Core.Tests |
| S2 (2 ngày) | P2 | Phiếu mở rộng + 2 trang + menu | guard + ShopERP.Tests (bUnit) |
| S3 | P3 | E2E spec + full test matrix | guard + build + tests + E2E (sau IMPLEMENT) |
| S4 | P4 | CD Multi-VPS + RV L1-L5 | RV protocol 5 lớp |

**Test delta ước lượng:** Core.Tests ~2111 → ~2140-2160 · ShopERP.Tests ~142 → ~150-155 · E2E +3-4 tests.

---

## 7. RỦI RO & MITIGATION

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | **Entry công nợ lọt vào doanh thu/chi phí** (trùng/thiếu báo cáo) | P1 verify IncomeStatement filter (EntryType vs AccountCode) + test regress Core.Tests hiện có; EntryType=Adjustment nếu cần [G6] |
| R2 | **Sai sót nhập liệu → sai số dư** | Reversal bắt buộc (không xóa) [G10] + số dư cộng dồn kiểm chéo bằng test kịch bản A-D |
| R3 | **Tuổi nợ FIFO phức tạp** | Không lưu mapping — tính động (đơn giản, ít state); test kịch bản D chốt thuật toán |
| R4 | **Đối tượng trùng tên khác nhau** (vd "Cty X" vs "Công ty X") | Hướng dẫn nhập chuẩn + tra MST tự điền tên giảm sai lệch [G11]; ghi chú trong UI |
| R5 | **Regress báo cáo kế toán hiện có** | Không sửa logic IncomeStatement/BalanceSheet/TrialBalance — chỉ thêm service đọc; chạy Core.Tests hồi quy toàn bộ |
| R6 | **Tra MST quota/rate-limit** | Reuse cơ chế có sẵn (BusinessLookupController đã xử lý 404/429/502) — hiển thị lỗi thân thiện |

---

## 8. OUT OF SCOPE / DEFERRED (SRS §3.2 [G13])

- Tự động công nợ từ Order/POS/checkout
- Danh bạ khách hàng/nhà cung cấp (đối tượng = chuỗi tên)
- Hạn thanh toán / quá hạn / lãi chậm trả (chỉ phân loại tuổi nợ)
- Hợp đồng / hóa đơn / biên bản đối chiếu
- Ngoại tệ · công nợ nội bộ · tạm ứng nhân viên (141/138)
- Tự động đối trừ công nợ với đơn hàng

---

## 9. SUCCESS CRITERIA (SRS §9 DoD)

- [ ] Phiếu thu/chi 3 loại + đối tượng bắt buộc + tra MST điền tên
- [ ] AccountingEntry 131/331 + Vendor + ReferenceType đúng dấu — KHÔNG entity/migration mới
- [ ] IncomeStatement/BalanceSheet không regress (test hồi quy PASS)
- [ ] Báo cáo công nợ: đầu kỳ/phát sinh/cuối kỳ + **tuổi nợ <30/30-60/60-90/>90 (FIFO)** đúng (kịch bản A/B/C/D)
- [ ] Sổ chi tiết cộng dồn + **truy ngược lịch sử thanh toán từng khoản** + đảo bút toán
- [ ] Menu "Công nợ" + Sitemap + bUnit + E2E spec
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests PASS mọi batch
- [ ] Deploy + RV production PASS → đóng master plan
