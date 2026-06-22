# TASK CARD: API - WAVE 5 - Tenant Controller (REST API)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `TenantController` trong `2_Gateway/Controllers/` — REST API endpoints cho Tenant management. Định nghĩa `SystemAdmin` policy trong Gateway `Program.cs`. Map domain exceptions sang HTTP status codes phù hợp.
- **Nghiệp vụ áp dụng:** SystemAdmin của VanAn platform (không phải Owner của 1 tenant) có thể tạo tenant mới, xem danh sách, đình chỉ, hoặc vô hiệu hóa thông qua REST API. Không có role nào khác (Owner, StoreKeeper, etc.) được truy cập các endpoints này.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `2_Gateway/Controllers/TenantController.cs` — TẠO MỚI
  - `2_Gateway/Program.cs` (hoặc `5_WebApps/ShopERP/Program.cs`) — SỬA: thêm `SystemAdmin` policy
  - `3_CoreHub/Services/ITenantManagementService.cs` — ĐỌC để biết interface methods (từ W5-T5)
  - `5_WebApps/ShopERP/Program.cs` — ĐỌC để xem existing policies (`OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove`)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `IVanAnDbContext` hoặc bất kỳ repository/DbContext vào Controller
  - KHÔNG đặt business logic trong Controller — chỉ inject `ITenantManagementService` và delegate
  - KHÔNG catch generic `Exception` — chỉ catch `InvalidOperationException` (domain exception) → 422
  - Governance Hard Stop: `Gateway MUST remain pure stateless Reverse Proxy. NO DbContext, NO EF Core namespaces, NO business logic/services.`
  - KHÔNG thêm policy vào `2_Gateway` nếu Gateway là YARP reverse proxy — policy phải ở `ShopERP/Program.cs`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architecture Boundary:** Controller tồn tại ở đúng layer. Theo codebase architecture: `5_WebApps/ShopERP` là main Web API Host — nếu Gateway là YARP, TenantController phải ở `5_WebApps/ShopERP/Controllers/` (không phải `2_Gateway`). Cần verify trước khi tạo file.
