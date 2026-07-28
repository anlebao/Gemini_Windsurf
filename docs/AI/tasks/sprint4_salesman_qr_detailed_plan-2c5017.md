# Sprint 4 Detailed Plan — Salesman + Composite QR Referral + Per-Product Commission + App-Install Bonus + Risk Scoring + Fraud Flagging (v1.2)

TDD plan (20+ test cases — v1.2: tăng từ 20), coding plan (7 sessions — v1.2: tăng từ 6), API specs (v1.2: +risk scoring + FraudFlag integration), QR generation, commission logic per-product, app-install bonus flow, **risk scoring hold/reject flow (v1.2 NEW)**.

> **v1.2 redesign (incremental trên v1.1):**
> - **Risk scoring mandatory:** Mọi SalesReferral + AppInstallAttribution compute RiskScore 0-100 qua IRiskScoringService (đã có từ Sprint 0).
> - **Hold 48h if RiskScore≥60:** CommissionStatus/AttributionStatus=Held + FraudFlag(Status=Pending).
> - **Auto-reject if RiskScore≥80:** CommissionStatus/AttributionStatus=Rejected + FraudFlag.
> - **Cooling period 24h if RiskScore<60:** Auto-approve sau 24h.
> - **Device fingerprint integration:** app-install/attributed request gửi kèm FingerprintHash.
> - **IFraudFlagService:** create/query FraudFlag.
> - **Sales Dashboard:** hiển thị Pending/Held/Rejected status.

---

## 1. API SPECIFICATIONS — v1.1

### 1.1 GET /api/community/nearby-products (v1.1: + commission rate + app-install bonus)
```
Query: lat, lng, radiusKm (default 10)
Header: X-Customer-Token
Auth: CustomerToken → check CommunityRole(Salesman, Active)
Response 200: [
  {
    "productId": "guid",
    "name": "string",
    "price": 50000,
    "shopName": "string",
    "distanceKm": 3.2,
    "commissionRate": 0.05,        // v1.1 NEW — từ ProductReferralConfig (null nếu chưa set)
    "appInstallBonus": 10000,      // v1.1 NEW — từ ProductReferralConfig (null nếu chưa set)
    "productShortCode": "TR-001",  // v1.1 NEW — từ ProductReferralConfig (null nếu chưa set)
    "hasReferralConfig": true      // v1.1 NEW — flag cho UI hiển thị "Chưa thiết lập"
  }
]
```

### 1.2 GET /api/community/salesman/qr?productId={productId} (v1.1: yêu cầu productId)
```
Header: X-Customer-Token
Auth: CustomerToken → check CommunityRole(Salesman, Active)
Query: productId (required — v1.1)
Response 200: {
  "salesmanCode": "ABC123",
  "productShortCode": "TR-001",       // v1.1 NEW
  "compositeCode": "ABC123|TR-001",   // v1.1 NEW — format "{salesmanCode}|{productShortCode}"
  "qrUrl": "https://{domain}/r/ABC123|TR-001",  // v1.1 — composite URL
  "productId": "guid"
}
Response 400: nếu productId không có ProductReferralConfig hoặc salesman không có role
```

### 1.3 GET /api/community/salesman/{salesmanId}/commissions (v1.1: tách biệt 2 nguồn)
```
Header: X-Customer-Token
Response 200: {
  "totalSales": 1500000,
  "totalCommission": 75000,           // commission chốt đơn
  "pendingCommission": 50000,
  "paidCommission": 25000,
  "totalAppInstallBonus": 30000,      // v1.1 NEW — app-install bonus
  "pendingAppInstallBonus": 20000,    // v1.1 NEW
  "paidAppInstallBonus": 10000,       // v1.1 NEW
  "commissionRecords": [              // v1.1 — commission chốt đơn
    { "orderId": "guid", "productId": "guid", "orderTotal": 150000, "commissionRate": 0.05, "commissionAmount": 7500, "status": "Pending", "createdAt": "..." }
  ],
  "appInstallBonusRecords": [         // v1.1 NEW — app-install bonus
    { "attributionId": "guid", "customerId": "guid", "productId": "guid", "bonusAmount": 10000, "status": "Pending", "installedAt": "..." }
  ]
}
```

