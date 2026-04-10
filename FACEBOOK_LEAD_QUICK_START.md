# Facebook Lead Integration - Hng dn Nhanh
## Bt u ngay trong 5 phút

---

## **1. Cài dng Facebook Lead Ads**

### **Bc 1: Tào Facebook Lead Ads**

1. **Vào Facebook Ads Manager**
   - Truy cp: [Facebook Ads Manager](https://www.facebook.com/adsmanager)
   - Chn "Create" > "Campaign"

2. **Chn mu ch campaign**
   - Chn "Lead Generation"
   - t tên campaign: "Lead Generation - VAn Group"

3. **Cài dng ad set**
   - Chn target audience phù hp
   - t ngân sách và lch ch
   - Chn "Continue"

4. **Tào Lead Form**
   - Chn "Create Form"
   - Thêm các trng thông tin:
     - Full Name (Bt buc)
     - Phone Number (Bt buc)
     - Email (Bt cuc)
     - Company (Tùy chn)
     - Job Title (Tùy chn)
   - Luu form

### **Bc 2: Cài dng Webhook**

1. **Nhân webhook URL**
   - Webhook URL: `https://your-domain.com/api/facebookwebhook/lead`
   - Verify Token: `vanan_facebook_lead_verify_token`

2. **Cài dng trong Facebook**
   - Vào campaign settings
   - Chn "Webhooks"
   - Nhân webhook URL
   - Thêm verify token
   - Luu cài dng

---

## **2. Ki tra Webhook**

### **Bc 1: Test Webhook**

```bash
# Test webhook endpoint
curl -X POST https://your-domain.com/api/facebookwebhook/lead \
  -H "Content-Type: application/json" \
  -H "x-hub-signature: sha1=test_signature" \
  -d '{"test": "payload"}'
```

### **Bc 2: Ki tra Database**

```sql
-- Ki tra lead mi nhn
SELECT * FROM Leads 
WHERE CreatedAt >= NOW() - INTERVAL '1 hour'
ORDER BY CreatedAt DESC;
```

---

## **3. X lý Lead mi**

### **Bc 1: Xem danh sách Lead**

1. **Vào Dashboard**
   - Truy cp: `https://your-domain.com/leads`
   - Xem danh sách lead mi

2. **Ki tra thông tin**
   - H và tên
   - S in thoi
   - Email
   - Diem lead (0-100)
   - Trng thái

### **Bc 2: Giao Lead cho Sales**

1. **Chn lead**
   - Click vào lead
   - Chn "Assign Staff"

2. **Chn nhân viên**
   - Chn sales phù hp
   - Thêm ghi chú
   - Luu

---

## **4. Chuyn i Lead thành Khách hàng**

### **Bc 1: Ki tra iu kin**

Lead ph i có:
- **Trng thái**: Qualified
- **Diem**: >= 70
- **Thông tin**: Có SDT và Email

### **Bc 2: Chuyn i**

1. **Chn lead**
   - Click vào lead
   - Chn "Convert to Customer"

2. **Xác minh thông tin**
   - Ki tra trùng SDT
   - Xác minh email
   - Thêm lý do chuyn i

3. **Hoàn thành**
   - Click "Convert"
   - H thng s tào customer mi

---

## **5. Onboarding Khách hàng**

### **Bc 1: Ki tra trng thái**

1. **Vào Customer Dashboard**
   - Truy cp: `https://your-domain.com/customers`
   - Xem khách hàng mi

2. **Ki tra onboarding**
   - Trng thái: NotStarted/InProgress/Completed
   - Bc hin ti: Welcome/AppInstall/ProfileSetup/LoyaltyActivation

### **Bc 2: H tr khách hàng**

1. **Gii thông báo**
   - Email chào mng (T ng)
   - Thông báo app (T ng)

2. **Hng dn thêm**
   - Hng dn cài dng app
   - Hng dn s dng tính nng

---

## **6. Báo cáo và Thng kê**

### **Bc 1: Xem báo cáo**

1. **Marketing Report**
   - S luong lead: 150/tháng
   - T l chuyn i: 25%
   - Chi phí/lead: 50.000 VN

2. **Sales Report**
   - T l liên h: 80%
   - T l chuyn i: 30%
   - Th i gian x lý: 2 ngày

### **Bc 2: Xuát báo cáo**

1. **Chn khung thi gian**
   - Ngày/Tu n/Tháng/Nm
   - Chn "Generate Report"

2. **Xuát file**
   - PDF/Excel/CSV
   - Gii email

---

## **7. Câu hi thng gp**

### **Q: Webhook không hoat ng?**
**A:** Ki tra:
- Webhook URL có chính xác không
- SSL certificate có hp l không
- Facebook app settings có úng không

### **Q: Lead không có diem?**
**A:** Ki tra:
- Thông tin lead có hoàn chnh không
- Email có hp l không
- Công ty có thông tin không

### **Q: Không chuyn i duce customer?**
**A:** Ki tra:
- Lead có trng thái Qualified không
- Lead có diem >= 70 không
- SDT/Email có trùng không

---

## **8. H tr k thut**

### **Liên h:**
- **Email**: support@vanan.com
- **Phone**: 1900-1234
- **Chat**: Trong h thng
- **Thi gian**: 8:00 - 18:00, Th 2 - Th 6

### **Tài nguyên:**
- **Video hng dn**: [Link video]
- **FAQ**: [Link FAQ]
- **Blog**: [Link blog]
- **Community**: [Link community]

---

## **9. Checklists**

### **Marketing Checklist:**
- [ ] Tào Facebook Lead Ads
- [ ] Cài dng webhook
- [ ] Test webhook endpoint
- [ ] Ki tra lead nhn
- [ ] Theo di báo cáo

### **Sales Checklist:**
- [ ] Xem danh sách lead
- [ ] Giao lead cho staff
- [ ] Liên h customer
- [ ] Cp nhp trng thái
- [ ] Chuyn i thành customer

### **Onboarding Checklist:**
- [ ] Ki tra trng thái
- [ ] Gii thông báo
- [ ] H tr cài dng app
- [ ] Hng dn s dng
- [ ] Hoàn thành onboarding

---

## **10. Muc tiêu**

### **Tu n 1:**
- [ ] 50+ lead nhn
- [ ] 20% chuyn i
- [ ] 80% lead liên h

### **Tháng 1:**
- [ ] 200+ lead nhn
- [ ] 25% chuyn i
- [ ] 90% lead liên h
- [ ] 50+ khách hàng mi

### **Quý 1:**
- [ ] 600+ lead nhn
- [ ] 30% chuyn i
- [ ] 95% lead liên h
- [ ] 150+ khách hàng mi

---

## **11. Tips & Tricks**

### **Marketing Tips:**
- S dng hình p thu hút
- Viêt content rõ ràng
- Target audience chính xác
- Test A/B caption

### **Sales Tips:**
- Liên h nhanh trong 24h
- Chu n b script
- Ghi chú chi ti t
- Theo di thng quy

### **Onboarding Tips:**
- Gii thông báo nháp
- Hng dn chi ti t
- T o tri nghi m t t
- Thu thp feedback

---

## **12. Kt lu n**

Facebook Lead Integration giúp:

- **T ng hóa quy trình**: Gi m công vi
- **Tng hiu qu**: Tng t l chuyn i
- **Theo di d dàng**: Báo cáo chi ti t
- **H tr khách hàng**: Onboarding chuyên nghi p

Bn s sn sàng bt u sau 5 phút cài dng!

---

**Bn cp nhp:** 09/04/2026  
**Phiên b n:** 1.0  
**Tác gi:** VAn Group Team
