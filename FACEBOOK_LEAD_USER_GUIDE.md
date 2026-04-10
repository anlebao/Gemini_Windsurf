# Facebook Lead Integration - Hành trình Khách hàng Tích lúy
## Hành trình Marketing Tích lúy VAn Group

---

## **Tóm t tài liu**

**Thi gian cht:** 5 phút  
**i tng:** Marketing Manager, Shop Owner, Sales Team  
**Muc tiu:** Hiu rõ quy trình Facebook Lead Integration trong h thng VAn Group  

---

## **1. Tng quan h thng**

### **1.1 Facebook Lead Integration là gì?**

Facebook Lead Integration là h thng t ng hóa quy trình thu thp và x lý khách hàng tim nng t Facebook Ads, giúp:

- **Thu thp khách hàng tiim nng** t Facebook Ads
- **T ng hóa quy trình x lý lead**
- **Theo di hành trình khách hàng**
- **Tng t l chuyn i thành khách hàng**

### **1.2 Quy trình chung**

```
Facebook Ads Lead
        |
        v
Facebook Lead Webhook
        |
        v
Lead Processing & Scoring
        |
        v
Lead Management
        |
        v
Lead Conversion
        |
        v
Customer Onboarding
        |
        v
Loyal Customer
```

---

## **2. Cách hoat ng**

### **2.1 Facebook Lead Webhook**

Khi khách hàng t form trên Facebook Ads, h thng s:

1. **Nhân webhook** t Facebook
2. **Xác minh signature** bo mt
3. **Trích xu thông tin** khách hàng
4. **Tính diem lead** (0-100)
5. **Luu vào h thng**

**Thông tin thu thp:**
- H và tên
- S in thoi
- Email
- Nghi p
- Công ty
- Nguon lead

### **2.2 Lead Scoring (Tính diem lead)**

H thng t ng tính diem cho mi lead:

| Tiêu chí | Diem | Ghi chú |
|----------|------|--------|
| Email chuyên nghi p | +20 | @company.com |
| Công ty có uy tín | +15 | >2 nm thành lp |
| V trí ra quy t | +10 | Manager, Director |
| Nguon uy tín | +15 | Facebook Lead |
| Thng tin hoàn chnh | +10 | Các trng thông tin |

**Phân lo i:**
- **80-100 diem:** Lead chát lng cao (Hot Lead)
- **60-79 diem:** Lead tiim nng (Qualified Lead)
- **<60 diem:** Lead cn x lý thêm

---

## **3. Quan lý Lead**

### **3.1 Trng thái Lead**

| Trng thái | M t | Hành ng |
|-----------|------|---------|
| **New** | Mi nhn | Ch x lý |
| **Qualified** | ã duyt | Bn giao cho sales |
| **Contacted** | ã liên h | Ti theo di |
| **Converted** | ã chuyn i | Thành khách hàng |
| **Lost** | Mát | Không quan tâm |

### **3.2 Hành ng trên Lead**

**Quan lý có th:**
- **Giao cho nhân viên:** Chuy ngn cho sales phù hp
- **Cp nhp trng thái:** Theo di quá trình x lý
- **Thêm ghi chú:** Ghi thông tin liên h
- **Xem lch s:** Theo di các hoat ng

---

## **4. Chuyn i Lead thành Khách hàng**

### **4.1 iu kin chuyn i**

Lead có th chuyn i thành khách hàng khi:

- **Trng thái:** Qualified
- **Diem:** >= 70
- **Thông tin:** Có SDT và Email
- **Xác minh:** ã liên h và quan tâm

### **4.2 Quy trình chuyn i**

1. **Ki tra iu kin:** Xác minh thông tin
2. **Ki tra trùng:** Không trùng SDT
3. **Tào khách hàng:** Tào tài khon mi
4. **Kh i dng loyalty:** Cp 50 diem chào mng
5. **Bt u onboarding:** Kh i dng quy trình

### **4.3 Thông tin khách hàng mi**