### 1.4 POST /api/community/app-install/attributed (v1.1 NEW, v1.2: +risk scoring + fingerprint)
```
Header: X-Customer-Token (customer đã cài app)
Body: {
  "referralCode": "ABC123|TR-001",       // composite code từ localStorage
  "fingerprintHash": "abc123...",         // v1.2 NEW — FingerprintJS hash (64 chars)
  "fingerprintSignals": "{...}",          // v1.2 NEW — raw signals JSON
  "deviceToken": "xyz789..."              // v1.2 NEW — device token từ localStorage
}
Response 200: {
  "attributionId": "guid",
  "salesmanId": "guid",
  "productId": "guid",
  "bonusAmount": 10000,
  "walletTransactionId": "guid" or null,  // null if RiskScore>=60 (hold) or >=80 (reject)
  "riskScore": 35,                        // v1.2 NEW
  "status": "Pending" | "Held" | "Rejected"  // v1.2 NEW
}
Response 409: customer đã có AppInstallAttribution trước đó (AC-12.2)
Response 400: referralCode không hợp lệ hoặc ProductReferralConfig không tồn tại (bonusAmount=0, vẫn ghi attribution)
Flow (v1.2 updated):
1. Resolve referralCode → split by "|" → salesmanCode + productShortCode
2. Lookup CommunityRole(SalesmanCode) → salesmanId
3. Lookup ProductReferralConfig(ProductShortCode) → productId + appInstallBonus
4. Check customer chưa có AppInstallAttribution (unique constraint)
5. v1.2 NEW: Lookup or create DeviceRegistration(customerId, deviceToken, fingerprintHash, fingerprintSignals)
6. Create AppInstallAttribution(customerId, salesmanId, productId, bonusAmount, deviceRegistrationId)
7. v1.2 NEW: Compute RiskScore qua IRiskScoringService (8 factors: sameFingerprint, sameIP24h, customerAgeDays<7, deviceFirstSeen<24h, ordersFromDeviceToday>3, referralBonusAmount>50K, appInstallTime<30s, blacklistedFingerprint)
8. v1.2 NEW: Set RiskScore + RiskFactors on AppInstallAttribution
9. v1.2 NEW: If RiskScore>=80 → AttributionStatus=Rejected + create FraudFlag(FlagType=HighRiskScore) — KHÔNG tạo WalletTransaction
10. v1.2 NEW: If RiskScore 60-79 → AttributionStatus=Held, HoldUntil=now+48h + create FraudFlag(FlagType=HighRiskScore) — KHÔNG tạo WalletTransaction (hold)
11. v1.2 NEW: If RiskScore<60 → AttributionStatus=Pending (cooling 24h) — KHÔNG tạo WalletTransaction ngay (create sau 24h qua background job OR admin approve)
12. Link AppInstallAttribution.SalesReferralId nếu có SalesReferral cùng salesmanId + customerId + productId
```

### 1.5 Order Creation with Composite Referral (v1.1)
```
POST /api/orders (existing endpoint, modified)
Body: { ...existing fields..., "referralCode": "ABC123|TR-001" (optional, composite format v1.1) }
→ If referralCode exists:
  → Split by "|" → salesmanCode + productShortCode
  → Resolve CommunityRole(SalesmanCode) → salesmanId
  → Resolve ProductReferralConfig(ProductShortCode) → productId
  → Set Order.SalesmanId + Order.ReferralProductId + Order.ReferralCode (composite)
→ Create SalesReferral khi Order.Completed (commission = orderTotal * ProductReferralConfig.CommissionRate)
```

