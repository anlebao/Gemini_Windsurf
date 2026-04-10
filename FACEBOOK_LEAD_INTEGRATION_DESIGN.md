# Facebook Lead Integration Design Document

## **TÀI LIÊU THAM CHIÉU CHÍNH THÍC**
**Ngày**: 8 tháng 4, 2026  
**Phiên**: 1.0  
**Trang thái**: Design Complete - Ready for Implementation

---

## **1. ARCHITECTURE DESIGN**

### **1.1 Domain Model Design**

#### **Lead Entity**
```csharp
public class Lead : BaseEntity, IMustHaveTenant
{
    public LeadId LeadId { get; set; } = new LeadId(Guid.NewGuid());
    
    // Lead Source & Tracking
    public LeadSource Source { get; set; } = LeadSource.Manual;
    public string? SourceReference { get; set; } // Facebook Lead ID, etc.
    public string? UTMParameters { get; set; }
    
    // Lead Status & Scoring
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public int LeadScore { get; set; } = 0;
    public string? LeadNotes { get; set; }
    
    // Assignment & Management
    public Guid? AssignedStaffId { get; set; }
    public DateTime? FirstContactDate { get; set; }
    public DateTime? LastContactDate { get; set; }
    public int ContactAttempts { get; set; } = 0;
    
    // Conversion Tracking
    public Guid? ConvertedCustomerId { get; set; }
    public DateTime? ConversionDate { get; set; }
    public string? ConversionReason { get; set; }
    
    // Basic Customer Info
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
}
```

#### **Facebook Lead Entity**
```csharp
public class FacebookLead : Lead
{
    public string FacebookLeadId { get; set; } = string.Empty;
    public string FacebookAdId { get; set; } = string.Empty;
    public string FacebookPageId { get; set; } = string.Empty;
    public string FacebookCampaignId { get; set; } = string.Empty;
    public DateTime FacebookCreatedTime { get; set; }
    public string FacebookFormData { get; set; } = string.Empty;
    public bool IsFacebookProcessed { get; set; } = false;
    public DateTime? FacebookProcessedAt { get; set; }
}
```

#### **Customer Onboarding Entity**
```csharp
public class CustomerOnboarding : BaseEntity, IMustHaveTenant
{
    public Guid CustomerId { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Welcome;
    
    // App Installation Tracking
    public bool HasInstalledApp { get; set; } = false;
    public DateTime? AppInstalledAt { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceType { get; set; }
    
    // Communication Tracking
    public bool WelcomeEmailSent { get; set; } = false;
    public DateTime? WelcomeEmailSentAt { get; set; }
    public bool WelcomeSMSsent { get; set; } = false;
    public DateTime? WelcomeSMSsentAt { get; set; }
    
    // Loyalty Program Activation
    public bool LoyaltyProgramActivated { get; set; } = false;
    public DateTime? LoyaltyActivatedAt { get; set; }
    public string? LoyaltyWelcomeOffer { get; set; }
}
```

### **1.2 Service Architecture**

#### **Facebook Lead Service**
```csharp
public interface IFacebookLeadService
{
    Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload);
    Task<FacebookLead> GetFacebookLeadByIdAsync(string facebookLeadId);
    Task<List<FacebookLead>> GetUnprocessedFacebookLeadsAsync();
    Task<bool> ValidateFacebookWebhookAsync(string signature, string payload);
}
```

#### **Lead Management Service**
```csharp
public interface ILeadManagementService
{
    Task<Lead> CreateLeadAsync(Lead lead);
    Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status);
    Task<Lead> AssignLeadToStaffAsync(Guid leadId, Guid staffId);
    Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status);
    Task<Lead> CalculateLeadScoreAsync(Guid leadId);
}
```

#### **Lead Conversion Service**
```csharp
public interface ILeadConversionService
{
    Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason);
    Task<bool> ValidateLeadForConversionAsync(Guid leadId);
    Task<CustomerOnboarding> StartCustomerOnboardingAsync(Guid customerId);
}
```

#### **Customer Onboarding Service**
```csharp
public interface ICustomerOnboardingService
{
    Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId);
    Task<CustomerOnboarding> UpdateOnboardingStepAsync(Guid customerId, OnboardingStep step);
    Task<CustomerOnboarding> TrackAppInstallationAsync(Guid customerId, string deviceType, string appVersion);
    Task<bool> CompleteOnboardingAsync(Guid customerId);
}
```

### **1.3 API Endpoints Design**

#### **Facebook Lead Controller**
```csharp
[ApiController]
[Route("api/facebook")]
public class FacebookLeadController : ControllerBase
{
    [HttpPost("webhooks/leads")]
    public async Task<IActionResult> ProcessFacebookLead([FromBody] FacebookWebhookPayload payload);
    
    [HttpGet("leads/unprocessed")]
    public async Task<IActionResult> GetUnprocessedLeads();
}
```

