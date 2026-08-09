# **ĐẶC TẢ YÊU CẦU NGHIỆP VỤ VÀ KỸ THUẬT (SRS)**

## **GATEWAY REFACTOR — PHƯƠNG ÁN LAI (OPTION 1 + 3 + 4) — TÁCH SYNC WORKER + TỐI ƯU CODE + UPGRADE VPS (VA-GRF-HYBRID)**

**Ngày cập nhật:** 08/08/2026
**Trạng thái:** Sẵn sàng cho Review
**Phạm vi:** Gateway VPS (vanan-gateway) + Sync Worker container mới
**Kiến trúc hiện tại:** 1 Gateway process (45 controllers, 5 background services, 4 SignalR hubs) trên e2-micro (0.25 vCPU, 1GB RAM)

---

## **1. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)**

### **1.1. Bối cảnh nghiệp vụ (Business Context)**

Gateway API hiện tại đang đảm nhiệm **3 vai trò khác nhau** trong 1 process:

| Vai trò | Thành phần | Traffic pattern |
|---|---|---|
| **Platform API** | Tenant CRUD, Onboarding, ShopInstances, AuditTrail (7 controllers) | Low traffic, SystemAdmin only |
| **Business API** | Orders, Catalog, Accounting, Loyalty, Customers, Community (34 controllers) | High traffic, tenant-scoped |
| **Realtime + Sync** | NatsSyncWorker, DataSyncSubscriber, EInvoiceSyncSubscriber, CoolingPeriodJob, HeldTimeoutJob + 4 SignalR hubs | Continuous CPU, poll mỗi 5s |

**Vấn đề hiện tại:**
- e2-micro (0.25 vCPU, 1GB RAM) quá nhỏ cho 3 vai trò
- NatsSyncWorker poll OutboxMessages mỗi 5s → CPU spike liên tục
- SSH không vào được khi Gateway overload (CPU > 80%)
- 45 controllers trong 1 project → code complexity cao, khó maintain
- Không scale được theo domain

### **1.2. Mục tiêu bài toán**

Giảm load Gateway API bằng **phương án lai 3 bước** với tối thiểu impact và chi phí:

1. **Bước 1 (Option 3 — Tối ưu code):** Tăng poll interval, disable unused services, giảm logging → vài giờ, không rủi ro
2. **Bước 2 (Option 1 — Tách Sync Worker):** Tách NatsSyncWorker + background jobs ra container riêng → 1-2 ngày, rollback dễ
3. **Bước 3 (Option 4 — Upgrade VPS):** Khi 2 bước trên chưa đủ, upgrade e2-micro → e2-small → 30 phút, +$5/tháng

### **1.3. Nguyên lý cốt lõi**

**"Fix the cheapest problem first, escalate only when needed."**

```
Bước 1: Tối ưu code (vài giờ, $0)
    ↓ Nếu chưa đủ
Bước 2: Tách Sync Worker (1-2 ngày, $0)
    ↓ Nếu chưa đủ
Bước 3: Upgrade VPS (30 phút, +$5/tháng)
```

### **1.4. Giá trị kinh doanh (Business Value)**

- **Giảm 80% CPU load** cho Gateway (tách CPU-heavy background jobs)
- **SSH ổn định** — không còn overload khi traffic tăng
- **Rollback dễ** — mỗi bước độc lập, có thể revert riêng
- **Chi phí tối thiểu** — chỉ upgrade VPS khi thực sự cần
- **Không break existing code** — controllers giữ nguyên, chỉ tách background services

---

## **2. PHẠM VI ÁP DỤNG (SCOPE)**

### **2.1. In Scope**

| Component | Bước 1 | Bước 2 | Bước 3 |
|---|---|---|---|
| NatsSyncWorker (poll interval) | ✅ 5s → 10s | ✅ Tách ra container | — |
| EInvoiceSyncSubscriber | ✅ Disable nếu chưa dùng | ✅ Move to sync-worker | — |
| CoolingPeriodJob | — | ✅ Move to sync-worker | — |
| HeldTimeoutJob | — | ✅ Move to sync-worker | — |
| DataSyncSubscriber (NATS→PG) | — | ✅ Move to sync-worker | — |
| SignalR hubs (Order, Kitchen, Location, Chat) | — | ❌ Giữ trong Gateway | — |
| 45 Controllers | — | ❌ Giữ nguyên | — |
| Gateway VPS machine type | — | — | ✅ e2-micro → e2-small |