### 1.6 Admin API: ProductReferralConfig CRUD (v1.1 NEW)
```
GET    /api/admin/products/{productId}/referral-config      → 200 + config (or 404 if not set)
POST   /api/admin/products/{productId}/referral-config      → 201 + config
  Body: { "commissionRate": 0.05, "appInstallBonus": 10000, "productShortCode": "TR-001", "isActive": true }
PUT    /api/admin/products/{productId}/referral-config      → 200 + updated config
DELETE /api/admin/products/{productId}/referral-config      → 204 (soft delete — IsActive=false)
GET    /api/admin/products/referral-configs                 → 200 + list all configs (admin dashboard)
Auth: SystemAdmin policy (JWT)
Validation: commissionRate 0.02-0.05, appInstallBonus >= 0, productShortCode unique within tenant
```

---

## 2. SERVICE SPECIFICATIONS — v1.1

### ISalesmanService (v1.1: + composite referral, + app-install attribution)
```csharp
public interface ISalesmanService
{
    Task<List<NearbyProductDto>> GetNearbyProductsAsync(double lat, double lng, int radiusKm, Guid salesmanId);
    Task<CompositeSalesmanQrDto> GetCompositeSalesmanQrAsync(Guid salesmanId, Guid productId);  // v1.1: + productId
    Task<CommissionSummaryDto> GetCommissionsAsync(Guid salesmanId);  // v1.1: tách biệt commission + app-install bonus
    Task<(Guid salesmanId, Guid productId)?> ResolveCompositeReferralCodeAsync(string referralCode);  // v1.1: composite
    Task CreateCommissionAsync(Guid orderId);  // v1.1: per-product commission từ ProductReferralConfig
}

public interface IAppInstallAttributionService  // v1.1 NEW
{
    Task<AppInstallAttributionDto> AttributeInstallAsync(Guid customerId, string referralCode);
    Task<List<AppInstallAttributionDto>> GetBySalesmanAsync(Guid salesmanId);
}

public interface IProductReferralConfigService  // v1.1 NEW
{
    Task<ProductReferralConfigDto?> GetByProductIdAsync(Guid productId);
    Task<ProductReferralConfigDto> CreateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode);
    Task<ProductReferralConfigDto> UpdateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode, bool isActive);
    Task DeactivateAsync(Guid productId);
    Task<List<ProductReferralConfigDto>> ListAllAsync();
}
```

### SalesmanService (v1.1 updated)
- `GetNearbyProductsAsync`: Query FeaturedProducts → join TenantSettings (via TenantId) → Haversine filter → LEFT JOIN ProductReferralConfig (v1.1) → project DTO với commissionRate + appInstallBonus + productShortCode
- `GetCompositeSalesmanQrAsync` (v1.1: + productId): Query CommunityRole WHERE CustomerId=salesmanId AND RoleType=Salesman AND IsActive → Query ProductReferralConfig WHERE ProductId=productId → return composite code `{salesmanCode}|{productShortCode}` + QR URL
- `GetCommissionsAsync` (v1.1: tách biệt): Query SalesReferral WHERE SalesmanId → aggregate commission totals + Query AppInstallAttribution WHERE SalesmanId → aggregate app-install bonus totals
- `ResolveCompositeReferralCodeAsync` (v1.1: composite): Split referralCode by "|" → lookup CommunityRole(SalesmanCode) + ProductReferralConfig(ProductShortCode) → return (salesmanId, productId)
- `CreateCommissionAsync` (v1.1: per-product): Called when Order.Completed. If Order.SalesmanId + Order.ReferralProductId not null → lookup ProductReferralConfig(ReferralProductId) → commissionAmount = orderTotal * config.CommissionRate → create SalesReferral with CommissionRate snapshot

