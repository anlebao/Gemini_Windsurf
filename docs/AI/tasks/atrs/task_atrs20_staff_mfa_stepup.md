# Task ATRS-20: Staff MFA/Step-Up cho Tác Vụ Nhạy Cảm (FR-014)

> **SRS:** `docs/requirements/VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` — FR-014 ("MFA/step-up staff" cho tác vụ nhạy cảm) + §13 phân quyền
> **Parent:** `task_atrs10_staff_rbac_admin.md` — residual "staff MFA step-up" ghi từ W3
> **Traceability gap:** `atrs_srs_traceability_review_2026-11-17.md` gap #2
> **Depends on:** **MFA-1 DONE** — `3_CoreHub/Services/MfaService.cs` (TOTP OtpNet + trusted-device + email fallback Brevo) + ShopERP `Login.cshtml` MFA flow + `DemoUser.MfaEnabled/MfaSecret` (migration `AddDemoUserTrustAndMfa`). atrs side: `StaffPrincipal` + `IAtrsAuthorizationService` + `ISecurityPolicyService` (W5c)
> **Mode:** IMPLEMENT (khi duyệt) · **Layer:** 11_Atrs (verify) + 5_WebApps/ShopERP (mint claims) — KHÔNG đụng Domain.cs trừ khi thật cần

## 1. OBJECTIVE

Staff JWT hiện chỉ chứng minh "đã login" — atrs enforce permission nhưng **không biết MFA có được hoàn thành gần đây không**. FR-014 yêu cầu tác vụ nhạy cảm (approve/reject recovery, dispute decision, grant permission, policy approve/change) phải có **step-up proof gần đây** (MFA TOTP hoặc trusted-device). Đóng gap này = staff không thể approve recovery chỉ bằng password bị phished.

## 2. VERIFIED FACTS

- `MfaService` (CoreHub): `GenerateSetup`, `VerifyLoginAsync(userId, code, trustedToken)` → `MfaVerifyResult` — TOTP window ±2, trusted-device cookie 30d, email OTP fallback. Login.cshtml đã enforce cho SystemAdmin/Owner.
- `AtrsAdminApiClient` (ShopERP) mint staff JWT cho atrs — hiện claims chỉ `sub` + roles (`StaffPrincipal.UserId/Roles`). **Không có `amr`/`auth_time`/`mfa` claim** (đã grep — không tồn tại).
- atrs staff endpoints check `StaffPrincipal.HasPermissionAsync` → `IAtrsAuthorizationService` — chỗ hook step-up tự nhiên: thêm check sau permission.
- `IStepUpService` (atrs) hiện chỉ phát hành/verify step-up token cho **customer** (`amr=passkey|recovery-code`) — staff cần cơ chế tương đương hoặc claim-based.
- Sensitive permission set đã xác định được từ `AtrsPermissions` + controllers: `recovery.approve`, `recovery.reject`, `dispute.decide`(+approve), `permission.grant`, `policy.approve`, `policy.manage`(?), `account-status` internal.

## 3. DESIGN (đề xuất — chốt khi implement)

### Option A — Claim-based (KHUYẾN NGHỊ, đơn giản nhất)

ShopERP đã verify MFA tại login → **truyền bằng chứng vào JWT**:

1. `MfaService.VerifyLoginAsync` trả kết quả → login flow ghi `MfaVerifiedAt`/`amr` vào claims của staff token (hoặc token riêng cho atrs).
2. `AtrsAdminApiClient`/staff token issuance thêm claims: `amr` = `pwd` | `mfa` | `mfa-device` (trusted device) ; `auth_time` = unix time MFA hoàn tất.
3. atrs: `StaffPrincipal` += `Amr()`, `AuthTime()`; authorization layer thêm `RequireRecentMfa(permission, maxAge)` — sensitive permissions yêu cầu `amr ∈ {mfa, mfa-device}` **và** `auth_time` trong cửa sổ (đề xuất 15-30', policy-configurable).
4. Staff chưa setup MFA (MfaEnabled=false): theo MFA-1 fail-safe — **banner + grace**, không lockout; sensitive actions bị chặn với thông báo "cần setup MFA" (hoặc cho qua trong grace period theo policy `staff.mfa.enforce`).

**Ưu:** không endpoint mới, không state, stateless verify, tận dụng MFA-1 100%.
**Nhược:** JWT sống lâu → `auth_time` cũ → staff phải re-login/re-MFA định kỳ (chấp nhận được — đó là mục đích step-up); trusted-device cookie 30d nghĩa `amr=mfa-device` hợp lệ 30d → step-up window phải nhỏ hơn nhiều (15') hoặc tách: trusted-device chỉ cho login, sensitive ops vẫn cần TOTP gần đây? **Cần chốt Q2.**

### Option B — atrs-native step-up endpoint

atrs phát hành staff step-up token qua TOTP verify trực tiếp (atrs không có user secrets — phải gọi ShopERP verify → phức tạp, thêm hop).
**Loại** trừ: duplicate MFA logic + atrs không giữ MfaSecret (đúng — secret ở ShopERP).

### Option C — Reuse customer WebAuthn cho staff

Staff là DemoUser ShopERP, không có atrs passkey → phải enroll riêng → scope lớn. **Phase 2** nếu cần possession-proof cho staff.

**Đề xuất: Option A**, với policy keys `staff.mfa.required_permissions`, `staff.mfa.max_auth_age_minutes`, `staff.mfa.grace_*`.

## 4. FILES (dự kiến)

| File | Change |
|---|---|
| `3_CoreHub/Services/MfaService.cs` | expose `amr`/`auth_time` trong kết quả verify (hoặc Login flow map sang claims) |
| ShopERP staff token issuance (`AtrsAdminApiClient` + nơi mint JWT — tìm trong ANALYZE) | +claims `amr`, `auth_time` |
| `11_Atrs .../Auth/StaffPrincipal.cs` | +`Amr()`, `AuthTime()` |
| `AtrsAuthorizationService` hoặc middleware | `RequireRecentMfa` check cho sensitive permission set |
| Controllers sensitive (AdminRecovery approve/reject, AdminDisputes decision, AdminPermissions grant, AdminPolicy approve) | enforce — tốt nhất tập trung ở authorization service, không rải controller |
| `SecurityPolicy` defaults | `staff.mfa.*` keys (W5c mechanism sẵn) |
| ShopERP UI | banner/redirect "cần MFA" khi atrs trả `step-up-required` (403 code mới) |
| Tests | claim parse · fresh/stale auth_time · amr matrix · permission × mfa matrix · grace behavior · fail khi thiếu claims (token cũ) |

## 5. ACCEPTANCE CRITERIA

- [ ] Staff JWT có `amr`+`auth_time`; atrs đọc đúng, token cũ không claim → xử lý rõ (reject sensitive ops với `step-up-required`, KHÔNG crash)
- [ ] Approve recovery/dispute decision/grant permission/policy approve → 403 `step-up-required` khi `auth_time` cũ hoặc `amr=pwd`
- [ ] Sau MFA TOTP verify gần → cùng action pass
- [ ] Trusted-device `amr=mfa-device`: theo quyết định Q2 (đề xuất: tính step-up nhưng window ngắn hơn, vd 15' kể từ login — trusted device chỉ prove device, không prove presence gần đây)
- [ ] Staff `MfaEnabled=false` → grace theo policy + banner; SystemAdmin/Owner đã enforce MFA-1 từ trước
- [ ] `step-up-required` response contract rõ để ShopERP UI redirect MFA setup/verify
- [ ] Audit: sensitive action thành công ghi `mfa_verified=true` trong event metadata
- [ ] Tests + build + guard PASS

## 6. RISKS / OPEN QUESTIONS (chốt trước IMPLEMENT)

1. **Trusted-device semantics:** `amr=mfa-device` (cookie 30d) có được tính step-up cho sensitive ops không? Đề xuất: **KHÔNG** — sensitive ops cần TOTP/email-OTP gần đây (`amr=mfa`); trusted-device chỉ đủ cho login. Nếu user muốn friction thấp hơn → window riêng ngắn.
2. **Step-up window:** 15'? 30'? 60'? Đề xuất 15' (cân bằng friction vs bảo mật — staff ops không frequent).
3. **Grace cho staff chưa setup MFA:** bao lâu? Đề xuất theo MFA-1 (banner + deadline), sensitive ops chặn ngay khi enforce=true.
4. **Token cũ đang lưu hành** (JWT không claims) → deploy xong staff phải re-login 1 lần — chấp nhận? (thường có, ghi vào runbook).
5. **`policy.manage` có cần step-up?** Đề xuất CÓ (policy change ảnh hưởng toàn hệ thống) — nhưng `policy.read`/view thì không. List sensitive permissions chốt trong ANALYZE.
6. Email OTP fallback của MFA-1 có set `amr=mfa` không? (đề xuất CÓ — email OTP là MFA yếu hơn TOTP nhưng vẫn second factor; ghi risk note).

## 7. VERIFICATION + RV

- Local: build + guard + atrs tests + Core.Tests MFA + ShopERP.Tests.
- RV prod: staff account có MFA → login (TOTP) → gọi approve recovery → pass; giả lập `auth_time` cũ (hoặc đợi window / mint token test) → 403 `step-up-required`; staff chưa MFA → behavior theo grace; regression staff ops đọc (queue list, audit view) không bị chặn.
- Playwright: chỉ nếu ShopERP UI có flow mới đáng kể (MFA redirect) — theo playwright.rules.

## 8. TRACEABILITY

| FR-014 clause | Cover |
|---|---|
| MFA/step-up staff cho tác vụ nhạy cảm | toàn bộ card |
| actor/reason/timestamp audit | giữ nguyên + thêm mfa metadata |
| maker-checker | không đổi — step-up bổ sung, không thay |
| least privilege | sensitive set = subset permission đã có |

**Status:** REVIEW_ONLY — chờ user duyệt + trả lời §6 (Q1 trusted-device, Q2 window, Q5 sensitive set).
**Naming:** ATRS-20. **Dependency:** MFA-1 ĐÃ DONE → không còn block ngoài; có thể implement độc lập, song song ATRS-18/19 được (khác file region).
**Ước lượng:** nhỏ-trung bình — phần lớn là claims plumbing + 1 check tập trung; rủi ro chính là semantics `amr`/trusted-device (Q1/Q2).
**Liên quan:** sau card này, atrs staff auth ngang customer step-up model → cân nhắc unify `IStepUpService` về sau (ghi nợ, không làm v1).
