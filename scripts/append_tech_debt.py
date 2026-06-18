import sys
sys.stdout.reconfigure(encoding="utf-8")

path = "docs/AI/phase-next-order-accounting-improvements.md"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

tech_debt = """

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
"""

content = content.rstrip() + tech_debt
with open(path, "w", encoding="utf-8") as f:
    f.write(content)
print("Done")