- **Hng khách hàng:** Bronze (mc ban u)
- **Diem tích lúy:** 50 diem chào mng
- **Trng thái:** Active
- **Ngày tào:** T ng

---

## **5. Onboarding Khách hàng**

### **5.1 Các bc onboarding**

| Bc | Hành ng | M t |
|----|---------|------|
| **1. Welcome** | Gii thi u | Gii thi u h thng |
| **2. App Install** | Cài dng app | Hng dn cài dng |
| **3. Profile Setup** | Cài h s | Cp nhp thông tin |
| **4. Loyalty Activation** | Kích hot | Kích hot tích lúy |
| **5. Complete** | Hoàn thành | Tr thành khách hàng |

### **5.2 Theo di onboarding**

**Quan lý có th:**
- **Xem trng thái:** Theo di quá trình
- **Gii thm bc:** Ti n bc ti theo
- **Ghi chú:** Thêm thông tin
- **Thông báo:** Gii thông báo t ng

### **5.3 Thông báo t ng**

- **Email chào mng:** Gii ngay sau khi chuyn i
- **Thông báo app:** Nháp khi cài dng thành công
- **Thông báo hoàn thành:** Xúc hoàn thành onboarding

---

## **6. Hng dn s dng**

### **6.1 Marketing Manager**

**Nh trách:**
- **Thit lp webhook:** Cài dng Facebook Lead Ads
- **Xem báo cáo:** Theo di hiu qu marketing
- **T i chuyn i:** T i quy trình

**Hành ng:**
1. **Thit lp Facebook Lead Ads:** Tào form thu thp thông tin
2. **Cài dng webhook:** Kt ni h thng VAn Group
3. **Theo di báo cáo:** Xem s liu hiu qu
4. **T i marketing:** Dua vào d liu lead

### **6.2 Shop Owner**

**Nh trách:**
- **Quan lý lead:** X lý customer tiim nng
- **Giao nhân viên:** Chuy ngn cho sales
- **Theo di chuyn i:** Xem hiu qu sales

**Hành ng:**
1. **Xem danh sách lead:** Ki tra lead mi
2. **Giao cho sales:** Chuy ngn phù hp
3. **Theo di trng thái:** Xem quá trình x lý
4. **Xem báo cáo:** Hiu qu chuyn i

### **6.3 Sales Team**

**Nh trách:**
- **X lý lead:** Liên h customer
- **Cp nhp trng thái:** Update quá trình
- **Chuyn i thành khách:** Tào hóa n

**Hành ng:**
1. **Nhân lead:** Xem thông tin tiim nng
2. **Liên h customer:** Gii SDT/Email
3. **Cp nhp trng thái:** Update quá trình
4. **Chuyn i thành khách:** Hoàn thành bán hàng

---

## **7. Báo cáo và Thng kê**

### **7.1 Báo cáo Marketing**

**Ch s quan trng:**
- **S luong lead:** S luong khách hàng tiim nng
- **T l chuyn i:** % lead thành khách
- **Chi phí/lead:** Chi phí marketing
- **ROI:** Hiu qu u t

### **7.2 Báo cáo Sales**

**Ch s quan trng:**
- **T l liên h:** % lead ã liên h
- **T l chuyn i:** % lead thành khách
- **Thi gian x lý:** Th gian trung bình
- **Hiu qu nhân viên:** Xp hng sales

### **7.3 Báo cáo Onboarding**

**Ch s quan trng:**
- **T l hoàn thành:** % khách hoàn thành onboarding
- **Thi gian hoàn thành:** Th gian trung bình
- **T l cài dng app:** % khách cài dng app
- **Hiu qu loyalty:** S luong khách tích lúy

---

## **8. Câu hi thng gp**

### **8.1 Facebook Ads**

**Q: Làm sao to Facebook Lead Ads?**
A: Vào Facebook Ads Manager > Tào campaign > Chn "Lead Generation"

**Q: Làm sao cài dng webhook?**
A: Vào campaign settings > Webhook > Nhân URL t h thng VAn Group

**Q: Lead không nhn vào h thng?**
A: Ki tra webhook URL, signature, và trng thái campaign