#### **Lead Management Controller**
```csharp
[ApiController]
[Route("api/leads")]
public class LeadManagementController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLeads([FromQuery] LeadStatus? status);
    
    [HttpPost]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request);
    
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateLeadStatus(Guid id, [FromBody] UpdateLeadStatusRequest request);
    
    [HttpPost("{id}/convert")]
    public async Task<IActionResult> ConvertLeadToCustomer(Guid id, [FromBody] ConvertLeadRequest request);
}
```

#### **Customer Onboarding Controller**
```csharp
[ApiController]
[Route("api/customers/{customerId}/onboarding")]
public class CustomerOnboardingController : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartOnboarding(Guid customerId);
    
    [HttpPut("step")]
    public async Task<IActionResult> UpdateOnboardingStep(Guid customerId, [FromBody] UpdateOnboardingStepRequest request);
    
    [HttpPost("app-install")]
    public async Task<IActionResult> TrackAppInstallation(Guid customerId, [FromBody] AppInstallRequest request);
}
```

---

## **2. WIREFRAME LUONG MÀN HÌNH**

### **2.1 Facebook Mobile Ad Screen**
```
Facebook Ad Screen
==================
Header: "Vàn An Coffee"
Content: 
- Hình  cà phê
- Text: "ÐÁT HÀNG NHANH - GIÃM 20%"
- Text: "Tãng 50 ðiãm thành viên"
- Text: "Cài app nhãn uu ðãi ðoç quyên"
Buttons:
- "ÐÁT HÀNG NGAY"
- "CÀI UNG DUNG"
Footer: "5.000+ ngýði ðã cài app"
```

### **2.2 Facebook Lead Form**
```
Facebook Lead Form
==================
Header: "Nhãn uu ðãi tù Vàn An Coffee"
Title: "ÐÁT HÀNG NHANH - GIÃM 20%"
Fields:
- Hõ và tên: "Nguyên Van A"
- Sõ ðiên thoãi: "0987654321"
- Email: "email@example.com"
Checkbox: "Tôi ðông ý nhãn thông tin"
Button: "GÖI THÔNG TIN"
```

### **2.3 Facebook Thank You Screen**
```
Facebook Thank You
==================
Title: "Cám õn!"
Message: "THÔNG TIN ÐÃ GÖI THÀNH CÔNG"
Info: "Nhân viên sã liên hê trong 5 phút"
Benefits:
- Theo dõi ðõn hàng
- Nhãn ðiãm thýýng
- Uu ðãi ðoç quyên
Buttons:
- "CÀI UNG DUNG NGAY"
- "QUAY LÀI FACEBOOK"
```

### **2.4 ShopERP Dashboard - New Lead Alert**
```
ShopERP Dashboard
=================
Alert: "LEAD MÓI Tù FACEBOOK!"
Lead Info:
- "Facebook Lead - 0987654321"
- "Vùa xong - 2 phút trýóc"
- "Ðiãm: 85/100 - Khách hàng tiãm nãng cao"
Actions:
- "GÖI NGAY (Uu tiên 1)"
- "XEM CHI TIÉT (Uu tiên 2)"
Stats:
- "Facebook Leads: 3 | Phone Leads: 2"
- "Chuyên ðôi: 85% | Thõi gian phãn hôi: 3"
Lead List:
- "Nguyên Van A - 0987654321 - Facebook - 2p"
- "Trân Thi B - 0912345678 - Phone - 5p"
- "Lê Van C - 0987654322 - Facebook - 8p"
```

### **2.5 ShopERP - Lead Detail Screen**
```
Lead Detail Screen
=================
Header: "Lead Details - Nguyên Van A"
Customer Info:
- Hõ tên: Nguyên Van A
- SÐT: 0987654321
- Email: email@example.com
- Nguõn: Facebook Ad - "Ðát hàng nhanh"
- Ðiãm: 85/100 - Khách hàng tiãm nãng cao
Interaction History:
- "10:30 - Lead tùo tù Facebook"
- "10:32 - [Ðang x lý] - Chõ gõi ðiên"
Actions:
- "GÖI ÐIÊN NGAY"
- "GHI CHÚ KÊ HOÃCH"
Next Actions:
- "Gõi ðiên tù vãn"
- "Gõi email thông tin"
- "Chuyên thành khách hàng"
- "Không quan tâm"
```

