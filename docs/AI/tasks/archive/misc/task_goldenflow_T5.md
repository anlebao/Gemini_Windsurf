# TASK CARD: CI FIX - GoldenFlow Test T5 — Full E2E Order Flow

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Activate `OrderFlow_KhachLink_To_ShopERP_To_KhachLink` — full E2E flow: KhachLink tạo Order → ShopERP confirm → KhachLink query status
- **Nghiệp vụ áp dụng:** Order workflow end-to-end: POST /api/orders → PUT /api/orders/{id}/status → GET /api/orders/{id}/status qua `CustomWebApplicationFactory`

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md` (nếu cần implement endpoints) hoặc `.devin/workflows/Fix_Tests.md` (nếu endpoints đã tồn tại)
- **Execution Mode:** ANALYZE trước → IMPLEMENT nếu endpoints missing

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/GoldenFlowSystemTests.cs` (T5 test body)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs`
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestEntityBuilder.cs`
  - `2_Gateway/Controllers/` (scan tất cả controllers cho `/api/orders` endpoints)
  - `5_WebApps/ShopERP/Controllers/` (scan cho order endpoints)
  - `3_CoreHub/Services/` (OrderWorkflowService nếu cần)
  - `1_Shared/Domain.cs` (OrderStatus type — value object hay enum?)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG implement endpoint nếu chưa có ANALYZE phase và user approval
  - KHÔNG sửa Domain.cs để fix test
  - KHÔNG thay đổi OrderWorkflow business logic — chỉ expose qua API nếu missing
  - KHÔNG stub/mock endpoints — test phải hit real production endpoints

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Endpoint Existence:** PHẢI verify `/api/orders` POST, `/api/orders/{id}/status` PUT/GET tồn tại trước khi bắt đầu
- [ ] **Auth:** `CustomWebApplicationFactory` dùng `TestAuthenticationHandler` — endpoints cần accept test auth scheme
- [ ] **OrderStatus Type:** `savedOrder.Status.Value` — verify `Status` là value object với `.Value` property
- [ ] **Cross-App Routing:** Test dùng single `_factory.CreateClient()` cho cả KhachLink và ShopERP — cần verify YARP routing hoặc điều chỉnh test
- [ ] **Domain Integrity:** Không thêm business logic vào Gateway/Controller — chỉ wire DI và routing
- [ ] **Guard Check:** `guard-check.ps1` PASS sau fix

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **Endpoint audit done:** Xác định chính xác 3 endpoints có tồn tại không: `POST /api/orders`, `PUT /api/orders/{id}/status`, `GET /api/orders/{id}/status`
- [ ] **T5 Pass:** Test chạy end-to-end không skip, không mock — hit thực sự production endpoints
- [ ] **Order created:** `POST /api/orders` trả 2xx + orderId
- [ ] **Status updated:** `PUT /api/orders/{id}/status` trả 2xx, DB record cập nhật `Status = "CONFIRMED"`
- [ ] **Status query:** `GET /api/orders/{id}/status` trả `status = "CONFIRMED"`
- [ ] **Rollback safety:** Nếu endpoints không tồn tại → lập implementation plan, chờ approve — KHÔNG tự implement
- [ ] **144+1/144:** Sau T5 activated, toàn suite pass

**Implementation Date:** TBD — BLOCKED pending endpoint audit
**Branch:** `fix/goldenflow-t5-order-flow` (tách riêng khỏi main)

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Phân tích missing endpoints và DI wiring
- `domain-integrity-validation` — Verify OrderStatus type trước khi assert `.Value`
- `pattern-based-fixing` — Apply pattern từ existing order flow tests (OrderWorkflowTests nếu có)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: Test T5 body đã viết đầy đủ — `POST /api/orders`, `PUT /api/orders/{id}/status`, `GET /api/orders/{id}/status`
  - Fact 2: `CustomWebApplicationFactory` boots ShopERP — single client, không phân biệt port 5002/5003
  - Fact 3: `savedOrder.Status.Value` — code giả định `Status` có `.Value` (value object pattern)
- **Assumptions:**
  - `POST /api/orders` endpoint tồn tại trong Gateway hoặc ShopERP — **CHƯA VERIFY**
  - `OrderStatus` là value object với `.Value: string` — **CHƯA VERIFY**
  - `TestEntityBuilder.CreateShop()` tồn tại — **CHƯA VERIFY**
- **Open Questions:**
  - Q1: `POST /api/orders` endpoint tồn tại ở Gateway hay ShopERP? Route path chính xác?
  - Q2: `PUT /api/orders/{id}/status` — endpoint này có trong OrderWorkflowController không?
  - Q3: `OrderStatus` — là `OrderStatusId` value object hay string enum?
- **Recommended Action:** INVESTIGATE trước — Assumptions >= Verified Facts. KHÔNG sửa code cho đến khi Q1–Q3 được trả lời.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `GoldenFlowSystemTests.cs` (remove `return;`) | T5 chạy thực sự — có thể expose missing endpoints | Chỉ remove `return;` sau khi endpoints verified |
| Gateway Controllers (nếu cần thêm endpoint) | Thêm route mới → cần auth + architecture tests update | Verify architecture tests (21/21) sau khi thêm |
| ShopERP Controllers (nếu cần thêm endpoint) | Tương tự Gateway | Verify architecture tests |
| `TestEntityBuilder.cs` (nếu `CreateShop` missing) | Thêm builder method | Chỉ thêm, không sửa existing |

## 9. TDD & E2E TESTING STRATEGY
- **Pre-condition:** PHẢI audit endpoints trước khi viết bất kỳ code nào
- **Nếu endpoints tồn tại:**
  - Remove `return;`, chạy test, fix assertion nếu cần
  - Verify `OrderStatus.Value` type match
- **Nếu endpoints KHÔNG tồn tại:**
  - Lập implementation plan: Controller method + DI wiring + route registration
  - Chờ approve trước khi implement
  - Implement theo `newfeaturebuild.md` flow
- **Test boundary:**
  - Unit tests: N/A (đây là E2E flow test)
  - Integration tests: T5 — 1 test, full HTTP round-trip
  - E2E tests: Đây chính là E2E test — không cần Playwright

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: ANALYZE → (approve) → IMPLEMENT

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1 — ANALYZE** | Audit 3 endpoints + OrderStatus type + TestEntityBuilder.CreateShop | Report findings: exists / missing / partial |
| **S2 — PLAN** (if missing) | Lập coding plan cho missing endpoints | Chờ user approve |
| **S3 — IMPLEMENT** | Fix theo plan đã approve | Implement endpoints, wire DI |
| **S4 — ACTIVATE** | Remove `return;`, run T5 | Fix assertions nếu cần, commit |

### Rules
- Gate cứng: KHÔNG remove `return;` trước khi S1 ANALYZE hoàn thành
- KHÔNG implement endpoint mới mà không có approve từ user
- Nếu S1 phát hiện endpoints đã tồn tại → bỏ qua S2/S3, thẳng S4

## 11. ESTIMATED EFFORT
- **Nếu endpoints tồn tại:** ~1 giờ (S1 audit + S4 activate)
- **Nếu endpoints missing:** ~3–4 giờ (S1 + S2 plan + S3 implement + S4 activate)
- 1–2 sessions theo JIT Planning
- **BLOCKER:** Endpoint audit PHẢI hoàn thành trước — không estimate được chính xác cho đến khi S1 done