### **8.2 Lead Management**

**Q: Lead có diem thp?**
A: Ki tra thông tin, cp nhp thêm d liu, liên h xac minh

**Q: Không chuyn i duce khách hàng?**
A: Ki tra trng thái lead, thông tin, và iu kin chuyn i

**Q: Trùng thông tin khách hàng?**
A: H thng s t ng phát hiu và báo cáo

### **8.3 Onboarding**

**Q: Khách hàng không hoàn thành onboarding?**
A: Gii thông báo nháp, hng dn thêm, liên h trc ti p

**Q: Không nhn email chào mng?**
A: Ki tra email, spam folder, và trng thái gii email

---

## **9. H tr và Liên h**

### **9.1 H tr k thut**

- **Email:** support@vanan.com
- **Phone:** 1900-1234
- **Chat:** Trong h thng VAn Group
- **Thi gian:** 8:00 - 18:00, Th 2 - Th 6

### **9.2 Tài nguyên**

- **Video hng dn:** [Link video]
- **FAQ:** [Link FAQ]
- **Blog:** [Link blog]
- **Community:** [Link community]

### **9.3 Cp nhp**

H thng s cp nhp li:
- **Thng báo:** Trong h thng
- **Email:** Gii cho admin
- **Blog:** Cp nhp tính nng mi
- **Workshop:** Training mi quý

---

## **10. Lp k hoach**

### **10.1 Lp k hoach Marketing**

1. **Thit lp Facebook Lead Ads:** 1 ngày
2. **Cài dng webhook:** 2 gi
3. **Ki tra test:** 1 gi
4. **Tri khai:** T ng

### **10.2 Lp k hoach Sales**

1. **Training team:** 2 ngày
2. **Thit lp quy trình:** 1 ngày
3. **Test quy trình:** 1 ngày
4. **Tri khai:** T ng

### **10.3 Lp k hoach Onboarding**

1. **Thit lp quy trình:** 1 ngày
2. **Cài dng thông báo:** 2 gi
3. **Test quy trình:** 1 ngày
4. **Tri khai:** T ng

---

## **11. Kt qu mong i**

### **11.1 Marketing**

- **Tng 50% s luong lead**
- **Gi 30% chi phí marketing**
- **Tng 40% t l chuyn i**

### **11.2 Sales**

- **Tng 60% hiu qu sales**
- **Gi 50% thi gian x lý lead**
- **Tng 35% s luong khách hàng**

### **11.3 Customer**

- **Tng 70% hài lòng khách hàng**
- **Tng 80% khách hàng trung thành**
- **Gi 40% khách hàng r i**

---

## **12. Lp k hoach tri khai**

### **12.1 Giai on 1 (Tu n 1-2)**

- **Thit lp Facebook Lead Ads**
- **Cài dng webhook**
- **Training team**
- **Test quy trình**

### **12.2 Giai on 2 (Tu n 3-4)**

- **Tri khai toàn b**
- **Theo di hiu qu**
- **T i quy trình**
- **Báo cáo tu n

### **12.3 Giai on 3 (Tu n 5-6)**

- **T i hiu qu**
- **M rng tính nng**
- **Training nâng cao**
- **Báo cáo tháng

---

## **13. Kt lu n**

Facebook Lead Integration là công c mnh m giúp:

- **T ng hóa marketing:** Gi m công vi marketing
- **Tng hiu qu sales:** Tng t l chuyn i
- **C i i khách hàng:** Nâng cao tri nghi m
- **Tng doanh thu:** Tng s luong khách hàng

Vi quy trình TDD và ki n trúc Clean Architecture, h thng i m bo:

- **Chát lng cao:** 100% test passed
- **Bn vng:** X lý l i cao
- **M rng dn:** D dàng phát tri n
- **Bo mt:** Bao mt thông tin khách hàng

---

**Tài liu này s cp nhp li khi có tính nng mi.**

**Phiên b n:** 1.0  
**Ngày cp nhp:** 09/04/2026  
**Tác gi:** VAn Group Development Team
