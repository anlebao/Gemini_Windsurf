# Task ATRS-21: Customer Passkey Login — Materialize Account cho Voice Call (FREE, 0đ)

> **SRS liên quan:** Identity tiered-trust L0→L1 (`van-an-identity-tiered-trust-master-plan.md`) · Voice Service S5 Option 1 (guest không gọi được) · ATRS-5 WebAuthn · ATRS-16 guest device identity
> **User directive (2026-11-18):** customer không có Google/Facebook → cần cách free hoàn toàn nâng guest → "tài khoản" để sẵn sàng voice call; ưu tiên dùng ATRS, KHÔNG phụ thuộc SMS/email/OAuth bên thứ 3. **TDD bắt buộc** — viết test trước, không bỏ sót requirement.
> **Depends on (ĐÃ DONE):** ATRS-5 `IWebAuthnService` + `WebAuthnChallenge` + `DeviceCredential` · ATRS-16 `GetOrCreateForDeviceAsync` (guest → TrustSubject) · `AtrsCustomerContextService` guest fallback · ShopERP `[InternalApiKey]` filter + `ICustomerTokenService` · KhachLink `webauthn.js` (global, `index.html:119`) · atrs→ShopERP named client `"shoperp"` (`Atrs:ShopErpBaseUrl`)
> **Mode:** IMPLEMENT (khi duyệt) · **Layer:** 11_Atrs (domain-pure service + api) + 5_WebApps/ShopERP (1 internal endpoint) + 5_WebApps/KhachLink (login UI) — **KHÔNG đụng voice service** (customer_token đã đủ `VoiceCallIdentity`)

## 1. OBJECTIVE

Customer (buyer) không có Google/Facebook account vẫn login được bằng **passkey** (vân tay/khuôn mặt/PIN — platform authenticator có sẵn mọi smartphone) → nhận `customer_token` → đủ điều kiện voice call, chat danh tính, loyalty merge. Chi phí 0đ, credential lưu trong ATRS Postgres, không phụ thuộc SMS/email/OAuth.

**Hai ceremony:**
- **Register** (guest → có passkey): chạy khi guest có `X-Customer-Device-Id` — gắn credential vào guest TrustSubject (DeviceCluster).
- **Login** (mọi thiết bị → token): discoverable-credential assertion → resolve TrustSubject → resolve customerId → ShopERP mint `customer_token` → trả về KhachLink.

## 2. VERIFIED FACTS (đã kiểm chứng 2026-11-18)

