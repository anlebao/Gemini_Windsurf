# Sprint 6 Detailed Plan — Admin + Fraud Review + Polish + Legal (v1.2)

TDD plan (14 test cases — v1.2: +6 Fraud Review + nav/menu), coding plan (4 sessions — v1.2: +Fraud Review session), admin API spec, Fraud Review API spec, nav/menu entry point spec, legal document outline, full regression checklist.

**v1.2 additions:** Fraud Review UI + Fraud Stats + FraudFlagController + 3-strike ban + nav/menu entry points (G1-G4 fixes) + shop owner wallet access + salesman self-view FraudFlag + Profile roles detail.

---

## 1. API SPECIFICATIONS

### 1.1 GET /api/admin/community/eligible
```
Header: Authorization: Bearer {adminJWT}
Auth: SystemAdmin policy
Query: page (default 1), pageSize (default 20)
Response 200: {
  "total": 15,
  "items": [
    {
      "customerId": "guid", "fullName": "string", "phoneNumber": "090***",
      "loyaltyPoints": 1500, "identityLevel": "Verified",
      "existingRoles": ["Shipper"]
    }
  ]
}
```

### 1.2 POST /api/admin/community/{customerId}/activate-role
```
Header: Authorization: Bearer {adminJWT}
Body: { "role": "Shipper" | "Salesman" }
Response 200: { "communityRoleId": "guid", "roleType": "Shipper", "activatedAt": "..." }
Response 409: Role already active for this customer
Response 400: Customer doesn't meet criteria (IdentityLevel < Verified or LoyaltyPoints < 1000)
```

### 1.3 POST /api/admin/community/{customerId}/deactivate-role
```
Header: Authorization: Bearer {adminJWT}
Body: { "role": "Shipper" | "Salesman" }
Response 200: { "deactivatedAt": "..." }
Response 404: No active role found
```

### 1.4 GET /api/customer-identity/me (modified — add roles)
```
Header: X-Customer-Token
Response 200: {
  ...existing fields...,
  "communityRoles": [
    { "roleType": "Shipper", "isActive": true, "activatedAt": "..." }
  ]
}
```

### 1.5 GET /api/admin/community/fraud-flags (v1.2 NEW)
```
Header: Authorization: Bearer {adminJWT}
Auth: SystemAdmin policy
Query: status (Pending|Confirmed|Dismissed|Reviewed — default Pending), page, pageSize
Response 200: {
  "total": 5, "items": [
    { "id": "guid", "customerId": "guid", "customerName": "string",
      "entityType": "SalesReferral|AppInstallAttribution", "entityId": "guid",
      "riskScore": 85, "riskFactors": { ... }, "status": "Pending",
      "createdAt": "..." }
  ]
}
```

### 1.6 GET /api/admin/community/fraud-flags/{id} (v1.2 NEW)
```
Header: Authorization: Bearer {adminJWT}
Response 200: { ...full detail + related entities (DeviceRegistration, Customer, Order) }
Response 404: Not found
```

### 1.7 POST /api/admin/community/fraud-flags/{id}/confirm (v1.2 NEW)
```
Header: Authorization: Bearer {adminJWT}
Response 200: { "status": "Confirmed", "sideEffects": ["SalesReferral.Rejected", "WalletReversal:50000"] }
Side effects: Update related entity status (Rejected). Create Reversal wallet tx if commission/bonus đã pay. Check 3-strike ban.
Response 409: Already confirmed/dismissed
```

### 1.8 POST /api/admin/community/fraud-flags/{id}/dismiss (v1.2 NEW)
```
Header: Authorization: Bearer {adminJWT}
Response 200: { "status": "Dismissed", "sideEffects": ["DeviceRegistration.IsVerified=true"] }
Side effects: Whitelist entity (DeviceRegistration.IsVerified=true, RiskScore giảm). KHÔNG tính strike.
```

