# Phase Next: Order Flow & Accounting Flow — Improvement Plan

> **Tạo:** 2026-06-18  
> **Nguồn:** Investigate session — trace code thực tế hai luồng Order và Accounting  
> **Mục đích:** Chuẩn hóa các vấn đề đã phát hiện → input cho phase cải tiến tiếp theo  
> **Status:** PENDING (chờ CD pipeline ổn định xong)

---

## 1. Order Flow — Vấn đề phát hiện

### 1.1 Accounting Entry tạo sai thời điểm

**Vấn đề:** `AccountingEntry` (Revenue + COGS) được tạo ngay trong `OrderService.CreateOrderFromCommandAsync()` — tức là **trước khi khách thanh toán**.

**Hệ quả:**
- Nếu khách quét QR nhưng không thanh toán → Entry vẫn tồn tại trong sổ sách
- Doanh thu bị ghi nhận sai thời điểm (vi phạm nguyên tắc thực thu)
- COGS cũng bị ghi nhận sai

**Hướng fix:**
- Di chuyển `GenerateAccountingEntriesAsync()` từ `CreateOrderFromCommandAsync()` sang `WebhookController.ProcessWebhookAsync()` — chỉ ghi nhận sau khi bank confirm thanh toán
- Hoặc: Tạo entry với status `Pending`, chuyển sang `Posted` khi webhook confirm

**File liên quan:**
- `3_CoreHub/Services/OrderService.cs` line 80 — `GenerateAccountingEntriesAsync()`
- `2_Gateway/Controllers/WebhookController.cs` — webhook handler
- `3_CoreHub/Services/IAccountingService.cs` — thêm `PostEntryAsync()`

---

### 1.2 COGS hardcode 70%

**Vấn đề:** `3_CoreHub/Services/OrderService.cs` tính COGS = `TotalPrice × 0.7` — không dựa trên giá vốn thực tế của từng sản phẩm.

**Hệ quả:**
- Báo cáo lợi nhuận không chính xác
- Không phân biệt được sản phẩm có margin cao/thấp

**Hướng fix:**
- Lấy `CostPrice` từ `Product` entity khi tạo `OrderItem`
- Tính COGS = `SUM(OrderItem.Quantity × Product.CostPrice)`
- Fallback về 70% nếu Product chưa có CostPrice (backward compat)

**File liên quan:**
- `3_CoreHub/Services/OrderService.cs` — lines 119-136
- `1_Shared/Domain.cs` — `Product` entity (kiểm tra có `CostPrice` field không)

---

### 1.3 TenantId hardcode fallback

**Vấn đề:** Nhiều controller/service có fallback `TenantId = Guid("00000000-0000-0000-0000-000000000001")` khi không đọc được từ JWT claims.

**Hệ quả:**
- Data production của tất cả tenants bị trộn lẫn vào TenantId demo
- Multi-tenancy bị phá vỡ silently

**Hướng fix:**
- Bỏ fallback hardcode — throw `UnauthorizedException` nếu không có TenantId trong claims
- Fix login flow để set TenantId claim đúng cách
- Thêm middleware validate TenantId claim

**File liên quan:**
- `2_Gateway/Controllers/OrdersController.cs` line 29 — `// TODO: Get from tenant provider`
- `5_WebApps/ShopERP/Components/Pages/Accounting/*.razor` — fallback `0000...0001`

---

### 1.4 Webhook chưa notify Kitchen

**Vấn đề:** `WebhookController.ProcessWebhookAsync()` update `PaymentStatus=Paid` nhưng không trigger kitchen workflow.

**Hệ quả:**
- Sau khi khách thanh toán, bếp không nhận được order tự động
- Staff phải refresh tay hoặc poll

**Hướng fix:**
- Sau `ProcessWebhookAsync()`, publish event `PaymentConfirmedEvent`
- `KitchenService` subscribe event này, tự động add order vào queue bếp
- Hoặc: Dùng SignalR `OrderHub.SendAsync("PaymentConfirmed", orderId)` → KhachLink + ShopERP update realtime

**File liên quan:**
- `2_Gateway/Controllers/WebhookController.cs`
- `2_Gateway/Hubs/OrderHub.cs`
- `3_CoreHub/Services/KitchenService.cs`

---

### 1.5 OrdersController — TenantId từ claims chưa hoàn thiện

**Vấn đề:** Comment `// TODO: Get from tenant provider` tại `OrdersController.cs:29`.

