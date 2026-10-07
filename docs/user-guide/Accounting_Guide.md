# HƯỚNG DẪN SỬ DỤNG MODULE KẾ TOÁN — VẠN AN ECOSYSTEM

> **Phiên bản:** MVP v1.1 — cập nhật 2026-10-07
> **Áp dụng:** Module kế toán Vạn An — Thu chi · Công nợ (Phải thu/Phải trả) · Sổ sách · Báo cáo tài chính — dành cho chủ hộ kinh doanh (HKD) và doanh nghiệp nhỏ.
> **Phạm vi:** Nhập phiếu thu/chi (3 loại mỗi loại) · Tra cứu MST · Lịch sử giao dịch · Báo cáo công nợ + tuổi nợ · Sổ theo dõi phải thu/phải trả + lịch sử thanh toán · Số dư tài khoản · Đóng kỳ kế toán · Sổ HKD (TT 152) / Báo cáo tài chính.
> **Luồng tổng thể:** Ghi nhận nghiệp vụ qua Phiếu → kiểm tra Lịch sử giao dịch → theo dõi Công nợ (báo cáo + sổ + tuổi nợ) → cuối tháng Đóng kỳ → xem Sổ HKD / Báo cáo tài chính.

---

## MỤC LỤC

1. [Tổng quan & vai trò](#1-tổng-quan--vai-trò)
2. [Nguyên tắc kế toán quan trọng](#2-nguyên-tắc-kế-toán-quan-trọng)
3. [Phiếu thu — 3 loại phiếu](#3-phiếu-thu--3-loại-phiếu)
4. [Phiếu chi — 3 loại phiếu](#4-phiếu-chi--3-loại-phiếu)
5. [Tra cứu MST (mã số thuế)](#5-tra-cứu-mst-mã-số-thuế)
6. [Lịch sử giao dịch](#6-lịch-sử-giao-dịch)
7. [Báo cáo công nợ tổng hợp + tuổi nợ](#7-báo-cáo-công-nợ-tổng-hợp--tuổi-nợ)
8. [Sổ theo dõi phải thu / phải trả](#8-sổ-theo-dõi-phải-thu--phải-trả)
9. [Số dư tài khoản](#9-số-dư-tài-khoản)
10. [Đóng kỳ kế toán](#10-đóng-kỳ-kế-toán)
11. [Sổ HKD (TT 152) & Báo cáo tài chính](#11-sổ-hkd-tt-152--báo-cáo-tài-chính)
12. [Câu hỏi thường gặp (FAQ)](#12-câu-hỏi-thường-gặp-faq)

---

## 1. TỔNG QUAN & VAI TRÒ

| Vai trò | Mô tả | Nền tảng truy cập | Phạm vi |
|---|---|---|---|
| **Chủ shop / Owner** | Người duy nhất được ghi sổ kế toán, xem báo cáo, đóng kỳ | ShopERP — menu **"Kế Toán"** (`/accounting/*`) | **Shop của mình** (per-tenant) |

**Module kế toán đáp ứng 4 nhu cầu:**

1. **Thu chi** — ghi nhận tiền thu vào / chi ra qua Phiếu thu / Phiếu chi.
2. **Phải thu** — khách hàng còn nợ bao nhiêu (bán chịu chưa thu).
3. **Phải trả** — mình còn nợ người bán / nhà cung cấp bao nhiêu (mua chịu chưa trả).
4. **Theo dõi** — từng khách / người bán: phát sinh → đã thu/trả → còn lại + **tuổi nợ** + **lịch sử thanh toán từng khoản nợ**.

**Vào module:** đăng nhập ShopERP bằng tài khoản **Owner** → menu **"Kế Toán"**:

| Menu | Trang | Công dụng |
|---|---|---|
| Kế Toán | `/accounting` | Dashboard: doanh thu/chi phí/lợi nhuận tháng + thao tác nhanh |
| Lịch Sử Giao Dịch | `/accounting/history` | Xem mọi bút toán theo tháng |
| **Công Nợ** | `/accounting/cong-no` | Báo cáo phải thu/phải trả + tuổi nợ |
| Số Dư Tài Khoản | `/accounting/balance` | Số dư từng tài khoản kế toán |
| Đóng Kỳ Kế Toán | `/accounting/period-closing` | Khóa sổ cuối tháng |
| Sổ HKD (TT 152) / Báo Cáo Tài Chính | `/accounting/hkd-books` / `/accounting/financial-reports` | Sổ sách & báo cáo theo chuẩn kế toán |

> 💡 Mọi trang cũng có trong **Sitemap** (`/sitemap` → thẻ "💰 Kế Toán & Tài Chính").

---

## 2. NGUYÊN TẮC KẾ TOÁN QUAN TRỌNG

| # | Nguyên tắc | Ý nghĩa thực tế |
|---|---|---|
| 1 | **Bút toán không sửa, không xóa** | Mọi phiếu sau khi lưu là **bất biến** (append-only). Sai sót → dùng nút **"Đảo"** (đảo bút toán) rồi lập phiếu mới đúng. |
| 2 | **Đảo bút toán** | Tạo phiếu đảo có số tiền ngược dấu + ghi lý do → số dư tự khớp lại. Phiếu gốc + phiếu đảo đều hiển thị trong sổ (minh bạch, kiểm toán được). |
| 3 | **Kỳ đóng sổ** | Cuối tháng đóng kỳ → **không thể thêm/đảo bút toán** vào kỳ đã đóng. Muốn sửa → mở lại kỳ (nếu được) hoặc hạch toán vào kỳ sau. |
| 4 | **Bán chịu không ghi doanh thu** | Phiếu "Ghi nhận phải thu" chỉ theo dõi công nợ (TK 131) — **không** tính vào doanh thu. Khi khách trả tiền, ghi doanh thu bằng phiếu thu thường. |
| 5 | **Mua chịu không ghi chi phí** | Phiếu "Ghi nhận phải trả" chỉ theo dõi công nợ (TK 331) — không tính vào chi phí. Khi trả tiền, ghi chi phí bằng phiếu chi thường. |
| 6 | **Tra MST** | Nhập mã số thuế + nút "Tra cứu" → tự điền tên công ty vào Đối tượng (không cần gõ tay). |

---

## 3. PHIẾU THU — 3 LOẠI PHIẾU

Vào ShopERP → **Kế Toán → Nhập Doanh Thu** (`/accounting/revenue`) hoặc nút **"➕ Nhập Doanh Thu"** trên dashboard.

Chọn **"Loại Phiếu Thu"** (3 lựa chọn):

| Loại phiếu | Khi nào dùng | Tài khoản | Số tiền | Ô "Đối tượng" |
|---|---|---|---|---|
| **Thu doanh thu** *(mặc định)* | Khách trả tiền, mình ghi doanh thu thật | 5xx / 7xx | dương | Ẩn (không bắt buộc) |
| **Ghi nhận phải thu** | **Bán chịu** cho khách (chưa thu tiền) | **131** — Phải thu | dương (tăng nợ) | **BẮT BUỘC** |
| **Thu tiền khách trả nợ** | Khách trả nợ khoản đã bán chịu | **131** — Phải thu | âm (giảm nợ) | **BẮT BUỘC** |

**Các bước lập phiếu:**

1. Chọn **Loại Phiếu Thu**.
2. Chọn **Ngày** phát sinh (mặc định hôm nay — có thể nhập ngày trước).
3. Nhập **Số Tiền**.
4. Nếu loại công nợ: nhập **Đối Tượng (khách hàng)** — có thể **Tra MST** để tự điền tên công ty (xem [mục 5](#5-tra-cứu-mst-mã-số-thuế)).
5. Chọn **Tài Khoản** (tự động gợi ý đúng theo loại phiếu — công nợ chỉ có 131).
6. **Diễn Giải** — để trống sẽ tự gợi ý: `Bán chịu — {khách} (MST ...)` / `Thu tiền khách trả nợ — {khách}`.
7. (Tùy chọn) **Số Chứng Từ** — số hóa đơn/chứng từ liên quan.
8. Nhấn **"Lưu Doanh Thu"** → thấy thông báo "Đã lưu doanh thu thành công!".

> 💡 **Hai phiếu cùng số tiền trong 5 phút:** hệ thống chỉ cảnh báo trùng lặp cho phiếu *thu doanh thu*; phiếu công nợ của 2 khách khác nhau cùng số tiền là hợp lệ, không bị chặn.

---

## 4. PHIẾU CHI — 3 LOẠI PHIẾU

Vào ShopERP → **Kế Toán → Nhập Chi Phí** (`/accounting/expenses`) hoặc nút **"➖ Nhập Chi Phí"** trên dashboard.

Chọn **"Loại Phiếu Chi"** (3 lựa chọn):

| Loại phiếu | Khi nào dùng | Tài khoản | Số tiền | Ô "Đối tượng" |
|---|---|---|---|---|
| **Chi phí** *(mặc định)* | Chi tiền mua hàng/dịch vụ, ghi chi phí thật | 6xx | dương | Không bắt buộc (có ô Nhà cung cấp) |
| **Ghi nhận phải trả** | **Mua chịu** nguyên liệu/hàng hóa (chưa trả tiền) | **331** — Phải trả | dương (tăng nợ) | **BẮT BUỘC** |
| **Trả tiền người bán** | Trả nợ cho người bán khoản đã mua chịu | **331** — Phải trả | âm (giảm nợ) | **BẮT BUỘC** |

Thao tác tương tự phiếu thu: chọn loại → ngày → số tiền → **Đối tượng (nhà cung cấp)** (loại công nợ) → tài khoản (tự gợi ý 331) → diễn giải (tự gợi ý `Mua chịu — {người bán}` / `Trả tiền người bán — {người bán}`) → **"Lưu Chi Phí"**.

---

## 5. TRA CỨU MST (MÃ SỐ THUẾ)

Trên phiếu thu/chi loại công nợ (hoặc phiếu thường):

1. Nhập **Mã Số Thuế** (10 hoặc 13 chữ số) vào ô MST.
2. Nhấn nút **"Tra cứu MST"**.
3. Hệ thống gọi cổng thông tin doanh nghiệp → hiện **thẻ thông tin** (tên công ty, địa chỉ, trạng thái hoạt động...) và **tự điền tên công ty vào ô Đối tượng** + gợi ý diễn giải kèm MST.

> ⚠️ Nếu MST không tồn tại / hệ thống tra cứu tạm gián đoạn (quá tải), sẽ hiện thông báo thân thiện — bạn vẫn có thể **gõ tên đối tượng bằng tay**. Cùng chuỗi tên = cùng đối tượng (nên gõ chuẩn để gom đúng 1 khách).

---

## 6. LỊCH SỬ GIAO DỊCH

Vào **Kế Toán → Lịch Sử Giao Dịch** (`/accounting/history`):

- Danh sách **mọi bút toán** (thu, chi, công nợ, đảo) theo khoảng ngày.
- **Tìm kiếm** theo diễn giải, **lọc** theo khoản tiền (min/max) và loại tài khoản.
- Nút **"Chi Tiết"** trên mỗi dòng → xem đầy đủ thông tin phiếu.
- Nút **"📊 Export Excel"** → tải file CSV mở được bằng Excel.

---

## 7. BÁO CÁO CÔNG NỢ TỔNG HỢP + TUỔI NỢ

Vào **Kế Toán → Công Nợ** (`/accounting/cong-no`) — báo cáo theo tháng, 2 khối:

**PHẢI THU (131)** và **PHẢI TRẢ (331)**, mỗi đối tượng một dòng:

| Đối tượng | Đầu kỳ | PS tăng | Đã thu/trả | Cuối kỳ | Tuổi nợ |
|---|---|---|---|---|---|
| Khách A | 0 | 5.000.000 | 2.000.000 | 3.000.000 | <30: 0 · 30-60: 3tr · 60-90: 0 · >90: 0 |
| Công Ty TNHH X | 2.000.000 | 0 | 0 | 2.000.000 | >90: 2tr |
| **TỔNG** | ... | ... | ... | ... | ... |

**Thao tác:**

- **Đổi tháng**: chọn Năm + Tháng → nút **"Xem"**.
- **Lọc nhanh**: chips **Tất cả / Còn nợ / Hết nợ**.
- **In báo cáo**: nút **"🖨️ In Báo Cáo"**.
- **Xuất Excel**: nút **"📊 Xuất Excel"** (file CSV mở bằng Excel).
- **Xem sổ chi tiết 1 đối tượng**: bấm vào **tên đối tượng** → sang [Sổ theo dõi](#8-sổ-theo-dõi-phải-thu--phải-trả).
- Nếu đối tượng **trả thừa / trả trước** (số dư âm) → hiện badge **"trả trước"** (hệ thống không chặn, hiển thị bình thường).

**Tuổi nợ** (tính theo **FIFO** — thanh toán trừ vào khoản **cũ nhất** trước):

| Nhóm | Ý nghĩa |
|---|---|
| < 30 ngày | Nợ mới phát sinh trong tháng |
| 30–60 ngày | Nợ từ 1-2 tháng trước |
| 60–90 ngày | Nợ từ 2-3 tháng trước |
| > 90 ngày | Nợ quá 3 tháng — cần chú ý thu hồi |

> 💡 **Ví dụ:** bán chịu 5tr ngày 01/07 + 3tr ngày 15/08, khách trả 2tr ngày 20/09 → 2tr trả vào khoản 01/07 (cũ nhất) → khoản 01/07 còn **3tr (>90 ngày)**, khoản 15/08 còn **3tr (30-60 ngày)**.

---

## 8. SỔ THEO DÕI PHẢI THU / PHẢI TRẢ

Từ báo cáo công nợ, bấm tên đối tượng → sổ chi tiết cộng dồn:

```
SỔ THEO DÕI PHẢI THU — Khách A (TK 131) — Kỳ 10/2026
──────────────────────────────────────────────
Ngày       Diễn giải                    Tăng      Giảm      Số dư
01/10      Bán chịu — Khách A         5.000.000           5.000.000
15/10      Thu tiền khách trả nợ                2.000.000  3.000.000
──────────────────────────────────────────────
Cuối kỳ                                               Số dư 3.000.000
```

**Thao tác trên mỗi dòng:**

| Nút | Công dụng |
|---|---|
| **"Lịch sử"** *(chỉ dòng khoản nợ — Tăng > 0)* | Mở **"Lịch Sử Thanh Toán Khoản Nợ"**: khoản này đã được thu/trả những lần nào, mỗi lần bao nhiêu, **còn nợ bao nhiêu** (tính FIFO động) |
| **"Đảo"** | Nhập sai → đảo bút toán: nhập **lý do** → "Xác Nhận Đảo" → số dư tự khớp lại; dòng đảo hiển thị kèm badge **"đảo"** |

> ⚠️ Phiếu **"Thu tiền khách trả nợ"** và **"Trả tiền người bán"** cũng có nút Đảo (nhập sai lần thu/trả → đảo để phục hồi số dư khoản nợ).

---

## 9. SỐ DƯ TÀI KHOẢN

Vào **Kế Toán → Số Dư Tài Khoản** (`/accounting/balance`): tổng hợp số dư từng tài khoản kế toán (111 Tiền mặt, 131 Phải thu, 331 Phải trả, 511 Doanh thu...) — dùng để đối chiếu nhanh.

---

## 10. ĐÓNG KỲ KẾ TOÁN

Vào **Kế Toán → Đóng Kỳ Kế Toán** (`/accounting/period-closing`), cuối mỗi tháng:

1. Chọn **kỳ (tháng/năm)** cần đóng.
2. Hệ thống kiểm tra số liệu (doanh thu/chi phí) → nhấn **Đóng kỳ**.
3. Kỳ đã đóng: **không thể thêm phiếu hoặc đảo bút toán** vào kỳ đó (có thông báo "Kỳ kế toán đã đóng sổ").
4. Nếu cần sửa số liệu kỳ đã đóng → dùng tính năng **mở lại kỳ** (nếu có) hoặc hạch toán bổ sung vào kỳ hiện tại.

---

## 11. SỔ HKD (TT 152) & BÁO CÁO TÀI CHÍNH

Tùy loại hình kinh doanh, menu "Kế Toán" hiển thị:

**Hộ kinh doanh (HKD):** **Sổ Kế Toán HKD** (`/accounting/hkd-books`) — bộ sổ theo TT 152/2025/TT-BTC:
- S1a — Sổ theo dõi hàng hóa/dịch vụ cung ứng
- S2a — Sổ theo dõi hàng hóa/dịch vụ cung ứng (nộp thuế GTGT)
- S2b — Sổ doanh thu bán hàng hóa/dịch vụ
- S2c — Sổ chi tiết doanh thu, chi phí
- S2d — Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa
- S2e — Sổ chi tiết tiền
- S3a — Sổ tổng hợp theo mẫu (nếu có)

**Doanh nghiệp:** **Báo Cáo Tài Chính** (`/accounting/financial-reports`) — hub gồm:
- Bảng cân đối số phát sinh (Trial Balance)
- Báo cáo kết quả kinh doanh (Income Statement)
- Bảng cân đối kế toán (Balance Sheet)
- Báo cáo lưu chuyển tiền tệ (Cash Flow)
- Thuyết minh báo cáo tài chính (Notes)

> 💡 Phiếu công nợ (131/331) **không** xuất hiện trong doanh thu/chi phí — đúng bản chất: bán chịu chưa phải doanh thu, mua chịu chưa phải chi phí. Chúng chỉ theo dõi trong báo cáo công nợ + sổ cái tài khoản.

---

## 12. CÂU HỎI THƯỜNG GẶP (FAQ)

**Q1. Nhập sai phiếu thì sửa thế nào?**
Không sửa được trực tiếp (bút toán bất biến). Dùng nút **"Đảo"** trên dòng phiếu trong sổ/lịch sử → nhập lý do → lập phiếu mới đúng.

**Q2. Khách trả thừa / trả trước thì sao?**
Hệ thống cho phép số dư âm và hiển thị badge **"trả trước"** — không chặn, không tính lãi.

**Q3. Hai khách cùng bán chịu 5 triệu trong 5 phút có bị chặn không?**
Không. Phiếu công nợ không áp dụng chống trùng (chỉ phiếu thu doanh thu/chi phí mới cảnh báo trùng trong 5 phút).

**Q4. Làm sao biết khoản nợ cũ còn bao nhiêu chưa thu?**
Báo cáo Công Nợ → bấm tên đối tượng → sổ chi tiết → nút **"Lịch sử"** trên khoản nợ → xem từng lần thu/trả + số còn lại.

**Q5. Tuổi nợ tính theo ngày nào?**
Theo **ngày phát sinh khoản nợ** (ngày trên phiếu) so với **cuối kỳ báo cáo**; thanh toán trừ vào khoản cũ nhất trước (FIFO).

**Q6. Cuối tháng cần làm gì?**
1) Rà soát Lịch sử giao dịch + Báo cáo công nợ → 2) Đảo các phiếu nhập sai → 3) **Đóng kỳ kế toán** → 4) Xem/lưu Sổ HKD hoặc Báo cáo tài chính.

**Q7. Tra MST báo lỗi "Mã số thuế không hợp lệ"?**
MST phải 10 hoặc 13 chữ số (bỏ dấu "-"). Nếu đúng mà vẫn lỗi → hệ thống tra cứu tạm gián đoạn → nhập tên đối tượng bằng tay.

**Q8. Ai được xem module kế toán?**
Chỉ tài khoản **Owner** (chủ shop). Nhân viên/khác không vào được.

---

*Tài liệu đi kèm: SRS Thu chi & Công nợ v1.1 (`docs/requirements/van_an_thu_chi_cong_no_srs_v1.md`) · Master plan (`docs/AI/plans/thu-chi-cong-no-master-plan.md`).*
