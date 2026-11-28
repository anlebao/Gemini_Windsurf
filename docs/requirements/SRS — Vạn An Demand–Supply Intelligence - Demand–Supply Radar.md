# SOFTWARE REQUIREMENTS SPECIFICATION (SRS)

## Vạn An Demand–Supply Intelligence / Demand–Supply Radar

**Document Version:** 1.0  
**Status:** Draft for Architecture & Development  
**Product:** Vạn An Ecosystem  
**Primary Modules:** KhachLink, ShopERP, Accounting, Local Commerce  
**Document Type:** Software Requirements Specification

---

# 1. Purpose

## 1.1. Objective

Xây dựng hệ thống **Demand–Supply Intelligence (DSI)** cho hệ sinh thái Vạn An nhằm thu thập, chuẩn hóa, phân tích và chuyển đổi dữ liệu hoạt động kinh tế địa phương thành các **Market Signals** phản ánh:

- Nhu cầu của người mua.
- Nguồn cung của người bán.
- Khoảng cách cung–cầu.
- Khả năng đáp ứng của thị trường.
- Xu hướng thay đổi theo thời gian.
- Hành vi giao dịch thực tế.
- Cơ hội mở rộng thị trường.
- Tín hiệu hỗ trợ quyết định kinh doanh và đầu tư.

Hệ thống không nhằm mục đích thay thế quyết định của nhà đầu tư hoặc doanh nghiệp.

---

# 2. Problem Statement

Các hệ thống marketplace thông thường chủ yếu tập trung vào:

> Buyer → Product → Seller → Transaction

Vạn An cần bổ sung một lớp:

> Demand → Supply → Transaction → Market Intelligence

Mục tiêu là biến dữ liệu vận hành được tạo ra tự nhiên từ hệ sinh thái thành thông tin có cấu trúc về thị trường.

Ví dụ:

```text
1,500 demand signals/day
        ↓
600 product/service views
        ↓
250 conversations
        ↓
180 orders
        ↓
30 suppliers
        ↓
Demand exceeds available supply
        ↓
Local Market Opportunity Signal
```

---

# 3. Scope

## 3.1. In Scope

Hệ thống phải hỗ trợ:

1. Thu thập Demand Signals.
2. Thu thập Supply Signals.
3. Thu thập Transaction Signals.
4. Chuẩn hóa dữ liệu theo:
   - Geographic area
   - Category
   - Product/service
   - Time
   - Price range
5. Tính toán Demand Metrics.
6. Tính toán Supply Metrics.
7. Tính toán Demand/Supply Gap.
8. Tính toán Market Opportunity Signals.
9. Phân tích xu hướng.
10. So sánh các khu vực.
11. So sánh các ngành hàng.
12. Dashboard cho:
   - Vạn An operator
   - SME
   - Investor/authorized analyst
13. Data privacy và access control.
14. Data quality monitoring.
15. Audit trail.

---

# 4. Out of Scope — MVP

MVP không bao gồm:

- Dự đoán chính xác giá cổ phiếu.
- Dự đoán kết quả đầu tư.
- Automated investment recommendation.
- Automated lending decision.
- Credit scoring cá nhân.
- Bán dữ liệu cá nhân.
- Công khai dữ liệu giao dịch của từng khách hàng.
- Automated pricing control.
- Automated inventory purchasing.

Các chức năng này có thể được xem xét ở các phiên bản sau khi có cơ sở pháp lý và dữ liệu đủ lớn.

---

# 5. System Goals

## G1 — Capture

Thu thập được tín hiệu cung và cầu từ hoạt động thực tế.

## G2 — Normalize

Chuẩn hóa dữ liệu từ nhiều hệ thống khác nhau.

## G3 — Measure

Đo được:

- Demand
- Supply
- Transactions
- Conversion
- Fulfillment
- Repeat
- Gap

## G4 — Detect

Phát hiện các tín hiệu:

- Demand tăng.
- Supply thiếu.
- Supply dư.
- Conversion giảm.
- Emerging category.
- Underserved area.

## G5 — Explain

Mỗi Market Signal phải có khả năng truy ngược về dữ liệu nguồn.

## G6 — Protect

Không làm lộ:

- PII.
- Individual transaction data.
- Confidential business data.
- Trade secrets.

---

# 6. Stakeholders

| Actor | Responsibility |
|---|---|
| Customer | Tạo Demand Signals |
| Shop/SME | Tạo Supply & Transaction Signals |
| ShopERP | Supply/operation data |
| KhachLink | Demand/engagement/transaction data |
| Accounting | Revenue/financial signals |
| Vạn