**Hướng fix:** Implement `ITenantProvider` service, inject vào controller, lấy TenantId từ claims đúng cách.

---

## 2. Accounting Flow — Vấn đề phát hiện

### 2.1 Manual entry không lưu Account Code

**Vấn đề:** `RevenueEntry.razor` và `ExpenseEntry.razor` cho user chọn account code (511, 621...) nhưng `CreateRevenueEntryRequest` / `CreateExpenseEntryRequest` **không có field `AccountCode`**. Service chỉ nhận `amount` và `description`.

**Hệ quả:**
- Account code mà user chọn trên UI bị bỏ qua
- Tất cả manual entries đều không có account code → sổ sách không đúng

**Hướng fix:**
- Thêm `AccountCode` vào `CreateRevenueEntryRequest` và `CreateExpenseEntryRequest`
- Truyền `AccountCode` xuống `IAccountingService.CreateRevenueEntryAsync()` / `CreateExpenseEntryAsync()`
- Lưu vào `AccountingEntry.AccountCode`

**File liên quan:**
- `2_Gateway/Controllers/AccountingEntriesController.cs` — request DTOs
- `3_CoreHub/Services/IAccountingService.cs` — method signatures
- `3_CoreHub/Services/AccountingEntryService.cs` — implementation
- `1_Shared/DTOs/AccountingEntryDto.cs` — đã có `AccountCode` field ✅

---

### 2.2 Manual entry thiếu field Vendor, Category, Reference ở API layer

**Vấn đề:** `ExpenseEntry.razor` có fields `vendor`, `category`, `reference` nhưng `CreateExpenseEntryRequest` không có các fields này → data bị mất khi submit.

**Hướng fix:**
- Thêm `Vendor?`, `Category?`, `Reference?` vào `CreateExpenseEntryRequest`
- Tương tự `RevenueEntry`: thêm `Reference?`
- Map sang `AccountingEntry` domain entity

**File liên quan:**
- `2_Gateway/Controllers/AccountingEntriesController.cs`
- `1_Shared/DTOs/ExpenseEntryDto.cs` — đã có các fields ✅ (chỉ cần wire lên)

---

### 2.3 Duplicate detection chỉ ở client-side

**Vấn đề:** Kiểm tra duplicate (same amount + date + account trong 5 phút) chỉ được thực hiện trong Razor component — server không validate.

**Hệ quả:**
- Có thể bypass bằng cách gọi API trực tiếp
- Race condition nếu 2 requests đồng thời

**Hướng fix:**
- Thêm server-side duplicate detection trong `AccountingEntryService`
- Query: `entries.Any(e => e.Amount == amount && e.TransactionDate >= now.AddMinutes(-5) && e.AccountCode == accountCode)`

---

### 2.4 Period Closing chưa ngăn chặn new entries

**Vấn đề:** Khi period đã đóng (`PeriodClosingStatus.Closed`), vẫn có thể tạo entry mới vào kỳ đó qua `POST /api/accountingentries/revenue` nếu gọi API trực tiếp.

**Hướng fix:**
- Trong `AccountingEntryService.CreateRevenueEntryAsync()`: kiểm tra `IPeriodClosingService.GetPeriodStatusAsync()` trước khi tạo entry
- Throw `InvalidOperationException("Kỳ kế toán đã đóng sổ")` nếu closed

---

### 2.5 AccountBalance.razor tính số dư sai kiến trúc

**Vấn đề:** `AccountBalance.razor` tự nhóm entries theo `AccountCode` in-memory ở UI layer — không dùng `IHKDBookService`.

**Hướng fix:**
- Thay bằng call `IHKDBookService.GetRevenueTotalAsync()` / `GetExpenseTotalAsync()`
- Hoặc thêm API endpoint `GET /api/accountingentries/balance?year=&month=` trả về pre-computed balance

---

### 2.6 Export Excel là placeholder

**Vấn đề:** `TransactionHistory.razor` có nút "Export Excel" nhưng chưa implement.

**Hướng fix (thấp ưu tiên):**
- Dùng `ClosedXML` hoặc `EPPlus` để generate `.xlsx`
- Hoặc export CSV trước (đơn giản hơn)

---

## 3. Priority Matrix