### AppInstallAttributionService (v1.1 NEW)
- `AttributeInstallAsync`: Resolve referralCode → check customer chưa có attribution (unique) → create AppInstallAttribution + FraudFlag if RiskScore>=60 (v1.4: KHÔNG tạo WalletTransaction trong Sprint 4 — WalletTransaction tạo bởi CoolingPeriodJob sau 24h hoặc admin approve Sprint 6) → link SalesReferral nếu có
- `GetBySalesmanAsync`: Query AppInstallAttribution WHERE SalesmanId → project DTO

### ProductReferralConfigService (v1.1 NEW)
- Standard CRUD — sysadmin only. Validation: CommissionRate 0.02-0.05, AppInstallBonus >= 0, ProductShortCode unique within tenant.

---

## 3. QR GENERATION SPEC — v1.1: composite code

### Client-side QR (qrcode.js via CDN)
```
CDN: https://cdn.jsdelivr.net/npm/qrcode@1.5.3/build/qrcode.min.js

JS interop (wwwroot/js/qrcode.js):
  - generateQR(elementId, text, width, height) → render QR canvas

SalesmanQR.razor (v1.1: yêu cầu productId):
  - User chọn product từ NearbyProducts page → navigate to SalesmanQR?productId={id}
  - OnAfterRender: call generateQR with composite qrUrl "https://{domain}/r/{salesmanCode}|{productShortCode}"
  - Display: QR canvas + composite code text + salesman code + product short code + "Lưu mã" button
  - Download: canvas.toDataURL → download as PNG
```

### QR Scan flow (QRScanner.razor modification — v1.1: composite)
```
Current: scan QR → extract tenantId → navigate to shop
New (v1.1): scan QR → check if URL matches /r/{salesmanCode}|{productShortCode} pattern
  → if yes: save composite referralCode to localStorage (key: "vanan_referral_code")
    → navigate to home
  → if no: existing tenant QR flow
```

### App-install event handler (app-install-tracker.js — v1.1 NEW)
```
// wwwroot/js/app-install-tracker.js
window.addEventListener('appinstalled', (event) => {
  const referralCode = localStorage.getItem('vanan_referral_code');
  if (!referralCode) return;  // no referral — no attribution
  
  fetch('/api/community/app-install/attributed', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Customer-Token': getCustomerToken()  // from localStorage
    },
    body: JSON.stringify({ referralCode: referralCode })
  }).then(resp => {
    if (resp.ok) {
      console.log('App install attributed to salesman');
      localStorage.removeItem('vanan_referral_code');  // clear after attribution
    }
  }).catch(err => console.error('Attribution failed:', err));
});

// Register in Program.cs: JS interop để load app-install-tracker.js
```

---

## 4. TDD PLAN (20+ TEST CASES — v1.2: tăng từ 20)

