# Task Card #4: X-Dev-OTP Gate — Security Fix (DEFERRED)

> **Status:** DEFERRED — ghi nhận để fix sau khi có alternative test auth mechanism
> **Priority:** P4 — security risk, nhưng cần cho bypass test hiện tại
> **Created:** 2026-09-15
> **Master plan:** `docs/AI/tasks/community_commerce_fixes/master_plan.md`
> **Prerequisite:** Alternative test auth mechanism (dev token endpoint hoặc staging env) trước khi fix
> **Effort:** 0.5 ngày (~2h) khi implement

## Problem

`CustomerIdentityController.cs:63` set `X-Dev-OTP` header **unconditional** — không gate `IsDevelopment()`. Bất kỳ ai call `POST /api/customer-identity/otp/send` đều nhận OTP trong response header → **bypass hoàn toàn SMS authentication**.

```csharp
// CustomerIdentityController.cs (ShopERP)
[HttpPost("otp/send")]
public IActionResult SendOtp([FromBody] SendOtpRequest request)
{
    var otp = _otpService.GenerateAndStoreOtp(request.PhoneNumber);
    _logger.LogInformation("OTP generated for phone {Phone}", MaskPhone(request.PhoneNumber));

    Response.Headers["X-Dev-OTP"] = otp;  // ← KHÔNG GATE — luôn expose OTP
    return Ok(new { message = "OTP đã được gửi. Vui lòng kiểm tra tin nhắn." });
}
```

Comment line 51 nói "In dev (IsDevelopment): exposes OTP via X-Dev-OTP response header" nhưng code **không có gate**.

**Tác động security:**
- Attacker gửi OTP request cho bất kỳ SĐT nào
- Đọc `X-Dev-OTP` response header → có OTP
- Call `POST /api/customer-identity/otp/verify` với OTP → có customer token
- Truy cập toàn bộ customer API (orders, loyalty, community, chat)

**Tại sao DEFER:**
- Cần `X-Dev-OTP` cho OTP bypass test (RV community commerce)
- Nếu fix ngay → không thể test full flow cho đến khi có alternative test auth
- Ghi nhận để fix khi:
  - Có dev token endpoint (staging env only)
  - Hoặc có staging deployment riêng với `IsDevelopment()=true`
  - Hoặc security review yêu cầu fix khẩn cấp

## Solution (khi implement)

Gate `X-Dev-OTP` header bằng `IHostEnvironment.IsDevelopment()` hoặc config flag:

### Option A: IHostEnvironment gate (preferred)

```csharp
public class CustomerIdentityController(
    // ... existing deps ...
    IHostEnvironment env,  // NEW
    // ...
)
{
    private readonly IHostEnvironment _env = env;

    [HttpPost("otp/send")]
    public IActionResult SendOtp([FromBody] SendOtpRequest request)
    {
        var otp = _otpService.GenerateAndStoreOtp(request.PhoneNumber);

        // Gate: chỉ expose OTP trong Development environment
        if (_env.IsDevelopment())
            Response.Headers["X-Dev-OTP"] = otp;

        return Ok(new { message = "OTP đã được gửi. Vui lòng kiểm tra tin nhắn." });
    }
}
```

### Option B: Config flag (flexible hơn)

```csharp
// appsettings.Production.json: "DevOtp": { "Enabled": false }
// appsettings.Development.json: "DevOtp": { "Enabled": true }

if (_configuration.GetValue<bool>("DevOtp:Enabled", false))
    Response.Headers["X-Dev-OTP"] = otp;
```

**Khuyến nghị:** Option A (IHostEnvironment) — đơn giản, không cần config, tự động gate theo environment.

**Áp dụng cho cả 2 endpoints:**
1. `POST /api/customer-identity/otp/send` (line 63) — anonymous OTP send
2. `POST /api/customer-identity/upgrade/send-otp` (line 241) — authenticated upgrade OTP

## Scope Checklist (khi implement)

### Phase A — Fix (Day 1)
- [ ] A1: `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — Inject `IHostEnvironment` vào constructor
- [ ] A2: Gate `X-Dev-OTP` header ở `SendOtp` (line 63) với `if (_env.IsDevelopment())`
- [ ] A3: Gate `X-Dev-OTP` header ở `SendUpgradeOtp` (line 241) với same gate
- [ ] A4: `dotnet build VanAn.sln` PASS
- [ ] A5: Unit test — `X-Dev-OTP` header present in Development, absent in Production

### Phase B — Deploy + Verify (Day 1)
- [ ] B1: CI/CD PASS
- [ ] B2: Production: `POST /api/customer-identity/otp/send` → response KHÔNG có `X-Dev-OTP` header
- [ ] B3: Development: same call → response CÓ `X-Dev-OTP` header
- [ ] B4: Existing OTP login flow still works (real SMS)

### Phase C — Alternative test auth (prerequisite for this fix)
- [ ] C1: Implement dev token endpoint OR staging environment
- [ ] C2: Update RV scripts to use alternative auth
- [ ] C3: Verify full community commerce RV still works without X-Dev-OTP

## Prerequisites (before this fix can be implemented)

- **Alternative test auth mechanism** — một trong:
  - Dev token endpoint (`POST /api/customer-identity/dev-token` gated by `IsDevelopment()`)
  - Staging deployment với `ASPNETCORE_ENVIRONMENT=Development`
  - Test user credentials với known tokens
- RV scripts updated to use alternative auth
- Security review approval

## Verification (khi implement)

1. **Production security:** `curl -X POST https://api2.khachvip.online/api/customer-identity/otp/send -H 'Content-Type: application/json' -d '{"phoneNumber":"0900000001"}' -i | grep X-Dev-OTP` → KHÔNG có header
2. **Dev functionality:** Same call in Development env → CÓ header
3. **OTP login still works:** Real SMS OTP flow unaffected
4. **RV still works:** Alternative test auth mechanism works for full flow

## Files to modify (khi implement)

| File | Change | Lines |
|---|---|---|
| `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | Inject IHostEnvironment + gate 2 headers | ~5 lines |

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Fix break bypass test | DEFER — chỉ fix khi có alternative auth |
| R2 | Production env misconfigured as Development | Verify `ASPNETCORE_ENVIRONMENT=Production` in docker-compose |
| R3 | Alternative auth mechanism không work | Test thoroughly before removing X-Dev-OTP |
| R4 | Existing OTP login flow break | Gate chỉ affect header, không affect OTP generation/verification logic |

## Related

- Master plan: `docs/AI/tasks/community_commerce_fixes/master_plan.md`
- Source: `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` (line 63, 241)
- Gateway forward: `2_Gateway/Controllers/CustomerIdentityController.cs` (line 42-43 — forward X-Dev-OTP header)
- RV bypass: `.devin/rv-cc-full-otp.js` (uses X-Dev-OTP for OTP login)
- Security: bất kỳ ai có internet access có thể bypass SMS auth
