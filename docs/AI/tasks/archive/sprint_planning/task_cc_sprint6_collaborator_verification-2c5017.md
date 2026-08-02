# CC-S6-T5: Collaborator SMS OTP + Deposit Wallet Toggle

## 1. Goal
SystemAdmin toggle to enable/disable mandatory SMS OTP verification for collaborators (Salesman, Shipper, Owner). When ON, collaborators must verify phone via SMS OTP. Fee deducted from deposit wallet per verification attempt.

## 2. Workflow Routing
- **Mode:** ANALYZE → IMPLEMENT (newfeaturebuild.md)
- **Skills:** domain-integrity-validation, accounting-ui-implementation

## 3. Relevant Files
- `1_Shared/Domain.cs` — WalletTransactionType.Deposit=12, SmsOtpFee=13; CommunityRole.IsPhoneVerified + PhoneVerifiedAt + MarkPhoneVerified()
- `1_Shared/Domain/Aggregates/SystemSettingAggregate/SystemSetting.cs` — global config keys
- `3_CoreHub/Services/ICollaboratorVerificationService.cs` — interface + DTOs
- `3_CoreHub/Services/CollaboratorVerificationService.cs` — implementation (233 lines)
- `3_CoreHub/Infrastructure/Configurations/CommunityRoleConfiguration.cs` — EF config for new fields
- `3_CoreHub/Infrastructure/Migrations/20260730153219_CollaboratorSmsOtp.cs` — migration
- `2_Gateway/Controllers/CollaboratorVerificationController.cs` — 5 API endpoints
- `2_Gateway/Program.cs` — DI registration
- `5_WebApps/ShopERP/Services/CollaboratorVerificationApiClient.cs` — admin API client
- `5_WebApps/ShopERP/Components/Pages/Admin/CollaboratorVerification.razor` — admin toggle UI
- `5_WebApps/KhachLink/Services/Http/CollaboratorVerificationHttpService.cs` — collaborator HTTP client
- `5_WebApps/KhachLink/Pages/CollaboratorVerification.razor` — collaborator verification page
- `6_Tests/VanAn.Core.Tests/Community/CollaboratorVerificationServiceTests.cs` — 21 unit tests

## 4. Constraints
- Domain layer: NO modifications (already done in prior session)
- AccountingEntry: immutable (not touched)
- UI Platform: all components from VanAn.UI.Platform
- Auth: SystemAdmin JWT for admin endpoints; X-Customer-Token for collaborator endpoints
- OTP retry limit: max 3 OTP sends per 24h (anti-spam)
- Customer redeem points: NEVER require SMS OTP

## 5. Success Criteria
- [x] Build passes (dotnet build VanAn.sln)
- [x] 21 unit tests pass (CollaboratorVerificationServiceTests)
- [x] Settings CRUD: GetSettings + SetSettings (3 keys: Enabled, FeePerVerification, MinDeposit)
- [x] Toggle OFF → InitVerification throws
- [x] Toggle ON + insufficient balance → InitVerification throws
- [x] Toggle ON + sufficient balance → SMS sent + fee deducted + OTP cached
- [x] VerifyOtp success → CommunityRole.IsPhoneVerified = true
- [x] Retry limit: 4th OTP in 24h → throws
- [x] IsVerificationRequired: false when toggle off, false when not collaborator, true when toggle on + not verified
- [x] Gateway DI registered
- [x] Controller: 5 endpoints (settings GET/POST, init, verify, deposit, status)
- [x] ShopERP admin page with toggle + fee + min deposit inputs
- [x] KhachLink verification page with phone → OTP → success flow
- [x] Nav link in AdminLayout

## 6. Skills
- domain-integrity-validation
- accounting-ui-implementation

## 7. Health Check
- Assumptions: 0
- Verified Facts: 21 (unit tests) + build pass
- Open Questions: 0
