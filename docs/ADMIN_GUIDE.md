# 🏢 Van An Ecosystem - Admin Guide

> **Hướng dẫn quản trị hệ thống cho Shop Admin và Quản lý cấp cao**
> 
> Version: 2.1 | Last Updated: 01/04/2026

---

## 📑 MỤC LỤC

1. [Dashboard & Analytics](#chương-1-dashboard--analytics)
2. [Quản lý Nhân sự](#chương-2-quản-lý-nhân-sự)
3. [Tài chính & Báo cáo](#chương-3-tài-chính--báo-cáo)
4. [Inventory Management](#chương-4-inventory-management)
5. [Marketing & Campaigns](#chương-5-marketing--campaigns)
6. [System Configuration](#chương-6-system-configuration)
7. [Security & Compliance](#chương-7-security--compliance)

---

## 📊 CHƯƠNG 1: DASHBOARD & ANALYTICS

### 1.1 Tổng quan Dashboard

**Truy cập ShopERP Admin:**
- **URL**: `http://localhost:5003`
- **Đăng nhập**: 
  - **Username**: `admin@vanan.com`
  - **Password**: `VanAn@2024!`
- **Dashboard**: Menu chính → Dashboard

**Layout Dashboard:**
```
┌─────────────────────────────────────────────────────────────┐
│                    📊 ADMIN DASHBOARD                      │
├─────────────────────────────────────────────────────────────┤
│  💰 Doanh thu hôm nay    📈 Xu hướng    👥 Khách hàng      │
│     5,250,000 VNĐ         ↗ +15%        128 người         │
├─────────────────────────────────────────────────────────────┤
│  📦 Tồn kho           🎯 Target     ⏰ Giờ cao điểm       │
│     85% sẵn sàng        92%          14:00-16:00         │
├─────────────────────────────────────────────────────────────┤
│  📈 Biểu đồ doanh thu  🗓️ Lịch      🔔 Thông báo         │
│     (7 ngày)           (Hôm nay)    (3 mới)             │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 KPIs chính

**📈 Business Metrics:**
- **Doanh thu ngày**: Tổng thu nhập trong ngày
- **Số đơn hàng**: Số lượng đơn đã xử lý
- **Giá trị trung bình**: Giá trị mỗi đơn hàng
- **Tỷ lệ chuyển đổi**: Khách hàng → Đơn hàng

**👥 Customer Metrics:**
- **Khách hàng mới**: Số khách mới hôm nay
- **Khách hàng quay lại**: Tỷ lệ khách hàng cũ
- **Điểm trung bình**: Rating dịch vụ
- **Thời gian chờ**: Thời gian xử lý đơn

**📦 Operations Metrics:**
- **Tồn kho**: Tỷ lệ sản phẩm sẵn sàng
- **Hiệu suất**: Tốc độ xử lý đơn
- **Lỗi**: Tỷ lệ đơn hàng bị lỗi
- **Chi phí**: Chi phí vận hành

### 1.3 Báo cáo chi tiết

**📅 Daily Report:**
- Doanh thu theo giờ
- Top sản phẩm bán chạy
- Phân tích khách hàng
- Hiệu suất nhân viên

**📊 Weekly Analysis:**
- Xu hướng 7 ngày
- So sánh tuần trước
- Phân tích chiến dịch
- Dự báo doanh thu

**📈 Monthly Review:**
- Báo cáo tài chính
- Phân tích thị trường
- Đánh giá nhân sự
- Kế hoạch tháng sau

---

## 👥 CHƯƠNG 2: QUẢN LÝ NHÂN SỰ

### 2.1 Quản lý nhân viên

**Thêm nhân viên mới:**
1. **Menu**: Nhân sự → Thêm nhân viên
2. **Thông tin cơ bản**:
   - Họ tên, ngày sinh, giới tính
   - Số điện thoại, email, địa chỉ
   - CCCD/Passport số
3. **Thông tin công việc**:
   - Vị trí (Barista, Cashier, Manager)
   - Lương cơ bản, thưởng
   - Ngày bắt đầu làm việc
4. **Phân quyền**:
   - Quyền truy cập hệ thống
   - Level quản lý
   - Phạm vi thao tác

**Phân cấp quyền hạn:**
```
👑 Super Admin
├── 🏪 Shop Manager
│   ├── 💰 Cashier
│   └── 🍵 Barista
└── 🔧 Technical Support
```

### 2.2 Lịch làm việc

**Tạo lịch trình:**
1. **Menu**: Nhân sự → Lịch làm việc
2. **Chọn tuần**: Chọn tuần cần lập lịch
3. **Drag & Drop**: Kéo nhân viên vào ca làm việc
4. **Xác nhận**: Lưu lịch trình

**Ca làm việc:**
- **Sáng**: 7:00 - 15:00
- **Chiều**: 15:00 - 23:00
- **Full-time**: 7:00 - 23:00
- **Part-time**: Linh hoạt

**Quản lý overtime:**
- **Đăng ký**: Nhân viên đăng ký overtime
- **Phê duyệt**: Manager duyệt overtime
- **Tính lương**: Tự động tính lương overtime

### 2.3 Đánh giá hiệu suất

**KPIs nhân viên:**
- **Số đơn xử lý**: Đơn/giờ
- **Độ chính xác**: Tỷ lệ đơn đúng
- **Thời gian xử lý**: Phút/đơn
- **Customer satisfaction**: Rating khách hàng

**Đánh giá định kỳ:**
- **Hàng tuần**: Review performance
- **Hàng tháng**: Formal review
- **Hàng quý**: Performance appraisal
- **Hàng năm**: Annual evaluation

**Khen thưởng & kỷ luật:**
- **Bonus**: Hiệu suất vượt trội
- **Promotion**: Thăng chức
- **Training**: Đào tạo thêm
- **Warning**: Cảnh báo hiệu suất kém

---

## 💰 CHƯƠNG 3: TÀI CHÍNH & BÁO CÁO

### 3.1 Quản lý doanh thu

**Doanh thu theo kênh:**
- **🏪 Tại quán**: Bán tại cửa hàng
- **📱 Online**: Đặt hàng online
- **🚗 Delivery**: Giao hàng tận nơi
- **📦 Bán sỉ**: Bán cho đối tác

**Phân tích doanh thu:**
```
📊 Doanh thu hôm nay: 5,250,000 VNĐ
├── 🏪 Tại quán: 3,200,000 VNĐ (61%)
├── 📱 Online: 1,850,000 VNĐ (35%)
├── 🚗 Delivery: 150,000 VNĐ (3%)
└── 📦 Bán sỉ: 50,000 VNĐ (1%)
```

### 3.2 Quản lý chi phí

**Các loại chi phí:**
- **🥤 Nguyên vật liệu**: Trà, sữa, topping
- **💰 Lương nhân viên**: Lương, thưởng, bảo hiểm
- **🏢 Chi phí mặt bằng**: Thuê, điện, nước
- **📢 Marketing**: Quảng cáo, khuyến mãi
- **🔧 Vận hành**: Maintenance, phần mềm

**Báo cáo chi phí:**
- **Ngày**: Chi phí hàng ngày
- **Tuần**: Tổng chi phí tuần
- **Tháng**: Báo cáo chi phí tháng
- **Năm**: Phân tích chi phí năm

### 3.3 Báo cáo tài chính

**📊 P&L Statement:**
- **Doanh thu**: Tổng thu nhập
- **Giá vốn hàng bán**: Chi phí sản phẩm
- **Lợi nhuận gộp**: Doanh thu - COGS
- **Chi phí hoạt động**: Chi phí vận hành
- **Lợi nhuận ròng**: Lợi nhuận cuối cùng

**📈 Cash Flow:**
- **Dòng tiền vào**: Doanh thu, vốn
- **Dòng tiền ra**: Chi phí, đầu tư
- **Dòng tiền ròng**: Chênh lệch
- **Dự báo**: Cash flow tương lai

**📋 Balance Sheet:**
- **Tài sản**: Cash, inventory, equipment
- **Nợ phải trả**: Supplier, payroll
- **Vốn chủ sở hữu**: Equity, retained earnings

---

## 📦 CHƯƠNG 4: INVENTORY MANAGEMENT

### 4.1 Quản lý nguyên vật liệu

**Danh mục nguyên vật liệu:**
- **🍵 Trà**: Trà đen, trà xanh, trà ô long
- **🥤 Sữa**: Sữa tươi, sữa đặc, sữa hạt
- **🧋 Topping**: Trân châu, thạch, pudding
- **🍯 Đường**: Đường trắng, đường nâu, mật ong
- **🧊 Đá**: Đá viên, đá bào

**Theo dõi tồn kho:**
- **Real-time**: Cập nhật tức thì
- **Low stock alert**: Cảnh báo tồn kho thấp
- **Expiry tracking**: Theo dõi hạn sử dụng
- **Cost tracking**: Theo dõi giá vốn

### 4.2 Đặt hàng & Supplier

**Quản lý supplier:**
- **Thông tin**: Tên, địa chỉ, liên hệ
- **Sản phẩm**: Danh mục cung cấp
- **Giá cả**: Bảng giá hiện tại
- **Đánh giá**: Quality score, delivery time

**Quy trình đặt hàng:**
1. **Kiểm tra tồn kho**: Xem mức tồn kho hiện tại
2. **Tính nhu cầu**: Dựa trên sales forecast
3. **Tạo PO**: Purchase order
4. **Gửi supplier**: Email/Phone
5. **Nhận hàng**: Check quality, quantity
6. **Cập nhật kho**: Nhập vào hệ thống

### 4.3 Forecasting & Planning

**Sales Forecast:**
- **Historical data**: Dữ liệu lịch sử
- **Seasonality**: Xu hướng theo mùa
- **Events**: Ngày lễ, promotion
- **Weather**: Thời tiết ảnh hưởng

**Inventory Planning:**
- **Safety stock**: Tồn kho an toàn
- **Reorder point**: Điểm đặt hàng lại
- **Lead time**: Thời gian giao hàng
- **EOQ**: Economic order quantity

---

## 🎯 CHƯƠNG 5: MARKETING & CAMPAIGNS

### 5.1 Customer Segmentation

**Phân loại khách hàng:**
- **🌟 VIP**: > 1 triệu/tháng
- **💎 Gold**: 500k - 1 triệu/tháng
- **🥈 Silver**: 200k - 500k/tháng
- **🥉 Bronze**: < 200k/tháng
- **👤 New**: Khách hàng mới

**Behavior analysis:**
- **Frequency**: Tần suất mua hàng
- **Recency**: Lần mua gần nhất
- **Monetary**: Giá trị đơn hàng
- **Product preference**: Sản phẩm yêu thích

### 5.2 Campaign Management

**Loại chiến dịch:**
- **🎁 Promotion**: Giảm giá, gift
- **📱 Social Media**: Facebook, Instagram, TikTok
- **📧 Email Marketing**: Newsletter, offers
- **🎪 Events**: Tổ chức sự kiện
- **🤝 Partnership**: Hợp tác đối tác

**Campaign Workflow:**
1. **Planning**: Set objectives, KPIs
2. **Creative**: Design content, visuals
3. **Execution**: Launch campaign
4. **Monitoring**: Track performance
5. **Optimization**: Adjust based on data
6. **Reporting**: Final analysis

### 5.3 Loyalty Program

**Points System:**
- **Earn**: 1 point = 1,000 VNĐ
- **Redeem**: Points for discounts
- **Bonus**: Birthday bonus, referral bonus
- **Tiers**: Different benefits per tier

**Membership Benefits:**
- **🌟 VIP**: 20% discount, priority service
- **💎 Gold**: 15% discount, exclusive offers
- **🥈 Silver**: 10% discount, birthday gift
- **🥉 Bronze**: 5% discount, welcome gift

---

## ⚙️ CHƯƠNG 6: SYSTEM CONFIGURATION

### 6.1 Shop Settings

**Basic Information:**
- **Shop name**: Tên cửa hàng
- **Address**: Địa chỉ kinh doanh
- **Phone**: Số điện thoại
- **Email**: Email liên hệ
- **Website**: Website (nếu có)

**Operating Hours:**
- **Opening time**: Giờ mở cửa
- **Closing time**: Giờ đóng cửa
- **Special hours**: Ngày lễ, sự kiện
- **Delivery hours**: Giờ giao hàng

**Payment Methods:**
- **💳 VietQR**: Mã QR thanh toán
- **💰 Cash**: Tiền mặt
- **📱 Mobile Banking**: Chuyển khoản
- **🏦 Card**: Thẻ tín dụng/ghi nợ

### 6.2 Product Management

**Product Categories:**
- **🍵 Trà sữa**: Các loại trà sữa
- **☕ Cà phê**: Các loại cà phê
- **🧋 Đặc biệt**: Sản phẩm đặc biệt
- **🍰 Topping**: Các loại topping

**Product Information:**
- **Name**: Tên sản phẩm
- **Description**: Mô tả chi tiết
- **Price**: Giá bán
- **Cost**: Giá vốn
- **Image**: Hình ảnh sản phẩm
- **Ingredients**: Thành phần
- **Allergens**: Dị ứng

**Pricing Strategy:**
- **Base price**: Giá cơ bản
- **Size variations**: Size S/M/L
- **Topping price**: Giá topping
- **Combo deals**: Combo khuyến mãi

### 6.3 Integration Settings

**VietQR Configuration:**
- **Bank info**: Thông tin ngân hàng
- **QR template**: Mẫu QR code
- **Auto-confirm**: Tự động xác nhận
- **Notification**: Thông báo thanh toán

**Social Media Integration:**
- **Facebook**: Page ID, access token
- **Instagram**: Business account
- **Zalo OA**: OA information
- **TikTok**: Business account

**API Integration:**
- **Delivery partners**: Grab, ShopeeFood
- **Payment gateways**: Momo, ZaloPay
- **Analytics**: Google Analytics
- **SMS**: SMS gateway

---

## 🔒 CHƯƠNG 7: SECURITY & COMPLIANCE

### 7.1 User Management

**Role-based Access Control:**
```
👑 Super Admin
├── 🏪 Shop Manager
│   ├── 💰 Cashier (Limited access)
│   └── 🍵 Barista (Order management)
├── 🔧 Technical Support (System access)
└── 📊 Accountant (Financial access)
```

**Permission Matrix:**
- **Dashboard**: View all metrics
- **Orders**: Create, edit, cancel orders
- **Inventory**: Manage stock levels
- **Customers**: View customer data
- **Reports**: Generate reports
- **Settings**: Configure system

### 7.2 Data Security

**Data Protection:**
- **Encryption**: Data encrypted at rest
- **Backup**: Daily automated backups
- **Access log**: Track all access
- **Audit trail**: Complete audit log

**Privacy Compliance:**
- **GDPR**: Customer data protection
- **Local laws**: Vietnamese data laws
- **Consent**: Customer consent management
- **Data retention**: Data retention policy

### 7.3 Compliance & Audit

**Financial Compliance:**
- **Tax compliance**: VAT, corporate tax
- **Accounting standards**: Vietnamese GAAP
- **Audit trail**: Complete transaction log
- **Reporting**: Regulatory reporting

**Operational Compliance:**
- **Food safety**: HACCP standards
- **Health regulations**: Local health codes
- **Labor laws**: Employment regulations
- **Environmental**: Waste management

---

## 📱 MOBILE ADMIN (COMING SOON)

### 7.4 Mobile App Features

**Real-time Monitoring:**
- **Live sales**: Real-time sales data
- **Staff tracking**: Employee location/schedule
- **Inventory alerts**: Low stock notifications
- **Customer feedback**: Real-time reviews

**Remote Management:**
- **Approve orders**: Remote order approval
- **Staff communication**: Team messaging
- **Emergency alerts**: Critical notifications
- **Performance tracking**: Live KPIs

---

## 📞 SUPPORT & TRAINING

### 7.5 Admin Support

**Training Resources:**
- **Video tutorials**: Step-by-step guides
- **Documentation**: Complete user manual
- **Webinars**: Live training sessions
- **On-site training**: In-person training

**Technical Support:**
- **24/7 hotline**: Emergency support
- **Email support**: Non-urgent issues
- **Remote assistance**: Screen sharing
- **On-site support**: Critical issues

### 7.6 Best Practices

**Daily Operations:**
- **Morning check**: Review daily targets
- **Staff briefing**: Daily team meeting
- **Quality control**: Product quality checks
- **Customer service**: Service standards

**Weekly Reviews:**
- **Performance analysis**: KPI review
- **Staff evaluation**: Performance review
- **Inventory audit**: Stock verification
- **Financial review**: P&L analysis

**Monthly Planning:**
- **Business planning**: Strategy review
- **Budget planning**: Financial planning
- **Marketing planning**: Campaign planning
- **Staff development**: Training planning

---

## 🎯 SUCCESS METRICS

### 7.7 KPIs for Admin

**Business KPIs:**
- **Revenue growth**: YoY growth > 20%
- **Profit margin**: Gross margin > 60%
- **Customer retention**: Repeat rate > 70%
- **Market share**: Local market position

**Operational KPIs:**
- **Order accuracy**: > 99%
- **Delivery time**: < 30 minutes
- **Staff productivity**: Orders/hour
- **Inventory turnover**: < 7 days

**Customer KPIs:**
- **Satisfaction score**: > 4.5/5
- **Net Promoter Score**: > 70
- **Complaint resolution**: < 2 hours
- **Response time**: < 5 minutes

---

## 📞 EMERGENCY CONTACTS

### 7.8 Critical Contacts

**🚨 Emergency Support:**
- **System Down**: 1900-XXXX (24/7)
- **Payment Issues**: 1900-YYYY (9-5)
- **Security Breach**: 1900-ZZZZ (24/7)

**📧 Support Channels:**
- **Technical Support**: tech@vanan.com
- **Business Support**: business@vanan.com
- **Emergency**: emergency@vanan.com

**👥 Management Contacts:**
- **CEO**: ceo@vanan.com
- **CTO**: cto@vanan.com
- **CFO**: cfo@vanan.com
- **COO**: coo@vanan.com

---

**🎉 Chúc mừng bạn đã trở thành Admin của hệ sinh thái Vạn An!**

**📱 Admin Portal: http://localhost:5003 | Hotline: 1900-XXXX**

**🎯 Mục tiêu: Xây dựng hệ thống F&B thông minh hàng đầu Việt Nam!**