| # | Fact | Evidence |
|---|---|---|
| F1 | `IWebAuthnService.BeginAuthenticationAsync(trustSubjectId,...)` là **subject-scoped** — cần biết subject trước, không dùng được cho login (user chưa biết mình là ai) | `WebAuthnService.cs:142` |
| F2 | `DeviceCredential.CredentialId` **unique toàn cục** (enforce `IsCredentialIdUniqueToUserCallback`) → có thể resolve credential→subject mà không cần userHandle | `WebAuthnService.cs:89-90` |
| F3 | `userHandle` = `trustSubjectId.ToByteArray()` (16 bytes) đã encode trong `Fido2User.Id` | `WebAuthnService.cs:41` |
| F4 | `WebAuthnChallenge.TrustSubjectId` là subject-scoped; challenge có `Purpose` (Registration/Authentication), TTL 5', one-time consume, origin+RP binding | `WebAuthnService.cs:307-357` |
| F5 | `TrustSubjectLink` có `TrustIdentityType.Customer` → `identityId = customerId` → atrs resolve được subject→customerId; `DeviceCluster` → `identityId = SHA256(deviceId)` (raw deviceId KHÔNG lưu) | `TrustProfileService.cs:34-95`, `AtrsEnums.cs:31-36` |
| F6 | `AtrsCustomerContextService` guest fallback: `X-Customer-Device-Id` (8-128 chars) → guest TrustSubject — register passkey cho guest đã khả dụng về auth context | `AtrsCustomerContextService.cs` |
| F7 | ShopERP `[InternalApiKey]` filter: `X-Internal-Api-Key` == `InternalLoyalty:ApiKey` config — atrs gọi được nếu có key | `InternalApiKeyAttribute.cs:17-40` |
| F8 | `ICustomerTokenService.CreateToken(customerId)` — DataProtection token, format giống OAuth callback token | `CustomerTokenService.cs` |
| F9 | atrs có named HttpClient `"shoperp"` → `Atrs:ShopErpBaseUrl` (compose `ATRS_SHOPERP_BASE_URL`) — tái dùng cho internal call | `Program.cs:74`, `docker-compose.atrs.yml:12` |
| F10 | KhachLink `webauthn.js` global: `isSupported()`, `getOrigin()`, `createCredential(options)`, `getAssertion(options)` — xử lý base64url; `allowCredentials` rỗng → browser hiện passkey picker (discoverable) | `webauthn.js:94-117` |
| F11 | `VoiceCallIdentity => IsStaff \|\| CustomerToken != null` — chỉ cần `customer_token` trong localStorage là gọi được, voice service KHÔNG cần đổi | `RealtimeChatPanel.razor:281` |
| F12 | RP ID suffix validation server-side (`IsRpIdValidForOrigin`) — passkey gắn domain `*.khachvip.online` nếu RpId=khachvip.online → dùng được trên mọi tenant subdomain | `WebAuthnService.cs:297-305` |
| F13 | Login.razor hiện chỉ Google/Facebook/Guest — KHÔNG có phone/password; customer token lưu `localStorage.customer_token` | `Login.razor` |
| F14 | Guest ShopERP Customer chỉ tồn tại SAU checkout stub (`CreateCustomerStubAsync`); guest chưa order → chưa có Customer row → endpoint by-device phải create stub | `CustomerIdentityController.cs` pattern |

## 3. DESIGN (đã chốt theo khuyến nghị — user duyệt card = duyệt design)

### 3.1 atrs — Passkey Login ceremony (discoverable credentials)

`IWebAuthnService` += 2 method mới (KHÔNG sửa method cũ — giữ subject-scoped cho step-up/recovery):

```csharp
Task<BeginPasskeyLoginResultDto> BeginPasskeyLoginAsync(string origin, string rpId, CancellationToken ct);
Task<FinishPasskeyLoginResultDto> FinishPasskeyLoginAsync(string challenge, string origin, string rpId,
    JsonElement assertionResponse, string? deviceId, CancellationToken ct);
```

- **Begin:** `GetAssertionOptions` với `AllowedCredentials = []` (rỗng = discoverable/resident-key flow — browser hiện danh sách passkey của RP). `UserVerification = Required` cho login (step-up dùng Preferred — login cần chắc chắn là chủ).
- **Challenge storage:** `WebAuthnChallenge.TrustSubjectId` hiện bắt buộc subject → **migration**: cho phép `TrustSubjectId = Guid.Empty` sentinel cho purpose `PasskeyLogin` (hoặc thêm cột nullable — chọn 1 trong ANALYZE, ghi quyết định vào card khi implement; khuyến nghị sentinel vì ít migration hơn). `StoreChallengeAsync` overload nhận subjectId nullable/empty.
- **Finish:** parse `rawResponse.RawId` (credentialId) → lookup `DeviceCredentials` by `CredentialId` + `Status=ACTIVE` **không filter subject** → lấy `TrustSubjectId` → verify assertion với `StoredPublicKey`/`SignCount` của credential đó (tái dùng logic `MakeAssertionAsync`, bỏ `IsUserHandleOwnerOfCredentialIdCallback` subject-check hoặc giữ userHandle==subjectId như hiện tại — credentialId đã đủ vì globally unique) → `credential.MarkUsed` → consume challenge → resolve customerId → gọi ShopERP → trả token.
- **Challenge binding login:** challenge lookup by `Challenge + Purpose=PasskeyLogin + ConsumedAt==null` (KHÔNG theo subject vì chưa biết) + origin/rpId/expiry checks giữ nguyên. Rate guard riêng (xem §3.4).

### 3.2 atrs → ShopERP — customer token issuance

Endpoint MỚI trong ShopERP:

```
POST /api/internal/customer-token/by-device
Headers: X-Internal-Api-Key: <InternalLoyalty:ApiKey>
Body: { "deviceId": "...", "tenantId": "optional-guid" }
→ find Customer by DeviceId (tenant scope nếu có) → chưa có → create stub tối thiểu
  (FullName="Khách hàng", PhoneNumber=null, DeviceId, IdentityLevel=DeviceVerified)
→ ICustomerTokenService.CreateToken(customer.Id)
→ 200 { customerToken, customerId, created: bool }
```

- `[InternalApiKey]` attribute có sẵn — atrs gửi key = `InternalLoyalty:ApiKey` value. **atrs cần config mới** `Atrs:ShopErpInternalApiKey` (env `ATRS_SHOPERP_INTERNAL_API_KEY`) — wire compose + GH secret theo pattern `ATRS_INTERNAL_API_KEY` vừa ship (ATRS-17). Key value = cùng `INTERNAL_LOYALTY_API_KEY` hiện có (1 shared internal secret — ghi rõ trong card, không tạo secret mới).
- **Prefer path có customerId sẵn:** nếu TrustSubject có link `Customer` → atrs resolve customerId → gọi `POST /api/internal/customer-token/by-id { customerId }` (nhẹ hơn, không scan DeviceId). Cả 2 endpoint share logic mint — implement `by-device` + `by-id` trong 1 controller `InternalCustomerTokenController`.
- **Chỉ DeviceCluster link (guest chưa từng order):** client gửi kèm `X-Customer-Device-Id` trong finish request → atrs verify `StableGuidFrom(deviceId)` == link.IdentityId (chứng minh deviceId thật thuộc subject — chống người khác dùng passkey-sync trên máy lạ? KHÔNG — passkey sync là feature; deviceId check chỉ để resolve customer, không phải auth). Thực ra deviceId chỉ cần để ShopERP tìm/tạo Customer — atrs forward nguyên deviceId, ShopERP tự xử. **Bỏ verify hash** — deviceId là input resolve, không phải proof (passkey assertion mới là proof). Ghi rõ: guest passkey login trên máy mới không có deviceId → ShopERP tạo customer stub mới theo... không có deviceId thì sao? → **quyết định:** login request BẮT BUỘC `deviceId` (client luôn có trong localStorage; máy mới → JS tạo mới → customer stub mới → merge sau qua CustomerMergeService khi cần). Customer merge theo phone/OAuth vẫn hoạt động sau.

### 3.3 KhachLink — Login UI

`Login.razor` step Choice += nút **"Đăng nhập bằng vân tay / khuôn mặt"** (chỉ hiện khi `vananWebAuthn.isSupported()`):

1. `GET deviceId` từ localStorage `customer_device_id` (tạo nếu chưa có — pattern `LoginWithGoogle` đã đọc key này).
2. `POST /api/v1/account-trust/passkey/login/begin` (qua gateway client — YARP `/api/v1/account-trust/*` đã route sẵn) → nhận `challenge` + `options`.
3. `vananWebAuthn.getAssertion(options)` → `POST .../finish` kèm `deviceId` + assertion JSON.
4. Success → `localStorage.customer_token = customerToken` (+ `login_provider="passkey"`) → redirect `/profile` (giống OAuth callback path).
5. Fail → error generic "Đăng nhập không thành công" (anti-enumeration — KHÔNG phân biệt sai passkey/không có passkey).

**Registration entry:** trong `SecurityCenter.razor` (U03) đã có flow register passkey — guest chưa login vào /security được không? Kiểm tra: SecurityCenter cần customer token? Nếu guest không vào được → thêm entry "Tạo passkey" ngay trên Login.razor sau guest-mode, hoặc cho phép register với guest context (atrs đã hỗ trợ `X-Customer-Device-Id`). **Verify trong ANALYZE** — nếu guest register chưa chạy, card này bao gồm fix nhỏ: SecurityCenter cho phép guest device context (atrs side đã sẵn, chỉ UI gate).

### 3.4 Security & abuse controls

