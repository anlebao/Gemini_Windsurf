# Sprint 6 Detailed Plan — Admin + Polish + Legal

TDD plan (8 test cases), coding plan (3 sessions), admin API spec, legal document outline, full regression checklist.

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

---

## 3. TDD PLAN (8 TEST CASES)

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

---

## 4. UI SPECS

### AdminPanel.razor (ShopERP)
```
@page "/community/admin"
@attribute [Authorize(Roles="Owner")]
- Header: "Quản lý cộng tác viên"
- Table:
  - Columns: Tên, SĐT, Điểm, Identity Level, Roles hiện tại, Actions
  - Action buttons: "Kích hoạt Shipper" / "Kích hoạt Salesman" / "Hủy role"
- Search box: by name or phone
- Pagination
- Toast on activate/deactivate success
```

### Profile.razor (KhachLink) — additions
```
- Section: "Vai trò cộng tác viên"
  - If no roles: "Bạn chưa là cộng tác viên"
  - If has roles: badge list (Shipper / Salesman) with active status
  - Link to Nearby Orders (if Shipper) / Sales Dashboard (if Salesman)
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

---

## 6. CODING PLAN — 3 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service + tests | CommunityAdminService + 8 unit tests |
| **S2** | Controller + Profile + AdminPanel + push | CommunityAdminController + Profile roles + AdminPanel.razor + push notification |
| **S3** | Legal docs + full regression | 3 legal documents + community-full-regression.spec.ts + guard-check + build |

---

## 7. VPS VERIFICATION (Sprint 6 — FULL REGRESSION)

| # | Test | Expected |
|---|---|---|
| RV6-1 | Admin eligible | 200 + customer list |
| RV6-2 | Activate role | 200 + CommunityRole |
| RV6-3 | Profile roles | 200 + roles array |
| RV6-4 | Full E2E regression | `npx playwright test e2e-tests/community-*.spec.ts` ALL PASS |
| RV6-5 | guard-check | ALL PASSED |
| RV6-6 | Architecture tests | ALL PASS |

---

## 8. FINAL DELIVERABLES CHECKLIST

- [ ] 7 sprint task cards (task_cc_sprint0-6)
- [ ] 7 sprint detailed plans (sprint0-6)
- [ ] 7 entity classes in Domain.cs
- [ ] 7 EF Configuration files
- [ ] PG + SQLite migrations applied
- [ ] 15+ API endpoints (community + admin)
- [ ] 2 SignalR hubs (LocationHub, ChatHub)
- [ ] 8+ KhachLink pages (NearbyOrders, DeliveryTracking, OrderTracking, ChatPanel, NearbyProducts, SalesmanQR, SalesDashboard, Wallet)
- [ ] 1 ShopERP admin page (AdminPanel)
- [ ] Leaflet map component
- [ ] QR generation + scan referral
- [ ] 60+ unit tests across all sprints
- [ ] 7 E2E spec files
- [ ] 3 legal documents
- [ ] CI/CD pipeline with VPS verification
- [ ] Full VPS regression PASS
