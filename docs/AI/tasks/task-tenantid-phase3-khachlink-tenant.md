# TASK CARD: [TENANTID REMEDIATION] - [PHASE 3] - KHACHLINK TENANT CONTEXT

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** KhachLink (customer-facing PWA) phải có tenant context — khách hàng chỉ thấy data của tenant (shop) mà họ đang truy cập. SignalR groups phải được authorize.
- **Nghiệp vụ áp dụng:** Customer PWA — khách quét QR code của shop, xem menu/đặt hàng của shop đó, không thấy data shop khác.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (cần Phase 2 merged trước khi bắt đầu)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Pages/Index.cshtml.cs` — remove `Guid.NewGuid()` demo data
  - `5_WebApps/KhachLink/Pages/Campaign.cshtml.cs` — remove `Guid.NewGuid()` demo data
  - `5_WebApps/KhachLink/Hubs/DashboardHub.cs` — authorize SignalR group join
  - `5_WebApps/KhachLink/Services/Dashboard/RealTimeDashboardService.cs` — tenant validation
  - `5_WebApps/KhachLink/Services/OfflineOrderService.cs` — tenant from shop context
  - `5_WebApps/KhachLink/Models/OfflineOrderDto.cs` — tenant resolution
  - `5_WebApps/KhachLink/Program.cs` — tenant middleware/mapping
  - `2_Gateway/Controllers/OrdersController.cs` — customer order creation tenant context
- **Boundary Rules (Nghiêm cấm):**
  - CẤM inject `IVanAnDbContext` vào KhachLink (VA-KHACHLINK-004)
  - CẤM tạo customer authentication system phức tạp trong Phase 3 — dùng shop URL/query param approach
  - CẤM sửa Domain.cs

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architecture VA-KHACHLINK-004:** KhachLink MUST NOT inject `IVanAnDbContext`. Tenant context qua HTTP/Gateway only.
- [ ] **SignalR authorization:** `JoinTenantGroup(tenantId)` MUST verify client có quyền join group đó (e.g., shop URL binding).
- [ ] **No demo data in production:** Remove all `Guid.NewGuid()` demo seeding.
- [ ] **Legal Standards:** Customer data (orders, loyalty) phải cách ly theo shop/tenant.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** KhachLink resolve tenant từ shop URL (e.g., `?shopId=xxx` hoặc subdomain `shop1.khachlink.app`) → query Gateway `/api/shops/{id}` → get TenantId.
- [ ] **SC2:** `DashboardHub.JoinTenantGroup` — verify `tenantId` khớp với shop mà client đang truy cập (from URL/context), reject nếu mismatch.
- [ ] **SC3:** `Index.cshtml.cs` và `Campaign.cshtml.cs` — không còn `Guid.NewGuid()` demo data. Data lấy từ Gateway API.
- [ ] **SC4:** `OfflineOrderService` — tenant từ shop context, không từ `offlineOrder.ShopId` (client-controlled).
- [ ] **SC5:** `dotnet build VanAn.sln` — 0 errors.
- [ ] **SC6:** `guard-check.ps1` — PASS.
- [ ] **SC7:** Architecture tests — PASS (VA-KHACHLINK-004 vẫn pass).
- [ ] **SC8:** Security test: client join SignalR group tenant_A khi đang truy cập shop tenant_B → rejected.

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation`
- `system-refactor-safety`
- `nats-sqlite-deployment-validation` (nếu liên quan offline sync)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4 defects đã verify
- **Verified Facts:**
  - Fact 1: `Index.cshtml.cs:37` — `TenantId tenantId = new(Guid.NewGuid())` — demo data
  - Fact 2: `Campaign.cshtml.cs:30,64` — `Guid.NewGuid()` "Demo tenant"
  - Fact 3: `DashboardHub.JoinTenantGroup(tenantId)` — no authorization, client chooses group
  - Fact 4: `OfflineOrderService:109` — `Guid tenantId = Guid.Parse(offlineOrder.ShopId)` — client-controlled
- **Assumptions:**
  - Shop có public URL/QR code chứa ShopId → có thể resolve TenantId qua Gateway
  - Customer không cần login (anonymous checkout OK) — tenant context từ shop, không từ user
- **Open Questions:**
  - Q1: Shop URL pattern là subdomain (`shop1.khachlink.app`) hay query param (`?shopId=xxx`) hay path (`/shop/shop1`)?
  - Q2: Customer có cần login cho loyalty program không? (nếu có, cần customer auth với tenant context)
- **Recommended Action:** **Continue** — Open Questions = 2 < 3. Có thể implement với assumption query param + clarify sau.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| KhachLink tenant from URL | Tất cả KhachLink pages cần đọc shopId từ URL | Update routing + layout |
| SignalR group authorization | Client hiện tại join freely → sẽ bị reject | Update client-side join logic |
| Remove demo data | KhachLink sẽ trống nếu không có real data | Gateway API phải return data |
| OfflineOrderService tenant | Offline orders cần gán tenant từ shop context | Update sync logic |

## 9. TDD & E2E TESTING STRATEGY
- **TDD khuyến khích:**
  - Trước khi fix SignalR auth, viết test FAIL: client join sai tenant group → should reject
  - Trước khi fix tenant resolution, viết test FAIL: KhachLink với shopId=A → chỉ thấy tenant A data
- **E2E Playwright test BẮT BUỘC (KhachLink là UI-heavy):**
  - KhachLink là customer-facing PWA → mọi thay đổi tenant resolution affect UI
  - Spec files: `order-flow.spec.ts`, `qr-payment.spec.ts` (cần tạo mới nếu chưa có)
  - Test case: truy cập `?shopId=A` → chỉ thấy product/order của tenant A
  - Test case: join SignalR group tenant_B khi đang ở shopId=A → rejected
  - Test case: tạo order với shopId=A → order gán tenant A (không random)
- **Test boundary:**
  - Unit tests: tenant resolution logic, SignalR group authorization
  - Integration tests: Gateway API với tenant context từ shopId
  - E2E tests: KhachLink full flow với tenant isolation

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Mỗi Session chạy 2 Micro-phases LIÊN TỤC trong 1 phiên:

```
[Session N]
  ├── Phase 1: JIT Planning
  │     Đọc boundary files 1 lần duy nhất → chốt: file cần sửa/tạo,
  │     tên test case, method signature, cấu trúc hàm.
  │     KHÔNG đọc ngoài boundary. KHÔNG giải thích dài.
  └── Phase 2: Pure Execution
        Bám chặt Phase 1 → viết thẳng.
        Token chỉ chi cho output code, không suy luận/re-explore.
```

### Micro-phase breakdown cho Phase 3 (KhachLink Tenant)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc KhachLink pages + Gateway shop API → chốt: tenant resolution flow, URL pattern | Implement tenant resolution from shop URL + remove demo data |
| **S2** | Đọc DashboardHub + RealTimeDashboardService → chốt: auth check logic | Fix SignalR group authorization + tenant validation |
| **S3** | Đọc E2E specs → chốt: test cases tenant isolation | Write E2E tests + verify build + guard-check |

### Rules
- JIT Planning: MAX 15 phút đọc, chốt output bằng text ngắn
- Pure Execution: KHÔNG re-read, chỉ viết code theo plan
- KhachLink KHÔNG inject IVanAnDbContext (VA-KHACHLINK-004) — verify trong mỗi session

## 11. ESTIMATED EFFORT
- 2-3 ngày (1 ngày tenant resolution + 0.5 ngày SignalR auth + 0.5 ngày remove demo + 1 ngày test)
- 3 sessions (S1-S3) theo JIT Planning