### File: `6_Tests/VanAn.Core.Tests/SalesmanServiceTests.cs`

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetNearbyProducts_FiltersByRadius` | Products outside radius excluded |
| 2 | `GetNearbyProducts_ReturnsProductDetails` | Name, price, shopName, distanceKm, commissionRate, appInstallBonus (v1.1) |
| 3 | `GetNearbyProducts_SortsByDistance` | Closest first |
| 4 | `GetNearbyProducts_NoConfig_ShowsNotSetup` (v1.1 NEW) | Product không có ProductReferralConfig → commissionRate=null, hasReferralConfig=false |
| 5 | `GetCompositeSalesmanQr_ReturnsCompositeCode` (v1.1) | Composite code `{salesmanCode}\|{productShortCode}` + QR URL |
| 6 | `GetCompositeSalesmanQr_NoProductConfig_Throws` (v1.1 NEW) | Throws khi productId không có ProductReferralConfig |
| 7 | `GetCompositeSalesmanQr_NoRole_Throws` | Throws when no active Salesman role |
| 8 | `ResolveCompositeReferralCode_Valid_ReturnsBothIds` (v1.1) | Returns (salesmanId, productId) từ composite code |
| 9 | `ResolveCompositeReferralCode_Invalid_ReturnsNull` (v1.1) | Null cho unknown composite code |
| 10 | `CreateCommission_PerProduct_CalculatesCorrectly` (v1.1) | CommissionAmount = orderTotal * ProductReferralConfig.CommissionRate (per-product, KHÔNG hardcode) |
| 11 | `CreateCommission_NoSalesmanId_NoOp` | No SalesReferral created when Order.SalesmanId null |
| 12 | `GetCommissions_AggregatesBothSources` (v1.1) | Total sales, pending/paid commission, pending/paid app-install bonus (tách biệt) |

### File: `6_Tests/VanAn.Core.Tests/AppInstallAttributionServiceTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 13 | `AttributeInstall_Valid_CreatesAttributionNoWallet` (v1.1 NEW, v1.4 CORRECTED) | AppInstallAttribution tạo, KHÔNG tạo WalletTransaction (create sau 24h bởi CoolingPeriodJob) |
| 14 | `AttributeInstall_DoubleAttribute_ThrowsConflict` (v1.1 NEW) | 2nd attribution cho same customer → 409 Conflict |
| 15 | `AttributeInstall_NoBonus_ZeroWalletTransaction` (v1.1 NEW) | ProductReferralConfig.AppInstallBonus=0 → attribution tạo, KHÔNG tạo WalletTransaction |
| 16 | `AttributeInstall_AlreadyInstalled_NoQualify` (v1.1 NEW) | Customer đã cài app trước → không qualify (AC-12.7) |

### File: `6_Tests/VanAn.Core.Tests/ProductReferralConfigServiceTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 17 | `Create_ValidFields_ReturnsConfig` (v1.1 NEW) | CommissionRate 0.05, AppInstallBonus 10000, ProductShortCode "TR-001" |
| 18 | `Create_InvalidRate_Throws` (v1.1 NEW) | CommissionRate < 0.02 or > 0.05 → throws |
| 19 | `Update_ModifiesFields` (v1.1 NEW) | Update commission rate + bonus + short code |
| 20 | `Deactivate_SetsIsActiveFalse` (v1.1 NEW) | Soft delete |

**Total: 20 test cases from v1.1 + v1.2 additions below**

### File: `6_Tests/VanAn.Core.Tests/RiskScoringIntegrationTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 21 | `SalesReferral_Create_HighRisk_HoldsCommission` (v1.2 NEW) | RiskScore=70 → CommissionStatus=Held, HoldUntil=now+48h, FraudFlag created |
| 22 | `SalesReferral_Create_VeryHighRisk_RejectsCommission` (v1.2 NEW) | RiskScore=85 → CommissionStatus=Rejected, FraudFlag created, no payout |
| 23 | `SalesReferral_Create_LowRisk_PendingWithCooling` (v1.2 NEW) | RiskScore=30 → CommissionStatus=Pending, HoldUntil=null, ready for 24h cooling then payout |
| 24 | `AppInstallAttribution_Create_HighRisk_HoldsBonus` (v1.2 NEW) | RiskScore=65 → AttributionStatus=Held, no WalletTransaction |
| 25 | `AppInstallAttribution_Create_VeryHighRisk_RejectsBonus` (v1.2 NEW) | RiskScore=90 → AttributionStatus=Rejected, no WalletTransaction, FraudFlag |
| 26 | `AppInstallAttribution_Create_LowRisk_PendingWithCooling` (v1.2 NEW) | RiskScore=20 → AttributionStatus=Pending, no WalletTransaction (create after 24h) |