- **Rate limit login:** challenge-guard hiện tại per-subject (5) không áp dụng (subject unknown) → thêm `Atrs:PasskeyLogin:MaxBeginPerHour` per IP (default 20) + per deviceId (default 10) — IMemoryCache/DB counter. Sai assertion liên tiếp → backoff.
- **Anti-enumeration:** mọi lỗi finish trả cùng message; không lộ credential tồn tại hay không.
- **Audit:** `AccountSecurityEvent` `passkey.login.success` / `passkey.login.fail` (reason code, không PII).
- **Notification (phase 2, ghi nợ):** in-app "đăng nhập từ thiết bị mới" — ATRS-13 writer sẵn, không làm v1.
- **Origin/RP binding:** giữ nguyên `CreateFido2` checks (origin allowlist + rpId suffix) — login endpoints dùng cùng.
- **UserVerification=Required** ở login (chống stolen-device assertion không PIN/biometric).

## 4. TDD PLAN — RED trước, GREEN sau (bắt buộc thứ tự)

### Phase R — Viết test FAIL trước

**`11_Atrs/vanan-atrs/tests/VanAn.Atrs.Tests/Services/PasskeyLoginServiceTests.cs` (MỚI):**

| # | Test | Expected |
|---|---|---|
| T1 | `BeginPasskeyLogin` trả options với `allowCredentials` rỗng + `userVerification=required` + challenge lưu `Purpose=PasskeyLogin`, `TrustSubjectId=Empty` | options shape đúng, challenge row đúng |
| T2 | `BeginPasskeyLogin` quá `MaxBeginPerHour`/IP → 429-equivalent exception | rate guard |
| T3 | `FinishPasskeyLogin` challenge sai/expired/đã consume/origin khác/rpId khác → fail `InvalidChallenge`, KHÔNG consume nhầm | từng case 1 test |
| T4 | `FinishPasskeyLogin` credentialId không tồn tại trong `DeviceCredentials` → fail, challenge consumed | anti-oracle |
| T5 | `FinishPasskeyLogin` credential REVOKED → fail `RevokedCredential` | status check |
| T6 | `FinishPasskeyLogin` assertion signature sai → fail `InvalidAssertion` | crypto path (dùng test credential đã enroll trong test DB — pattern `WebAuthnServiceTests` hiện có) |
| T7 | `FinishPasskeyLogin` assertion đúng → resolve subject từ credential → gọi ShopERP by-device/by-id → trả `customerToken` + `customerId` | happy path, verify mock HttpClient nhận đúng body+key header |
| T8 | subject có link `Customer` → gọi `by-id` (không gọi by-device) | branch coverage |
| T9 | subject chỉ có `DeviceCluster` link + deviceId gửi kèm → gọi `by-device` với deviceId | branch coverage |
| T10 | subject chỉ có `DeviceCluster` + KHÔNG deviceId → fail graceful `NoCustomerIdentity` (không crash) | edge |
| T11 | ShopERP trả 401/500/timeout → fail `TokenIssuanceFailed`, audit fail event, challenge vẫn consumed (không replay) | failure path |
| T12 | Audit events: success ghi `passkey.login.success`, fail ghi `passkey.login.fail` reason | audit coverage |
| T13 | Guest register passkey (device context) → credential gắn guest subject → login sau resolve đúng subject | end-to-end trong service tests |

**`6_Tests/VanAn.ShopERP.Tests/` (MỚI `InternalCustomerTokenControllerTests.cs`):**

| # | Test | Expected |
|---|---|---|
| S1 | không `X-Internal-Api-Key` → 401 | auth |
| S2 | sai key → 401 | auth |
| S3 | `by-device` customer tồn tại → trả token verify được bằng `ICustomerTokenService` round-trip | token validity |
| S4 | `by-device` chưa có → tạo stub `DeviceVerified` + token + `created:true` | materialize |
| S5 | `by-device` có `tenantId` → scope đúng tenant | multi-tenancy |
| S6 | `by-id` customer tồn tại → token; không tồn tại → 404 | branch |
| S7 | deviceId rỗng/sai format → 400 | validation |

