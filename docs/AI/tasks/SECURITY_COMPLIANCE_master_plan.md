# MASTER IMPLEMENTATION PLAN — Security & Compliance Gaps
# VanAn Ecosystem — Real Product Hardening

**Created:** 2026-06-23
**Last Updated:** 2026-06-23
**Current Status:** COMPLETED — Wave 2 merged (PR #40), Wave 3 ready to start
**Branch strategy:** feature branch per wave → PR → merge vào `main`
**Execution principle:** Wave-by-wave sequential. Wave N không bắt đầu khi Wave N-1 chưa pass exit criteria. Mỗi wave là 1 PR độc lập.

---

## 0. EXECUTION RULES

### Session protocol
1. **Đọc `docs/AI/project_state.md` + task card của wave đang active TRƯỚC KHI viết bất kỳ dòng code nào.**
2. **Chạy `dotnet build VanAn.sln` trước khi bắt đầu và sau khi kết thúc session — 0 errors bắt buộc.**
3. **Chỉ sửa files nằm trong "Files được phép" của task card đang active — không drift sang module khác.**
4. **Sau mỗi micro-phase: commit intermediate, ghi rõ `[WaveX-SY]` trong commit message.**
5. **Nếu phát sinh compile error > 5: STOP, ghi vào investigation_log.md, hỏi user trước khi tiếp tục.**

### Branch protocol
```
main
    └── feature/wave0-jwt-auth           (Wave 0)
    └── feature/wave1-notifications      (Wave 1)
    └── feature/wave2-data-protection    (Wave 2)
    └── feature/wave3-report-export      (Wave 3)
    └── feature/wave4-rbac-ui            (Wave 4)
    └── feature/wave5-tenant-mgmt        (Wave 5 — NEW)
    └── feature/wave6-user-rbac-mgmt     (Wave 6 — NEW)
    └── feature/wave7-prod-hardening     (Wave 7)
```
- Mỗi wave tạo branch từ `main` (sau khi wave trước đã merge).
- KHÔNG merge wave sau khi wave trước chưa pass exit criteria.
- PR description phải link task card tương ứng.
- Squash merge để giữ lịch sử sạch.

### Hard rules (không violate)
- **Domain Layer Protection:** KHÔNG sửa `1_Shared/Domain.cs` để fix authentication/security. Nếu cần thêm field → báo cáo Domain Modeling Defect.
- **AccountingEntry Immutability:** Không ảnh hưởng tới immutable accounting entries trong bất kỳ wave nào.
- **Multi-tenancy:** Mọi thay đổi phải preserve `TenantId` filtering. Không bypass global query filter.
- **Architecture test phải PASS:** `6_Tests/VanAn.Architecture.Tests` phải green sau mỗi wave.
- **guard-check.ps1 phải PASS:** Chạy trước mỗi PR.

---

## 1. WAVE 0 — JWT Authentication Foundation

**Branch:** `feature/wave0-jwt-auth`
**Estimated sessions:** 3
**Priority:** 🔴 CRITICAL — Mọi wave sau đều depend vào JWT claims
**Conflict risk:** HIGH — `Program.cs` (Gateway + ShopERP), `Login.cshtml.cs`, `HttpContextTenantProvider.cs`

### Vấn đề cụ thể cần fix
- `Login.cshtml.cs`: So sánh password plain text (`Password == "VanAn@2026"`) — **không chấp nhận được trên production**
- `2_Gateway/Program.cs`: `AddAuthentication()` chỉ có Cookie, không có `AddJwtBearer` → API không validate JWT
- `5_WebApps/ShopERP/Program.cs`: OIDC config trỏ `Authority = "https://localhost:5001"` — không có Identity Server thực
- `DevLoginController.cs`: Tồn tại nhưng cần giữ lại cho E2E tests (chỉ Development env)

### Quyết định kiến trúc (đã confirm với user)
JWT Bearer stateless: ShopERP tự issue JWT sau khi verify credentials → Gateway validate JWT → claims chứa `tenant_id` + `role`.
Không cần external Identity Server cho MVP — dùng `Microsoft.AspNetCore.Authentication.JwtBearer` + symmetric key từ config.

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 1 | W0-T1 | Thêm `Microsoft.AspNetCore.Authentication.JwtBearer` + `BCrypt.Net-Next` vào `Directory.Packages.props` + các `.csproj` liên quan | — | [W0-T1-card.md](#) | ✅ DONE |
| 2 | W0-T2 | Implement `JwtTokenService` — issue JWT với claims: `sub`, `role`, `tenant_id`, `exp` | W0-T1 | [W0-T2-card.md](#) | ✅ DONE |
| 3 | W0-T3 | Migrate `Login.cshtml.cs`: thay plain-text compare bằng BCrypt verify → issue JWT → set Cookie chứa JWT | W0-T2 | [W0-T3-card.md](#) | ✅ DONE |
| 4 | W0-T4 | Add `AddJwtBearer` vào `2_Gateway/Program.cs` — validate JWT từ ShopERP | W0-T2 | [W0-T4-card.md](#) | ✅ DONE |
| 5 | W0-T5 | Seed `DemoUser.PasswordHash` trong ShopERP `Program.cs` bằng BCrypt hash (thay Guid hardcode) | W0-T3 | [W0-T5-card.md](#) | ✅ DONE |
| 6 | W0-T6 | Viết unit tests: `JwtTokenServiceTests` (valid/expired/tampered token), `LoginPasswordTests` (hash verify) | W0-T3 | [W0-T6-card.md](#) | ✅ DONE |
| 7 | W0-T7 | Cập nhật E2E Playwright: `DevLoginController` giữ nguyên (Development only), update `dev/login` trả về JWT token để E2E tests dùng | W0-T4 | [W0-T7-card.md](#) | ✅ DONE |

### Entry criteria (Wave 0)
- [ ] Branch `feature/wave0-jwt-auth` tạo từ `main` mới nhất
- [ ] `dotnet build VanAn.sln` → 0 errors trên branch hiện tại
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 0) — TẤT CẢ phải PASS trước khi merge
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: không có test nào bị break thêm
- [ ] Unit test mới: `JwtTokenServiceTests` — minimum 5 test cases PASS
- [ ] Manual smoke: POST `/dev/login` → nhận JWT → GET `/api/orders` với Bearer header → 200 OK
- [ ] Manual smoke: GET `/api/orders` không có Bearer header → 401 Unauthorized
- [ ] Manual smoke: GET `/api/orders` với JWT của tenant A → chỉ thấy data tenant A

### Why first
- JWT claims là foundation để tất cả các wave sau có thể dùng `tenant_id` claim một cách đúng nghĩa
- BCrypt hash là prerequisite của Wave 3 (Data Protection) — không thể encrypt fields nếu password vẫn plain text
- Gateway JWT validation là prerequisite của Wave 5 (RBAC enforcement ở UI)
- Risk cao nhất về breaking changes → giải quyết sớm nhất

---

## 2. WAVE 1 — Notification Integration (Brevo Email + ESMS SMS)

**Branch:** `feature/wave1-notifications`
**Estimated sessions:** 2
**Priority:** 🔴 CRITICAL — Welcome email, order notifications không hoạt động
**Conflict risk:** LOW — Chỉ sửa `NotificationService.cs` + thêm config, không đụng core flow
**Depends on:** Wave 0 (JWT xong để có authenticated calls nếu cần)

### Vấn đề cụ thể cần fix
- `NotificationService.SendEmailAsync()`: chỉ `Task.Delay(100)` — không gửi được email
- `NotificationService.SendSMSAsync()`: chỉ `Task.Delay(100)` — không gửi được SMS
- `CustomerOnboardingService`: gọi `SendEmailAsync` nhưng email không đi
- Không có retry, không có delivery tracking, không có template

### Quyết định kiến trúc (đã confirm với user)
- **Email:** Brevo (Sendinblue) — free 300 email/ngày, REST API, hỗ trợ template HTML
- **SMS:** ESMS.vn — provider SMS Vietnam phổ biến, API key-based, hỗ trợ Unicode tiếng Việt
- **Pattern:** `INotificationService` hiện tại giữ nguyên interface, chỉ swap implementation
- **Config:** API keys từ `appsettings.json` → `appsettings.Production.json` (secrets không commit)

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 8 | W1-T1 | Thêm `Brevo.Api` (hoặc dùng `HttpClient` thuần vì Brevo có REST API đơn giản) vào `Directory.Packages.props` | — | — | ✅ DONE |
| 9 | W1-T2 | Implement `BrevoEmailService` — gọi Brevo API, hỗ trợ HTML template, error handling, logging | W1-T1 | [W1-T2-card.md](#) | ✅ DONE |
| 10 | W1-T3 | Implement `EsmsNotificationService` — gọi ESMS API, Unicode SMS, retry 1 lần khi fail | W1-T1 | [W1-T3-card.md](#) | ✅ DONE |
| 11 | W1-T4 | Refactor `NotificationService` thành `CompositeNotificationService` — delegate sang Email/SMS service theo channel | W1-T2, W1-T3 | — | ✅ DONE |
| 12 | W1-T5 | Thêm `appsettings.Production.json` template cho Brevo + ESMS keys (values là placeholder `__REPLACE__`) | W1-T2 | — | ✅ DONE |
| 13 | W1-T6 | Viết unit tests với mock HttpClient: `BrevoEmailServiceTests` + `EsmsServiceTests` — verify request format, error path | W1-T2, W1-T3 | — | ✅ DONE |

### Entry criteria
- [ ] Wave 0 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave1-notifications` tạo từ updated `main`
- [ ] Có Brevo API key test + ESMS API key test (user cung cấp, lưu local `.env` không commit)

### Exit criteria — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Unit tests: `BrevoEmailServiceTests` minimum 4 cases (success, API error, invalid email, retry)
- [ ] Unit tests: `EsmsServiceTests` minimum 4 cases (success, API error, invalid phone, Unicode)
- [ ] Integration smoke: `CustomerOnboardingService.OnboardCustomerAsync()` → email thực sự được gửi đến test inbox
- [ ] Integration smoke: SMS thực sự nhận được trên SIM test
- [ ] Secrets KHÔNG commit vào git (verify với `git diff --name-only`)
- [ ] `guard-check.ps1` → PASS

### Why here (not later)
- Notification là blocking cho business operation — customer onboarding không hoạt động khi không có email
- Low conflict risk → an toàn làm ngay sau Wave 0
- Cần test integration thực với API keys → cần môi trường sạch từ Wave 0

---

## 3. WAVE 2 — Data Protection (Field-level Encryption)

**Branch:** `feature/wave2-data-protection`
**Estimated sessions:** 3
**Priority:** 🔴 CRITICAL — Sensitive PII không được encrypt là vi phạm data security
**Conflict risk:** MEDIUM — Sửa `VanAnDbContext.cs`, `DemoUser` configs, migration required
**Depends on:** Wave 0 (cần BCrypt hoàn thành trước, `AddDataProtection` cần JWT key infrastructure)

### Vấn đề cụ thể cần fix
- `DemoUser.PasswordHash` column: lưu BCrypt hash (sau Wave 0) — đây là encryption ở application layer, đủ cho password
- `Customer.PhoneNumber`, `Customer.Email`: PII fields — lưu plain text trong SQLite
- `Lead.PhoneNumber`, `Lead.Email`: PII fields — lưu plain text
- `FacebookLead`: data từ Facebook có thể chứa PII
- Không có `AddDataProtection` configuration nào trong codebase

### Quyết định kiến trúc (đã confirm với user)
Scope: **Column-level encryption với ASP.NET Core Data Protection API**
- Dùng `IDataProtectionProvider` + `ValueConverter<string, string>` trong EF Core configurations
- Protected data được stored as encrypted Base64 string trong DB column
- Keys được persist tới file system (local) / Azure Key Vault (production-ready path)
- **Chỉ encrypt:** `PhoneNumber`, `Email` trên `Customer`, `Lead`, `FacebookLead`, `DemoUser` (bổ sung)

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 14 | W2-T1 | `AddDataProtection()` vào `3_CoreHub/Program.cs` + `5_WebApps/ShopERP/Program.cs` — persist keys tới `./keys/` folder, configure application name | — | [W2-T1-card.md](#) | ✅ DONE |
| 15 | W2-T2 | Tạo `EncryptedStringConverter` — EF Core `ValueConverter<string, string>` dùng `IDataProtector` | W2-T1 | [W2-T2-card.md](#) | ✅ DONE |
| 16 | W2-T3 | Apply `EncryptedStringConverter` vào `CustomerConfiguration.cs`: `PhoneNumber`, `Email` | W2-T2 | [W2-T3-card.md](#) | ✅ DONE |
| 17 | W2-T4 | Apply `EncryptedStringConverter` vào `LeadConfiguration.cs` + `FacebookLeadConfiguration.cs`: `PhoneNumber`, `Email` | W2-T2 | [W2-T3-card.md](#) | ✅ DONE |
| 18 | W2-T5 | EF Core Migration: tạo migration mới để resize columns (encrypted values dài hơn — cần `HasMaxLength(500)`) | W2-T3, W2-T4 | — | ✅ DONE |
| 19 | W2-T6 | Data migration script: encrypt existing plain-text data nếu có trong dev DB | W2-T5 | — | ✅ DONE |
| 20 | W2-T7 | Viết integration tests: insert Customer với PII → verify raw DB value khác plain text → query trả về plain text đúng | W2-T3 | [W2-T7-card.md](#) | ✅ DONE |
| 21 | W2-T8 | Cập nhật `appsettings.Production.json`: `DataProtection:KeyDirectory`, `DataProtection:ApplicationName` | W2-T1 | — | ✅ DONE |

### Entry criteria
- [ ] Wave 0 + Wave 1 merged
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Integration tests không có regression

### Exit criteria Phase A — Data Protection setup
- [ ] `AddDataProtection()` registered trong ShopERP + CoreHub
- [ ] `EncryptedStringConverter` compile và unit test pass
- [ ] Keys được persist tới `./keys/` — không bị recreate mỗi restart

### Exit criteria Phase B — PII Encrypted
- [ ] Raw SQLite DB: `SELECT PhoneNumber FROM Customers` → thấy ciphertext (không phải plain số)
- [ ] API: `GET /api/customers/{id}` → trả về plain text phone/email đúng
- [ ] Integration test: `CustomerEncryptionTests` — minimum 6 cases PASS
- [ ] Migration apply thành công trên fresh DB
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Architecture tests: 7/7 PASS

### Why here (not Wave 0)
- Cần `AddDataProtection` key infrastructure hoạt động ổn định trước khi encrypt data
- Nếu key thay đổi sau Wave 0, encrypted data trong Wave 2 bị corrupt → Wave 2 phải là cuối cùng trong critical path
- Column resize migration cần DB trong trạng thái stable (sau Auth migration xong)

---

## 4. WAVE 3 — Report Export (Excel với EPPlus)

**Branch:** `feature/wave3-report-export`
**Estimated sessions:** 2
**Priority:** 🟠 HIGH — Kế toán/thuế VN bắt buộc cần xuất Excel
**Conflict risk:** LOW — Thêm mới hoàn toàn, không sửa core services
**Depends on:** Wave 0 (cần JWT để protect export endpoints)

### Vấn đề cụ thể cần fix
- `HKDTaxReportingService.ExportToExcelAsync()`: trả về mock content với comment "would use Excel library"
- `HKDTaxReportingService.ExportToPdfAsync()`: mock content
- Không có API controller endpoint nào expose export
- File `export-excel-flow.spec.ts` tồn tại trong E2E tests nhưng test stub

### Quyết định kiến trúc (đã confirm với user)
- **Excel:** EPPlus (LGPL license) — chuẩn cho kế toán VN, hỗ trợ formatting, formula, multiple sheets
- **PDF:** Không triển khai trong wave này (out of scope per user choice)
- **Endpoint:** Thêm `GET /api/reports/export/excel?type={revenue|inventory|customer}&from={date}&to={date}` vào Gateway
- **Auth:** `[Authorize(Policy = "RequireTenantAccess")]` + `RequireRole("Owner", "StoreKeeper")`

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 22 | W3-T1 | Thêm `EPPlus` vào `Directory.Packages.props` + `VanAn.CoreHub.csproj` (EPPlus 7.x, LGPL mode) | — | — | ⬜ PENDING |
| 23 | W3-T2 | Tạo `IExcelExportService` interface + `ExcelExportService` implementation — 3 loại báo cáo: Revenue, Inventory, Customer | W3-T1 | [W3-T2-card.md](#) | ⬜ PENDING |
| 24 | W3-T3 | Implement `RevenueExcelReport`: sheet 1 = summary, sheet 2 = detail by order, VND formatting, date range filter | W3-T2 | — | ⬜ PENDING |
| 25 | W3-T4 | Implement `InventoryExcelReport`: tồn kho theo nguyên liệu, low-stock highlight (red), số lượng + đơn vị | W3-T2 | — | ⬜ PENDING |
| 26 | W3-T5 | Implement `CustomerExcelReport`: danh sách khách hàng, điểm loyalty, tier, tổng chi tiêu, last order date | W3-T2 | — | ⬜ PENDING |
| 27 | W3-T6 | Thêm `ReportController` vào `2_Gateway/Controllers/` — endpoint export với JWT auth + tenant isolation | W3-T2 | [W3-T6-card.md](#) | ⬜ PENDING |
| 28 | W3-T7 | Update `6_Testing/e2e-tests/export-excel-flow.spec.ts`: thay stub bằng real test — download file, verify Content-Type `application/vnd.openxmlformats` | W3-T6 | — | ⬜ PENDING |
| 29 | W3-T8 | Viết unit tests: `ExcelExportServiceTests` — verify file bytes > 0, correct sheet names, correct column headers | W3-T2 | — | ⬜ PENDING |

### Entry criteria
- [ ] Wave 0 merged (JWT auth cho endpoint protection)
- [ ] `dotnet build VanAn.sln` → 0 errors

### Exit criteria Phase A — Export service
- [ ] `ExcelExportService` compile và unit tests PASS
- [ ] Revenue report: file Excel mở được, có đúng 2 sheets, VND format đúng
- [ ] Inventory report: low-stock rows highlighted
- [ ] Customer report: tất cả columns có data

### Exit criteria Phase B — API endpoint
- [ ] `GET /api/reports/export/excel?type=revenue` với JWT Owner → 200 + file download
- [ ] `GET /api/reports/export/excel?type=revenue` không có JWT → 401
- [ ] `GET /api/reports/export/excel?type=revenue` với JWT Staff → 403
- [ ] Tenant isolation: JWT tenant A không thấy data tenant B trong export
- [ ] E2E test `export-excel-flow.spec.ts`: minimum 3 cases PASS
- [ ] `guard-check.ps1` → PASS

### Why here (not Wave 1)
- EPPlus không conflict với bất kỳ thứ gì đang build
- Report endpoint cần JWT protection từ Wave 0
- Low risk → có thể làm song song với Wave 2 về mặt logic, nhưng sequential để dễ debug

---

## 5. WAVE 4 — RBAC Enforcement tại Blazor UI Layer

**Branch:** `feature/wave4-rbac-ui`
**Estimated sessions:** 2
**Priority:** 🟠 HIGH — Role policies đã định nghĩa nhưng không được enforce ở UI
**Conflict risk:** MEDIUM — Sửa Blazor pages/components trong `5_WebApps/ShopERP`
**Depends on:** Wave 0 (JWT + role claims), Wave 2 (auth state stable)

### Vấn đề cụ thể cần fix
- Tất cả Blazor `.razor` components không có `<AuthorizeView>` hay `[Authorize(Policy="...")]`
- User với role `Staff` vẫn có thể access các trang chỉ dành cho `Owner`
- Không có UI feedback khi unauthorized (redirect về 403 page)

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 30 | W4-T1 | Audit tất cả `.razor` pages trong `5_WebApps/ShopERP/Components/` — map từng page với required role | — | [W4-T1-card.md](#) | ⬜ PENDING |
| 31 | W4-T2 | Thêm `[Authorize(Policy = "OwnerOnly")]` vào accounting/report pages. Thêm `[Authorize(Policy = "StoreManagement")]` vào inventory pages | W4-T1 | — | ⬜ PENDING |
| 32 | W4-T3 | Wrap navigation menu items bằng `<AuthorizeView Roles="Owner,StoreKeeper">` — ẩn menu theo role | W4-T1 | — | ⬜ PENDING |
| 33 | W4-T4 | Tạo `403-AccessDenied.razor` page — hiện thông báo + link về home | W4-T2 | — | ⬜ PENDING |
| 34 | W4-T5 | Cập nhật `Login.cshtml.cs` redirect logic: role-based redirect sau login (Staff → KDS, Owner → Dashboard) | W4-T1 | — | ⬜ PENDING |
| 35 | W4-T6 | Viết E2E tests: login Staff → thử access Owner-only page → verify redirect 403. Login Owner → access OK | W4-T2 | [W4-T6-card.md](#) | ⬜ PENDING |

### Entry criteria
- [ ] Wave 0 merged (JWT + role claims working)
- [ ] Wave 2 merged (auth state stable)

### Exit criteria Phase 4
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] E2E test: Staff không access được Accounting page
- [ ] E2E test: Owner access tất cả pages
- [ ] E2E test: Guard chỉ access Guard/Scan
- [ ] Navigation menu: Staff không thấy menu items của Owner
- [ ] `guard-check.ps1` → PASS
- [ ] Architecture tests: 7/7 PASS

### Why here (not Wave 0)
- Cần JWT claims (Wave 0) ổn định trước để `AuthorizeView` evaluate đúng
- Cần Data Protection (Wave 2) xong trước để không có regression khi auth state thay đổi
- UI changes là low-risk về core functionality

---

## 6. WAVE 5 — Domain Refactor (God File Split) + Tenant Rich Domain Model + Tenant CRUD

**Branch:** `feature/wave5-tenant-mgmt`
**Estimated sessions:** 4 (tăng từ 3 vì có Domain phase)
**Priority:** 🔴 CRITICAL — God File `Domain.cs` là Technical Debt + Tenant không có CRUD
**Conflict risk:** HIGH — Sửa `1_Shared/Domain.cs` + `TenantConfiguration.cs` + `VanAnDbContext.cs`
**Depends on:** Wave 0 (JWT), Wave 1 (Brevo email)
**Domain Decision D1:** ✅ APPROVED — Option B mở rộng (Rich Domain Model + God File split)

### Vấn đề cụ thể cần fix (updated sau phán quyết D1)
- `Domain.cs` — **2,050+ lines, 79 types trong 1 file** — God File Anti-Pattern cần tách
- `record Tenant` — immutable, không có vòng đời, không có domain methods → phải convert sang `class`
- Không có `AggregateRoot` base class, không có `IDomainEvent` interface — thiếu DDD foundation
- Không có `TenantStatus` enum (Active/Suspended/Inactive) — business states không được model
- Không có `TenantController`, `ITenantManagementService` — zero CRUD API

### Phạm vi tách file Domain.cs (Wave 5 scope)
Wave 5 chỉ tách **TenantAggregate** + thêm **DDD foundation** vào Common.cs. Các aggregate khác (User, Order, Invoice...) sẽ tách ở Wave 6 hoặc dedicated refactor wave sau.
```
WAVE 5 tạo mới:
  1_Shared/Domain/Common.cs              ← THÊM: AggregateRoot, IDomainEvent
  1_Shared/Domain/Aggregates/
    TenantAggregate/
      Tenant.cs                          ← MỚI: class thay record
      TenantStatus.cs                    ← MỚI: enum
      TenantSettings.cs                  ← MỚI: value object
      TenantEvents.cs                    ← MỚI: TenantCreatedEvent, TenantDeactivatedEvent

WAVE 5 sửa:
  1_Shared/Domain.cs                     ← [Obsolete] record Tenant (KHÔNG XÓA — tránh break)
  3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs  ← cập nhật EF mapping
  3_CoreHub/Infrastructure/VanAnDbContext.cs                       ← TenantStatus column
```

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 36 | W5-T1 | **Domain Phase:** Thêm `AggregateRoot` base + `IDomainEvent` interface vào `Common.cs` | — | [W5-T1-card.md](#) | ⬜ PENDING |
| 37 | W5-T2 | **Domain Phase:** Tạo `TenantAggregate/Tenant.cs` (class, Rich Domain) + `TenantStatus.cs` + `TenantSettings.cs` | W5-T1 | [W5-T2-card.md](#) | ⬜ PENDING |
| 38 | W5-T3 | **Domain Phase:** Tạo `TenantAggregate/TenantEvents.cs` — `TenantCreatedEvent`, `TenantDeactivatedEvent`, `TenantSuspendedEvent` | W5-T2 | — | ⬜ PENDING |
| 39 | W5-T4 | **Domain Phase:** Mark `record Tenant` trong `Domain.cs` là `[Obsolete]` + EF mapping update `TenantConfiguration.cs` + EF migration (TenantStatus column) | W5-T2 | [W5-T4-card.md](#) | ⬜ PENDING |
| 40 | W5-T5 | **Service Phase:** Implement `ITenantManagementService` + `TenantManagementService`: `CreateTenant`, `GetTenantById`, `ListTenants`, `UpdateProfile`, `Suspend`, `Deactivate` — dùng Tenant domain methods | W5-T4 | [W5-T5-card.md](#) | ⬜ PENDING |
| 41 | W5-T6 | **API Phase:** Tạo `TenantController` — `POST /api/tenants`, `GET /api/tenants`, `GET /api/tenants/{id}`, `PATCH /api/tenants/{id}/profile`, `POST /api/tenants/{id}/deactivate` — `[Authorize(Policy="SystemAdmin")]` | W5-T5 | [W5-T6-card.md](#) | ⬜ PENDING |
| 42 | W5-T7 | **Notification:** `TenantManagementService.CreateTenant()` → phát `TenantCreatedEvent` → handler gọi `INotificationService.SendEmailAsync()` (welcome email) | W5-T5, Wave 1 | [W5-T7-card.md](#) | ⬜ PENDING |
| 43 | W5-T8 | Email template `TenantWelcomeEmail.html` — tên tenant, link login, support contact | W5-T7 | — | ⬜ PENDING |
| 44 | W5-T9 | Blazor UI: `TenantManagement.razor` — list, create form, edit profile, deactivate/suspend buttons. `[Authorize(Policy="SystemAdmin")]` | W5-T6 | [W5-T9-card.md](#) | ⬜ PENDING |
| 45 | W5-T10 | Unit tests: `TenantTests` (domain methods: lifecycle transitions, guard clauses, event emission), `TenantManagementServiceTests` (CRUD, notification trigger, cross-tenant isolation) | W5-T5 | — | ⬜ PENDING |

### Entry criteria
- [ ] Wave 0 merged (JWT + `SystemAdmin` role claim working)
- [ ] Wave 1 merged (Brevo email functional)
- [ ] D1 ✅ APPROVED (done — xem §13b)
- [ ] `dotnet build VanAn.sln` → 0 errors trước khi bắt đầu

### Exit criteria — Phase A: Domain (W5-T1 → W5-T4)
- [ ] `AggregateRoot` + `IDomainEvent` compile clean
- [ ] `Tenant` class có đủ lifecycle methods: `Activate()`, `Suspend()`, `Deactivate()`
- [ ] `Deactivate()` từ `Suspended` → throws `DomainException` (guard)
- [ ] `TenantCreatedEvent`, `TenantDeactivatedEvent` được phát ra qua `AddDomainEvent()`
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `[Obsolete]` trên `record Tenant` cũ → không có compile error mới (chỉ warning)

### Exit criteria — Phase B: Service + API + UI (W5-T5 → W5-T10)
- [ ] `POST /api/tenants` + SystemAdmin JWT → 201 Created, tenant trong DB với `Status=Active`
- [ ] `POST /api/tenants/{id}/deactivate` với Guard JWT → 403 Forbidden
- [ ] `POST /api/tenants/{id}/deactivate` với SystemAdmin + tenant đang Suspended → 422 (DomainException propagated)
- [ ] Create tenant → welcome email gửi thành công (verify Brevo log)
- [ ] Domain tests: `TenantTests` minimum 10 cases PASS (lifecycle guards đặc biệt)
- [ ] Service tests: `TenantManagementServiceTests` minimum 8 cases PASS
- [ ] `guard-check.ps1` → PASS
- [ ] Architecture tests: 7/7 PASS
- [ ] Migration apply thành công trên fresh DB

### Why Domain phase first (W5-T1 → W5-T4 before API)
Domain phải stable trước khi Service layer dùng. Nếu viết Service trước khi Tenant có domain methods, Service sẽ lại chứa business logic — đúng cái lỗi Anemic Model đang phải fix.

---

## 7. WAVE 6 — UserAggregate Domain Phase + User CRUD + Permission Group

**Branch:** `feature/wave6-user-rbac-mgmt`
**Estimated sessions:** 5 (tăng từ 4 — có Domain phase tách UserAggregate ra file riêng)
**Priority:** 🔴 CRITICAL — Không thể gán role, tạo user qua UI là sản phẩm chưa hoàn thiện
**Conflict risk:** HIGH — Tiếp tục tách `Domain.cs` + sửa `DemoUserConfiguration.cs` + `Login.cshtml.cs`
**Depends on:** Wave 0 (JWT + BCrypt), Wave 5 (Tenant domain stable, `AggregateRoot` đã có)
**Domain Decision D2:** ✅ APPROVED — `PermissionGroup` bundle roles (không granular permissions)
**Domain Decision D3:** ✅ APPROVED — Giữ `DemoUser` trong Domain, DTO boundary dùng `UserDto`

### Vấn đề cụ thể cần fix
- `DemoUser`, `UserTenant`, `UserRole` vẫn nằm trong `Domain.cs` (God File) — cần tách ra `UserAggregate/`
- `DemoUser` là `BaseEntity` đơn giản, **không có domain methods** — không thể `Deactivate()`, không có guard
- `UserTenant.Role` là `string` thay vì typed enum — type-unsafe
- Không có `PermissionGroup` entity — không gom roles thành group được
- Admin không thể tạo user, reset password qua UI — phải can thiệp DB

### Phạm vi tách file Domain.cs (Wave 6 scope)
```
WAVE 6 tạo mới:
  1_Shared/Domain/Aggregates/
    UserAggregate/
      DemoUser.cs              ← MOVE + UPGRADE: thêm domain methods Deactivate(), ChangePassword()
      UserTenant.cs            ← MOVE + UPGRADE: Role từ string → UserRole enum
      UserRole.cs              ← MOVE: enum ra file riêng
      PermissionGroup.cs       ← MỚI: class bundle UserRoles
      UserPermissionGroup.cs   ← MỚI: mapping entity
      UserEvents.cs            ← MỚI: UserCreatedEvent, UserDeactivatedEvent

WAVE 6 sửa:
  1_Shared/Domain.cs           ← [Obsolete] DemoUser, UserTenant, UserRole
  3_CoreHub/Infrastructure/Configurations/DemoUserConfiguration.cs   ← update mapping
  3_CoreHub/Infrastructure/Configurations/UserTenantConfiguration.cs ← Role: string → enum
  3_CoreHub/Infrastructure/VanAnDbContext.cs                          ← thêm DbSet mới
  Migration mới: PermissionGroup + UserPermissionGroup tables
```

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 46 | W6-T1 | **Domain Phase:** Tạo `UserAggregate/DemoUser.cs` — class với domain methods: `Deactivate()`, `Reactivate()`, `ChangePassword(BCryptHash)`, `AssignRole()` + `UserCreatedEvent`, `UserDeactivatedEvent` | Wave 5 (AggregateRoot) | [W6-T1-card.md](#) | ⬜ PENDING |
| 47 | W6-T2 | **Domain Phase:** Tạo `UserAggregate/UserTenant.cs` (UserRole enum thay string), `UserAggregate/UserRole.cs`, `UserAggregate/PermissionGroup.cs`, `UserAggregate/UserPermissionGroup.cs` | W6-T1 | [W6-T2-card.md](#) | ⬜ PENDING |
| 48 | W6-T3 | **Domain Phase:** `[Obsolete]` mark `DemoUser`, `UserTenant`, `UserRole` trong `Domain.cs`. EF configurations update. Migration mới. | W6-T2 | [W6-T3-card.md](#) | ⬜ PENDING |
| 49 | W6-T4 | **Service Phase:** `IUserManagementService` + `UserManagementService`: `CreateUser` (BCrypt hash), `GetUser`, `ListUsers`, `UpdateProfile`, `ChangePassword`, `DeactivateUser`, `ReactivateUser` | W6-T1 | [W6-T4-card.md](#) | ⬜ PENDING |
| 50 | W6-T5 | **Service Phase:** `IRoleAssignmentService` + `RoleAssignmentService`: `AssignRoleToUser`, `RevokeRole`, `GetUserRoles`, `AssignToGroup`, `RemoveFromGroup` — cross-tenant guard bắt buộc | W6-T2, W6-T4 | [W6-T5-card.md](#) | ⬜ PENDING |
| 51 | W6-T6 | **Service Phase:** `IPermissionGroupService` + `PermissionGroupService`: `CreateGroup`, `UpdateGroup`, `AddRoleToGroup`, `RemoveRoleFromGroup`, `ListGroups`, `GetEffectiveRoles(userId)` | W6-T2 | [W6-T6-card.md](#) | ⬜ PENDING |
| 52 | W6-T7 | **API Phase:** `UserController` — `POST /api/users`, `GET /api/users`, `PATCH /api/users/{id}`, `POST /api/users/{id}/deactivate`, `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{role}` | W6-T4, W6-T5 | [W6-T7-card.md](#) | ⬜ PENDING |
| 53 | W6-T8 | **API Phase:** `PermissionGroupController` — `POST /api/permission-groups`, `GET /api/permission-groups`, `PATCH /api/permission-groups/{id}`, `POST /api/permission-groups/{id}/roles` | W6-T6 | — | ⬜ PENDING |
| 54 | W6-T9 | **Notification:** Create user → `UserCreatedEvent` → handler gọi `INotificationService.SendEmailAsync()` (welcome + link đổi password) | W6-T4, Wave 1 | — | ⬜ PENDING |
| 55 | W6-T10 | **UI Phase:** `UserManagement.razor` — list users, create form (role dropdown từ `UserRole` enum), assign to PermissionGroup, deactivate. `[Authorize(Policy="OwnerOnly")]` | W6-T7 | [W6-T10-card.md](#) | ⬜ PENDING |
| 56 | W6-T11 | **UI Phase:** `PermissionGroupManagement.razor` — CRUD groups, multi-select roles | W6-T8 | — | ⬜ PENDING |
| 57 | W6-T12 | **Tests:** `DemoUserDomainTests` (domain methods, guards, events), `UserManagementServiceTests` (CRUD, duplicate check, BCrypt), `RoleAssignmentServiceTests` (assign/revoke, cross-tenant) | W6-T4, W6-T5 | — | ⬜ PENDING |

### Entry criteria
- [ ] Wave 5 merged (`AggregateRoot` + `IDomainEvent` đã có, Tenant stable)
- [ ] Wave 0 merged (BCrypt, JWT)
- [ ] D2 + D3 ✅ APPROVED (done — xem §13b)
- [ ] `dotnet build VanAn.sln` → 0 errors

### Exit criteria — Phase A: Domain (W6-T1 → W6-T3)
- [ ] `DemoUser.Deactivate()` → guard: không deactivate user đang là Owner duy nhất của tenant
- [ ] `UserTenant.Role` là `UserRole` enum (không phải string)
- [ ] `PermissionGroup.GetEffectiveRoles()` trả về union của tất cả roles trong group
- [ ] `[Obsolete]` marks trên `Domain.cs` cũ → compile với warnings, không errors
- [ ] `dotnet build VanAn.sln` → 0 errors mới

### Exit criteria — Phase B: Service + API + UI (W6-T4 → W6-T12)
- [ ] `POST /api/users` với Owner JWT → 201, password stored as BCrypt hash
- [ ] `POST /api/users` duplicate username trong same tenant → 409 Conflict
- [ ] `POST /api/users/{id}/deactivate` — last Owner guard: 422 nếu xóa Owner cuối của tenant
- [ ] Cross-tenant: Owner tenant A assign user vào tenant B → 403 Forbidden
- [ ] `GetEffectiveRoles(userId)` khi user thuộc 2 groups → union roles không trùng
- [ ] Create user → welcome email gửi (verify Brevo log)
- [ ] Domain tests: `DemoUserDomainTests` minimum 8 cases PASS
- [ ] Service tests: `UserManagementServiceTests` minimum 10 cases PASS
- [ ] `guard-check.ps1` → PASS
- [ ] Architecture tests: 7/7 PASS
- [ ] Migration apply thành công trên fresh DB

### Why Domain phase first
Cùng lý do như Wave 5: `DemoUser` phải có domain methods (`Deactivate`, `ChangePassword`) trước khi `UserManagementService` dùng — tránh logic rò ra Service layer. `UserTenant.Role` phải typed trước khi `RoleAssignmentService` assign role an toàn.

---

## 8. WAVE 7 — Production Hardening & Non-functional

**Branch:** `feature/wave7-prod-hardening`
**Estimated sessions:** 3
**Priority:** 🟡 MEDIUM
**Conflict risk:** LOW — Mostly config changes và infra additions
**Depends on:** Wave 0, Wave 6

### Tasks (priority order)
| # | Task ID | Task | Depends on |
|---|---|---|---|
| 54 | W7-T1 | **HTTPS Enforcement:** Bật `app.UseHttpsRedirection()` trong Gateway + ShopERP (đang bị comment `// Local-First`) — chỉ enable khi `ASPNETCORE_ENVIRONMENT == Production` | Wave 0 |
| 55 | W7-T2 | **CORS Hardening:** Thay `AllowAnyOrigin()` bằng whitelist domain cụ thể trong `appsettings.Production.json` | Wave 0 |
| 56 | W7-T3 | **Backup script:** Tạo `scripts/backup-db.sh` — SQLite WAL checkpoint + copy to `./backups/YYYY-MM-DD/` + retain 7 ngày | — |
| 57 | W7-T4 | **Health checks:** `AddHealthChecks().AddDbContextCheck<VanAnDbContext>()` + expose `/health/detail` endpoint (chỉ Owner role) | Wave 0 |
| 58 | W7-T5 | **Rate limiting:** `AddRateLimiter()` cho login endpoint (max 5 req/minute per IP) — chống brute force | Wave 0 |
| 59 | W7-T6 | **Distributed cache migration:** Conditional Redis — nếu có `Redis:ConnectionString` dùng Redis, otherwise fallback Memory | — |
| 60 | W7-T7 | **WCAG basic fixes:** Thêm `aria-label` vào Login form, Order form, User form, Tenant form | — |

---

## 9. FILE CONFLICT MATRIX (updated — 7 waves + Domain Split)

| File zone | W0 | W1 | W2 | W3 | W4 | W5 | W6 | W7 | Conflict mitigation |
|---|---|---|---|---|---|---|---|---|---|
| `1_Shared/Domain.cs` | — | — | — | — | — | ✏️`[Obs]` | ✏️`[Obs]` | — | W5+W6 chỉ THÊM `[Obsolete]`, KHÔNG xóa. Xóa thực ở Cleanup wave riêng |
| `1_Shared/Domain/Common.cs` | — | — | — | — | — | ✏️ | — | — | W5 thêm AggregateRoot+IDomainEvent — append only |
| `1_Shared/Domain/Aggregates/TenantAggregate/` | — | — | — | — | — | ✏️ NEW | — | — | W5 tạo toàn bộ folder mới — no conflict |
| `1_Shared/Domain/Aggregates/UserAggregate/` | — | — | — | — | — | — | ✏️ NEW | — | W6 tạo toàn bộ folder mới — no conflict |
| `2_Gateway/Program.cs` | ✏️ | — | — | — | — | — | — | ✏️ | W7 merge từ W0 result |
| `5_WebApps/ShopERP/Program.cs` | ✏️ | — | ✏️ | — | — | — | ✏️ | ✏️ | Sequential — đọc lại trước khi sửa |
| `5_WebApps/ShopERP/Pages/Login.cshtml.cs` | ✏️ | — | — | — | ✏️ | — | — | — | W4 chỉ sửa redirect logic |
| `3_CoreHub/Services/NotificationService.cs` | — | ✏️ | — | — | — | hook W5 | hook W6 | — | W5/W6 gọi interface — không sửa implementation |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | — | — | ✏️ | — | — | ✏️ | ✏️ | — | Sequential: W2→W5→W6 mỗi wave append DbSet/config |
| `3_CoreHub/Infrastructure/Configurations/` | — | — | ✏️ | — | — | ✏️ | ✏️ | — | Mỗi wave thêm file mới, update file liên quan |
| `2_Gateway/Controllers/` | — | — | — | ✏️ NEW | — | ✏️ NEW | ✏️ NEW | — | Mỗi wave thêm controller mới, không sửa cũ |
| `5_WebApps/ShopERP/Components/**/*.razor` | — | — | — | — | ✏️ | ✏️ NEW | ✏️ NEW | ✏️ | W4 sửa existing, W5/W6/W7 thêm page mới |
| `Directory.Packages.props` | ✏️ | ✏️ | — | ✏️ | — | — | — | ✏️ | Sequential — merge latest main trước khi add |

**Ghi chú quan trọng về Domain split:**
- `Domain.cs` **KHÔNG XÓA** trong Wave 5 hay Wave 6 — chỉ `[Obsolete]` mark types đã được move
- Các type mới trong `Domain/Aggregates/` dùng cùng namespace `VanAn.Shared.Domain` — không breaking
- Xóa thực sự `Domain.cs` types cũ chỉ sau khi toàn bộ consumer code đã migrate (Cleanup Wave riêng, sau Wave 7)

---

## 10. VISUAL TIMELINE (updated — 7 waves + Domain Split)

```
Tuần 1                    Tuần 2                    Tuần 3                    Tuần 4-5
│                         │                         │                         │
├── WAVE 0 (3 sessions)   │                         │                         │
│   JWT Auth Foundation   │                         │                         │
│   BCrypt passwords      │                         │                         │
│   ─────────────────►    │                         │                         │
│                         ├── WAVE 2 (3 sessions)   │                         │
│   WAVE 1 (2 sessions)   │   PII Encryption         │                         │
│   Brevo + ESMS          │   Data Protection        │                         │
│   ─────────────────►    │   ──────────────────►   │                         │
│                         │                         │                         │
│                         ├── WAVE 3 (2 sessions)   │                         │
│                         │   Report Export          │                         │
│                         │   EPPlus Excel           │                         │
│                         │   ─────────────────►    │                         │
│                         │                         ├── WAVE 4 (2 sessions)   │
│                         │                         │   RBAC UI Enforcement   │
│                         │                         │   ──────────────────►   │
│                         │                         │                         │
│                         │                         ├── WAVE 5 (4 sessions)   │
│                         │                         │   [Domain] TenantAggreg │
│                         │                         │   [Domain] God File     │
│                         │                         │   Tenant CRUD + Notif   │
│                         │                         │   ──────────────────────►
│                         │                         │                         ├── WAVE 6 (5 sessions)
│                         │                         │                         │   [Domain] UserAggreg
│                         │                         │                         │   User CRUD + Roles
│                         │                         │                         │   PermissionGroup
│                         │                         │                         │   ──────────────────►
│                         │                         │                         │
│                         │                         │                         ├── WAVE 7 (3 sessions)
│                         │                         │                         │   Prod Hardening
│
Critical path:     Wave 0 → Wave 2 → Wave 5 → Wave 6 → Wave 7
Secondary path:    Wave 0 → Wave 4 (RBAC UI — không block Wave 5)
Parallel possible: Wave 1 ∥ Wave 3 (sau Wave 0 xong)
                   Wave 4 ∥ Wave 5 Phase A/Domain (khác file zones hoàn toàn)
```

**Ghi chú:**
- Wave 5 tăng từ 3 → **4 sessions** (thêm Domain Phase: TenantAggregate + God File split)
- Wave 6 tăng từ 4 → **5 sessions** (thêm Domain Phase: UserAggregate split)
- Wave 4 (RBAC UI) có thể chạy song song với Wave 5 Domain Phase vì không đụng cùng file
- Tổng ước tính: **~22 sessions** (~4-5 tuần nếu 5 sessions/tuần)
- Domain cleanup (xóa types cũ khỏi Domain.cs) là **Wave 8 riêng** sau Wave 7 — không nằm trong plan này

---

## 11. SESSION CHECKLIST (cho mỗi session)

### Before session start
- [ ] `git pull origin main` — lấy code mới nhất
- [ ] `git checkout feature/waveX-...` — đúng branch đang làm
- [ ] Đọc `docs/AI/project_state.md` — nắm current objective
- [ ] Đọc task card của task đang implement
- [ ] `dotnet build VanAn.sln` → confirm 0 errors trước khi bắt đầu

### During session
- [ ] Chỉ sửa files nằm trong "Files được phép" của task card
- [ ] Sau mỗi micro-phase: `dotnet build` — không để lỗi tích lũy
- [ ] Commit intermediate với message format: `[W0-T2-S1] implement JwtTokenService - issue claims`

### Before session end
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `dotnet test 6_Tests/VanAn.Architecture.Tests` → 7/7 PASS
- [ ] `dotnet test` cho test project liên quan đến wave đang làm
- [ ] `./guard-check.ps1` → PASS
- [ ] Commit tất cả changes chưa commit
- [ ] Update `docs/AI/project_state.md` → ghi task đã xong, task tiếp theo
- [ ] Push branch lên remote

---

## 12. ROLLBACK PLAN

Nếu wave fail/conflict không resolve:

1. **Không merge** wave có vấn đề — giữ trên feature branch
2. `git stash` hoặc `git checkout -- .` nếu mid-session fail
3. Nếu migration đã apply mà có bug: tạo **reversal migration** (KHÔNG `dotnet ef database drop`)
4. Nếu Wave 2 (encryption) gây data corruption: feature branch vẫn independent — chỉ ảnh hưởng dev DB
5. Nếu Wave 0 (JWT) break E2E tests: `DevLoginController` giữ nguyên là safety net cho E2E
6. Emergency: `git revert <merge-commit>` trên `main` nếu merge đã xảy ra

---

## 13. PACKAGES + DOMAIN DECISIONS

### 13a. PACKAGES CẦN THÊM (tóm tắt)

| Package | Version | Wave | Project |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.x | Wave 0 | `VanAn.Gateway`, `VanAn.ShopERP` |
| `BCrypt.Net-Next` | 4.0.x | Wave 0 | `VanAn.CoreHub`, `VanAn.ShopERP` |
| `Microsoft.IdentityModel.Tokens` | 7.x | Wave 0 | `VanAn.CoreHub` |
| `System.IdentityModel.Tokens.Jwt` | 7.x | Wave 0 | `VanAn.CoreHub` |
| `EPPlus` | 7.x (LGPL) | Wave 3 | `VanAn.CoreHub` |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 8.0.x | Wave 7 | `VanAn.CoreHub`, `VanAn.Gateway` |

> **Brevo + ESMS:** Dùng `HttpClient` thuần — không cần thêm NuGet package.
> **Wave 5 + Wave 6 (Tenant/User CRUD):** Không cần package mới — dùng EF Core + ASP.NET Core đã có sẵn.

---

### 13b. DOMAIN DECISIONS — LOG

| # | Wave | Decision | Verdict | Date |
|---|---|---|---|---|
| D1 | Wave 5 | `Tenant` record → Rich Domain Model như thế nào? | ✅ **APPROVED — Option B mở rộng** (xem chi tiết bên dưới) | 2026-06-23 |
| D2 | Wave 6 | Permission granularity | ✅ **APPROVED — Option A**: `PermissionGroup` bundle roles (MVP) | 2026-06-23 |
| D3 | Wave 6 | `DemoUser` rename | ✅ **APPROVED — Option A**: giữ nguyên tên Domain, DTO boundary dùng `UserDto` | 2026-06-23 |

---

### D1 — PHÁN QUYẾT TECH LEAD (2026-06-23): Tenant Rich Domain Model

**Verdict:** ❌ Option A (TenantProfile) bị bác. ✅ Option B mở rộng được duyệt.

**Lý do bác Option A (Anemic Domain Model trap):**
- `Tenant` là một Aggregate Root có vòng đời thực: Created → Active → Suspended → Inactive → Terminated
- Tách sang `TenantProfile` riêng vi phạm tính toàn vẹn: không ai bảo vệ bất biến "khi Deactivate → không cho sửa Profile"
- Business rules phải query 2 Aggregates = logic domain rò ra Application Service layer

**Verdict chi tiết — Option B mở rộng (DDD chuẩn):**

1. **Tách file `Domain.cs` (God File — 2,050+ lines):** Không để tiếp tục nhồi nhét vào 1 file. Cấu trúc mới:
```
1_Shared/
  Domain.cs                          ← GIỮ LẠI: chỉ chứa Value Objects + Enums + Legacy records
  Domain/
    Common.cs                        ← BaseEntity, IMustHaveTenant (đã có)
    Events/                          ← Domain Events (đã có OrderCompletedEvent)
    Aggregates/
      TenantAggregate/
        Tenant.cs                    ← MỚI: Rich Domain class thay record
        TenantStatus.cs              ← MỚI: enum TenantStatus
        TenantSettings.cs            ← MỚI: value object cài đặt tenant
        TenantEvents.cs              ← MỚI: TenantDeactivatedEvent, TenantCreatedEvent
      UserAggregate/
        DemoUser.cs                  ← MOVE ra khỏi Domain.cs (Wave 6)
        UserTenant.cs                ← MOVE ra khỏi Domain.cs (Wave 6)
        UserRole.cs                  ← MOVE enum ra khỏi Domain.cs (Wave 6)
        PermissionGroup.cs           ← MỚI (Wave 6)
```

2. **`Tenant` class thay `record` — Rich Domain Model:**
```csharp
// 1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs
public class Tenant : AggregateRoot  // AggregateRoot sẽ được thêm vào Common.cs
{
    public TenantId Id { get; private set; }
    public string Name { get; private set; }
    public BusinessType BusinessType { get; private set; }
    public HKDGroup? HKDGroup { get; private set; }
    public TenantStatus Status { get; private set; }
    public TenantSettings Settings { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Domain constructor (tạo mới)
    public Tenant(TenantId id, string name, BusinessType businessType, HKDGroup? hkdGroup = null)

    // Domain methods (không phải CRUD)
    public void UpdateProfile(string name, string contactEmail, string businessAddress)
    public void Activate()
    public void Suspend(string reason)        // → phát TenantSuspendedEvent
    public void Deactivate(string reason)     // guard: không Deactivate từ Suspended thẳng
    public void UpdateSettings(TenantSettings settings)
}
```

3. **`AggregateRoot` base class** — thêm vào `Common.cs`:
```csharp
public abstract class AggregateRoot : BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
public interface IDomainEvent { DateTime OccurredAt { get; } }
```

**Phạm vi thay đổi của Wave 5 (D1 implementation):**
- `1_Shared/Domain/Common.cs` → thêm `AggregateRoot` + `IDomainEvent` interface
- `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` → tạo mới (class, không phải record)
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantStatus.cs` → enum mới
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` → value object
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantEvents.cs` → domain events
- `1_Shared/Domain.cs` → **KHÔNG XÓA** `record Tenant` ngay — đổi thành `[Obsolete]` redirect sang class mới để tránh break existing code. Xóa thực sự ở Wave 6 cleanup.
- `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` → cập nhật EF mapping
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` → cập nhật DbSet type
- Migration EF Core mới cho TenantStatus column

**Hard constraints:**
- `AccountingEntry` vẫn tuyệt đối immutable — không đụng
- Single Namespace: `VanAn.Shared.Domain` — các file mới vẫn dùng namespace này
- `Domain.cs` không xóa trong Wave 5 — chỉ obsolete-mark `record Tenant`

---

## REFERENCES
- `docs/requirements/functional-requirements.md` — nguồn yêu cầu gốc
- `docs/AI/project_state.md` — current sprint state
- `.devin/rules/governance.md` — hard stop rules
- `.devin/workflows/newfeaturebuild.md` — implement workflow
- `6_Testing/e2e-tests/` — existing E2E test suite
- `1_Shared/Domain.cs` — Tenant record, DemoUser, UserTenant, UserRole enum (Wave 5/6 targets)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` — DB context, DbSet<Tenant>, DbSet<UserTenant>
- `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` — Tenant EF mapping
- `5_WebApps/ShopERP/Pages/Login.cshtml.cs` — auth entry point (Wave 0 target)
- `2_Gateway/Program.cs` — gateway auth setup (Wave 0 target)