### File: `6_Tests/VanAn.Core.Tests/FraudFlagServiceTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 27 | `FraudFlagService_Create_WhenRiskScoreHigh_CreatesFlag` (v1.2 NEW) | RiskScore=60 → FraudFlag(Status=Pending, FlagType=HighRiskScore) created |
| 28 | `FraudFlagService_GetPendingFlags_ReturnsSortedByRiskScore` (v1.2 NEW) | List pending flags sorted by RiskScore desc |
| 29 | `FraudFlagService_Confirm_UpdatesEntityStatus` (v1.2 NEW) | Confirm flag → related SalesReferral.CommissionStatus=Rejected |

### File: `6_Tests/VanAn.Core.Tests/DeviceFingerprintIntegrationTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 30 | `AppInstall_WithFingerprint_MatchesSalesman_HighRisk` (v1.2 NEW) | customerFingerprint == salesmanFingerprint → RiskScore includes +50 |
| 31 | `AppInstall_WithFingerprint_DifferentFromSalesman_LowRisk` (v1.2 NEW) | Different fingerprints → no +50 factor |

**Total: 31 test cases (≥20 minimum met — v1.2)**

---

## 5. CODING PLAN — 7 SESSIONS (v1.2: tăng từ 6, +risk scoring + FraudFlag session)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service interfaces + tests | ISalesmanService + IAppInstallAttributionService + IProductReferralConfigService + **IFraudFlagService (v1.2)** + 31 unit tests |
| **S2** | Service implementations | SalesmanService + AppInstallAttributionService + ProductReferralConfigService + **FraudFlagService (v1.2)** + DI registration + risk scoring integration |
| **S3** | Controller + order modification | CommunityController salesman endpoints (composite QR, app-install/attributed + **fingerprintHash body field v1.2**) + ProductReferralConfigController (admin CRUD) + FraudFlagController (preview, v1.2) + OrdersController composite referralCode + **risk scoring on commission calc (v1.2)** |
| **S4** | UI: NearbyProducts + SalesmanQR + SalesDashboard | NearbyProducts.razor + SalesmanQR.razor (composite) + SalesDashboard.razor (tách biệt 2 nguồn + **Held/Rejected status v1.2**) + qrcode.js |
| **S5** (v1.1 NEW) | Admin UI + app-install tracker | ProductReferralConfigs.razor (admin CRUD) + app-install-tracker.js (+ **fingerprint send v1.2**) + sw.js/pwa.js wiring + QRScanner composite handling |
| **S6** (v1.2 NEW, v1.4: use IWalletService from Sprint 0) | Risk scoring integration + cooling period job | Risk scoring integration in SalesmanService + AppInstallAttributionService + CoolingPeriodJob (HostedService, hourly, auto-approve RiskScore<60 sau 24h — **gọi IWalletService.CreateTransactionAsync từ Sprint 0**) + HeldTimeoutJob (auto-reject Held sau 48h nếu admin không review) |
| **S7** | E2E + regression | community-salesman.spec.ts (scan QR → order → commission + app-install → bonus + **risk scoring hold/reject flow v1.2**) + regression |

---

## 6. VPS VERIFICATION (Sprint 4 — v1.1: 8 tests thay vì 5)

| # | Test | Expected |
|---|---|---|
| RV4-1 | Nearby products + config (v1.1) | 200 + products array với commissionRate + appInstallBonus |
| RV4-2 | Composite Salesman QR (v1.1) | 200 + composite code `{salesmanCode}\|{productShortCode}` |
| RV4-3 | Order with composite referral (v1.1) | 200 + Order.SalesmanId + Order.ReferralProductId set |
| RV4-4 | Commission list (v1.1) | 200 + commission records + appInstallBonus records (tách biệt) |
| RV4-5 | App-install attribution (v1.1 NEW) | 200 + AppInstallAttribution + WalletTransaction |
| RV4-6 | Double attribution rejected (v1.1 NEW) | 409 Conflict |
| RV4-7 | Admin ProductReferralConfig (v1.1 NEW) | 201 + config record |
| RV4-8 | E2E Playwright (v1.1) | community-salesman.spec.ts PASS (scan QR → order → commission + app-install → bonus) |
