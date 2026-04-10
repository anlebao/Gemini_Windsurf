# YÊU CÂU CHUC NANG - VANAN ECOSYSTEM

## **THÔNG TIN CHUNG**

**Tên Hê Thông:** VanAn Ecosystem  
**Phiên Ban Hiên Tai:** MVP 1.0  
**Ngày Câp Nhât:** 10/04/2026  
**Trang Thái:** Hoàn thành MVP, Build thành công

---

## **1. QUAN LY DON HANG (ORDER MANAGEMENT)**

### **1.1 Tao Don Hang Mói**
- **Mô tà:** Khách hàng có thê tao don hàng mói qua các kênh (dine-in, takeaway, delivery)
- **Yêu câu:**
  - Chon món an tu menu
  - Nhâp thông tin khách hàng (tên, sô diên thoai)
  - Chon phuong thuc thanh toán
  - Tính toán tông tiên (thuê VAT 10%)
- **Trang thái:** Hoàn thành

### **1.2 Theo Dõi Trang Thái Don Hang**
- **Mô tà:** Theo dõi don hàng qua các trang thái (Draft, Confirmed, Preparing, Ready, Completed, Cancelled)
- **Yêu câu:**
  - Câp nhât trang thái don hang
  - Thông báo cho khách hàng
  - Lich su thay doi trang thái
- **Trang thái:** Hoàn thành

### **1.3 Quan Ly Kitchen Display System (KDS)**
- **Mô tà:** Hê thông hiên thi don hàng cho bep
- **Yêu câu:**
  - Hiên thi don hàng chua xu ly
  - Ghi chú don hàng (text + voice)
  - Câp nhât trang thái bep
- **Trang thái:** Hoàn thành

---

## **2. QUAN LY KHACH HANG (CUSTOMER MANAGEMENT)**

### **2.1 Thông Tin Khách Hang**
- **Mô tà:** Quan ly thông tin chi tiêt khách hàng
- **Yêu câu:**
  - Thông tin cá nhân (tên, sô diên thoai, email)
  - Lich su don hàng
  - Diem tich luy (loyalty points)
  - Phân loai khách hàng (Bronze, Silver, Gold)
- **Trang thái:** Hoàn thành

### **2.2 Onboarding Khách Hang**
- **Mô tà:** Quy trình chào don khách hàng mói
- **Yêu câu:**
  - Welcome email/SMS
  - Hô trô cài dat app
  - Kích hoat chuong trình loyalty
- **Trang thái:** Hoàn thành

---

## **3. QUAN LY INVENTORY (STOCK MANAGEMENT)**

### **3.1 Quan Ly Nguyên Liêu**
- **Mô tà:** Theo dõi tôn kho nguyên liêu
- **Yêu câu:**
  - Thông tin nguyên liêu (tên, don vi, sô luong)
  - Câp nhât tôn kho
  - Cánh báo tôn kho thap
- **Trang thái:** Hoàn thành

### **3.2 Quan Ly Công Thuc**
- **Mô tà:** Quan ly công thuc món an
- **Yêu câu:**
  - Danh sách nguyên liêu cho món
  - Sô luong nguyên liêu câm
  - Tính toán chi phí
- **Trang thái:** Hoàn thành

---

## **4. FACEBOOK LEADS INTEGRATION**

### **4.1 Tích Hop Facebook Leads**
- **Mô tà:** Nhân tin tiêm nang tu Facebook Lead Ads
- **Yêu câu:**
  - Kêt nôi Facebook Lead Ads
  - Tao khách hàng tiêm nang
  - Ghi nguôn tiêm nang
- **Trang thái:** Hoàn thành

### **4.2 Xu Ly Lead**
- **Mô tà:** Quy trình xu ly khách hàng tiêm nang
- **Yêu câu:**
  - Phân loai lead (Facebook, Website, Referral)
  - Ghi chú lead
  - Chuyên doi lead thành khách hàng
- **Trang thái:** Hoàn thành

---

## **5. MULTI-TENANCY**

### **5.1 Quan Ly Nhiêu Cua Hang**
- **Mô tà:** Hê thông hô trô nhiêu cua hang
- **Yêu câu:**
  - Phân cách dô liêu theo cua hang
  - Quan ly thông tin cua hàng
  - Phân quyên truy câp