### 1.9 GET /api/admin/community/fraud-stats (v1.2 NEW)
```
Header: Authorization: Bearer {adminJWT}
Response 200: {
  "pending": 5, "confirmed": 12, "dismissed": 3, "reviewed": 8,
  "totalLossPrevented": 350000,
  "topFlaggedCustomers": [
    { "customerId": "guid", "customerName": "string", "flagCount": 4 }
  ]
}
```

### 1.10 GET /api/community/my-fraud-flags (v1.2 NEW — salesman self-view)
```
Header: X-Customer-Token
Auth: X-Customer-Token (salesman/shipper own flags only)
Response 200: [
  { "id": "guid", "entityType": "SalesReferral", "riskScore": 45, "status": "Pending", "createdAt": "..." }
]
Response 401: No token
```

---

## 2. SERVICE SPECIFICATIONS

### ICommunityAdminService
```csharp
public interface ICommunityAdminService
{
    Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersAsync(int page, int pageSize);
    Task<CommunityRole> ActivateRoleAsync(Guid customerId, CommunityRoleType role, Guid activatedBy);
    Task DeactivateRoleAsync(Guid customerId, CommunityRoleType role);
    Task<List<CommunityRole>> GetCustomerRolesAsync(Guid customerId);
}
```

### CommunityAdminService
- `GetEligibleCustomersAsync`: Query Customers WHERE IdentityLevel >= Verified AND LoyaltyPoints >= 1000. Left join CommunityRoles to show existing roles. Paginate.
- `ActivateRoleAsync`: Verify customer meets criteria. Check no active role of same type. Create CommunityRole. Send push notification. Return.
- `DeactivateRoleAsync`: Find active CommunityRole for customer + roleType. Call Deactivate(). Save.
- `GetCustomerRolesAsync`: Query CommunityRole WHERE CustomerId, include inactive.

### IFraudReviewService (v1.2 NEW)
```csharp
public interface IFraudReviewService
{
    Task<PagedResult<FraudFlagDto>> GetPendingAsync(string status, int page, int pageSize);
    Task<FraudFlagDetailDto> GetDetailAsync(Guid id);
    Task<ConfirmResultDto> ConfirmAsync(Guid fraudFlagId, Guid confirmedBy);
    Task<DismissResultDto> DismissAsync(Guid fraudFlagId, Guid dismissedBy);
    Task<FraudStatsDto> GetStatsAsync();
    Task<List<FraudFlagDto>> GetMyFlagsAsync(Guid customerId); // salesman self-view
}
```

### FraudReviewService (v1.2 NEW)
- `GetPendingAsync`: Query FraudFlag WHERE Status = status (default Pending), sort by RiskScore desc. Paginate. Join Customer for name.
- `GetDetailAsync`: Load FraudFlag + related entities (DeviceRegistration, Customer, SalesReferral/AppInstallAttribution). Return full detail DTO.
- `ConfirmAsync`: Set FraudFlag.Status=Confirmed. Update related entity status (SalesReferral.CommissionStatus=Rejected, AppInstallAttribution.AttributionStatus=Rejected). If commission/bonus đã pay → create Reversal wallet tx via IWalletService.ReverseTransactionAsync. Check 3-strike: count Confirmed FraudFlags for same CustomerId → if >=3 → auto-ban (Customer.IsActive=false). Return side effects list.
- `DismissAsync`: Set FraudFlag.Status=Dismissed. Whitelist entity (DeviceRegistration.IsVerified=true). KHÔNG tính strike. Return side effects list.
- `GetStatsAsync`: Aggregate counts by status + SUM commission amounts for confirmed (loss prevented) + top 5 flagged customers.
- `GetMyFlagsAsync`: Query FraudFlag WHERE CustomerId = customerId (salesman self-view, X-Customer-Token auth).

---