**`6_Tests/VanAn.Core.Tests/`** — chỉ nếu có logic shared mới (vd deviceId validation) — có thể skip nếu logic nằm trong ShopERP.

### Phase G — Implement cho test xanh

Thứ tự: ShopERP endpoint+tests → atrs service+tests → atrs controller+tests → KhachLink UI → wire compose/env.

## 5. FILES (dự kiến — cập nhật khi implement)

| File | Change |
|---|---|
| `VanAn.Atrs.Domain` | `WebAuthnChallenge.TrustSubjectId` nullable/empty-allowed (Domain change → Gate 5, cần user approval — **ghi rõ trong PR**) hoặc bảng/convention mới |
| `VanAn.Atrs.Infra/WebAuthnService.cs` | +`BeginPasskeyLoginAsync`/`FinishPasskeyLoginAsync` + `IPasskeyLoginTokenIssuer` (abstraction gọi ShopERP) |
| `VanAn.Atrs.Infra/ShopErpTokenIssuer.cs` (MỚI) | HTTP client "shoperp" + `X-Internal-Api-Key` + DTOs |
| `VanAn.Atrs.Api/Controllers/` | `PasskeyLoginController` (public, rate-limited) `POST passkey/login/begin|finish` |
| atrs migration | `TrustSubjectId` empty-allowed hoặc cột mới — additive |
| atrs `appsettings`/compose/env | `Atrs:ShopErpInternalApiKey`, `Atrs:PasskeyLogin:*` limits |
| `5_WebApps/ShopERP/Controllers/InternalCustomerTokenController.cs` (MỚI) | by-device + by-id, `[InternalApiKey]` |
| `5_WebApps/KhachLink/Pages/Login.razor` | +passkey button + flow |
| `5_WebApps/KhachLink/Services/Http/` | `PasskeyLoginHttpService` hoặc method trong service sẵn có |
| `webauthn.js` | verify `getAssertion` xử lý `allowCredentials:[]` (có thể cần patch nhỏ — test trong RV) |
| Guest register verify | nếu guest chưa register được → fix gate (xem §3.3) |
| Tests | §4 |

## 6. ACCEPTANCE CRITERIA

- [ ] Guest (chưa login, có deviceId) register passkey thành công → credential ACTIVE gắn guest TrustSubject
- [ ] Passkey login trên thiết bị khác (passkey synced) → nhận `customer_token` hợp lệ → `GET /api/customer-identity/me` 200 → voice call button khả dụng (`VoiceCallIdentity`)
- [ ] Login fail mọi case → message generic, không oracle
- [ ] Rate limit begin/finish hoạt động; challenge one-time/TTL/origin/rpId binding giữ nguyên
- [ ] subject→customerId resolve đúng cả 3 link types (Customer / DeviceCluster+deviceId / DeviceCluster-only → fail graceful)
- [ ] ShopERP stub creation không phá multi-tenancy (tenantId optional, default tenant)
- [ ] Audit `passkey.login.*` events ghi nhận
- [ ] `dotnet build VanAn.sln` + `guard-check.ps1` + toàn bộ tests mới PASS
- [ ] Regression: OAuth login, guest checkout, security-center passkey register (logged-in) không đổi

## 7. VERIFICATION + RV

**Local:**
```powershell
dotnet build VanAn.sln
.\scripts\guard-check.ps1
dotnet test 11_Atrs/vanan-atrs/tests --filter "FullyQualifiedName~PasskeyLogin"
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~InternalCustomerToken"
dotnet test 6_Tests/VanAn.Core.Tests
```

**RV production (sweep-first, STOP at first fail):**
- [ ] L1: atrs+shoperp+khachlink images mới deploy; `Atrs__ShopErpInternalApiKey` env non-empty (không echo); ShopERP `[InternalApiKey]` endpoint live
- [ ] L2: `POST /api/v1/account-trust/passkey/login/begin` không token → 200 options; bad origin → 4xx
- [ ] L3 Playwright/manual: guest → register passkey (virtual authenticator CDP như W2 probe `atrs-security-probe.mjs`) → logout → login bằng passkey → `customer_token` trong localStorage → `/api/customer-identity/me` 200 → vào store → nút Gọi khả dụng
- [ ] L3 negative: assertion giả → fail generic; rate limit begin >20/h → 429
- [ ] L4: ShopERP DB verify customer stub created + token round-trip; atrs audit events
- [ ] Cleanup: test customer/passkey revoke sau RV

