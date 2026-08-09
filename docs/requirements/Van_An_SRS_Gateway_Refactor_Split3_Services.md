# **ĐẶC TẢ YÊU CẦU NGHIỆP VỤ VÀ KỸ THUẬT (SRS)**

## **GATEWAY REFACTOR — OPTION 2 — CHIA 3 SERVICES (PLATFORM API + BUSINESS API + SYNC WORKER) (VA-GRF-SPLIT3)**

**Ngày cập nhật:** 08/08/2026
**Trạng thái:** Sẵn sàng cho Review
**Phạm vi:** Gateway VPS + Sync Worker VPS + Platform API VPS (tùy chọn)
**Kiến trúc hiện tại:** 1 Gateway process (45 controllers, 5 background services, 4 SignalR hubs) trên e2-micro
**Kiến trúc target:** 3 services độc lập, scale được theo domain

---

## **1. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)**

### **1.1. Bối cảnh nghiệp vụ (Business Context)**

Gateway API hiện tại gộp 3 vai trò vào 1 process:

| Vai trò | Thành phần | Traffic | Auth |
|---|---|---|---|
| **Platform API** | Tenant CRUD, Onboarding, ShopInstances, AuditTrail, FraudFlag, KhachLinkHomeSettings (7 controllers) | Low, admin-only | SystemAdmin JWT |
| **Business API** | Orders, Catalog, Accounting, Loyalty, Customers, Community, Commerce, Marketing (34 controllers) + 4 SignalR hubs | High, tenant-scoped | Cookie + JWT |
| **Sync Worker** | NatsSyncWorker, DataSyncSubscriber, EInvoiceSyncSubscriber, CoolingPeriodJob, HeldTimeoutJob (5 background services) | Continuous, no HTTP | Internal |

**Vấn đề:**
- 3 vai trò chia sẻ 0.25 vCPU → overload
- Không scale độc lập theo traffic pattern
- Code complexity: 45 controllers, 609 dòng Program.cs
- Fault không isolate — 1 background service crash → toàn bộ Gateway restart

### **1.2. Mục tiêu bài toán**

Chia Gateway thành **3 services độc lập**:

1. **vanan-platform-api** — Platform admin API (7 controllers, SystemAdmin only)
2. **vanan-business-api** — Business API (34 controllers + SignalR hubs, tenant-scoped)
3. **vanan-sync-worker** — Background jobs (5 services, no HTTP)

Mỗi service:
- Docker image riêng
- Scale độc lập
- Fault isolation
- Deploy độc lập

### **1.3. Nguyên lý cốt lõi**

**"Separate by traffic pattern + responsibility, not by domain."**

```
                    ┌─────────────────────────┐
                    │     Nginx (gateway)     │
                    │   SSL + reverse proxy   │
                    └────────────┬────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              │                  │                  │
              ▼                  ▼                  ▼
   ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
   │  Platform API    │ │  Business API    │ │  Sync Worker     │
   │  (7 controllers) │ │  (34 controllers)│ │  (5 bg services) │
   │  SystemAdmin JWT │ │  Cookie + JWT    │ │  No HTTP         │
   │  Low traffic     │ │  High traffic    │ │  Continuous CPU  │
   │  e2-micro OK     │ │  e2-small needed │ │  e2-micro OK     │
   └────────┬─────────┘ └────────┬─────────┘ └────────┬─────────┘
            │                    │                    │
            └──────────┬─────────┴────────────────────┘
                       ▼
              ┌──────────────────┐
              │  PostgreSQL      │
              │  + NATS          │
              └──────────────────┘
```

### **1.4. Giá trị kinh doanh (Business Value)**

- **Scale độc lập** — Business API scale lên khi traffic tăng, Platform API giữ nhỏ
- **Fault isolation** — Sync worker crash không ảnh hưởng API
- **Deploy độc lập** — Update Business API không restart Platform API
- **Code organization** — Mỗi service focus 1 vai trò, dễ onboard developer mới
- **Resource optimization** — Platform API + Sync Worker chạy e2-micro ($7), Business API chạy e2-small ($12)

---

## **2. PHẠM VI ÁP DỤNG (SCOPE)**

### **2.1. In Scope**