## 3. TDD PLAN (14 TEST CASES — v1.2: +6 Fraud Review)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetEligible_FiltersByVerifiedAndPoints` | Only Verified+ ≥1000pts returned |
| 2 | `GetEligible_Paginates` | Correct page/pageSize |
| 3 | `ActivateRole_CreatesRole` | CommunityRole exists, IsActive=true |
| 4 | `ActivateRole_AlreadyActive_Throws` | Throws on duplicate active role |
| 5 | `ActivateRole_NotEligible_Throws` | Throws when <1000pts or <Verified |
| 6 | `DeactivateRole_SetsInactive` | IsActive=false, DeactivatedAt set |
| 7 | `DeactivateRole_NotFound_Throws` | Throws when no active role |
| 8 | `GetCustomerRoles_ReturnsAll` | Both active and inactive roles |
| 9 | `FraudReview_GetPending_SortsByRiskScoreDesc` (v1.2) | Highest RiskScore first |
| 10 | `FraudReview_Confirm_SetsStatusAndRejectsEntity` (v1.2) | FraudFlag=Confirmed + SalesReferral=Rejected |
| 11 | `FraudReview_Confirm_CreatesWalletReversalIfPaid` (v1.2) | Reversal tx created when commission already paid |
| 12 | `FraudReview_Confirm_ThreeStrikes_AutoBans` (v1.2) | 3rd confirm → Customer.IsActive=false |
| 13 | `FraudReview_Dismiss_WhitelistsDevice_NoStrike` (v1.2) | DeviceRegistration.IsVerified=true, no ban count |
| 14 | `FraudReview_GetMyFlags_ReturnsOwnOnly` (v1.2) | Salesman sees only own FraudFlags |

---

## 4. UI SPECS

### AdminPanel.razor (ShopERP)
```
@page "/admin/community/admin-panel"
@attribute [Authorize(Roles="SystemAdmin")]
- Header: "Quản lý cộng tác viên"
- Table:
  - Columns: Tên, SĐT, Điểm, Identity Level, Roles hiện tại, Actions
  - Action buttons: "Kích hoạt Shipper" / "Kích hoạt Salesman" / "Hủy role"
- Search box: by name or phone
- Pagination
- Toast on activate/deactivate success
```

### FraudFlags.razor (ShopERP — v1.2 NEW)
```
@page "/admin/community/fraud-flags"
@attribute [Authorize(Roles="SystemAdmin")]
- Header: "Fraud Review — Flags chờ xử lý"
- Filter: status dropdown (Pending default / Confirmed / Dismissed / Reviewed)
- Table:
  - Columns: Customer, Entity Type, RiskScore (badge: red ≥80, orange ≥50, green <50), Status, CreatedAt, Actions
  - Sort by RiskScore desc
  - Action buttons: "Chi tiết" (opens modal), "Confirm" (red), "Dismiss" (gray)
- Detail modal:
  - Risk factors (JSON pretty-printed)
  - Related entities: DeviceRegistration, Customer, SalesReferral/AppInstallAttribution
  - Side effects preview (what will happen on Confirm/Dismiss)
- Toast on confirm/dismiss success
```

### FraudStats.razor (ShopERP — v1.2 NEW)
```
@page "/admin/community/fraud-stats"
@attribute [Authorize(Roles="SystemAdmin")]
- Header: "Fraud Stats Dashboard"
- Cards: Pending count, Confirmed count (+ $ loss prevented), Dismissed count, Reviewed count
- Top 5 flagged customers table (name, flag count, last flag date)
- Auto-refresh every 30s
```

### Profile.razor (KhachLink) — additions
```
- Section: "Vai trò cộng tác viên"
  - If no roles: "Bạn chưa là cộng tác viên"
  - If has roles: badge list (Shipper / Salesman / Shop Owner) with active/inactive status
  - Link to Nearby Orders (if Shipper) / Sales Dashboard (if Salesman) / Wallet (if Shipper or Shop Owner)
- Section (v1.2 NEW — salesman only): "Fraud Flag Status"
  - If no flags: "Tài khoản tốt — không có flag"
  - If has flags: list with RiskScore + status + entity type
  - Note: "Nếu bạn cho rằng flag sai, liên hệ admin để review"
```

### Nav/Menu Entry Points (v1.2 NEW — G1-G4 FIX)

