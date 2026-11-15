# Task ATRS-3: Token Revocation + Step-Up Authentication Primitive

> **Master plan:** §4 · **Phụ thuộc:** ATRS-1 (D3) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-011 ("thu hồi session/token cũ"), FR-010 step-up, §8.1, NFR-001
> **Gap nguồn:** JWT HS256 8h stateless + customer IDataProtector token 30d — cả hai đều không revoke được (gap analysis G-AUTH-1/G-AUTH-2)

## 1. OBJECTIVE

Xây primitive bắt buộc cho recovery/dispute: (a) revoke token theo user, (b) step-up auth ("vừa xác thực gần đây") để gate hành động nhạy cảm.

## 2. THIẾT KẾ

### 2.1 Token revocation
- Thêm `tokenVersion` (int) vào `DemoUser` + customer-side equivalent (hoặc `TrustSubject.SecurityVersion` — quyết định D1/D3 trong ATRS-1).
- JWT: claim `tv` khi issue (`JwtTokenService.GenerateToken` overload mới — **không phá claims cũ**).
- Validation: `StaffJwtValidator`/JwtBearer `OnTokenValidated` → so `tv` claim với version hiện tại (cache 30-60s, invalidate on bump) → mismatch = 401.
- Customer token: `CustomerTokenService` payload thêm version → `ValidateToken` check; bump version = kill mọi token cũ ngay (tốt hơn rotate DataProtection key).
- `RevokeAllTokensAsync(trustSubjectId)` — gọi từ recovery-complete (ATRS-8), admin break-glass.

### 2.2 Step-up primitive
- `IStepUpService` (CoreHub/Contracts): `IssueStepUpAsync(subjectId, method, amr)` → token ngắn hạn (5-10') signed, claims `sub`, `amr` (passkey|totp|recovery-code|password), `iat`.
- `RequireStepUp` filter/endpoint helper: check `X-Step-Up-Token` header hoặc claim — dùng cho: revoke device, regenerate recovery codes, đổi security settings, dispute adjust approval.
- Tái dùng `MfaService` trusted-device/TOTP làm 1 trong các `amr` cho staff; customer step-up = passkey assert (ATRS-5) hoặc recovery code (ATRS-4).

## 3. FILES (dự kiến)

- `3_CoreHub/Services/JwtTokenService.cs` (+ interface), `StaffJwtValidator` (Gateway + voice clone?), `CustomerTokenService.cs`
- `1_Shared/Domain.cs` DemoUser/Customer += `TokenVersion` (Gate 5 — field additive)
- `5_WebApps/ShopERP` customer token issue/validate path
- `2_Gateway` JWT validation events + new filter `RequireStepUp`
- `10_Voice`/`9_TenantAI` `StaffJwtValidator` clones — phải đồng bộ tv check (multi-service)

## 4. TESTS

- [ ] Token cũ (tv cũ) → 401 sau khi bump version; token mới OK
- [ ] Customer token version mismatch → invalid
- [ ] Step-up token: sai method/sai subject/hết hạn → reject; đúng → pass
- [ ] Regression: login flow hiện tại không đổi khi chưa bump version
- [ ] Cache invalidation: bump → request kế tiếp reject (không chờ cache hết hạn — hoặc chấp nhận TTL 60s, ghi rõ)

## 5. ACCEPTANCE

- [ ] Recovery-complete có API gọi `RevokeAllTokensAsync`
- [ ] Step-up gate hoạt động trên ≥1 endpoint nhạy cảm thật (device revoke)
- [ ] `dotnet build` + guard + tests pass; RV: revoke → token cũ 401 trên prod

## 6. RISKS

- Multi-service JWT validation: voice/tenantai clone `StaffJwtValidator` — sót 1 service = token cũ vẫn sống ở service đó. Card phải list ALL validators.
- Customer token nằm ở ShopERP DataProtection — version check cần chỗ lưu (ShopERP SQLite per-tenant? customer là per-tenant → version per-customer-row OK, nhưng TrustSubject cross-tenant cần quyết định).
