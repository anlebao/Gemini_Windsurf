# Hướng dẫn sử dụng Phân tích tài chính (Financial Intelligence MVP-2)

> **Dành cho:** Owner (chủ doanh nghiệp HKD/SME)
> **Phiên bản:** MVP-2 Sprint A — 2026-08-21
> **Yêu cầu:** Đã nhập doanh thu + chi phí kỳ kế toán (TT 152/2025/TT-BTC)

---

## 1. Tổng quan

Module **Phân tích tài chính** biến số liệu kế toán đã nhập thành công cụ hỗ trợ quyết định cho chủ doanh nghiệp:

- **Hồ sơ doanh nghiệp** — khai báo chi phí cố định + sức chứa + mô hình giá
- **Dashboard tài chính** — 5 widget tổng quan kỳ
- **Phân tích điểm hòa vốn** — khi nào bắt đầu có lãi?
- **Kinh tế đơn vị sản phẩm** — sản phẩm nào đóng góp lợi nhuận nhiều nhất?

Toàn bộ tính toán **Trust Level 1** (deterministic, không AI) — kết quả có thể kiểm chứng chéo với Báo cáo kết quả HĐ (B02-DN).

---

## 2. Bước 1: Khai báo Hồ sơ doanh nghiệp

**Route:** `/admin/business-profile` (Sidebar → "Hồ sơ doanh nghiệp")

Điền 7 loại chi phí cố định hàng tháng (VND):

| # | Chi phí | Ghi chú |
|---|---------|---------|
| 1 | Tiền thuê mặt bằng | Nếu自有 mặt bằng → 0 |
| 2 | Lương nhân sự | Tổng payroll sau thuế |
| 3 | Điện nước + tiện ích | Điện, nước, internet, gas... |
| 4 | Marketing + quảng cáo | Facebook Ads, Google Ads, in ấn... |
| 5 | Vận chuyển + logistics | Shipper, xe tải, fuel... |
| 6 | Chi phí vận hành khác | Bảo hiểm, phí cấp, văn phòng phẩm... |
| 7 | Khấu hao tài sản | CAPEX amortization (thiết bị, xe, máy...) |

**Sức chứa & mô hình giá:**
- Sản lượng/ngày (đơn vị) — sức chứa tối đa 1 ngày
- Ngày hoạt động/tháng (1-31) — ngày thực tế mở cửa
- Mô hình giá: Cố định / Linh hoạt / Kết hợp

**Lưu ý:**
- Mỗi lần lưu → tự động tăng **phiên bản mô hình** (BR-006 — traceability).
- Có thể cập nhật bất cứ lúc nào — kết quả phân tích sẽ tự recompute kỳ tiếp theo.
- Chi phí cố định là **ước tính chủ quan của Owner** — không auto-derive từ kế toán.

---

## 3. Bước 2: Dashboard tài chính

**Route:** `/financial` (Sidebar → "Thông tin tài chính")

5 widget:

| # | Widget | Ý nghĩa |
|---|--------|---------|
| 1 | Doanh thu kỳ | Doanh thu + lợi nhuận ròng + biên lợi nhuận + trạng thái (Có lãi/Hòa vốn/Lỗ) |
| 2 | Điểm hòa vốn | Doanh thu cần đạt để bù chi phí cố định + biên an toàn + trạng thái |
| 3 | Top 5 sản phẩm | 5 sản phẩm đóng góp lợi nhuận cao nhất kỳ |
| 4 | Bottom 5 sản phẩm | 5 sản phẩm đóng góp thấp nhất + cảnh báo thiếu giá vốn |
| 5 | Lợi nhuận mục tiêu | Nhập số tiền mục tiêu → tính doanh thu/số lượng cần bán + khả thi/vượt sức chứa |

**Period picker:** chọn Tháng + Năm → widget tự reload.

**Warning banners:**
- ⚠️ "Chưa khai báo hồ sơ" → bấm link tới `/admin/business-profile`
- ℹ️ "Chưa có dữ liệu kỳ này" → nhập doanh thu/chi phí trước
- 🚨 "Biên đóng góp âm" (CM ≤ 0) — critical, kiểm tra giá vốn vs giá bán

---

## 4. Bước 3: Phân tích điểm hòa vốn

**Route:** `/financial/break-even`