#### ShopERP AdminLayout.razor — add to AdminMenuItems
```csharp
new() { Title = "Cộng tác viên", Icon = "people-fill", Url = "/admin/community/admin-panel" },
new() { Title = "Fraud Review", Icon = "shield-exclamation", Url = "/admin/community/fraud-flags" },
new() { Title = "Fraud Stats", Icon = "graph-up-arrow", Url = "/admin/community/fraud-stats" },
new() { Title = "Referral Configs", Icon = "diagram-3", Url = "/admin/product-referral-configs" }, // Sprint 4 debt
```

#### ShopERP NavMenu.razor — add Community section under SystemAdmin AuthorizeView
```razor
<AuthorizeView Roles="SystemAdmin">
    <Authorized>
        <div class="nav-section-label px-3 mt-2 mb-1 ...">Cộng đồng</div>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="admin/community/admin-panel">
                <span class="bi bi-people-fill-nav-menu"></span> Quản lý cộng tác viên
            </NavLink>
        </div>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="admin/community/fraud-flags">
                <span class="bi bi-shield-exclamation-nav-menu"></span> Fraud Review
            </NavLink>
        </div>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="admin/community/fraud-stats">
                <span class="bi bi-graph-up-arrow-nav-menu"></span> Fraud Stats
            </NavLink>
        </div>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="admin/product-referral-configs">
                <span class="bi bi-diagram-3-nav-menu"></span> Referral Configs
            </NavLink>
        </div>
    </Authorized>
</AuthorizeView>
```

#### KhachLink NavMenu.razor — add _isShopOwner + wallet link
```razor
@code {
    private bool _isShopOwner = false; // NEW
    // In OnAfterRenderAsync:
    _isShopOwner = role?.IsShopOwner ?? false; // NEW — need IsShopOwner in GetRoleAsync response
}

// Desktop sidebar — wallet link for shipper OR shop owner:
@if (_isShipper || _isShopOwner)
{
    <div class="nav-item">
        <NavLink class="nav-link" href="/community/wallet">
            <span class="bi bi-wallet2 me-2"></span> Ví cộng tác viên
        </NavLink>
    </div>
}

// Mobile bottom tab — same condition
```

---

## 5. LEGAL DOCUMENTS OUTLINE

### 5.1 community-terms-of-service.md
- Giới thiệu nền tảng
- Điều kiện tham gia (IdentityLevel, LoyaltyPoints)
- Quyền và nghĩa vụ cộng tác viên
- Quyền và nghĩa vụ nền tảng
- Cơ chế tính hoa hồng + thanh toán
- Xử lý vi phạm
- Giải quyết tranh chấp
- Terminsation

### 5.2 community-privacy-policy.md
- Dữ liệu thu thập (SĐT, email, GPS location)
- Mục đích sử dụng
- Chia sẻ dữ liệu (shop, shipper, customer)
- Quyền của người dùng (xem, xóa, chỉnh sửa)
- Bảo mật dữ liệu
- Lưu trữ và xóa dữ liệu
- Cookies và tracking
- **v1.2 NEW: Device fingerprint consent clause** — FingerprintJS v5.2.0 thu thập device fingerprint cho anti-fraud, user đồng ý khi đăng ký cộng tác viên
- Theo Nghị định 13/2023/NĐ-CP

### 5.3 marketplace-policy.md
- Phạm vi hoạt động sàn TMĐT
- Điều kiện tham gia (cửa hàng, cộng tác viên)
- Quản lý nội dung/sản phẩm
- Cơ chế đánh giá
- Xử lý khiếu nại
- Bảo vệ người tiêu dùng
- Phí và hoa hồng
- Theo Thông tư 39/TT-BCT (sàn TMĐT)