- [ ] **SystemAdmin Policy:** Thêm `options.AddPolicy("SystemAdmin", policy => policy.RequireRole("SystemAdmin"))` vào `ShopERP/Program.cs` — sau block các policies hiện có (`OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove`).
- [ ] **422 vs 500:** `InvalidOperationException` từ service → `return UnprocessableEntity(new { error = ex.Message })` — KHÔNG để exception propagate thành 500.
- [ ] **DTO, không Domain object:** Controller trả về `TenantDto` (anonymous object hoặc record) — KHÔNG trả về `Tenant` class trực tiếp. Domain objects không expose ra API boundary.
- [ ] **Async/Await:** Tất cả action methods phải `async Task<IActionResult>`.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `POST /api/tenants` với valid JWT có role `SystemAdmin` → 201 Created với Location header.
- [ ] **SC-2:** `POST /api/tenants` với JWT có role `Owner` (không phải SystemAdmin) → 403 Forbidden.
- [ ] **SC-3:** `POST /api/tenants` không có JWT → 401 Unauthorized.
- [ ] **SC-4:** `GET /api/tenants` → 200 với danh sách (có thể rỗng).
- [ ] **SC-5:** `GET /api/tenants/{id}` với ID không tồn tại → 404 Not Found.
- [ ] **SC-6:** `POST /api/tenants/{id}/suspend` với tenant đang Active → 200 OK.
- [ ] **SC-7:** `POST /api/tenants/{id}/deactivate` với tenant đang Suspended → 422 Unprocessable Entity (domain exception, không phải 500).
- [ ] **SC-8:** `SystemAdmin` policy defined và registered trong `Program.cs` — `guard-check.ps1` PASS.
- [ ] **SC-9:** Controller không có reference đến `IVanAnDbContext` hoặc any EF Core namespace.
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify Controller không contain business logic, đúng layer placement
- `build-error-analysis` — Handle Controller layer registration, policy setup
- `pattern-based-fixing` — Consistent exception-to-HTTP mapping pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: Policies đã định nghĩa trong `ShopERP/Program.cs`: `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove` — CHƯA CÓ `SystemAdmin`
  - Fact 2: Governance Hard Stop: `Gateway MUST remain pure stateless Reverse Proxy (YARP). NO DbContext, NO EF Core namespaces, NO business logic/services`
  - Fact 3: Governance: `No business logic allowed in Controllers, Gateway, or Hubs`
  - Fact 4: Data Flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite Database`
  - Fact 5: `ITenantManagementService` với 6 methods (từ W5-T5): Create, GetById, List, UpdateProfile, Suspend, Deactivate
  - Fact 6: `InvalidOperationException` từ domain methods khi vi phạm lifecycle rules (từ W5-T2 spec)
  - Fact 7: `5_WebApps/ShopERP` là main Web API Host — Controllers thuộc về đây (không phải YARP Gateway)
- **Assumptions:**
  - Controller nằm ở `5_WebApps/ShopERP/Controllers/TenantController.cs` (không phải `2_Gateway`) — cần verify kiến trúc thực tế
  - `TenantDto` là anonymous object hoặc cần tạo record DTO class
- **Open Questions:**
  - Q1: `2_Gateway` là YARP project hay là một ASP.NET Core project có controllers? (Nếu YARP → Controller phải ở ShopERP)
  - Q2: Tenant ID trong URL (`{id}`) là `Guid` hay `TenantId` type? (Strong-typed ID parsing cần custom model binder)
- **Recommended Action:** IMPLEMENT — nhưng verify Q1 (Gateway architecture) trước khi tạo file ở sai location

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `TenantController.cs` (mới) | W5-T9 UI sẽ call endpoints này — interface phải stable | Freeze endpoint URLs sau khi define |
| `ShopERP/Program.cs` (SystemAdmin policy) | Existing policies không bị ảnh hưởng — chỉ ADD mới | Append sau existing policies block |
| Routing (POST /api/tenants) | Conflict nếu đã có tenant-related endpoints | Verify không có existing tenant routes |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Test — HTTP Layer:**
  - Test: POST /api/tenants với SystemAdmin JWT → 201
  - Test: POST /api/tenants với Owner JWT → 403
  - Test: POST /api/tenants với no auth → 401
  - Test: POST /api/tenants/{id}/deactivate với tenant Suspended → 422 (mock service throw InvalidOperationException)
  - Test: GET /api/tenants/{id} với nonexistent → 404
- **Unit Test — Controller:**
  - Mock `ITenantManagementService` → verify Controller calls correct service method
  - Verify 422 mapping: service throws InvalidOperationException → controller returns 422 with message
- **Test boundary:**
  - Unit tests: mock `ITenantManagementService` — 5+ test cases
  - Integration tests: WebApplicationFactory với in-memory auth
  - E2E tests: N/A trong task này

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này là SINGLE-SESSION với 2 files (Controller + Program.cs update).

### Micro-phase breakdown cho W5-T6

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1 (phase A)** | Đọc `ShopERP/Program.cs` → xem existing policies pattern, AddAuthorization block. Verify xem `2_Gateway` là YARP hay có controllers. Xác định Controller location (ShopERP hay Gateway) | Thêm `SystemAdmin` policy vào `Program.cs`. Tạo `TenantController.cs` với 6 endpoints, [Authorize(Policy="SystemAdmin")] trên tất cả. Tạo request DTOs (CreateTenantRequest, UpdateProfileRequest, SuspendRequest, DeactivateRequest) inline hoặc nested classes |
| **S1 (phase B)** | Review exception mapping pattern — verify 422 cho InvalidOperationException | Implement try-catch block cho InvalidOperationException → 422. Verify null check → 404. Run `dotnet build`. Run `guard-check.ps1` |

### Rules
- Exception mapping: `InvalidOperationException` → 422, `null` return → 404, success → 200/201
- Controller action names phải match HTTP verbs semantically
- Không return Domain object — wrap trong anonymous `new { id, name, status, ... }`

## 11. ESTIMATED EFFORT
- 1 session (45-60 phút)
- **Phụ thuộc:** W5-T5 (ITenantManagementService phải tồn tại)
- **BLOCKER:** Nếu `2_Gateway` là YARP project → TenantController phải ở `5_WebApps/ShopERP/Controllers/` — file path trong task card này cần điều chỉnh khi implement