### **2.2. Out of Scope**

- Tách Gateway thành Platform API + Business API (Option 2 — separate SRS)
- Refactor controllers thành microservices
- Thay đổi database schema
- Thay đổi NATS subject naming
- Thay đổi auth/JWT flow

### **2.3. Assumptions**

- Gateway Docker image build hiện tại đã include đầy đủ migrations (verified 08/08/2026)
- NatsSyncWorker poll interval 5s đã giảm load đáng kể (verified sau CD #4)
- EInvoiceSyncSubscriber chưa được sử dụng trong production (cần verify)
- SignalR hubs cần HTTP endpoint → phải giữ trong Gateway

---

## **3. YÊU CẦU CHI TIẾT (DETAILED REQUIREMENTS)**

### **3.1. Bước 1 — Tối ưu code (Option 3)**

#### **REQ-1.1: Tăng NatsSyncWorker poll interval**

| Attribute | Value |
|---|---|
| **Config key** | `Sync__PollIntervalMs` |
| **Current value** | 5000 (5s) |
| **New value** | 10000 (10s) |
| **File** | `docker-compose.gateway.yml` |
| **Impact** | Giảm 50% DB query load (1 query/10s thay vì 1 query/5s) |
| **UX impact** | Order delivery to ShopERP <10s (đủ nhanh cho kitchen/POS) |
| **Rollback** | Đổi env var về 5000, restart container |

#### **REQ-1.2: Disable unused background services**

| Service | Action | Rationale |
|---|---|---|
| EInvoiceSyncSubscriber | Disable nếu chưa dùng | Cần verify — nếu chưa tích hợp e-invoice production |
| SimpleAccountingEventHandler | Giữ | Cần cho accounting event handling |
| DataSyncSubscriber | Giữ | Cần cho NATS→PG sync |
| CoolingPeriodJob | Giữ | Cần cho order cooling period |
| HeldTimeoutJob | Giữ | Cần cho held order timeout |

**Cách disable:** Thêm env var `BackgroundJobs__DisableEInvoiceSync=true` + check trong Program.cs

#### **REQ-1.3: Giảm logging level cho NatsSyncWorker**

| Logger | Current level | New level |
|---|---|---|
| `VanAn.CoreHub.Services.NatsSyncWorker` | INF | WRN |
| `Microsoft.EntityFrameworkCore.Database.Command` | INF | WRN (production) |

**Impact:** Giảm log volume 90% (NatsSyncWorker log mỗi poll cycle)

### **3.2. Bước 2 — Tách Sync Worker (Option 1)**

#### **REQ-2.1: Tạo vanan-sync-worker Dockerfile**

```dockerfile
# 2_Gateway/Dockerfile.sync-worker
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VanAn.sln", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["2_Gateway/VanAn.Gateway.csproj", "2_Gateway/"]
COPY ["3_CoreHub/VanAn.CoreHub.csproj", "3_CoreHub/"]
# ... (same as Gateway Dockerfile for build)
RUN dotnet publish "2_Gateway/VanAn.Gateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Entry point: run as sync-worker mode (no HTTP server)
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll", "--sync-worker-only"]
```

#### **REQ-2.2: Thêm --sync-worker-only CLI argument**

| Attribute | Value |
|---|---|
| **File** | `2_Gateway/Program.cs` |
| **Logic** | Nếu `args.Contains("--sync-worker-only")` → skip `app.MapControllers()`, skip SignalR hubs, skip HTTPS, chỉ register `AddHostedService<NatsSyncWorker>()` + các background jobs |
| **Port** | Không expose port (no HTTP) |
| **Health check** | `/health` endpoint tối thiểu (chỉ return OK) |

#### **REQ-2.3: Tạo docker-compose.sync-worker.yml**

```yaml
services:
  sync-worker:
    image: ghcr.io/anlebao/vanan-gateway:latest
    container_name: vanan-sync-worker
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;...
      - ConnectionStrings__Nats=nats://nats:4222
      - Sync__PollIntervalMs=10000
      - Sync__BatchSize=50
    command: ["dotnet", "VanAn.Gateway.dll", "--sync-worker-only"]
    networks:
      - vanan-network
    depends_on:
      - postgres
      - nats
```

#### **REQ-2.4: Cập nhật docker-compose.gateway.yml**

Remove background services registration khỏi Gateway Program.cs khi chạy mode bình thường (không có `--sync-worker-only`):

| Service | Action |
|---|---|
| NatsSyncWorker | Remove khỏi Gateway (chỉ chạy trong sync-worker) |
| DataSyncSubscriber | Remove khỏi Gateway |
| EInvoiceSyncSubscriber | Remove khỏi Gateway |
| CoolingPeriodJob | Remove khỏi Gateway |
| HeldTimeoutJob | Remove khỏi Gateway |

**Cách implement:** Dùng `if (!args.Contains("--sync-worker-only"))` ngược lại — Gateway bình thường skip background services, sync-worker skip HTTP.

#### **REQ-2.5: Cập nhật CD workflow**

| Job | Change |
|---|---|
| Build & Push Images | Build 2 images: `vanan-gateway` + `vanan-sync-worker` (same Dockerfile, different tag) |
| Deploy to Gateway VPS | Deploy cả gateway + sync-worker containers |
| Health check | Check cả gateway `/health` + sync-worker `/health` |

### **3.3. Bước 3 — Upgrade VPS (Option 4)**

#### **REQ-3.1: Upgrade gateway VPS machine type**

| Attribute | Value |
|---|---|
| **Current** | e2-micro (0.25 vCPU, 1GB RAM) — ~$7/tháng |
| **Target** | e2-small (0.5 vCPU, 2GB RAM) — ~$12/tháng |
| **Command** | `gcloud compute instances set-machine-type vanan-gateway --machine-type=e2-small --zone=asia-southeast1-a` |
| **Downtime** | ~2 phút (stop VM → change type → start VM) |
| **Trigger** | Chỉ khi Bước 1 + 2 chưa đủ (CPU > 70% sustained) |

#### **REQ-3.2: Monitoring threshold**

| Metric | Threshold | Action |
|---|---|---|
| CPU sustained > 70% | 30 phút | Trigger Bước 3 (upgrade) |
| Memory > 80% | 30 phút | Trigger Bước 3 (upgrade) |
| SSH timeout | 1 lần | Trigger Bước 3 ngay lập tức |

---

## **4. PHÂN TÍCH ƯU KHUYẾT (PROS/CONS ANALYSIS)**

### **4.1. Ưu điểm**

| Ưu điểm | Chi tiết |
|---|---|
| **Ít impact nhất** | Bước 1 chỉ đổi env var, Bước 2 chỉ tách Program.cs, Bước 3 chỉ 1 lệnh gcloud |
| **Rollback dễ** | Mỗi bước độc lập — revert Bước 2 không ảnh hưởng Bước 1 |
| **Chi phí tiềm ẩn thấp** | Bước 1+2 = $0, Bước 3 = +$5/tháng (chỉ khi cần) |
| **Giảm load ngay lập tức** | Bước 1 giảm 50% DB query, Bước 2 giảm 100% background CPU cho Gateway |
| **Không break code** | Controllers giữ nguyên, không refactor DI |
| **Tăng dần** | Làm từng bước, verify trước khi escalate |

### **4.2. Khuyết điểm**

| Khuyết điểm | Chi tiết |
|---|---|
| **Không giải quyết code complexity** | Gateway vẫn 45 controllers trong 1 project |
| **Sync-worker vẫn query PG** | 2 containers cùng query DB → contention |
| **Không scale theo domain** | Không thể scale Platform API riêng vs Business API |
| **Bước 2 cần refactor Program.cs** | Có rủi ro break DI nếu không cẩn thận |
| **Bước 3 là "throwing hardware"** | Không giải quyết root cause architecture |

### **4.3. So sánh với Option 2 (3 services split)**

| Tiêu chí | Hybrid (1+3+4) | Option 2 (3 services) |
|---|---|---|
| Effort | 1-2 ngày | 1-2 tuần |
| Impact | Thấp | Cao |
| Sạch sẽ/mở rộng | Trung bình | Cao nhất |
| Chi phí tiềm ẩn | Thấp | Cao (+VPS, CI/CD) |
| Rủi ro break | Thấp | Cao |
| Scalability lâu dài | Tốt | Tốt nhất |

---

## **5. KẾ HOẠCH TRIỂN KHAI (IMPLEMENTATION PLAN)**

### **5.1. Thứ tự ưu tiên**

| Phase | Task | Effort | Dependency |
|---|---|---|---|
| **Phase 1** | REQ-1.1: Tăng poll interval 5s → 10s | 30 phút | None |
| **Phase 1** | REQ-1.3: Giảm logging level | 30 phút | None |
| **Phase 1** | Verify Gateway không overload sau 24h | 24h | REQ-1.1, 1.3 |
| **Phase 2** | REQ-2.2: Thêm --sync-worker-only arg | 4 giờ | Phase 1 verified |
| **Phase 2** | REQ-2.1: Tạo Dockerfile.sync-worker | 2 giờ | REQ-2.2 |
| **Phase 2** | REQ-2.3: Tạo docker-compose.sync-worker.yml | 1 giờ | REQ-2.1 |
| **Phase 2** | REQ-2.4: Remove background services khỏi Gateway | 2 giờ | REQ-2.2 |
| **Phase 2** | REQ-2.5: Cập nhật CD workflow | 2 giờ | REQ-2.3, 2.4 |
| **Phase 2** | Deploy + verify 48h | 48h | All Phase 2 |
| **Phase 3** | REQ-3.1: Upgrade VPS (chỉ nếu cần) | 30 phút | Phase 2 verified |
| **Phase 3** | REQ-3.2: Setup monitoring | 1 giờ | REQ-3.1 |

### **5.2. Verification checklist**

- [ ] Phase 1: SSH vào Gateway VPS không timeout trong 24h
- [ ] Phase 1: CPU Gateway < 50% sustained
- [ ] Phase 1: API response time < 500ms
- [ ] Phase 2: sync-worker container healthy
- [ ] Phase 2: Gateway CPU < 30% sustained (background jobs đã tách)
- [ ] Phase 2: Order delivery to ShopERP vẫn < 10s
- [ ] Phase 2: NATS events vẫn deliver đúng
- [ ] Phase 3: (chỉ nếu trigger) 2GB RAM đủ cho tất cả containers

---

## **6. RỦI RO VÀ MITIGATION (RISK ANALYSIS)**

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Bước 2 break DI registration | Trung bình | Cao | Test local trước, có rollback plan |
| sync-worker crash → events không deliver | Thấp | Cao | restart: unless-stopped + health check + alerting |
| 2 containers contention PG | Thấp | Trung bình | PG connection pool tuning |
| Bước 3 không đủ (vẫn overload) | Thấp | Cao | Escalate lên Option 2 (separate SRS) |

---

## **7. ACCEPTANCE CRITERIA**

| # | Criteria | Verification |
|---|---|---|
| AC-1 | Gateway CPU < 50% sustained sau Phase 1 | `docker stats` 24h |
| AC-2 | SSH vào Gateway VPS không timeout | SSH test 5 lần liên tiếp |
| AC-3 | sync-worker container healthy sau Phase 2 | `docker ps` + health endpoint |
| AC-4 | Gateway CPU < 30% sustained sau Phase 2 | `docker stats` 48h |
| AC-5 | Order delivery to ShopERP < 10s | E2E test tạo order → verify ShopERP nhận |
| AC-6 | API response time < 500ms | curl + measure |
| AC-7 | No data loss (OutboxMessages vẫn process) | DB query count before/after |

---

## **8. APPENDIX**

### **8.1. Current Gateway resource usage (baseline)**

| Metric | Value (08/08/2026) |
|---|---|
| Docker image size | 460MB |
| RAM usage | 165MB / 512MB |
| CPU usage | 7.5% (sau poll interval 5s) |
| Controllers | 45 |
| Background services | 5 |
| SignalR hubs | 4 |
| Program.cs lines | 609 |

### **8.2. Expected resource usage after Hybrid**

| Metric | After Phase 1 | After Phase 2 | After Phase 3 |
|---|---|---|---|
| Gateway CPU | ~5% | ~2% | ~2% |
| Gateway RAM | 165MB | 120MB | 120MB |
| sync-worker CPU | — | ~5% | ~5% |
| sync-worker RAM | — | ~80MB | ~80MB |
| Total RAM | 165MB | 200MB | 200MB / 2GB |