### **2.6 ShopERP - Convert to Customer**
```
Convert to Customer
==================
Title: "XÁC NHÂN CHUYÊN ÐÔI LEAD -> KHÁCH HÀNG"
Customer Info:
- Khách hàng: Nguyên Van A
- SÐT: 0987654321
- Email: email@example.com
- Ðiãm: 85/100 - Khách hàng tiãm nãng
Welcome Offer:
- "GIÃM 20% ðõn hàng ðâu tiên"
- "50 ðiãm thành viên"
- "Hýýng dãn cài app"
Auto Actions:
- "Tú ðông gõi SMS chào mùng"
- "Tú ðông tào tài khoàn app"
- "Tú ðông kích hoát ðiãm thýýng"
Notes:
- "Lý do chuyên ðôi: Khách hàng quan tâm cao..."
Button: "XÁC NHÂN CHUYÊN ÐÔI"
```

### **2.7 KhachLink Welcome Page**
```
KhachLink Welcome Page
======================
Title: "CHÀO MÙNG NGUYÊN VÃN A ÐÊN VÔI VÀN AN!"
Welcome Offer:
- "GIÃM 20% ÐÃN HÀNG ÐÀU TIÊN"
- "50 ÐIÊM THUÔNG THÀNH VIÊN"
- "HÃNG BRONZE"
App Download:
- "CÀI UNG DUNG VÀN AN"
- QR Code for App Store/Google Play
- Link: "vanan.app/download"
Benefits:
- "Theo dõi ðõn hàng real-time"
- "Tích ðiãm ðôi quà dê dàng"
- "Uu ðãi ðoç quyên"
- "Ðát hàng nhanh hôn 50%"
Button: "ÐÁT HÀNG NGAY TRÊN WEB"
```

### **2.8 KhachLink - App Download Instructions**
```
App Download Instructions
========================
Title: "HÚÔNG DÃN CÀI ÐÃT UNG DUNG VÀN AN"
Step 1: QR Code
- Large QR Code
- "App Store | Google Play"
Step 2: Choose Platform
- iOS App Store | Android Google Play
Step 3: Install & Login
- "Mõ ung dúng sau khi cài xong"
- "Ðang nháp bàng SÐT: 0987654321"
- "Nhãn mã OTP qua SMS"
- "Bát ðâu trãi nghiêm ngay!"
Button: "CÃN HÔ TRÔ? GÓI 1900-1234"
```

### **2.9 Smartphone App - Welcome Screen**
```
App Welcome Screen
==================
Title: "CHÀO MÙNG NGUYÊN VÃN A!"
Account Info:
- "50 ðiãm thýýng"
- "Hãng: Bronze"
- "GiÃM 20% lân ðâu"
Login Options:
- "ÐANG NHÃP BÀNG SÐT: 0987654321"
- "GÖI MÃ OTP"
Social Login:
- "Facebook"
- "Google"
Register: "CHÚA CÓ TÀI KHOÃN? ÐANG KÝ NGAY"
```

### **2.10 Smartphone App - OTP Verification**
```
OTP Verification
===============
Title: "XÁC THÛC OTP"
Info: "MÃ OTP ÐÃ GÖI ÐÊN: 09****4321"
Timer: "Mã có hiêu lusc trong: 2:30"
Input: "[1][2][3][4][5][6]"
Options:
- "GÖI LÀI MÃ (30s)"
- "GÓI ÐIÊN XÁC THÛC"
Button: "XÁC NHÂN"
Change: "Thay ðôi sõ ðiên thoãi?"
```

### **2.11 Smartphone App - Home Screen (First Time)**
```
App Home Screen
===============
Welcome: "CHÀO MÙNG ÐÊN VÔI VÀN AN!"
Welcome Offer:
- "GIÃM 20% ÐÃN HÀNG ÐÀU TIÊN"
- "50 ÐIÊM THUÔNG THÀNH VIÊN"
- "HÃNG BRONZE"
Menu:
- "Cà phê ðác biêt"
- "Bánh mì Pháp"
- "Che khúc bách"
- "Trà sua"
Button: "ÐÁT HÀNG NGAY"
Features:
- "Theo dõi ðõn hàng"
- "Tích ðiãm ðôi quà"
- "Uu ðãi ðoç quyên"
- "Liên hê cùa hàng"
```

### **2.12 Smartphone App - First Order**
```
First Order Screen
==================
Title: "UU ÐÃI ÐÀU TIÊN CÙA BÀN!"
Offer: "GIÃM 20% - Hãn sù dungs: 7 ngày"
Menu Items:
- "Cà phê sua ðá: 25.000ð -> 20.000ð"
- "Bánh mì Pháp: 15.000ð -> 12.000ð"
Cart:
- "Cà phê sua ðá x1: 20.000ð"
- "Tông: 20.000ð"
- "GiÃM 20%: -4.000ð"
- "Thanh toán: 16.000ð"
Note: "Ban sã nhãn +10 ðiãm!"
```