**Bảng tổng hợp:**
- Tổng chi phí cố định, tổng doanh thu, tổng chi phí biến đổi
- Tỷ lệ biên đóng góp (Contribution Margin Ratio)
- Doanh thu hòa vốn + sản lượng hòa vốn
- Biên an toàn (VND + %)
- Trạng thái: Vượt hòa vốn / Đạt / Chưa đạt / Chưa đủ DL

**Biểu đồ so sánh:** doanh thu thực tế vs doanh thu hòa vốn (bar chart).

**Bảng đa sản phẩm:** break-down per-product — biên đóng góp từng SP, cơ cấu bán, SL hòa vốn từng SP.

**Export Excel:** nút "Xuất Excel" → file `.xlsx` (2 sheets: tổng hợp + đa SP).

---

## 5. Bước 4: Kinh tế đơn vị sản phẩm

**Route:** `/financial/unit-economics`

**Bảng sortable** (click header để sort ↑/↓):
- Tên SP, Nhóm, Giá bán, Chi phí biến đổi, Biên đóng góp, Tỷ lệ BCM, SL bán, Doanh thu, Đóng góp LN, Hạng

**Filter theo nhóm** (dropdown).

**Cảnh báo:**
- Dòng vàng: sản phẩm thiếu giá vốn (CostPrice = 0) → biên đóng góp dùng ước tính 70% giá bán
- Link "Cập nhật giá vốn" → tới `/admin/product-cost-prices`

**Export Excel:** nút "Xuất Excel" → file `.xlsx` (1 sheet: danh sách + summary).

---

## 6. Guard conditions & cách xử lý

| Mã cảnh báo | Ý nghĩa | Cách xử lý |
|-------------|---------|-----------|
| `PROFILE_MISSING` | Chưa khai báo hồ sơ | Vào `/admin/business-profile` điền form |
| `INSUFFICIENT_DATA` | Chưa có dữ liệu kế toán kỳ này | Nhập doanh thu/chi phí kỳ |
| `COST_PRICE_MISSING` | Có SP thiếu giá vốn | Vào `/admin/product-cost-prices` cập nhật |
| `CM_RATIO_ZERO_OR_NEG` | Biên đóng góp âm | Kiểm tra giá vốn > giá bán? |
| `CAPACITY_EXCEEDED` | RequiredDaily > sức chứa | Tăng sức chứa hoặc giảm target |
| `FIXED_COST_ZERO` | Tổng fixed cost = 0 | Khai báo lại hồ sơ |

---

## 7. Câu hỏi thường gặp

**Q: Tại sao doanh thu hòa vốn khác doanh thu thực tế?**
A: Điểm hòa vốn = mức doanh thu tối thiểu để bù chi phí cố định. Nếu doanh thu thực tế > hòa vốn → có lãi. Nếu < → lỗ.

**Q: Biên an toàn là gì?**
A: Khoảng cách giữa doanh thu thực tế và điểm hòa vốn. Càng cao càng an toàn (buffer trước khi lỗ).

**Q: Tại sao cần khai báo fixed cost tay?**
A: Chi phí cố định mang tính ước tính chủ quan (Owner biết rõ nhất — vd: tiền thuê có thể đã gồm management fee, lương có thể có thưởng). Auto-derive từ kế toán sẽ miss các khoản ẩn.

**Q: Phiên bản mô hình (1.0, 1.1...) để làm gì?**
A: Traceability (BR-006) — mỗi lần cập nhật hồ sơ, version tăng. Khi xem báo cáo kỳ trước, biết được hồ sơ dùng version nào để recompute.

---

## 8. Tech notes (cho dev)

- **Source of truth:** Gateway PostgreSQL — ShopERP proxy qua HTTP (`FinancialIntelligenceHttpService`)
- **Domain purity:** Services trong `3_CoreHub/Services/FinancialIntelligence/`, không inject DbContext
- **Auth:** `[Authorize(Policy = "SystemAdmin")]` (HKD tenant có quyền truy cập)
- **W8 feature flag bypass:** Controller inject `IIncomeStatementService` trực tiếp, không qua `IncomeStatementsController`
- **UI Platform 100%:** VanACard, VanAMetricsCard, VanAButton, VanAAlert (no raw HTML/CSS bypass)
- **Export:** EPPlus 7.6.1 (`FinancialExportService`), precedent `InventoryExcelReport` (Wave 3)
- **E2E:** `6_Testing/e2e-tests/financial-dashboard.spec.ts` (Gate 4 compliance)
- **i18n:** 100% tiếng Việt có dấu (NFR-12)

---

**Tạo:** 2026-08-21 · **Owner:** VanAn Team · **Spec:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md`