| # | Vấn đề | Impact | Effort | Priority |
|---|---|---|---|---|
| 1.1 | Accounting entry tạo trước khi thanh toán | 🔴 Critical | Medium | **P0** |
| 1.3 | TenantId hardcode fallback | 🔴 Critical | Low | **P0** |
| 2.1 | Account code không được lưu | 🔴 High | Low | **P0** |
| 2.2 | Vendor/Category/Reference bị mất | 🟠 High | Low | **P1** |
| 1.4 | Webhook không notify Kitchen | 🟠 High | Medium | **P1** |
| 2.3 | Duplicate detection chỉ client-side | 🟡 Medium | Low | **P2** |
| 2.4 | Period closing không block new entries | 🟡 Medium | Low | **P2** |
| 1.2 | COGS hardcode 70% | 🟡 Medium | Medium | **P2** |
| 2.5 | AccountBalance tính in-memory | 🟢 Low | Medium | **P3** |
| 1.5 | TenantProvider chưa implement | 🟡 Medium | Medium | **P2** |
| 2.6 | Export Excel placeholder | 🟢 Low | Low | **P3** |

---

## 4. Đề xuất scope Phase Next

### Phase Next — Sprint A: Data Integrity Fixes (P0)
1. Fix TenantId: bỏ fallback hardcode, throw nếu không có claim
2. Fix account code không lưu: wire `AccountCode` từ UI → API → DB
3. Move accounting entry creation: từ `CreateOrder` → `PaymentWebhook`

### Phase Next — Sprint B: Flow Completion (P1)
1. Wire Vendor/Category/Reference fields từ UI xuống DB
2. Webhook notify Kitchen via SignalR sau payment confirm
3. Server-side duplicate detection cho accounting entries

### Phase Next — Sprint C: Polish (P2-P3)
1. Period closing block new entries at service layer
2. COGS từ Product.CostPrice thay vì 70% hardcode
3. AccountBalance dùng HKDBookService
4. Export CSV/Excel

---

## 5. Files cần đọc khi bắt đầu phase

```
3_CoreHub/Services/OrderService.cs              — Order creation + accounting entry trigger
3_CoreHub/Services/AccountingEntryService.cs    — Accounting business logic
2_Gateway/Controllers/WebhookController.cs      — Payment webhook
2_Gateway/Controllers/AccountingEntriesController.cs — API endpoints + request DTOs
5_WebApps/ShopERP/Components/Pages/Accounting/RevenueEntry.razor
5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor
1_Shared/Domain.cs                              — AccountingEntry entity
```

---

## 6. KhachLink Architecture Debt (Discovered 2026-06-18)

### Root Cause
KhachLink trực tiếp inject CoreHub services + repositories đòi hỏi `IVanAnDbContext`:
- `CustomerRepository(IVanAnDbContext)`
- `LoyaltyRewardsRepository(IVanAnDbContext)`
- `SocialCampaignRepository(IVanAnDbContext)`
- `OrderRepository(IVanAnDbContext)`
- `SystemMetricsRepository(IVanAnDbContext)`

Architecture test **VA-KHACHLINK-004** enforce: KhachLink (Client UI) không được phép access DB trực tiếp.

### Fix tạm (đã áp dụng trong PR #35)
- Bỏ Loyalty + Customer khỏi `Index.cshtml` — page load được, mất tính năng điểm thưởng.
- Bỏ tất cả repository registrations khỏi `KhachLink/Program.cs`.

### Fix triệt để (Sprint B — P1)
**Gateway cần thêm CustomersController:**
- `POST /api/customers/device` — GetOrCreateCustomerByDeviceId(deviceId: Guid)
- `GET /api/customers/{id}/loyalty` — GetCustomerRewards(customerId: Guid)

**KhachLink cần Gateway-backed service implementations:**
- `GatewayCustomerService : ICustomerService` — gọi HttpClient("gateway") thay vì DB
- `GatewayLoyaltyRewardsService : ILoyaltyRewardsService` — gọi HttpClient("gateway") thay vì DB
- Register Gateway-backed implementations thay cho CoreHub implementations trong Program.cs
- Restore LoyaltyRewards + Customer usage trong Index.cshtml.cs

**Files cần tạo/sửa:**
- `2_Gateway/Controllers/CustomersController.cs` (mới)
- `5_WebApps/KhachLink/Services/GatewayCustomerService.cs` (mới)
- `5_WebApps/KhachLink/Services/GatewayLoyaltyRewardsService.cs` (mới)
- `5_WebApps/KhachLink/Program.cs` — swap DI registrations
- `5_WebApps/KhachLink/Pages/Index.cshtml.cs` — restore Loyalty usage
