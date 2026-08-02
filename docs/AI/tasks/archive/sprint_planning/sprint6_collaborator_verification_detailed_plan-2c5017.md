# CC-S6-T5 Detailed Plan: Collaborator SMS OTP + Deposit Wallet

## 1. Use Cases

### UC-02b: Collaborator SMS OTP Verification + Deposit Wallet
- **Actor:** Salesman, Shipper, Owner (collaborators)
- **Precondition:** SystemAdmin has toggled ON `CollaboratorSmsVerificationEnabled`
- **Flow:**
  1. Collaborator navigates to `/collaborator-verification` (KhachLink)
  2. Page calls `GET /api/collaborator-verification/status` → shows verification required
  3. Collaborator enters phone number → `POST /api/collaborator-verification/init`
  4. Service checks: toggle ON, deposit balance ≥ fee, retry count < 3/24h
  5. Service generates 6-digit OTP, sends SMS via `ISmsService`, deducts fee via `IWalletService`
  6. OTP cached in `IMemoryCache` (5-min expiry)
  7. Collaborator enters OTP → `POST /api/collaborator-verification/verify`
  8. Service validates OTP from cache, finds active `CommunityRole`, calls `MarkPhoneVerified()`
  9. OTP removed from cache (one-time use)
- **Alternative flows:**
  - Toggle OFF → status returns `verificationRequired=false` → no action needed
  - Insufficient balance → init throws → UI shows deposit form
  - SMS send fails → init throws → no fee deducted
  - OTP mismatch → verify throws → user can retry
  - OTP expired → verify throws → user requests new OTP
  - 3 OTPs already sent in 24h → init throws (anti-spam)

### UC-Admin: SystemAdmin Toggle
- **Actor:** SystemAdmin
- **Flow:**
  1. Admin navigates to `/admin/collaborator-verification` (ShopERP)
  2. Page calls `GET /api/admin/collaborator-verification/settings` → loads current state
  3. Admin toggles ON/OFF, sets fee + min deposit → `POST /api/admin/collaborator-verification/settings`
  4. Service upserts 3 `SystemSetting` rows: `CollaboratorSmsVerificationEnabled`, `SmsOtpFeePerVerification`, `CollaboratorMinDeposit`

## 2. TDD Plan (21 test cases)

### Settings (4 tests)
1. `GetSettings_Defaults_WhenNoSettingRow` — returns defaults (false, 200, 10000)
2. `SetSettings_PersistsAllThreeKeys` — all 3 settings saved + retrievable
3. `SetSettings_ThrowsWhenFeeNegative` — ArgumentException
4. `SetSettings_ThrowsWhenMinDepositNegative` — ArgumentException

### InitVerification (5 tests)
5. `InitVerification_ThrowsWhenToggleOff` — InvalidOperationException
6. `InitVerification_ThrowsWhenInsufficientBalance` — InvalidOperationException with balance message
7. `InitVerification_SendsSms_AndDeductsFee_AndCachesOtp` — SMS sent, fee deducted, OTP in cache
8. `InitVerification_ThrowsWhenSmsSendFails` — InvalidOperationException
9. `InitVerification_ThrowsWhenEmptyPhoneNumber` — ArgumentException

### Retry Limit (1 test)
10. `InitVerification_ThrowsAfterMaxRetriesPerDay` — 3 OK, 4th throws

### VerifyOtp (4 tests)
11. `VerifyOtp_ThrowsWhenOtpNotFound` — no OTP in cache
12. `InitAndVerifyOtp_Success_MarksPhoneVerified` — full flow, role updated, OTP removed
13. `VerifyOtp_ThrowsWhenOtpMismatch` — wrong code
14. `VerifyOtp_ThrowsWhenNoActiveRole` — no CommunityRole found
15. `VerifyOtp_ThrowsWhenEmptyCode` — ArgumentException

### Deposit (2 tests)
16. `Deposit_PositiveAmount_CreatesTransaction` — balance updated
17. `Deposit_ThrowsWhenZeroOrNegative` — ArgumentException

### IsVerificationRequired (4 tests)
18. `IsVerificationRequired_FalseWhenToggleOff`
19. `IsVerificationRequired_FalseWhenNotCollaborator`
20. `IsVerificationRequired_TrueWhenToggleOn_AndNotVerified`
21. `IsVerificationRequired_FalseWhenAlreadyVerified`

## 3. Coding Plan

### Layer 1: Domain (already done)
- `WalletTransactionType`: Deposit=12, SmsOtpFee=13
- `CommunityRole`: IsPhoneVerified, PhoneVerifiedAt, MarkPhoneVerified()
- No new entities (SystemSetting exists from Sprint 7)

### Layer 2: Infrastructure (already done)
- `CommunityRoleConfiguration`: IsPhoneVerified (bool, default false), PhoneVerifiedAt (nullable)
- Migration: `20260730153219_CollaboratorSmsOtp`

### Layer 3: Service (already done + retry limit added)
- `CollaboratorVerificationService` — 233 lines
- Dependencies: `IVanAnDbContext`, `ISmsService`, `IWalletService`, `IMemoryCache`
- SystemSetting keys: `CollaboratorSmsVerificationEnabled`, `SmsOtpFeePerVerification`, `CollaboratorMinDeposit`
- OTP: 6-digit, 5-min expiry, IMemoryCache
- Retry: max 3/24h, IMemoryCache counter

### Layer 4: Gateway Controller (new)
- `CollaboratorVerificationController` — 5 endpoints
- Admin: `GET/POST /api/admin/collaborator-verification/settings` (SystemAdmin JWT)
- Collaborator: `POST /api/collaborator-verification/init|verify|deposit`, `GET /api/collaborator-verification/status` (X-Customer-Token)
- DI: registered in `Program.cs`

### Layer 5: ShopERP Admin UI (new)
- `CollaboratorVerificationApiClient` — extends `GatewayAdminApiClientBase`
- `CollaboratorVerification.razor` — `/admin/collaborator-verification`, AdminLayout, SystemAdmin auth
- Nav link in `AdminLayout.razor`

### Layer 6: KhachLink UI (new)
- `CollaboratorVerificationHttpService` — X-Customer-Token header, 4 methods
- `CollaboratorVerification.razor` — `/collaborator-verification`, 3-step flow (phone → OTP → success)
- DI: registered in `Program.cs`

## 4. Namespace Strategy
- Service: `VanAn.CoreHub.Services` (existing)
- Controller: `VanAn.Gateway.Controllers` (existing)
- ShopERP client: `VanAn.ShopERP.Services` (existing)
- KhachLink HTTP: `VanAn.KhachLink.Services.Http` (existing)
- Tests: `VanAn.Core.Tests.Community` (existing)

## 5. API Endpoints Summary

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/admin/collaborator-verification/settings` | SystemAdmin JWT | Get toggle + fee + min deposit |
| POST | `/api/admin/collaborator-verification/settings` | SystemAdmin JWT | Update toggle + fee + min deposit |
| GET | `/api/collaborator-verification/status` | X-Customer-Token | Check if verification required |
| POST | `/api/collaborator-verification/init` | X-Customer-Token | Send SMS OTP, deduct fee |
| POST | `/api/collaborator-verification/verify` | X-Customer-Token | Verify OTP, mark phone verified |
| POST | `/api/collaborator-verification/deposit` | X-Customer-Token | Deposit money for SMS fees |