| Component | Platform API | Business API | Sync Worker |
|---|---|---|---|
| TenantOnboardingController | ✅ | — | — |
| TenantsController | ✅ | — | — |
| ShopInstancesController | ✅ | — | — |
| AuditTrailController | ✅ | — | — |
| FraudFlagController | ✅ | — | — |
| KhachLinkHomeSettingsController | ✅ | — | — |
| OnboardingController | ✅ | — | — |
| OrdersController | — | ✅ | — |
| CustomerOrdersController | — | ✅ | — |
| PublicOrdersController | — | ✅ | — |
| KitchenController | — | ✅ | — |
| VoiceCommandController | — | ✅ | — |
| CatalogController | — | ✅ | — |
| ProductsController | — | ✅ | — |
| FeaturedProductsController | — | ✅ | — |
| ProductCostPriceController | — | ✅ | — |
| ProductReferralConfigController | — | ✅ | — |
| ShopConfigController | — | ✅ | — |
| TenantStoreController | — | ✅ | — |
| CustomersController | — | ✅ | — |
| CustomerIdentityController | — | ✅ | — |
| CustomerProfileController | — | ✅ | — |
| DeviceRegistrationController | — | ✅ | — |
| LoyaltyController | — | ✅ | — |
| LoyaltyConfigController | — | ✅ | — |
| RedemptionController | — | ✅ | — |
| InternalLoyaltyController | — | ✅ | — |
| MissionsController | — | ✅ | — |
| CommunityController | — | ✅ | — |
| CommunityAdminController | — | ✅ | — |
| CommunityFundController | — | ✅ | — |
| CollaboratorVerificationController | — | ✅ | — |
| CampaignsController | — | ✅ | — |
| NotificationsController | — | ✅ | — |
| AccountingEntriesController | — | ✅ | — |
| HKDBooksController | — | ✅ | — |
| ReportController | — | ✅ | — |
| DashboardController | — | ✅ | — |
| CommerceModeController | — | ✅ | — |
| VietQrController | — | ✅ | — |
| WebhookController | — | ✅ | — |
| BuildController | — | ✅ | — |
| LocalizationController | — | ✅ | — |
| ProviderController | — | ✅ | — |
| NatsSyncWorker | — | — | ✅ |
| DataSyncSubscriber | — | — | ✅ |
| EInvoiceSyncSubscriber | — | — | ✅ |
| CoolingPeriodJob | — | — | ✅ |
| HeldTimeoutJob | — | — | ✅ |
| OrderHub (SignalR) | — | ✅ | — |
| KitchenHub (SignalR) | — | ✅ | — |
| LocationHub (SignalR) | — | ✅ | — |
| ChatHub (SignalR) | — | ✅ | — |

### **2.2. Out of Scope**

- Thay đổi database schema
- Thay đổi NATS subject naming
- Thay đổi auth/JWT flow
- Tách CoreHub Services thành microservices
- Event sourcing / CQRS

### **2.3. Assumptions**

- CoreHub (3_CoreHub) vẫn là shared class library — cả 3 services reference nó
- PostgreSQL + NATS vẫn shared (không tách DB per service)
- Nginx reverse proxy route theo path prefix:
  - `/api/v1/onboarding/*`, `/api/tenants/*`, `/api/v1/shop-instances/*` → Platform API
  - `/api/*` (rest) → Business API
  - SignalR hubs → Business API

---

## **3. YÊU CẦU CHI TIẾT (DETAILED REQUIREMENTS)**

### **3.1. vanan-platform-api**

#### **REQ-P.1: Tạo project + Dockerfile**

| Attribute | Value |
|---|---|
| **Project** | `2_Gateway/VanAn.Gateway.Platform.csproj` (hoặc dùng same csproj với filter) |
| **Dockerfile** | `2_Gateway/Dockerfile.platform` |
| **Controllers** | 7 (TenantOnboarding, Tenants, ShopInstances, AuditTrail, FraudFlag, KhachLinkHomeSettings, Onboarding) |
| **Port** | 5001 |
| **Auth** | SystemAdmin JWT only |
| **SignalR** | None |
| **Background services** | None |
| **DB migration** | ✅ Owner (chạy MigrateAsync on startup) |

#### **REQ-P.2: Program.cs (Platform)**

```csharp
// 2_Gateway/Program.Platform.cs
var builder = WebApplication.CreateBuilder(args);
// Register only Platform controllers
builder.Services.AddControllers()
    .AddApplicationPart(typeof(TenantOnboardingController).Assembly);
// Filter controllers: only Platform-related
// Register CoreHub services needed by Platform
// Register VanAnDbContext (PostgreSQL)
// Auth: JWT Bearer only (no Cookie)
// NO SignalR
// NO background services
var app = builder.Build();
app.MapControllers();
// Migration on startup
await app.Services.GetRequiredService<VanAnDbContext>().Database.MigrateAsync();
app.Run();
```

### **3.2. vanan-business-api**

#### **REQ-B.1: Tạo project + Dockerfile**

