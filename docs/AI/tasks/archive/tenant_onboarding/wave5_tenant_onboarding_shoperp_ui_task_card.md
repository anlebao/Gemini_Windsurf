# TASK CARD: Tenant Onboarding - Wave 5 - ShopERP Admin UI

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cung cấp UI trong ShopERP để SystemAdmin tạo tenant onboarding với chọn ngành
- **Nghiệp vụ áp dụng:** Quản trị viên tạo tenant mới cho khách hàng F&B
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave5-shoperp-ui`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 5 of 6
- **Dependency:** Wave 4 must be merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave4_tenant_onboarding_gateway_api_task_card.md` (READ)
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor.cs` (UPDATE nếu có)
- `5_WebApps/ShopERP/Program.cs` (UPDATE - register HttpClient/DI nếu cần)
- `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` (READ - để cập nhật link nếu cần)
- `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` (READ - nếu cần thêm assertion)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa CoreHub service trong wave này
- KHÔNG bypass UI Platform components
- KHÔNG hardcode Gateway URL — dùng IConfiguration
- KHÔNG lưu password ở client

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** MUST use `VanAButton`, `VanACard`, `VanAForm`, `VanAInput`, `VanASpinner`, `VanAAlert`
- [ ] **Authorization:** Page yêu cầu `SystemAdmin` policy
- [ ] **Form Validation:** Validate required fields, email format, password strength
- [ ] **Password Handling:** Mask password input, không log
- [ ] **Industry Selection:** Dropdown với các industry codes đã implement (chỉ F&B enabled)
- [ ] **Error Handling:** Hiển thị lỗi rõ ràng từ API
- [ ] **Success Feedback:** Hiển thị kết quả onboarding (tenant id, owner username, counts)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** TenantManagement page có button "Tạo Tenant + Onboarding"
- [ ] **SC2:** Modal form cho onboarding với fields: tenant info, industry, owner info
- [ ] **SC3:** Industry dropdown chỉ hiển thị F&B (và các stub labels nếu cần)
- [ ] **SC4:** Form validation hoạt động
- [ ] **SC5:** Submit gọi Gateway API `POST /api/v1/onboarding/tenants`
- [ ] **SC6:** Hiển thị kết quả thành công
- [ ] **SC7:** Hiển thị lỗi từ API
- [ ] **SC8:** Build: 0 errors
- [ ] **SC9:** No regression in existing tests

---

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure UI uses Platform components
- `build-error-analysis` — Verify ShopERP build passes
- `test-system-upgrade` — Add/update startup tests if needed

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `TenantManagement.razor` exists và hiển thị list tenants
  - Fact 2: `SystemAdmin` policy đã có trong ShopERP authorization
  - Fact 3: UI Platform components được sử dụng trong các trang Admin khác
  - Fact 4: `HttpClient` với tên "gateway" hoặc tương tự có thể đã được đăng ký
- **Assumptions:**
  - ShopERP sẽ gọi Gateway API (HTTP) thay vì trực tiếp CoreHub service
  - Có thể mở modal trong `TenantManagement.razor` hoặc tạo page mới
- **Open Questions:**
  - Q1: Nên cập nhật `TenantManagement.razor` hay tạo page `TenantOnboarding.razor` riêng?
  - Q2: Có cần hiển thị password tạm thời cho owner trong response?
  - Q3: Có cần pre-fill default values (address, phone) không?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | UPDATE - add onboarding UI | Use modal, keep existing list |
| `5_WebApps/ShopERP/Program.cs` | UPDATE - ensure HttpClient registered | Reuse existing gateway client if any |
| `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | Possibly READ | Không thêm service mới trong ShopERP DI |

---

## 9. TDD & TESTING STRATEGY
- **Manual tests:**
  - SystemAdmin mở Tenant Management
  - Click "Tạo Tenant + Onboarding"
  - Điền form F&B
  - Submit
  - Verify new tenant appears in list
  - Verify owner can login
- **Unit tests:** Có thể không cần (UI logic đơn giản)
- **Integration tests:** Không trong wave này
- **E2E tests:** Không trong wave này (Playwright disabled until Wave 6)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt UI approach (modal vs new page)<br>- Chốt form fields<br>- Chốt API client | - Update TenantManagement.razor<br>- Add modal form<br>- Add API call<br>- Run build |
| **S2** (nếu cần) | - Chốt styling và responsive | - Polish UI<br>- Test manually |

---

## 11. DETAILED CODING STEPS

### 11.1 Form Fields
- Tenant Name (required)
- Business Type (dropdown: Company, HouseholdBusiness)
- HKD Group (dropdown if HouseholdBusiness)
- Contact Email
- Contact Phone
- Address
- Tax Code
- Industry Code (dropdown: F&B)
- Owner Username (required)
- Owner Password (required, masked)
- Owner Display Name (required)

### 11.2 UI Flow
1. SystemAdmin mở `/admin/tenants`
2. Click "+ Tạo Tenant + Onboarding"
3. Modal hiện form
4. Submit → call Gateway API
5. Hiển thị result card (tenant id, owner username, số products/ingredients/groups created)
6. Đóng modal, refresh tenant list

### 11.3 API Client
```csharp
var response = await HttpClient.PostAsJsonAsync(
    "api/v1/onboarding/tenants", 
    request);
```

### 11.4 Validation
- Required fields
- Email format (nếu provided)
- Password minimum 8 characters
- Username không trùng (handled by API)

---

## 12. EXIT CHECKLIST
- [ ] UI implemented
- [ ] Manual test successful
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[WAVE 5] Tenant onboarding ShopERP UI`
- [ ] Ready for Wave 6