## 8. RISKS / OPEN QUESTIONS (khuyến nghị kèm — user confirm khi duyệt)

1. **Domain change `WebAuthnChallenge.TrustSubjectId`** — entity atrs (không phải Domain.cs shared) nhưng vẫn theo entity rules: sentinel `Guid.Empty` vs nullable column. Khuyến nghị sentinel — ít migration, rõ nghĩa. Cần confirm.
2. **Resident key requirement:** registration hiện `ResidentKey=Preferred` — passkey non-resident (device-bound, không sync) sẽ KHÔNG login được từ máy khác. Đề xuất: credentials MỚI register cho customer-login đặt `Required` (config `WebAuthn:ResidentKey` per-purpose nếu tách; đơn giản nhất: đổi global Preferred→Required — passkey hiện đại đều support). Non-resident cred cũ vẫn dùng cho step-up (subject-scoped) bình thường.
3. **Passkey sync = portability:** user mất điện thoại → passkey sync qua Google/iCloud vẫn login được máy mới — đúng mong muốn; rủi ro = ai chiếm Google account user → mất passkey → mitigated bởi UserVerification=Required + ATRS device signals sau này.
4. **by-device tạo customer stub "rỗng"** (không phone/email) → customer này chưa verify liên hệ — `IdentityLevel=DeviceVerified` đúng L0/L1; nâng cấp sau qua OTP/OAuth merge như hiện tại.
5. **Tenant resolution:** ShopERP multi-tenant — by-device không tenantId → default tenant (giống OTP verify `GetTenantId`); guest đa-tenant cùng device → customer per-tenant? OTP path hiện tại cũng vậy (1 phone → 1 customer/tenant đầu tiên thấy) — ghi nợ nhất quán, không giải quyết trong card này.
6. **webauthn.js `allowCredentials:[]`** — chuẩn WebAuthn, nhưng verify thực tế trên Chrome/Safari trong RV (1 dòng patch nếu cần).
7. **Phạm vi KHÔNG làm (phase 2):** notification "login new device", passkey cho staff/DemoUser, revoke-all-on-password-change (customer không có password), Zalo Login.

## 9. TRACEABILITY

| Requirement | Cover bởi |
|---|---|
| Customer không Google/FB vẫn có account free | toàn bộ card — passkey 0đ, ATRS-native |
| Sẵn sàng voice call | §3.2 customer_token → `VoiceCallIdentity` (F11) |
| Materialize guest → customer | §3.2 by-device stub + CustomerMergeService giữ nguyên |
| ATRS không phụ thuộc hệ ngoài | credential trong atrs PG; chỉ phụ thuộc ShopERP mint token (internal, đã có pattern) |
| TDD không bỏ sót | §4 — 13 service tests + 7 controller tests viết TRƯỚC |
| Security | §3.4 rate limit/anti-enum/audit/UV-required |
| Governance | entity change → Gate 5 note §8.1; UI Platform: Login.razor dùng VanAnButton sẵn có; secrets qua GH secret + preserve-pattern |

**Status:** REVIEW_ONLY — chờ user duyệt + confirm §8.1/§8.2.
**Naming:** ATRS-21 (sau ATRS-20 staff MFA card — độc lập, có thể song song).
**Ước lượng:** vừa — atrs service methods là phần lớn nhất (discoverable flow mới); ShopERP endpoint nhỏ; UI nhỏ. Rủi ro chính: challenge schema cho subject-unknown + discoverable credential edge cases trên browser thật (RV bắt được).
**Liên quan:** sau card này, gap "customer không OAuth" đóng → voice call coverage ~100% smartphone; phone-OTP SMS vẫn là alternative nếu sau này cần (card riêng, tốn phí).