| Attribute | Value |
|---|---|
| **Project** | `2_Gateway/VanAn.Gateway.Business.csproj` (hoặc same csproj với filter) |
| **Dockerfile** | `2_Gateway/Dockerfile.business` |
| **Controllers** | 34 (Orders, Catalog, Accounting, Loyalty, Customers, Community, Commerce, Marketing, Infra) |
| **Port** | 5002 |
| **Auth** | Cookie + JWT (tenant-scoped) |
| **SignalR** | 4 hubs (Order, Kitchen, Location, Chat) |
| **Background services** | None |
| **DB migration** | ❌ Không migrate (Platform API là owner) |

#### **REQ-B.2: Program.cs (Business)**

```csharp
// 2_Gateway/Program.Business.cs
var builder = WebApplication.CreateBuilder(args);
// Register only Business controllers (exclude Platform)
builder.Services.AddControllers()
    .AddApplicationPart(typeof(OrdersController).Assembly);
// Register CoreHub services needed by Business
// Register VanAnDbContext (PostgreSQL) — NO migration
// Auth: Cookie + JWT
// SignalR: 4 hubs
// NO background services
var app = builder.Build();
app.MapControllers();
app.MapHub<OrderHub>("/orderHub");
app.MapHub<KitchenHub>("/kitchenhub");
app.MapHub<LocationHub>("/hubs/location");
app.MapHub<ChatHub>("/hubs/chat");
// NO migration — Platform API owns migration
app.Run();
```

### **3.3. vanan-sync-worker**

#### **REQ-S.1: Tạo project + Dockerfile**

| Attribute | Value |
|---|---|
| **Project** | Reuse `2_Gateway/VanAn.Gateway.csproj` |
| **Dockerfile** | `2_Gateway/Dockerfile.sync-worker` |
| **Controllers** | None |
| **Port** | None (no HTTP, chỉ `/health` minimal) |
| **Background services** | 5 (NatsSyncWorker, DataSyncSubscriber, EInvoiceSyncSubscriber, CoolingPeriodJob, HeldTimeoutJob) |
| **DB migration** | ❌ Không migrate |

#### **REQ-S.2: Program.cs (Sync Worker)**

```csharp
// 2_Gateway/Program.SyncWorker.cs
var builder = WebApplication.CreateBuilder(args);
// Register CoreHub services needed by background jobs
// Register VanAnDbContext (PostgreSQL) — NO migration
// Register NATS publisher
// Register ALL 5 background services
builder.Services.AddHostedService<NatsSyncWorker>();
builder.Services.AddHostedService<DataSyncSubscriber>();
builder.Services.AddHostedService<EInvoiceSyncSubscriber>();
builder.Services.AddHostedService<CoolingPeriodJob>();
builder.Services.AddHostedService<HeldTimeoutJob>();
// Minimal HTTP for health check only
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
// NO controllers, NO SignalR, NO migration
app.Run();
```

### **3.4. Nginx routing**

#### **REQ-N.1: Cập nhật nginx template**

```nginx
# Platform API routes
location ~ ^/api/(v1/onboarding|tenants|v1/shop-instances|audit|fraud|khachlink-home) {
    proxy_pass http://platform-api:5001;
}

# Business API routes (catch-all)
location /api/ {
    proxy_pass http://business-api:5002;
}

# SignalR hubs
location ~ ^/(orderHub|kitchenhub|hubs/) {
    proxy_pass http://business-api:5002;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

### **3.5. Docker Compose**

#### **REQ-D.1: docker-compose.gateway.yml (updated)**

```yaml
services:
  platform-api:
    image: ghcr.io/anlebao/vanan-platform-api:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;...
      - Authentication__JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on: [postgres]
    restart: unless-stopped

  business-api:
    image: ghcr.io/anlebao/vanan-business-api:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;...
      - ConnectionStrings__Nats=nats://nats:4222
      - Authentication__JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on: [postgres, nats]
    restart: unless-stopped

  sync-worker:
    image: ghcr.io/anlebao/vanan-sync-worker:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;...
      - ConnectionStrings__Nats=nats://nats:4222
      - Sync__PollIntervalMs=10000
    depends_on: [postgres, nats]
    restart: unless-stopped

  nginx:
    depends_on: [platform-api, business-api]
    # ... (existing config)
