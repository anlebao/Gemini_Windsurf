# SRS — THU CHI & CÔNG NỢ (PHẢI THU / PHẢI TRẢ) v1.0

> **Ngày:** 2026-10-06 · **Trạng thái:** DRAFT — chờ user review
> **Nền tảng:** Module kế toán hiện tại (TT 71 HKD / TT 133 DN / TT 99 DN lớn) — mở rộng tối thiểu, KHÔNG thiết kế lại
> **Nguyên tắc:** "những điều chưa rõ → giả định kịch bản ĐƠN GIẢN NHẤT" (mọi giả định đánh dấu **[G#]**)

---

## 1. BỐI CẢNH & MỤC TIÊU

**Yêu cầu người dùng:** *"tôi chỉ cần thu chi, báo cáo công nợ, phải thu phải trả, theo dõi phải trả phải thu thôi"*

→ Người dùng (chủ hộ kinh doanh / doanh nghiệp nhỏ) muốn:
1. **Thu chi** — ghi nhận tiền thu vào, tiền chi ra (đã có sẵn trong module).
2. **Phải thu** — khách hàng còn nợ bao nhiêu (bán chịu chưa thu).
3. **Phải trả** — mình còn nợ người bán / nhà cung cấp bao nhiêu (mua chịu chưa trả).
4. **Theo dõi** — từng khách/người bán: phát sinh, đã thu/đã trả, còn lại.

**Mục tiêu SRS:** mở rộng module kế toán hiện tại để đáp ứng 4 nhu cầu trên với **chi phí thay đổi tối thiểu**, **không phá vỡ** các báo cáo hiện có (Trial Balance, Báo cáo kết quả KD, Bảng cân đối, B01-B09 TT 71…).

---

## 2. HIỆN TRẠNG MODULE KẾ TOÁN (khảo sát 2026-10-06)

| Thành phần | Hiện trạng | Kết luận cho yêu cầu |
|---|---|---|
| **Phiếu thu** (`RevenueEntry.razor`) | Nhập doanh thu: số tiền, ngày, diễn giải, mã TK (chỉ 5xx/7xx), chứng từ → tạo `AccountingEntry` (immutable, append-only, có Reversal) | ✅ Thu chi đã có — giữ nguyên |
| **Phiếu chi** (`ExpenseEntry.razor`) | Nhập chi phí: 6xx + **đã có trường `vendor` (nhà cung cấp)** | ✅ Giữ nguyên + tận dụng vendor |
| **Sổ nhật ký / Lịch sử giao dịch** (`TransactionHistory.razor`) | Danh sách mọi AccountingEntry theo tháng | ✅ Tái dùng |
| **Tài khoản 131 / 331** | Tồn tại trong bảng cân đối + CashFlow (`deltaReceivables 131/136/138`, `deltaPayables 331`) | 🟡 Có khái niệm nhưng **KHÔNG có nghiệp vụ + báo cáo công nợ theo đối tượng** |
| **Đối tượng công nợ** | `AccountingEntry.Vendor` (string) — đã có, chỉ phiếu chi dùng | 🟡 Cần dùng cho cả phải thu |
| **Báo cáo** | TrialBalance · IncomeStatement · BalanceSheet · CashFlow · Notes · HKDBooks B01-B09 · PeriodClosing | ✅ Không đụng |
| **Hạn chế chính** | Phiếu thu **chặn** account 131 (chỉ 5xx/7xx) · chưa có báo cáo nhóm theo đối tượng · không có khái niệm "bán chịu / mua chịu / trả nợ" | ❌ Điểm cần mở rộng |

---

## 3. PHẠM VI

### 3.1 Trong phạm vi (MVP)

| # | Nhu cầu | Giải pháp |
|---|---|---|
| 1 | Thu chi | ✅ Đã có — mở rộng nhẹ để hỗ trợ công nợ (mục 5) |
| 2 | Báo cáo công nợ tổng hợp | **MỚI** — bảng phải thu / phải trả theo đối tượng + số dư cuối kỳ |
| 3 | Sổ theo dõi phải thu / phải trả | **MỚI** — chi tiết từng đối tượng: phát sinh → đã thu/trả → còn lại |
| 4 | Ghi nhận bán chịu / mua chịu | **MỚI** — qua Phiếu thu/chi loại "Công nợ" |

### 3.2 Ngoài phạm vi (ghi nợ — KHÔNG làm MVP) [G10]

- Tự động sinh công nợ từ Order/POS/checkout (chỉ nhập tay qua phiếu — tránh đụng OrderService, không regress)
- Danh bạ khách hàng / nhà cung cấp riêng (đối tượng = tên nhập tay)
- Hạn thanh toán, quá hạn, lãi chậm trả, phân loại tuổi nợ (aging)
- Hợp đồng / hóa đơn / biên bản đối chiếu công nợ
- Công nợ ngoại tệ, công nợ nội bộ, tạm ứng nhân viên (141/138)
- Tự động đối trừ công nợ với đơn hàng

---

## 4. GIẢ ĐỊNH — KỊCH BẢN ĐƠN GIẢN NHẤT

| # | Giả định | Mô tả |
|---|---|---|
| [G1] | **2 tài khoản công nợ** | Chỉ theo dõi **131** (phải thu khách hàng) và **331** (phải trả người bán). Không dùng 136/138/141/338. |
| [G2] | **Đối tượng = chuỗi tên tự nhập** | Người dùng gõ tên đối tượng (vd "Khách A", "Cty TNHH X"). **Cùng chuỗi = cùng đối tượng**. Không có danh bạ, không chọn từ danh sách (không thêm entity mới). Lưu vào trường `Vendor` đã có. |
| [G3] | **Nhập tay toàn bộ** | Mọi nghiệp vụ công nợ (bán chịu, thu nợ, mua chịu, trả nợ) do người dùng nhập trên Phiếu thu/chi. Không tích hợp Order/POS. |
| [G4] | **Bán chịu = 1 phiếu thu đặc biệt** | Khi bán chịu cho khách: tạo **Phiếu thu loại "Ghi nhận phải thu"** → tăng số dư phải thu của đối tượng. KHÔNG ghi nhận doanh thu 511 ở phiếu này (tránh trùng doanh thu — [G6]). |
| [G5] | **Thu nợ = 1 phiếu thu thường + đối tượng** | Khi khách trả tiền: tạo **Phiếu thu loại "Thu tiền khách trả nợ"** (ghi nhận tiền vào) → giảm số dư phải thu đối tượng tương ứng. |
| [G6] | **Tránh trùng doanh thu / chi phí** | Phiếu "Ghi nhận phải thu" KHÔNG tính vào doanh thu (IncomeStatement); Phiếu "Ghi nhận phải trả" KHÔNG tính vào chi phí. Chỉ ảnh hưởng bảng công nợ. Người dùng muốn ghi doanh thu thật khi thu tiền thì dùng Phiếu thu doanh thu (5xx) như hiện tại. |
| [G7] | **Số dư theo tháng** | Báo cáo theo kỳ (tháng) — dùng `AccountingPeriod` hiện có. Đầu kỳ = tổng phát sinh trước tháng; cuối kỳ = đầu kỳ + phát sinh trong tháng. |
| [G8] | **Đơn vị VNĐ** | Không ngoại tệ. |
| [G9] | **Số dư có thể âm** | Cho phép số dư âm (khách trả thừa / trả trước) — hiển thị bình thường, không chặn. |
| [G10] | **Xóa = đảo ngược** | Sai sót → đảo bút toán (reversal — cơ chế đã có), không xóa. |

---

## 5. YÊU CẦU CHỨC NĂNG (FR)

### 5.1 Phiếu thu — mở rộng (`RevenueEntry.razor`)

**FR-1.** Thêm **"Loại phiếu thu"** (dropdown, 3 lựa chọn):

| Loại phiếu | Ý nghĩa | Mã TK hợp lệ | Trường "Đối tượng" |
|---|---|---|---|
| Thu doanh thu *(mặc định — hành vi hiện tại)* | Ghi doanh thu | 5xx / 7xx (như hiện tại) | Ẩn / không bắt buộc |
| **Ghi nhận phải thu** *(MỚI)* | Bán chịu — tăng nợ phải thu | **131** | **BẮT BUỘC** |
| **Thu tiền khách trả nợ** *(MỚI)* | Khách trả tiền — giảm nợ phải thu | **131** | **BẮT BUỘC** |

**FR-2.** Khi chọn loại "Ghi nhận phải thu" / "Thu tiền khách trả nợ":
- Ô **"Đối tượng (khách hàng)"** hiện ra, bắt buộc nhập [G2].
- Lưu: `AccountingEntry(AccountCode="131", Vendor=<đối tượng>, Amount=<số tiền>, ReferenceType=<"RECEIVABLE" | "RECEIVABLE_PAYMENT">)`.
- "Ghi nhận phải thu" → Amount **dương** (tăng nợ) · "Thu tiền khách trả nợ" → Amount **âm** (giảm nợ) [G9].
- Diễn giải tự động gợi ý: `"Bán chịu — {đối tượng}"` / `"Thu tiền khách trả nợ — {đối tượng}"` (vẫn sửa được).

**FR-3.** Báo cáo kết quả kinh doanh (IncomeStatement) **KHÔNG** tính entry 131 từ 2 loại phiếu mới [G6].

### 5.2 Phiếu chi — mở rộng (`ExpenseEntry.razor`)

**FR-4.** Tương tự FR-1, thêm "Loại phiếu chi":

| Loại phiếu | Ý nghĩa | Mã TK hợp lệ | Trường "Đối tượng" |
|---|---|---|---|
| Chi phí *(mặc định — hiện tại)* | Ghi chi phí | 6xx | Đã có `vendor` (tùy chọn) |
| **Ghi nhận phải trả** *(MỚI)* | Mua chịu — tăng nợ phải trả | **331** | **BẮT BUỘC** |
| **Trả tiền người bán** *(MỚI)* | Trả nợ — giảm nợ phải trả | **331** | **BẮT BUỘC** |

**FR-5.** "Ghi nhận phải trả" → Amount dương (tăng nợ) · "Trả tiền người bán" → Amount âm (giảm nợ) · `ReferenceType=<"PAYABLE" | "PAYABLE_PAYMENT">`.

**FR-6.** IncomeStatement KHÔNG tính entry 331 từ 2 loại phiếu mới [G6].

### 5.3 Báo cáo công nợ tổng hợp — MỚI (`/accounting/cong-no`)

**FR-7.** Bảng 2 khối (Phải thu 131 / Phải trả 331) — theo tháng:

```
BÁO CÁO CÔNG NỢ — THÁNG 10/2026
──────────────────────────────────────────────
PHẢI THU (131)                         Số dư
  Khách A                             5.000.000
  Cty TNHH X                          2.000.000
  ...                                 ...
  TỔNG PHẢI THU                      9.000.000
──────────────────────────────────────────────
PHẢI TRẢ (331)
  Nhà cung cấp Y                      3.000.000
  TỔNG PHẢI TRẢ                      3.000.000
```

- Cột hiển thị: **Tên đối tượng · Đầu kỳ · Phát sinh tăng · Đã thu/trả · Cuối kỳ** (tính theo [G7]).
- Chỉ hiển thị đối tượng có số dư khác 0 (hoặc bộ lọc "Tất cả / Còn nợ / Hết nợ").
- Chọn tháng + nút **Xem**; nút **In / Xuất Excel** (tái dùng EPPlus pattern hiện có).
- Số liệu: query `AccountingEntry` `AccountCode IN (131, 331)` group by `Vendor` — **không cần bảng mới** [G1][G2].

### 5.4 Sổ theo dõi phải thu / phải trả — MỚI (`/accounting/cong-no/{loai}/{doidTuong}` hoặc modal)

**FR-8.** Chọn 1 đối tượng từ báo cáo tổng hợp → sổ chi tiết:

```
SỔ THEO DÕI PHẢI THU — Khách A (TK 131)
──────────────────────────────────────────────
Ngày       Diễn giải                    Tăng      Giảm      Số dư
01/10      Bán chịu — Khách A         5.000.000           5.000.000
15/10      Thu tiền khách trả nợ                2.000.000  3.000.000
──────────────────────────────────────────────
Cuối kỳ                                               Số dư 3.000.000
```

- Nguồn: entries `AccountCode=131 AND Vendor=<đối tượng>` sắp theo ngày — cộng dồn số dư [G7].
- Mỗi dòng có nút **"Đảo bút toán"** (reversal — cơ chế đã có) khi nhập sai [G10].

### 5.5 Menu & truy cập

**FR-9.** Thêm mục **"Công nợ"** vào menu kế toán (cạnh Báo cáo tài chính / Sổ kế toán) — quyền Owner (hiện trạng menu kế toán).
**FR-10.** Sitemap + NavMenu (IShopErpMenuService) + bUnit test cho trang mới.

---

## 6. YÊU CẦU DỮ LIỆU & KỸ THUẬT

- **KHÔNG thêm entity/bảng mới** — tận dụng `AccountingEntry` (immutable) + `AccountCode` + `Vendor` + `ReferenceType` [G1][G2].
- **Dấu số tiền:** dương = tăng công nợ, âm = giảm công nợ [G9]. *(Open: kiểm tra `AccountingValidationService` có chặn Amount âm không — nếu chặn → thêm EntryType `Adjustment` hoặc trường dấu riêng; đây là điểm duy nhất cần xác nhận kỹ thuật.)*
- **Phân biệt phiếu công nợ vs doanh thu/chi phí:** `ReferenceType` ("RECEIVABLE"/"RECEIVABLE_PAYMENT"/"PAYABLE"/"PAYABLE_PAYMENT") → IncomeStatement/BalanceSheet **loại trừ** các entry này [G6] (kiểm tra service hiện tại filter theo AccountCode — 131/331 vốn đã không nằm trong 5xx/6xx nên các báo cáo kết quả KD tự nhiên không tính; BalanceSheet giữ nguyên xử lý 131/331 hiện có).
- **Các báo cáo hiện có:** TrialBalance / IncomeStatement / BalanceSheet / CashFlow / B01-B09 **không thay đổi logic** (chỉ cần đảm bảo không regress — chạy test hồi quy).
- **Multi-tenancy:** mọi query filter TenantId (lesson a21f97f2).

---

## 7. LUỒNG NGHIỆP VỤ ĐIỂN HÌNH (kịch bản đơn giản)

**Kịch bản A — Bán chịu & thu nợ:**
1. 01/10 bán chịu 5tr cho "Khách A" → **Phiếu thu → Ghi nhận phải thu** (131, đối tượng "Khách A", 5.000.000).
2. 15/10 khách trả 2tr → **Phiếu thu → Thu tiền khách trả nợ** (131, "Khách A", 2.000.000).
3. Báo cáo công nợ tháng 10: Khách A **cuối kỳ 3.000.000** ✓. Sổ theo dõi: 2 dòng, số dư cộng dồn ✓.

**Kịch bản B — Mua chịu & trả nợ:**
1. 05/10 mua chịu 3tr nguyên liệu "Nhà cung cấp Y" → **Phiếu chi → Ghi nhận phải trả** (331, "Y", 3.000.000).
2. 20/10 trả 1tr → **Phiếu chi → Trả tiền người bán** (331, "Y", 1.000.000).
3. Báo cáo: Y cuối kỳ **2.000.000** ✓.

**Kịch bản C — Nhập sai → đảo bút toán:**
- Phiếu "Ghi nhận phải thu" nhập nhầm 5tr (đúng 500k) → đảo bút toán (reversal) → tạo lại phiếu đúng → số dư tự khớp [G10].

---

## 8. YÊU CẦU PHI CHỨC NĂNG

- **NFR-1 Hiệu năng:** báo cáo công nợ ≤ 2s với 10k entries/tháng (query group by có index sẵn TenantId + AccountCode).
- **NFR-2 Bảo mật:** quyền Owner (như báo cáo tài chính hiện tại); không lộ cross-tenant.
- **NFR-3 Nhất quán dữ liệu:** AccountingEntry immutable — mọi sửa sai qua reversal [G10].
- **NFR-4 UI:** UI Platform 100% (VanATable, VanACard, VanAButton — Gate 5).

---

## 9. TIÊU CHÍ HOÀN THÀNH (DoD)

- [ ] Phiếu thu/chi có 3 loại phiếu (doanh thu/chi phí giữ nguyên + 2 loại công nợ), đối tượng bắt buộc khi chọn loại công nợ
- [ ] Phiếu "Ghi nhận phải thu/trả" và "Thu/Trả nợ" tạo AccountingEntry 131/331 + Vendor + ReferenceType đúng dấu
- [ ] IncomeStatement/BalanceSheet KHÔNG regress (test hồi quy PASS — Core.Tests hiện có)
- [ ] Báo cáo công nợ tổng hợp: đúng đầu kỳ/phát sinh/cuối kỳ theo đối tượng (test 3 kịch bản A/B/C)
- [ ] Sổ theo dõi chi tiết: cộng dồn số dư đúng + đảo bút toán hoạt động
- [ ] Menu "Công nợ" + Sitemap + bUnit
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests PASS
- [ ] E2E (Gate 4): spec công nợ (phiếu thu công nợ → báo cáo → sổ chi tiết)

---

## 10. OPEN QUESTIONS (chờ user — nếu không trả lời, giữ giả định mặc định)

| # | Câu hỏi | Giả định mặc định |
|---|---|---|
| OQ1 | Bán chịu có cần ghi nhận đồng thời doanh thu (511) không? | **KHÔNG** — phiếu công nợ chỉ theo dõi 131; người dùng tự ghi doanh thu khi thu tiền (phiếu thu doanh thu) [G6] |
| OQ2 | Có cần tự động gắn công nợ từ đơn hàng bán chịu không? | **KHÔNG** — nhập tay qua phiếu [G3] |
| OQ3 | Có cần danh bạ khách hàng/nhà cung cấp không? | **KHÔNG** — tên tự nhập [G2] |
| OQ4 | Có cần tuổi nợ (quá hạn 30/60/90 ngày) không? | **KHÔNG** — chỉ số dư [G10] |

---

## 11. TÀI LIỆU LIÊN QUAN

- Module kế toán hiện tại: `5_WebApps/ShopERP/Components/Pages/Accounting/` · `3_CoreHub/Services/` (AccountingEntryService, TrialBalance/IncomeStatement/BalanceSheet/CashFlow)
- Chuẩn mực: TT 71/2024 (HKD) · TT 133/2016 (DN nhỏ) · TT 99 (DN lớn)
- Hướng dẫn sử dụng module Đặt lịch hẹn (ví dụ tài liệu user-guide): `docs/user-guide/Booking_Guide.md`