### **2.13 Smartphone App - Order Success**
```
Order Success Screen
==================
Title: "ÐÃT HÀNG THÀNH CÔNG!"
Order Info:
- "MÃ ÐÃN HÀNG: #VA2024040801"
- "Cà phê sua ðá x1: 20.000ð"
- "Tông: 20.000ð"
- "GiÃM 20%: -4.000ð"
- "Thanh toán: 16.000ð"
Rewards:
- "+10 ðiãm thýýng"
- "Tông ðiãm: 60ð"
- "Tiên tói: Silver (100ð)"
Order Tracking:
- "Ðang chuãn bî"
- "Cùa hàng: Vàn An Q1"
- "Dý kiên: 15 phút"
Button: "THEO DÕI ÐÃN HÀNG"
Button: "TIÊP TÔC ÐÁT HÀNG"
```

---

## **3. COMPLETE USER JOURNEY**

### **3.1 Flow Overview**
1. **Facebook Ad** -> Lead capture form
2. **Facebook Thank You** -> App download prompt
3. **ShopERP** -> Lead management & conversion
4. **KhachLink Web** -> Welcome & app download
5. **Smartphone App** -> Onboarding & first order

### **3.2 Key Touchpoints**
- **Facebook**: Lead generation & initial engagement
- **ShopERP**: Lead qualification & customer conversion
- **KhachLink Web**: Welcome experience & app promotion
- **Mobile App**: Onboarding & loyalty activation

### **3.3 User Benefits**
- **Seamless Experience**: Facebook -> App in 4 steps
- **Instant Rewards**: 50 points + 20% discount
- **Personal Journey**: Tailored onboarding experience
- **Long-term Value**: Loyalty program & exclusive offers

---

## **4. IMPLEMENTATION REQUIREMENTS**

### **4.1 Technical Requirements**
- Multi-tenancy compliance (IMustHaveTenant)
- Clean Architecture patterns
- TDD approach (tests first)
- C# 11 raw string literals for JS injection
- EF Core with SQLite WAL mode
- Anti-panic governance (no deletions)

### **4.2 Database Schema**
- Lead tables with foreign keys to existing Customer table
- Facebook lead specific tables
- Onboarding tracking tables
- Activity logging tables

### **4.3 API Requirements**
- RESTful design with proper HTTP methods
- Tenant-aware endpoints
- Authentication/authorization
- Rate limiting for Facebook webhooks
- Comprehensive error handling

### **4.4 Test Requirements**
- 4-layer test pyramid (Unit, Integration, API, E2E)
- Facebook webhook testing
- Lead conversion workflow testing
- Customer onboarding testing
- Cross-system integration testing

---

## **5. SUCCESS CRITERIA**

### **5.1 Technical Success**
- All 4 test layers pass 100%
- Facebook webhook processing functional
- Lead -> Customer conversion working
- Customer onboarding complete

### **5.2 Business Success**
- Facebook leads captured automatically
- Lead conversion rate > 80%
- Customer onboarding completion > 90%
- Loyalty program activation > 95%

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
- **Facebook API Changes**: Use versioned API endpoints
- **Webhook Reliability**: Implement retry mechanisms
- **Data Consistency**: Use transaction management
- **Performance**: Implement async processing

### **6.2 Business Risks**
- **Facebook Policy Changes**: Diversify lead sources
- **Data Privacy**: GDPR compliance
- **Lead Quality**: Implement lead scoring
- **Customer Adoption**: Optimize onboarding flow

---

## **7. VERSION CONTROL**

### **7.1 Document Version**
- **Version**: 1.0
- **Created**: 8 tháng 4, 2026
- **Author**: AI Coding Agent
- **Status**: Design Complete - Ready for Implementation

### **7.2 Update Process**
- Any changes to design must be documented
- Version bump required for significant changes
- All implementations must reference this document
- Discrepancies must be approved before implementation

---

## **8. NEXT STEPS**

### **8.1 Immediate Actions**
1. **Phase 2 Completion**: Finish API Tests (Layer 3)
2. **Phase 2 Completion**: Finish E2E Tests (Layer 4)
3. **Phase 3**: Test Execution - 100% pass requirement
4. **Phase 4**: Coding Implementation

### **8.2 Implementation Order**
1. Domain Models in 1_Shared/Domain.cs
2. Service Layer in 3_CoreHub/Services/
3. API Controllers in 2_Gateway/Controllers/
4. Database Migrations
5. Test Implementation
6. Documentation Updates

---

## **9. APPROVAL**

**This document serves as the authoritative reference for Facebook Lead Integration implementation.**

**All subsequent phases must reference this document. Any deviations require explicit approval.**

**Status: APPROVED FOR IMPLEMENTATION**

---

*Document End*