```

### **3.6. CI/CD**

#### **REQ-C.1: Build 3 Docker images**

| Image | Dockerfile | Tag |
|---|---|---|
| `ghcr.io/anlebao/vanan-platform-api` | `2_Gateway/Dockerfile.platform` | `:latest` + SHA |
| `ghcr.io/anlebao/vanan-business-api` | `2_Gateway/Dockerfile.business` | `:latest` + SHA |
| `ghcr.io/anlebao/vanan-sync-worker` | `2_Gateway/Dockerfile.sync-worker` | `:latest` + SHA |

#### **REQ-C.2: Deploy 3 containers**

| Container | VPS | Resources |
|---|---|---|
| `vanan-platform-api` | Gateway VPS (e2-micro OK) | 50MB RAM |
| `vanan-business-api` | Gateway VPS (e2-small recommended) | 150MB RAM |
| `vanan-sync-worker` | Gateway VPS (e2-micro OK) hoặc VPS riêng | 80MB RAM |

### **3.7. Database migration ownership**

#### **REQ-M.1: Single migration owner**

| Service | Migrate on startup? |
|---|---|
| Platform API | ✅ YES (single owner) |
| Business API | ❌ NO |
| Sync Worker | ❌ NO |

**Rationale:** Tránh race condition khi 3 services cùng migrate. Platform API start first → migrate → Business API + Sync Worker start sau.

---

## **4. PHÂN TÍCH ƯU KHUYẾT (PROS/CONS ANALYSIS)**

### **4.1. Ưu điểm**

| Ưu điểm | Chi tiết |
|---|---|
| **Sạch sẽ nhất** | Mỗi service 1 vai trò rõ ràng, code organization tốt |
| **Scale độc lập** | Business API scale lên khi traffic tăng, Platform API giữ nhỏ |
| **Fault isolation** | Sync worker crash → Business API vẫn serve requests |
| **Deploy độc lập** | Update Business API không restart Platform API |
| **Resource optimization** | Platform API + Sync Worker = e2-micro ($7), Business API = e2-small ($12) |
| **Dễ onboard developer** | Mỗi service scope nhỏ, dễ hiểu |
| **Scalability lâu dài tốt nhất** | Có thể tách tiếp thành microservices khi cần |

### **4.2. Khuyết điểm**

| Khuyết điểm | Chi tiết |
|---|---|
| **Effort cao nhất** | 1-2 tuần refactor + testing |
| **3 Docker images** | 3 build pipelines, CI/CD phức tạp hơn |
| **Shared code duplication** | Cả 3 reference CoreHub → DI registration duplicate |
| **Cross-service calls** | Platform API cần query Business API → network latency |
| **DB migration race** | Cần single owner (Platform API) → start order matters |
| **Debug khó hơn** | 3 logs streams, cần centralized logging (Seq) |
| **Chi phí VPS tăng** | Tối thiểu 2 VPS (~$15/tháng), lý tưởng 3 VPS (~$21/tháng) |
| **Nginx routing phức tạp** | Cần maintain path prefix mapping |
| **Rủi ro break cao** | Refactor DI registration, split controllers → dễ break |

### **4.3. Chi phí tiềm ẩn**

| Item | Cost | Frequency |
|---|---|---|
| VPS thêm (Business API) | +$5-12/tháng | Recurring |
| VPS thêm (Sync Worker, tùy chọn) | +$7/tháng | Recurring |
| CI/CD complexity | +2h/tháng maintenance | Recurring |
| Debugging overhead | +4h/sprint | Recurring |
| Initial refactor effort | 1-2 tuần | One-time |
| Testing effort | 1 tuần | One-time |

### **4.4. So sánh với Hybrid (Option 1+3+4)**

| Tiêu chí | Option 2 (3 services) | Hybrid (1+3+4) |
|---|---|---|
| Effort | 1-2 tuần | 1-2 ngày |
| Impact | Cao | Thấp |
| Sạch sẽ/mở rộng | Cao nhất | Trung bình |
| Chi phí tiềm ẩn | Cao (+$15-21/tháng) | Thấp (+$5/tháng max) |
| Rủi ro break | Cao | Thấp |
| Scalability lâu dài | Tốt nhất | Tốt |
| Khi nào nên chọn | Traffic cao, team 3+ devs | Traffic thấp, team 1-2 devs |

---

## **5. KẾ HOẠCH TRIỂN KHAI (IMPLEMENTATION PLAN)**

### **5.1. Thứ tự ưu tiên**

| Phase | Task | Effort | Dependency |
|---|---|---|---|
| **Phase 1: Prep** | Tạo 3 Program.cs variants | 2 ngày | None |
| **Phase 1: Prep** | Tạo 3 Dockerfiles | 1 ngày | Program.cs |
| **Phase 1: Prep** | Refactor DI registration (split per service) | 3 ngày | Program.cs |
| **Phase 1: Prep** | Update nginx routing template | 1 ngày | DI refactor |
| **Phase 1: Prep** | Update docker-compose.gateway.yml | 4 giờ | Nginx |
| **Phase 2: CI/CD** | Update CD workflow (3 images) | 1 ngày | Phase 1 |
| **Phase 2: CI/CD** | Update deploy scripts (3 containers) | 1 ngày | CD workflow |
| **Phase 3: Test** | Local test 3 services | 2 ngày | Phase 2 |
| **Phase 3: Test** | Integration test (auth, API, SignalR, sync) | 2 ngày | Local test |
| **Phase 3: Test** | Staging deploy + smoke test | 1 ngày | Integration test |
| **Phase 4: Deploy** | Production deploy | 4 giờ | Phase 3 |
| **Phase 4: Deploy** | Monitor 48h | 48h | Deploy |

**Total effort:** ~2 tuần (10 working days)

### **5.2. Verification checklist**

- [ ] Platform API: 7 controllers respond correctly
- [ ] Business API: 34 controllers respond correctly
- [ ] Sync Worker: 5 background services running
- [ ] SignalR hubs: 4 hubs connect from client
- [ ] Auth: JWT login works (via ShopERP → Platform API)
- [ ] Auth: Cookie auth works (via ShopERP → Business API)
- [ ] DB migration: Platform API migrates on startup, others skip
- [ ] NATS: events deliver from Sync Worker → ShopERP
- [ ] Nginx: routing correct (Platform vs Business)
- [ ] No data loss during migration
- [ ] CPU: Business API < 50%, Platform API < 10%, Sync Worker < 10%

---

## **6. RỦI RO VÀ MITIGATION (RISK ANALYSIS)**

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| DI registration break | Cao | Cao | Test local từng service trước deploy |
| DB migration race | Trung bình | Cao | Single owner (Platform API), start order trong docker-compose |
| Nginx routing sai | Trung bình | Trung bình | Test từng path prefix, có fallback catch-all |
| SignalR hub không connect | Trung bình | Cao | Verify proxy_set_header Upgrade, test từ client |
| Cross-service call latency | Thấp | Trung bình | Cache tenant data trong Business API |
| 3 images build chậm | Cao | Thấp | Parallel build jobs trong CI |
| Rollback phức tạp | Trung bình | Cao | Keep old Gateway image, revert docker-compose |

---

## **7. ACCEPTANCE CRITERIA**

| # | Criteria | Verification |
|---|---|---|
| AC-1 | Platform API serve 7 controllers | curl test từng endpoint |
| AC-2 | Business API serve 34 controllers | curl test representative endpoints |
| AC-3 | Sync Worker run 5 background services | docker logs + DB query (OutboxMessages processed) |
| AC-4 | SignalR hubs connect from KhachLink | Browser test |
| AC-5 | Auth flow works (login → JWT → API call) | E2E test |
| AC-6 | DB migration runs on Platform API only | Check __EFMigrationsHistory |
| AC-7 | Nginx routes correctly | curl test path prefixes |
| AC-8 | No data loss | Compare record counts before/after |
| AC-9 | CPU: Business API < 50% sustained | docker stats 48h |
| AC-10 | Deploy rollback works | Test revert to single Gateway image |

---

## **8. APPENDIX**

### **8.1. Resource estimation per service**

| Service | CPU | RAM | Image size | VPS |
|---|---|---|---|---|
| Platform API | ~2% | 50MB | ~200MB | e2-micro ($7) |
| Business API | ~15% | 150MB | ~450MB | e2-small ($12) |
| Sync Worker | ~5% | 80MB | ~200MB | e2-micro ($7) hoặc share |
| **Total** | ~22% | 280MB | — | $19-26/tháng |

### **8.2. VPS layout options**

**Option A: 2 VPS (cost-optimized)**
- Gateway VPS (e2-small, $12): Platform API + Business API + Nginx + Postgres + NATS
- Sync Worker VPS (e2-micro, $7): Sync Worker only

**Option B: 3 VPS (fault-isolated)**
- Platform VPS (e2-micro, $7): Platform API + Nginx
- Business VPS (e2-small, $12): Business API + SignalR
- Sync VPS (e2-micro, $7): Sync Worker + Postgres + NATS

**Option C: 1 VPS (minimal, not recommended)**
- Gateway VPS (e2-medium, $25): All 3 services + Nginx + Postgres + NATS

### **8.3. When to choose Option 2 over Hybrid**

| Trigger | Action |
|---|---|
| Traffic > 1000 req/day | Consider Option 2 |
| Team size > 2 devs | Consider Option 2 |
| Need independent deploy per domain | Choose Option 2 |
| CPU > 70% sustained after Hybrid | Escalate to Option 2 |
| Need fault isolation (SLA) | Choose Option 2 |