- **Trang thái:** Hoàn thành

### **5.2 Tenant Isolation**
- **Mô tà:** Dô liêu cua hàng không xen lâan
- **Yêu câu:**
  - Tenant ID trong mõi entity
  - Filter theo tenant
  - Bao mat dô liêu
- **Trang thái:** Hoàn thành

---

## **6. OMNICHANNEL INTEGRATION**

### **6.1 Tích Hop Kênh Bán Hàng**
- **Mô tà:** Kêt nôi nhiêu kênh bán hàng
- **Yêu câu:**
  - Website
  - Mobile App
  - POS System
  - Delivery Apps
- **Trang thái:** Hoàn thành

### **6.2 Dong Bô Dô Liêu**
- **Mô tà:** Dong bô dô liêu giua các kênh
- **Yêu câu:**
  - Real-time sync
  - Conflict resolution
  - Data versioning
- **Trang thái:** Hoàn thành

---

## **7. REPORTING & ANALYTICS**

### **7.1 Dashboard**
- **Mô tà:** Giao diên phân tích dô liêu
- **Yêu câu:**
  - Doanh thu theo ngày/tháng
  - Top bán chay
  - Hiêu suât bep
  - Khách hàng mói
- **Trang thái:** Hoàn thành

### **7.2 Báo Cáo**
- **Mô tà:** Xuât báo cáo chi tiêt
- **Yêu câu:**
  - Báo cáo doanh thu
  - Báo cáo tôn kho
  - Báo cáo khách hàng
- **Trang thái:** Hoàn thành

---

## **8. TECHNICAL REQUIREMENTS**

### **8.1 Architecture**
- **Clean Architecture:** Domain, Application, Infrastructure layers
- **DDD:** Domain-Driven Design principles
- **EF Core:** Entity Framework Core 8.0
- **Multi-tenancy:** Tenant isolation
- **Value Objects:** LeadId, CustomerId, etc.

### **8.2 Security**
- **Authentication:** JWT tokens
- **Authorization:** Role-based access
- **Data Protection:** Encryption at rest
- **Tenant Isolation:** Data separation

### **8.3 Performance**
- **Response Time:** < 2s cho operations
- **Concurrent Users:** 100+ users
- **Database:** Optimized queries
- **Caching:** Redis cache

---

## **9. NON-FUNCTIONAL REQUIREMENTS**

### **9.1 Availability**
- **Uptime:** 99.5%
- **Backup:** Daily backups
- **Disaster Recovery:** 4-hour RTO

### **9.2 Scalability**
- **Horizontal Scaling:** Support multiple instances
- **Load Balancing:** Distribute traffic
- **Database Scaling:** Read replicas

### **9.3 Usability**
- **Mobile Responsive:** Works on mobile devices
- **Accessibility:** WCAG 2.1 AA
- **Localization:** Vietnamese language support

---

## **10. INTEGRATION REQUIREMENTS**

### **10.1 External APIs**
- **Facebook Graph API:** Lead ads integration
- **Payment Gateway:** Stripe/VNPay
- **SMS Gateway:** Twilio/Viettel
- **Email Service:** SendGrid

### **10.2 Internal APIs**
- **Gateway API:** Central API gateway
- **Core Hub API:** Business logic
- **Mobile API:** Mobile app backend

---

## **TRANG THAI HIEN TAI**

### **Completed Features:**
- [x] Order Management
- [x] Customer Management  
- [x] Inventory Management
- [x] Facebook Leads Integration
- [x] Multi-tenancy
- [x] Omnichannel Integration
- [x] Reporting & Analytics
- [x] Technical Architecture

### **Build Status:**
- [x] Build passes (0 errors)
- [x] Domain purity maintained
- [x] No duplicate Value Objects
- [x] All converters 2-way

### **Next Phase:**
- [ ] Performance optimization
- [ ] Advanced reporting
- [ ] Mobile app development
- [ ] Cloud deployment

---

## **PHÊ DUYÊT**

**Ngày:** 10/04/2026  
**Ngày Phê Duyêt:** [Tên]  
**Chuc Vu:** [Chuc Vu]  
**Email:** [Email]

---

*Luu y: Tài liêu này se câp nhât khi có thay doi trong yêu câu hoac khi thêm tính nang mói.*
