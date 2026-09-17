# Task Card #4: X-Dev-OTP Gate — Security Fix (DEFERRED)

> **Status:** ✅ COMPLETE (session 2026-09-15) — X-Dev-OTP security hole sealed + dev-token endpoint added
> **Priority:** P4 → promoted + fixed (security risk resolved without breaking RV)
> **Created:** 2026-09-15
> **Master plan:** `docs/AI/tasks/community_commerce_fixes/master_plan.md`
> **Prerequisite:** None — dev-token endpoint replaces X-Dev-OTP bypass
> **Effort:** 0.5 ngày (~2h)

## Implementation (2026-09-15)

**Approach:** Test users with tokens via secret-gated dev-token endpoint.

**Changes (5 files):**
1. `5_WebApps/ShopERP/Services/CustomerTokenService.cs` — add `CreateLongLivedToken(customerId, days)` method to interface + impl (same IDataProtector, custom TTL)
2. `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — remove `X-Dev-OTP` header from `/otp/send` + `/upgrade/send-otp`; add `POST /api/customer-identity/dev-token` endpoint (secret-gated via `X-Dev-Secret` header + `DevToken:Secret` config)
3. `2_Gateway/Controllers/CustomerIdentityController.cs` — remove `X-Dev-OTP` forwarding from `/otp/send` + `/upgrade/send-otp`; add forward for `/dev-token` (forwards `X-Dev-Secret` header)
4. `5_WebApps/ShopERP/appsettings.json` — add `"DevToken": { "Secret": "" }` (empty = disabled)
5. `docker-compose.shoperp.yml` — add `DevToken__Secret=${DEV_TOKEN_SECRET:-}` env var

**Security model:**
- `DevToken:Secret` empty/unset → endpoint returns 404 (disabled). Production MUST set `DEV_TOKEN_SECRET` env var.
- Wrong secret → 401 Unauthorized.
- Correct secret → finds or creates customer by phone, returns 365-day token.
- RV scripts call `/dev-token` once with secret → use returned `X-Customer-Token` for all subsequent calls.
- Secret is rotatable — change `DEV_TOKEN_SECRET` env var, old tokens still valid (IDataProtector-based, independent of secret).

**Validation:** Build 0 errors · Guard ALL PASSED · 6/6 unit tests PASS (CustomerTokenServiceDevTokenTests).

## ⚠️ TEMPORARY DEV-TOKEN SECRET — RV ONLY (removed 2026-09-16)

> **STATUS: REMOVED — production is back to `DevToken:Secret` unset (`/dev-token` → 404).**
> Nothing to do unless RV needs to run again.

For the chat/GPS/QR RV on 2026-09-16 the dev-token endpoint was temporarily enabled on the
ShopERP VPS to mint a test customer token (no other way to obtain one — SMS OTP is not
configured and Google OAuth needs a real browser session).

**Re-used 2026-09-17** for the salesman referral QR RV (scan → buy → commission,
`task_card_06_referral_qr_scan_commission.md`) — enabled, used, then removed again the same way.

**Re-used again 2026-09-17** for the commission-base + self-referral RV
(`task_card_07_commission_base_selfreferral.md`) — same enable → use → remove cycle.

**How it was enabled (NOT persisted to `.env.shoperp`):**

```bash
# on vanan-shop-a (ShopERP VPS)
cd /opt/vanan
SECRET=$(openssl rand -hex 32)
DEV_TOKEN_SECRET="$SECRET" docker compose -f docker-compose.shoperp.yml \
  --env-file .env.shoperp up -d --force-recreate shoperp
```

**How it was removed (run this to re-secure at any time):**

```bash
cd /opt/vanan
docker compose -f docker-compose.shoperp.yml --env-file .env.shoperp up -d --force-recreate shoperp
```

**Safety properties:**
- The secret was passed as an **inline shell env var only** — it is NOT in `.env.shoperp`,
  so the next CD deploy (which rewrites `.env.shoperp` from scratch) clears it automatically.
- Verify it is off: `docker exec vanan-shoperp-1 sh -c 'env | grep -c "^DevToken__Secret=.\+"'` → must print `0`
  and `POST /api/customer-identity/dev-token` → **404**.
- A VPS marker note was left at `~/.devtoken_rv_README.txt` on the ShopERP VPS.
- Tokens already minted stay valid after the secret is removed (IDataProtector-based, independent of the secret).
- Never commit a real secret. Rotate `DEV_TOKEN_SECRET` if it is ever exposed.

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