### 5.4 anti-fraud-policy.md (v1.2 NEW)
- Mục đích: chống gian lận hoa hồng + self-deal + device spoofing
- Device fingerprint: thu thập qua FingerprintJS, consent tại đăng ký
- FraudFlag workflow: Pending → Confirmed/Dismissed/Reviewed
- 3-strike ban: 3 FraudFlag Confirmed → auto-ban (IsActive=false)
- Hold 48h: commission hold 24h cooling + 48h timeout (Sprint 4)
- KYC bank account: yêu cầu cho payout (withdrawal Sprint 7+)
- Quyền khiếu nại: user có thể request review qua admin
- Reversal: commission/bonus đã pay → Reversal wallet transaction
- Theo Nghị định 13/2023/NĐ-CP (data protection) + Thông tư 39/TT-BCT

---

## 6. CODING PLAN — 4 SESSIONS (v1.2: +Fraud Review session)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service + tests | CommunityAdminService + 8 unit tests + FraudReviewService + 6 unit tests (14 total) |
| **S2** | Controllers + DI | CommunityAdminController + FraudFlagController (6 endpoints) + GET /api/community/my-fraud-flags + DI registration + GET /api/customer-identity/me modified (add communityRoles) |
| **S3** | UI + nav/menu (v1.2) | AdminPanel.razor + FraudFlags.razor + FraudStats.razor + Profile.razor (roles + fraud status) + ShopERP AdminLayout + NavMenu (Community section, 4 links) + KhachLink NavMenu (_isShopOwner + wallet link) |
| **S4** | Legal docs + full regression | 4 legal documents (ToS + Privacy + Marketplace + Anti-Fraud) + community-full-regression.spec.ts + community-fraud-review.spec.ts + guard-check + build |

---

## 7. VPS VERIFICATION (Sprint 6 — FULL REGRESSION)

| # | Test | Expected |
|---|---|---|
| RV6-1 | Admin eligible | 200 + customer list |
| RV6-2 | Activate role | 200 + CommunityRole |
| RV6-3 | Profile roles | 200 + roles array |
| RV6-4 | Fraud flags list (v1.2) | 200 + fraud flag list (401 no-admin-token) |
| RV6-5 | Fraud stats (v1.2) | 200 + stats object (401 no-admin-token) |
| RV6-6 | My fraud flags (v1.2) | 401 no-token (salesman self-view endpoint) |
| RV6-7 | ShopERP admin nav (v1.2 G2) | AdminLayout + NavMenu có Community section với 4 links |
| RV6-8 | KhachLink shop owner wallet (v1.2 G1/G4) | NavMenu có wallet link cho shop owner |
| RV6-9 | Full E2E regression | `npx playwright test e2e-tests/community-*.spec.ts` ALL PASS |
| RV6-10 | guard-check | ALL PASSED |
| RV6-11 | Architecture tests | ALL PASS |

---

## 8. FINAL DELIVERABLES CHECKLIST

- [ ] 7 sprint task cards (task_cc_sprint0-6)
- [ ] 7 sprint detailed plans (sprint0-6)
- [ ] 7 entity classes in Domain.cs
- [ ] 7 EF Configuration files
- [ ] PG + SQLite migrations applied
- [ ] 20+ API endpoints (community + admin + fraud review)
- [ ] 2 SignalR hubs (LocationHub, ChatHub)
- [ ] 8+ KhachLink pages (NearbyOrders, DeliveryTracking, OrderTracking, ChatPanel, NearbyProducts, SalesmanQR, SalesDashboard, Wallet)
- [ ] 4 ShopERP admin pages (AdminPanel, FraudFlags, FraudStats, ProductReferralConfigs) — all with nav links
- [ ] Nav entry points: ShopERP AdminLayout + NavMenu (Community section) + KhachLink NavMenu (_isShopOwner + wallet)
- [ ] Leaflet map component
- [ ] QR generation + scan referral
- [ ] 66+ unit tests across all sprints (52 existing + 14 Sprint 6)
- [ ] 7 E2E spec files
- [ ] 4 legal documents (ToS + Privacy + Marketplace + Anti-Fraud)
- [ ] CI/CD pipeline with VPS verification
- [ ] Full VPS regression PASS
