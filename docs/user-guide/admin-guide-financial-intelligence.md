# Hướng dẫn sử dụng Phân tích tài chính (Financial Intelligence MVP-2)

> **Dành cho:** Owner (chủ doanh nghiệp HKD/SME)
> **Phiên bản:** MVP-2 Sprint A — 2026-08-21
> **Yêu cầu:** Đã nhập doanh thu + chi phí kỳ kế toán (TT 152/2025/TT-BTC)
> **Thời gian đọc:** ~15 phút
> **Mức độ:** Không cần kiến thức kế toán chuyên sâu — giải thích từ cơ bản

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Kiến thức nền tảng (đọc nếu mới lần đầu)](#2-kiến-thức-nền-tảng)
3. [Bước 1: Khai báo Hồ sơ doanh nghiệp](#3-bước-1-khai-báo-hồ-sơ-doanh-nghiệp)
4. [Bước 2: Dashboard tài chính](#4-bước-2-dashboard-tài-chính)
5. [Bước 3: Phân tích điểm hòa vốn](#5-bước-3-phân-tích-điểm-hòa-vốn)
6. [Bước 4: Kinh tế đơn vị sản phẩm](#6-bước-4-kinh-tế-đơn-vị-sản-phẩm)
7. [Ví dụ thực tế theo ngành hàng](#7-ví-dụ-thực-tế-theo-ngành-hàng)
8. [Cảnh báo (guard conditions) & cách xử lý](#8-cảnh-báo-guard-conditions--cách-xử-lý)
9. [Export Excel](#9-export-excel)
10. [Câu hỏi thường gặp (FAQ)](#10-câu-hỏi-thường-gặp-faq)
11. [Kiểm chứng chéo với Báo cáo kế toán](#11-kiểm-chứng-chéo-với-báo-cáo-kế-toán)
12. [Giới hạn & Lưu ý quan trọng](#12-giới-hạn--lưu-ý-quan-trọng)
13. [Ghi chú kỹ thuật (cho dev)](#13-ghi-chú-kỹ-thuật-cho-dev)

---

## 1. Tổng quan

Module **Phân tích tài chính** biến số liệu kế toán đã nhập thành công cụ hỗ trợ quyết định cho chủ doanh nghiệp:

- **Hồ sơ doanh nghiệp** — khai báo chi phí cố định + sức chứa + mô hình giá (1 lần, cập nhật khi cần)
- **Dashboard tài chính** — 5 widget tổng quan kỳ (tháng nào có lãi không? hòa vốn chưa? SP nào ăn tiền nhất?)
- **Phân tích điểm hòa vốn** — cần bán bao nhiêu mới bù chi phí cố định?
- **Kinh tế đơn vị sản phẩm** — sản phẩm nào đóng góp lợi nhuận nhiều nhất? nên giữ hay cắt?

Toàn bộ tính toán **Trust Level 1** (deterministic, không AI, không ước lượng thống kê) — kết quả có thể kiểm chứng chéo với Báo cáo kết quả HĐ (B02-DN).

### Đối tượng sử dụng
- **Owner** (chủ doanh nghiệp) — vai trò duy nhất được truy cập
- **Staff / StoreKeeper / Guard** — không thấy module này
- **SystemAdmin** — có quyền truy cập để hỗ trợ kỹ thuật

### Tại sao cần module này?
Kế toán truyền thống cho bạn **báo cáo quá khứ** (tháng trước lãi/lỗ bao nhiêu). Phân tích tài chính cho bạn **công cụ quyết định tương lai**:
- "Tháng này cần bán thêm bao nhiêu mới hết lỗ?"
- "Có nên cắt sản phẩm A không? Nó đóng góp bao nhiêu cho lợi nhuận?"
- "Nếu muốn lợi nhuận 50 triệu tháng tới, cần doanh thu bao nhiêu?"
- "Đang bán có đủ bù chi phí cố định (thuê mặt bằng, lương) không?"

---

## 2. Kiến thức nền tảng

> Đọc section này nếu bạn lần đầu tiếp xúc với khái niệm "điểm hòa vốn" hoặc "biên đóng góp".

### 2.1 Chi phí cố định (Fixed Cost) vs Chi phí biến đổi (Variable Cost)

| Loại | Đặc điểm | Ví dụ |
|------|-----------|-------|
| **Chi phí cố định** | Không đổi dù bán nhiều hay bán ít (trong 1 kỳ) | Tiền thuê mặt bằng, lương nhân sự cố định, khấu hao tài sản |
| **Chi phí biến đổi** | Tăng theo số lượng bán | Nguyên vật liệu (COGS), hoa hồng bán hàng, phí thanh toán |

**Ví dụ:** Quán cà phê thuê mặt bằng 15 triệu/tháng + lương 2 nhân viên 20 triệu/tháng = **chi phí cố định 35 triệu**. Mỗi ly cà phê giá 30K, vốn (cà phê + sữa + ly) 12K = **chi phí biến đổi 12K/ly**.

### 2.2 Biên đóng góp (Contribution Margin)

```
Biên đóng góp = Giá bán − Chi phí biến đổi
Tỷ lệ biên đóng góp = Biên đóng góp / Giá bán
```

**Ý nghĩa:** Mỗi sản phẩm bán ra đóng góp bao nhiêu để bù chi phí cố định.

**Ví dụ:** Ly cà phê 30K, vốn 12K → biên đóng góp = 30K − 12K = **18K/ly**. Tỷ lệ biên đóng góp = 18K/30K = **60%**.

### 2.3 Điểm hòa vốn (Break-even Point)

```
Doanh thu hòa vốn = Chi phí cố định / Tỷ lệ biên đóng góp
Sản lượng hòa vốn = Chi phí cố định / Biên đóng góp trên 1 đơn vị
```

**Ý nghĩa:** Mức doanh thu/số lượng tối thiểu để **không lỗ không lãi**. Bán dưới mức này → lỗ. Bán trên → có lãi.

**Ví dụ quán cà phê:**
- Chi phí cố định = 35 triệu/tháng
- Biên đóng góp/ly = 18K
- Sản lượng hòa vốn = 35,000K / 18K = **1,944 ly/tháng** (~65 ly/ngày nếu mở 30 ngày)
- Doanh thu hòa vốn = 1,944 × 30K = **58,3 triệu/tháng**

→ Nếu bán được 2,000 ly → **có lãi** (vượt hòa vốn 56 ly)
→ Nếu bán được 1,500 ly → **lỗ** (thiếu 444 ly)

### 2.4 Biên an toàn (Margin of Safety)

```
Biên an toàn = Doanh thu thực tế − Doanh thu hòa vốn
Biên an toàn (%) = Biên an toàn / Doanh thu thực tế × 100
```

**Ý nghĩa:** Khoảng "đệm" an toàn trước khi lỗ. Càng cao càng an toàn.

**Ví dụ:** Bán 2,500 ly (doanh thu 75 triệu) vs hòa vốn 58.3 triệu → biên an toàn = 75M − 58.3M = **16.7M** (22%). Nghĩa là doanh thu có giảm 22% vẫn không lỗ.

### 2.5 Đa sản phẩm (Multi-product Break-even)

Khi bán nhiều sản phẩm khác giá khác vốn, hệ thống tính **biên đóng góp trung bình có trọng số** (weighted average) dựa trên cơ cấu bán thực tế.

**Ví dụ quán F&B:**
| Sản phẩm | Giá | Vốn | Biên đóng góp | Tỷ lệ bán | Đóng góp weighted |
|----------|-----|-----|---------------|-----------|-------------------|
| Cà phê sữa | 30K | 12K | 18K (60%) | 60% | 10.8K |
| Trà đào | 35K | 15K | 20K (57%) | 30% | 6.0K |
| Bánh mì | 20K | 8K | 12K (60%) | 10% | 1.2K |
| **Trung bình** | | | | | **18K** |

→ Chi phí cố định 35M / 18K = **1,944 đơn vị** (mixed) để hòa vốn.

---

## 3. Bước 1: Khai báo Hồ sơ doanh nghiệp

**Route:** `/admin/business-profile` (Sidebar → "Hồ sơ doanh nghiệp")

### 3.1 Tại sao phải khai báo tay?

Chi phí cố định mang tính **ước tính chủ quan của Owner** — chỉ bạn biết rõ nhất:
- Tiền thuê có thể đã gồm management fee hoặc phí chung cư
- Lương có thể có thưởng Tết, thưởng doanh số (không cố định)
- Khấu hao tài sản phụ thuộc cách bạn tính (3 năm? 5 năm?)
- Một số chi phí ẩn (bảo hiểm, phí cấp phép) không có trong sổ kế toán

Auto-derive từ kế toán sẽ miss các khoản này → kết quả phân tích sai.

### 3.2 7 loại chi phí cố định hàng tháng (VND)

| # | Chi phí | Ghi chú | Nếu không có |
|---|---------|---------|--------------|
| 1 | **Tiền thuê mặt bằng** | Nếu自有 mặt bằng → 0. Bao gồm management fee nếu có | Nhập 0 |
| 2 | **Lương nhân sự** | Tổng payroll sau thuế. Chỉ tính phần CỐ ĐỊNH (không thưởng doanh số) | Nhập 0 |
| 3 | **Điện nước + tiện ích** | Điện, nước, internet, gas, phí rác | Nhập 0 |
| 4 | **Marketing + quảng cáo** | Facebook Ads, Google Ads, in ấn, KOL | Nhập 0 |
| 5 | **Vận chuyển + logistics** | Shipper, xe tải, fuel giao hàng | Nhập 0 |
| 6 | **Chi phí vận hành khác** | Bảo hiểm, phí cấp phép, văn phòng phẩm, bảo trì | Nhập 0 |
| 7 | **Khấu hao tài sản** | CAPEX amortization (thiết bị, xe, máy, nội thất) | Nhập 0 |

> **Lưu ý quan trọng:** KHÔNG nhập chi phí biến đổi (nguyên vật liệu, COGS) vào đây. Hệ thống tự lấy COGS từ sổ kế toán (mã 20 — giá vốn hàng bán).

### 3.3 Sức chứa & mô hình giá

| Trường | Ý nghĩa | Ghi chú |
|--------|---------|---------|
| **Sản lượng/ngày** (đơn vị) | Sức chứa tối đa 1 ngày hoạt động | Dùng cho feasibility check target profit |
| **Ngày hoạt động/tháng** (1-31) | Số ngày thực tế mở cửa | VD: Chủ nhật nghỉ → 26 ngày |
| **Mô hình giá** | Cố định / Linh hoạt / Kết hợp | Ảnh hưởng cách tính trung bình (MVP-2 mặc định Cố định) |

### 3.4 Ghi chú (tùy chọn)

Mô tả giả định chi phí cố định, nguồn số liệu, ngày cập nhật. VD:
```
- Cập nhật 2026-08-21
- Lương: 2 nhân viên part-time, chưa tính thưởng Tết
- Khấu hao: máy pha cà phê 45M / 3 năm = 1.25M/tháng
- Tiền thuê: đã gồm phí management 500K
```

### 3.5 Phiên bản mô hình (BR-006 — Traceability)

Mỗi lần lưu → tự động tăng phiên bản (1.0 → 1.1 → 1.2...). Khi xem báo cáo kỳ trước, biết được hồ sơ dùng version nào để recompute.

**Ví dụ:**
- Version 1.0 (01/08): Lương 20M
- Version 1.1 (15/08): Lương 25M (thêm 1 nhân viên)
- Xem báo cáo tháng 08 → hệ thống dùng version mới nhất (1.1)

---

## 4. Bước 2: Dashboard tài chính

**Route:** `/financial` (Sidebar → "Thông tin tài chính")

### 4.1 Period picker (chọn kỳ)

Góc phải header: dropdown **Tháng** + **Năm**. Đổi kỳ → toàn bộ widget tự reload.

> Mặc định hiển thị kỳ hiện tại (tháng + năm hiện tại).

### 4.2 5 Widget

#### Widget 1: Doanh thu kỳ
| Thông tin | Ý nghĩa |
|-----------|---------|
| **Doanh thu kỳ** | Tổng doanh thu từ sổ kế toán (mã 21 — doanh thu) |
| Lợi nhuận ròng | Doanh thu − COGS − OpEx |
| Biên lợi nhuận | Lợi nhuận ròng / Doanh thu × 100 |
| Trạng thái | Có lãi / Hòa vốn / Lỗ / Chưa đủ dữ liệu |

#### Widget 2: Điểm hòa vốn
| Thông tin | Ý nghĩa |
|-----------|---------|
| **Doanh thu hòa vốn** | Mức doanh thu tối thiểu để bù chi phí cố định |
| Tổng chi phí cố định | Từ hồ sơ doanh nghiệp (7 loại cộng lại) |
| Biên an toàn | Doanh thu thực tế − Doanh thu hòa vốn |
| Trạng thái | Vượt hòa vốn / Đạt / Chưa đạt / Chưa đủ DL |

#### Widget 3: Top 5 sản phẩm
5 sản phẩm **đóng góp lợi nhuận cao nhất** kỳ (theo ProfitContribution). Dùng để biết SP nào nên đẩy mạnh marketing.

#### Widget 4: Bottom 5 sản phẩm
5 sản phẩm **đóng góp thấp nhất** + cờ "Thiếu giá vốn" (dòng vàng). Dùng để xem có nên cắt SP nào không.

#### Widget 5: Lợi nhuận mục tiêu (Target Profit Calculator)
1. Nhập **lợi nhuận mục tiêu** (VD: 50 triệu)
2. Bấm **"Tính"**
3. Hệ thống trả về:
   - Doanh thu cần thiết
   - Số lượng cần bán
   - Doanh thu/ngày cần đạt
   - **Khả thi** (✓ nếu ≤ sức chứa) hoặc **Vượt sức chứa** (⚠️)

> **Use case:** "Tháng tới muốn lợi nhuận 50M, cần bán bao nhiêu?"

### 4.3 Warning banners (hiển thị trên cùng, trước widget)

| Cảnh báo | Ý nghĩa | Cách xử lý |
|----------|---------|-----------|
| ⚠️ "Chưa khai báo hồ sơ" | Chưa có BusinessProfile | Bấm link → `/admin/business-profile` |
| ℹ️ "Chưa có dữ liệu kỳ này" | Chưa nhập doanh thu/chi phí kỳ | Nhập sổ kế toán trước |
| 🚨 "Biên đóng góp âm" (CM ≤ 0) | Giá vốn > giá bán → bán càng nhiều càng lỗ | Kiểm tra giá vốn vs giá bán |
| ⚠️ "Chưa nhập fixed costs" | Tổng chi phí cố định = 0 | Khai báo lại hồ sơ |

---

## 5. Bước 3: Phân tích điểm hòa vốn

**Route:** `/financial/break-even`

### 5.1 Bảng tổng hợp (Single Break-even)

| Chỉ tiêu | Công thức | Ý nghĩa |
|----------|-----------|---------|
| Tổng chi phí cố định | Σ 7 loại từ hồ sơ | Chi phí phải trả dù không bán gì |
| Tổng doanh thu | Từ sổ kế toán (mã 21) | Doanh thu thực tế kỳ |
| Tổng chi phí biến đổi | COGS từ sổ kế toán (mã 20) | Vốn hàng bán |
| Tổng biên đóng góp | Doanh thu − COGS | Số tiền đóng góp bù fixed cost |
| Tỷ lệ biên đóng góp | Biên đóng góp / Doanh thu | % mỗi 1K doanh thu đóng góp |
| **Doanh thu hòa vốn** | Fixed cost / Tỷ lệ BCM | Mức DT tối thiểu không lỗ |
| **Sản lượng hòa vốn** | Fixed cost / Biên đóng góp/đơn vị | SL tối thiểu không lỗ |
| Biên an toàn (VND) | DT thực − DT hòa vốn | Khoảng đệm trước khi lỗ |
| Biên an toàn (%) | Biên an toàn / DT thực × 100 | % giảm DT vẫn an toàn |
| Trạng thái | So sánh DT thực vs hòa vốn | Vượt / Đạt / Chưa đạt |
| Phiên bản mô hình | Từ BusinessProfile.Version | Traceability |

### 5.2 Biểu đồ so sánh

Bar chart so sánh **Doanh thu thực tế** vs **Doanh thu hòa vốn**:
- Bar xanh (thực tế) dài hơn bar cam (hòa vốn) → **Vượt hòa vốn** (có lãi)
- Bar xanh ngắn hơn → **Chưa đạt** (lỗ)

### 5.3 Bảng đa sản phẩm (Multi-product Break-even)

Break-down per-product:
| Cột | Ý nghĩa |
|-----|---------|
| Sản phẩm | Tên SP |
| Giá bán | Đơn giá |
| Chi phí biến đổi | Product.CostPrice (hoặc 70% giá bán nếu thiếu) |
| Biên đóng góp | Giá bán − Chi phí biến đổi |
| Tỷ lệ BCM | Biên đóng góp / Giá bán |
| Cơ cấu bán | % SL bán SP này / tổng SL |
| SL bán kỳ | Từ Orders kỳ |
| **SL hòa vốn** | Fixed cost × cơ cấu / Biên đóng góp |

> **Use case:** "Nếu đẩy mạnh SP A lên 50% cơ cấu, hòa vốn giảm bao nhiêu?" → export Excel, sửa cơ cấu, tính tay.

### 5.4 Export Excel

Nút **"Xuất Excel"** (góc phải header) → file `.xlsx` với 2 sheets:
- Sheet 1 "Hòa vốn tổng hợp": bảng tổng hợp (11 dòng)
- Sheet 2 "Hòa vốn đa sản phẩm": bảng per-product

---

## 6. Bước 4: Kinh tế đơn vị sản phẩm

**Route:** `/financial/unit-economics`

### 6.1 Metrics tổng quan (4 cards trên cùng)

| Metric | Ý nghĩa |
|--------|---------|
| Sản phẩm phân tích | Tổng SP có bán trong kỳ |
| Tổng biên đóng góp | Σ biên đóng góp tất cả SP |
| Biên đóng góp TB | Trung bình trên tất cả SP |
| Thiếu giá vốn | Số SP có CostPrice = 0 (dòng vàng) |

### 6.2 Bảng sortable (click header để sort ↑/↓)

| Cột | Sort được | Ý nghĩa |
|-----|-----------|---------|
| Sản phẩm | ✓ | Tên SP |
| Nhóm | ✓ | Category SP |
| Giá bán | ✓ | Đơn giá |
| Chi phí biến đổi | ✓ | CostPrice (hoặc 70% giá bán nếu thiếu) |
| Biên đóng góp | ✓ | Giá bán − Chi phí biến đổi |
| Tỷ lệ BCM | ✓ | Biên đóng góp / Giá bán |
| SL bán | ✓ | Từ Orders kỳ |
| Doanh thu | ✓ | Giá bán × SL bán |
| **Đóng góp LN** | ✓ | Biên đóng góp × SL bán (ranking chính) |
| Hạng | — | Xếp hạng theo đóng góp LN (1 = cao nhất) |

### 6.3 Filter theo nhóm

Dropdown "Lọc theo nhóm" → chỉ hiển thị SP trong category đã chọn. VD: chỉ xem "Đồ uống".

### 6.4 Cảnh báo thiếu giá vốn

- Dòng **vàng**: SP có `CostPrice = 0` → biên đóng góp dùng **ước tính 70% giá bán** (precedent OrderService.CalculateCogsAmount)
- Link **"Cập nhật giá vốn"** → tới `/admin/product-cost-prices`
- Sau khi cập nhật, kỳ sau sẽ tính chính xác

> **Use case:** "SP nào đang bán lỗ (biên đóng góp âm)?" → sort by "Biên đóng góp" ↑ → SP có biên âm nằm trên cùng.

### 6.5 Export Excel

Nút **"Xuất Excel"** → file `.xlsx` (1 sheet: danh sách + summary trên cùng).

---

## 7. Ví dụ thực tế theo ngành hàng

### 7.1 Quán cà phê (F&B)

**Hồ sơ:**
- Thuê mặt bằng: 15M
- Lương 2 nhân viên: 20M
- Điện nước: 3M
- Marketing: 2M
- Khấu hao máy pha: 1.25M
- **Tổng fixed cost: 41.25M/tháng**

**Kỳ 08/2026:**
- Doanh thu: 80M (2,667 ly × 30K)
- COGS: 32M (12K/ly)
- OpEx: 5M
- Net profit: 80M − 32M − 5M = **43M** (trừ fixed cost: 43M − 41.25M = **1.75M lãi**)

**Break-even:**
- Biên đóng góp/ly = 30K − 12K = 18K (60%)
- SL hòa vốn = 41.25M / 18K = **2,292 ly** (~76 ly/ngày)
- Đang bán 2,667 ly → **vượt hòa vốn 375 ly** (biên an toàn 14%)

**Target profit 20M:**
- Required revenue = (41.25M + 20M) / 0.60 = **102M** (3,400 ly, ~113 ly/ngày)
- Nếu sức chứa 150 ly/ngày → **khả thi** ✓

### 7.2 Cửa hàng bán lẻ (Retail)

**Hồ sơ:**
- Thuợ mặt bằng: 25M
- Lương 3 nhân viên: 30M
- Điện nước: 4M
- Vận chuyển: 3M
- Khấu hao nội thất: 2M
- **Tổng fixed cost: 64M/tháng**

**Kỳ 08/2026:**
- Doanh thu: 120M
- COGS: 84M (70% — retail thường biên thấp)
- OpEx: 8M
- Net: 120M − 84M − 8M = **28M** (trừ fixed: 28M − 64M = **-36M lỗ**)

**Break-even:**
- Tỷ lệ BCM = (120M − 84M) / 120M = 30%
- DT hòa vốn = 64M / 0.30 = **213M/tháng**
- Đang 120M → **chưa đạt hòa vốn 93M** (cần tăng DT 78%)

**Hành động:**
- Tăng DT lên 213M (khó) HOẶC
- Giảm fixed cost (cắt nhân viên, dời mặt bằng rẻ hơn) HOẶC
- Tăng biên đóng góp (đàm phán giá vốn với supplier, tăng giá bán)

### 7.3 Dịch vụ (Spa / Salon)

**Hồ sơ:**
- Thuợ mặt bằng: 20M
- Lương 2 kỹ thuật viên: 24M
- Điện nước: 2M
- Marketing: 5M (spa phụ thuộc marketing)
- Khấu hao thiết bị: 1.5M
- **Tổng fixed cost: 52.5M/tháng**

**Đặc thù:** Chi phí biến đổi thấp (chỉ mỹ phẩm + dụng cụ) → biên đóng góp cao (80%+).

**Kỳ 08/2026:**
- Doanh thu: 70M (350 dịch vụ × 200K avg)
- COGS: 7M (10% — mỹ phẩm)
- OpEx: 3M
- Net: 70M − 7M − 3M = **60M** (trừ fixed: 60M − 52.5M = **7.5M lãi**)

**Break-even:**
- Tỷ lệ BCM = (70M − 7M) / 70M = 90%
- DT hòa vốn = 52.5M / 0.90 = **58.3M/tháng** (~292 dịch vụ)
- Đang 350 dịch vụ → **vượt hòa vốn 58 dịch vụ** (biên an toàn 17%)

→ Spa có biên cao nhưng fixed cost cũng cao. Nếu khách giảm 20% vẫn có lãi (biên an toàn 17%).

---

## 8. Cảnh báo (guard conditions) & cách xử lý

| Mã cảnh báo | Hiển thị | Nguyên nhân | Cách xử lý |
|-------------|----------|-------------|-----------|
| `PROFILE_MISSING` | ⚠️ "Chưa khai báo hồ sơ doanh nghiệp" | Chưa có BusinessProfile | Vào `/admin/business-profile` điền form |
| `INSUFFICIENT_DATA` | ℹ️ "Chưa có dữ liệu kế toán kỳ này" | Chưa nhập doanh thu/chi phí kỳ | Nhập sổ kế toán (doanh thu + chi phí) kỳ đó |
| `COST_PRICE_MISSING` | ⚠️ "Có N sản phẩm thiếu giá vốn" | Product.CostPrice = 0 | Vào `/admin/product-cost-prices` cập nhật |
| `CM_RATIO_ZERO_OR_NEG` | 🚨 "Biên đóng góp âm" | COGS > Doanh thu (giá vốn > giá bán) | Kiểm tra giá vốn vs giá bán. Có thể nhập sai mã kế toán (COGS vào mã 11 thay vì 20) |
| `CAPACITY_EXCEEDED` | ⚠️ "Vượt sức chứa" | RequiredDaily > DailyCapacityUnits | Tăng sức chứa (thêm giờ, thêm nhân viên) hoặc giảm target profit |
| `FIXED_COST_ZERO` | ⚠️ "Chưa nhập fixed costs" | Tổng fixed cost = 0 (tất cả 7 loại = 0) | Khai báo lại hồ sơ — chắc chắn có ít nhất 1 loại chi phí cố định |

---

## 9. Export Excel

### 9.1 BreakEven export
- Route: `/financial/break-even` → nút **"Xuất Excel"**
- File: `BreakEven_YYYY_MM.xlsx`
- 2 sheets:
  - **"Hòa vốn tổng hợp"**: 11 dòng chỉ tiêu + giá trị
  - **"Hòa vốn đa sản phẩm"**: bảng per-product (nếu có dữ liệu)

### 9.2 UnitEconomics export
- Route: `/financial/unit-economics` → nút **"Xuất Excel"**
- File: `UnitEconomics_YYYY_MM.xlsx`
- 1 sheet "Kinh tế đơn vị":
  - 4 dòng summary (tổng SP, tổng biên, biên TB, SL thiếu giá vốn)
  - Bảng per-product (sorted by đóng góp LN desc) + dòng vàng cho SP thiếu giá vốn

### 9.3 Thư viện
- **EPPlus 7.6.1** (LicenseContext = NonCommercial)
- Format số: `#,##0` (VND không dấu thập phân)
- Dòng header: bold + nền xám
- Dòng thiếu giá vốn: nền vàng `#FFEB9C`

---

## 10. Câu hỏi thường gặp (FAQ)

### Q1: Tại sao doanh thu hòa vốn khác doanh thu thực tế?
**A:** Điểm hòa vốn = mức doanh thu **tối thiểu** để bù chi phí cố định. Nếu DT thực > hòa vốn → có lãi. Nếu < → lỗ. Đây là 2 số liệu khác nhau, không nên bằng nhau.

### Q2: Biên an toàn là gì? Bao nhiêu là tốt?
**A:** Khoảng cách giữa DT thực và DT hòa vốn. Quy ước:
- **> 30%**: An toàn cao (chịu được giảm DT 30% vẫn không lỗ)
- **15-30%**: An toàn vừa
- **5-15%:** Cần cẩn thận
- **< 5%**: Nguy hiểm (DT giảm chút đã lỗ)
- **Âm**: Đang lỗ

### Q3: Tại sao cần khai báo fixed cost tay?
**A:** Chi phí cố định mang tính ước tính chủ quan. Owner biết rõ nhất — vd: tiền thuê có thể đã gồm management fee, lương có thể có thưởng Tết. Auto-derive từ kế toán sẽ miss các khoản ẩn.

### Q4: Phiên bản mô hình (1.0, 1.1...) để làm gì?
**A:** Traceability (BR-006). Mỗi lần cập nhật hồ sơ, version tăng. Khi xem báo cáo kỳ trước, biết được hồ sơ dùng version nào để recompute. VD: kỳ 08 dùng version 1.0 (lương 20M), kỳ 09 dùng version 1.1 (lương 25M).

### Q5: SP thiếu giá vốn ảnh hưởng gì?
**A:** Hệ thống dùng ước tính **70% giá bán** làm chi phí biến đổi (precedent OrderService). Kết quả có thể sai. **Cần cập nhật CostPrice** tại `/admin/product-cost-prices` để có số liệu chính xác kỳ sau.

### Q6: "Biên đóng góp âm" nghĩa là gì?
**A:** Giá vốn (COGS) > Giá bán. Bán càng nhiều càng lỗ. Nguyên nhân:
- Nhập sai giá vốn (vd: 300K thay vì 30K)
- Nhập sai mã kế toán (COGS vào mã 11 thay vì 20)
- Thực sự bán lỗ (khuyến mãi quá sâu)

### Q7: Target profit "Vượt sức chứa" là gì?
**A:** RequiredDailyUnits > DailyCapacityUnits (từ hồ sơ). Nghĩa là cần bán/ngày nhiều hơn khả năng phục vụ. Cách xử lý:
- Tăng sức chứa (thêm giờ, thêm nhân viên, mở rộng)
- Giảm target profit (thực tế hơn)
- Tăng biên đóng góp (giảm COGS, tăng giá bán → giảm SL cần bán)

### Q8: Tại sao số liệu Dashboard khác B02-DN?
**A:** Nên giống nhau (cùng source từ IncomeStatement). Nếu khác:
- Kiểm tra kỳ chọn có đúng không
- Kiểm tra AccountingStandard (TT99 vs TT200) — MVP-2 mặc định TT99
- Báo cáo dev nếu vẫn khác (có thể bug)

### Q9: Có thể xem nhiều kỳ cùng lúc không?
**A:** MVP-2 chỉ xem 1 kỳ/lần. Multi-period comparison (trend) defer sang sprint sau.

### Q10: Ai thấy được module này?
**A:** Chỉ **Owner** (vai trò duy nhất). Staff/StoreKeeper/Guard không thấy. SystemAdmin có quyền truy cập để hỗ trợ.

### Q11: Dữ liệu có bị mất khi cập nhật hồ sơ?
**A:** Không. Mỗi lần lưu là **upsert** (update or insert). Version tăng nhưng dữ liệu cũ không mất. Nếu cần xem version cũ, check audit log.

### Q12: Có thể rollback về version cũ không?
**A:** MVP-2 không hỗ trợ rollback tự động. Cần re-input tay nếu muốn quay về giá trị cũ.

---

## 11. Kiểm chứng chéo với Báo cáo kế toán

MVP-2 thiết kế để **kiểm chứng được** với B02-DN (Báo cáo kết quả HĐ):

| Chỉ tiêu MVP-2 | Mã kế toán | Báo cáo tương đương |
|----------------|------------|---------------------|
| Doanh thu | 21 | B02-DN dòng "Doanh thu thuần" |
| COGS | 20 | B02-DN dòng "Giá vốn hàng bán" |
| OpEx | 11 | B02-DN dòng "Chi phí bán hàng + Chi phí QLDN" |
| Lợi nhuận ròng | Net | B02-DN dòng "Lợi nhuận sau thuế" |

**Cách verify:**
1. Mở `/accounting/income-statement` → chọn cùng kỳ
2. So sánh 4 chỉ tiêu trên với Dashboard MVP-2
3. Nếu khác → báo dev (có thể bug extract)

---

## 12. Giới hạn & Lưu ý quan trọng

### 12.1 MVP-2 KHÔNG làm được (defer sang sprint sau)
- ❌ Export PDF (chỉ có Excel — project chưa có iText library)
- ❌ Multi-period comparison (xu hướng tăng/giảm qua nhiều tháng)
- ❌ What-if simulation (sửa giả định → xem kết quả ngay, không cần save)
- ❌ Rollback hồ sơ về version cũ
- ❌ Alert tự động (email/SMS khi vượt hòa vốn, biên an toàn < 5%)
- ❌ Forecast (dự báo kỳ sau dựa trend)

### 12.2 Giả định tính toán
- **CostPrice fallback 70% UnitPrice** khi CostPrice = 0 (precedent OrderService) — kết quả có thể sai cho SP thiếu giá vốn
- **PricingModel mặc định FixedPrice** — DynamicPricing/Mixed chưa affect calculation logic
- **Multi-product dùng weighted average** dựa cơ cấu bán thực tế kỳ đó — không phải cơ cấu tối ưu
- **Trust Level 1 only** — pure deterministic, không AI, không statistical forecast

### 12.3 Độ chính xác
- **BreakEven**: < 500ms (cold cache, ≤ 1000 AccountingEntry) — NFR-1
- **UnitEconomics**: < 1s (≤ 200 products, ≤ 5000 OrderItem) — NFR-2
- **Multi-tenancy**: tenant A không thấy data tenant B — NFR-4 (enforced)

### 12.4 Khi nào nên cập nhật hồ sơ?
- Thay đổi mặt bằng (dời, mở thêm, đóng cửa chi nhánh)
- Thay đổi nhân sự (thêm, cắt, tăng lương)
- Thay đổi giá vốn đáng kể (supplier mới, nhập hàng lớn)
- Đầu kỳ mới (review hàng quý/6 tháng)

---

## 13. Ghi chú kỹ thuật (cho dev)

- **Source of truth:** Gateway PostgreSQL — ShopERP proxy qua HTTP (`FinancialIntelligenceHttpService`)
- **Domain purity:** Services trong `3_CoreHub/Services/FinancialIntelligence/`, không inject DbContext
- **Auth:** `[Authorize(Policy = "SystemAdmin")]` (HKD tenant có quyền truy cập)
- **W8 feature flag bypass:** Controller inject `IIncomeStatementService` trực tiếp, không qua `IncomeStatementsController`
- **UI Platform 100%:** VanACard, VanAMetricsCard, VanAButton, VanAAlert (no raw HTML/CSS bypass)
- **Export:** EPPlus 7.6.1 (`FinancialExportService`), precedent `InventoryExcelReport` (Wave 3)
- **E2E:** `6_Testing/e2e-tests/financial-dashboard.spec.ts` (Gate 4 compliance) — runs in CI (localhost + dev login)
- **i18n:** 100% tiếng Việt có dấu (NFR-12)
- **SRS:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md`
- **Task card:** `docs/AI/tasks/task_financial_intelligence_mvp2.md`
- **PR:** #152 (merged 2026-08-21, commit `dc8338ed`)

### Files chính
| File | Role |
|------|------|
| `1_Shared/Domain/BusinessProfile.cs` | Entity (Single-Identity, 7 fixed costs + capacity + pricing + version) |
| `1_Shared/Domain/FinancialIntelligenceRecords.cs` | 5 result records + 2 enums |
| `3_CoreHub/Services/FinancialIntelligence/*.cs` | 4 calculation services + BusinessProfileService |
| `3_CoreHub/Services/FinancialIntelligence/Dtos/FinancialDtos.cs` | 8 DTOs (camelCase JSON) |
| `2_Gateway/Controllers/FinancialIntelligenceController.cs` | 7 endpoints (class-level [Authorize JwtBearer]) |
| `5_WebApps/ShopERP/Services/FinancialIntelligenceHttpService.cs` | ShopERP HTTP proxy (extends GatewayAdminApiClientBase) |
| `5_WebApps/ShopERP/Services/FinancialExportService.cs` | Excel export (EPPlus) |
| `5_WebApps/ShopERP/Components/Pages/Admin/BusinessProfile.razor` | CRUD form |
| `5_WebApps/ShopERP/Components/Pages/Financial/Dashboard.razor` | 5 widgets + period picker + warning banners |
| `5_WebApps/ShopERP/Components/Pages/Financial/BreakEven.razor` | Single + multi table + bar chart + export |
| `5_WebApps/ShopERP/Components/Pages/Financial/UnitEconomics.razor` | Sortable table + filter + export |

---

**Tạo:** 2026-08-21 · **Owner:** VanAn Team · **Spec:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` · **PR:** #152
