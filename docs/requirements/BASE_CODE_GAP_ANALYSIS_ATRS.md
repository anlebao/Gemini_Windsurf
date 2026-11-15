# BASE_CODE_GAP_ANALYSIS — VAN_AN Account Trust & Recovery Service (ATRS) v1.0

> **Phase 0 deliverable** theo SRS service §17.1 + UI/UX SRS §16 Phase 0.
> **Ngày:** 2026-11-15 · **Mode:** ANALYZE (read-only audit, không sửa code)
> **Nguồn SRS:** `VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` + `VAN_AN_Account_Trust_UI_UX_SRS_v1.0 (1).md`
> **Phạm vi audit:** auth/user/session, device registry, identity trust, loyalty/wallet ledger, audit, notification, RBAC, deployment pattern.

---

## 0. Kết luận điều hành

1. **ATRS KHÔNG phải greenfield hoàn toàn.** ~40% nền đã có: device registry (fingerprint), loyalty ledger append-only + adjust API, audit log immutable, notification đa kênh, outbox/NATS, MFA TOTP cho DemoUser, trust fields ID-P1.
2. **Blocker lớn nhất là identity split** — đúng như review trước: customer KhachLink dùng `Customer` (tenant-scoped, SQLite per tenant + PG sync) + `X-Customer-Token` (IDataProtector, KHÔNG phải JWT); staff dùng `DemoUser`/`User` + JWT HS256 8h. SRS giả định một `UserId` CoreHub duy nhất — **không tồn tại**.
3. **Không có WebAuthn/passkey ở bất kỳ đâu** (grep `WebAuthn|Passkey|Fido2` = 0 kết quả). Phase 2 của cả 2 SRS phụ thuộc hoàn toàn vào việc chọn thư viện + quyết định deployment.
4. **Token revocation là lỗ hổng thật**: cả 2 cơ chế token hiện hữu đều stateless (JWT 8h; customer token 30 ngày, chỉ thu hồi được bằng rotate DataProtection key). FR-011 "thu hồi session/token cũ" chưa có nền để đứng.
5. **Đề xuất positioning:** ATRS = lớp recovery/dispute/evidence **phía trên** initiative "Zero-Cost Identity L0-L3" đã approved — tái dùng `IdentityLevel`, `UserTrustLevel`, `VerifiedChannel`, `DeviceRegistration`, `ILoyaltyPointLedgerService`, `AuditLog`, `INotificationService`. Không tạo ledger/device/audit song song.

---

## 1. AuthN/AuthZ hiện hữu (verified)

| Thành phần | Hiện trạng | File tham chiếu |
|---|---|---|
| Staff JWT | HS256, `Jwt:Secret` shared, 8h expiry, claims `sub/email/role/tenant_id`; **không có revocation/blacklist** | `3_CoreHub/Services/JwtTokenService.cs` |
| Cross-service auth | Pattern `StaffJwtValidator` (clone cho Voice/TenantAI) + `IRealtimeTokenValidator`; internal API dùng `X-Internal-Api-Key` (`InternalApiKey` filter) | `2_Gateway/Realtime/StaffJwtValidator.cs`, `2_Gateway/Filters/` |
| Customer token | **IDataProtector**, format `{customerId}:{expiry}`, TTL 30 ngày, protector purpose `CustomerToken.v1`; validate bằng forward tới ShopERP `/api/customer-identity/me` | `5_WebApps/ShopERP/Services/CustomerTokenService.cs`, `2_Gateway/Services/CustomerTokenValidationHelper.cs` |
| Customer login | Phone OTP → `OtpService` (ShopERP) → tạo/nâng `Customer` → trả customer token. OTP verify nâng `IdentityLevel.Verified` + merge guest stubs theo `DeviceId` | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` |
| DemoUser (staff/owner) | Bcrypt password, `UserRole` tenant-scoped (Owner…DebtAccountant), **đã có** `PhoneNumber` (encrypted at rest), `PhoneVerifiedAt`, `VerifiedChannel`, `TrustLevel` (None/ContactVerified/BusinessVerified), **MFA TOTP** `MfaEnabled/MfaSecret`, `TrustedDeviceHash` (1 device) | `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` |
| SystemAdmin | Policy `SystemAdmin` platform-level cross-tenant, dùng cho admin controllers | `2_Gateway/Controllers/DeviceRegistrationAdminController.cs` |
| Session | **Không có session store** — JWT + cookie auth; SignalR dùng `StaffJwtValidator` | — |

**Gap:**
- **G-AUTH-1 (Missing):** Token revocation strategy. Cả JWT (8h) lẫn customer token (30d) đều không revoke được từng phiên. Recovery flow cần: `securityStamp`/`tokenVersion` claim + validation hook, hoặc revocation list ngắn hạn, hoặc chấp nhận "đá" bằng cách rotate secret (quá nặng). → **Quyết định kiến trúc bắt buộc trước Phase 3.**
- **G-AUTH-2 (Missing):** Step-up authentication primitive. Chưa có cơ chế "re-auth gần đây" (auth_time claim / step-up token) — mọi SRS step-up (FR-007, FR-010, U03/U05) cần nền này. MFA-1 TOTP mới là 1 phần.
- **G-AUTH-3 (Wrong/Risk):** `OtpService` ShopERP dùng `Random` non-CSPRNG (đã ghi trong audit identity) + `CustomerIdentityController` tra cứu customer theo phone **in-memory** (cột PII encrypted không query được). Recovery flow sẽ đụng cùng vấn đề → phải giải trong P4 hardening hoặc ATRS tự xử lý.
- **G-AUTH-4 (Missing):** `RecoveryRequest`/`DisputeCase` entity — grep = 0. Toàn bộ workflow case là mới.

---

## 2. Device registry hiện hữu

| Thành phần | Hiện trạng | File |
|---|---|---|
| `DeviceRegistration` entity | Customer-scoped, có `FingerprintHash`, `FingerprintSignals`, `DeviceToken`, `UserAgent`, `Platform`, `IpAddress`, `IsActive`, `IsVerified`, `RiskScore` | `1_Shared/Domain.cs` (~5695) |
| `DeviceRegistrationService` | Max **3 active devices/customer**; device thứ 4 → `IsActive=false` + `FraudFlag(DeviceLimitExceeded)`; admin list/deactivate/verify/risk-score/by-fingerprint | `3_CoreHub/Services/DeviceRegistrationService.cs` |
| Public endpoint | `POST /api/customer-identity/device/register` — validate `X-Customer-Token` qua ShopERP `/me`, ghi IP/UA | `2_Gateway/Controllers/DeviceRegistrationController.cs` |
| Admin endpoint | `/api/admin/device-registrations/*` — SystemAdmin only | `2_Gateway/Controllers/DeviceRegistrationAdminController.cs` |

**Đánh giá vs SRS:**
- ✅ Đúng hướng: device registry tồn tại, có fraud flag, có giới hạn thiết bị (SRS §18 Q3 "số thiết bị tối đa" → **đã có câu trả lời: 3**).
- ⚠️ **Modify:** model hiện tại là fingerprint-centric (`FingerprintHash` + signals JSON) — SRS FR-003/FR-005 muốn `DeviceId` ngẫu nhiên do Vạn An cấp + trạng thái `PENDING/ACTIVE/SUSPICIOUS/REVOKED/LOST` + `FriendlyName` + `RegistrationMethod` + link tới credential. Cần mở rộng entity (thêm cột) chứ không tạo bảng mới — **hoặc** tách `UserDevices` (trust) khỏi `DeviceRegistration` (fraud signal). **Quyết định modeling cần Tech Lead duyệt (Gate 5).**
- ⚠️ **Modify:** `DeviceRegistration` gắn `CustomerId` — không cover DemoUser/staff devices. `DemoUser.TrustedDeviceHash` mới chỉ 1 device cho MFA. Cần thống nhất "device" thuộc về account identity nào.
- ❌ **Missing:** User-facing device list/detail/rename/revoke-report-lost (U07/U08) — hiện chỉ có admin endpoints.
- ❌ **Missing:** `DeviceCredential`/passkey storage (§7.3) — hoàn toàn mới.

---

## 3. Identity trust hiện hữu (initiative L0-L3 — BẮT BUỘC tái dùng)

| Thành phần | Hiện trạng | File |
|---|---|---|
| `IdentityLevel` (Customer) | Guest=0, Social=1, Verified=2 (OTP), Full=3, DeviceVerified=4; rank map riêng vì ordering vỡ | `1_Shared/Domain.cs:815`, `IdentityLevelRank` |
| `UserTrustLevel` (DemoUser) | None/ContactVerified/BusinessVerified; `VerifiedChannel` provenance; downgrade hook `RevokeBusinessVerification` | `DemoUser.cs`, `UserTrustLevel.cs` |
| `CustomerMergeService` | Merge guest stubs (DeviceId-based) vào login customer, transfer points | `3_CoreHub/Services/` (dùng trong `CustomerIdentityController`) |
| OTP channels | `IIdentityOtpChannel` + `BrevoEmailOtpChannel` + `FallbackOtpChannel` + `MerchantSignupOtpService` (resend cooldown + daily limit) | `3_CoreHub/Services/Otp/` |
| MFA-1 (card approved) | TOTP fields đã có trên DemoUser; card `task_idp8_admin_mfa.md` — trạng thái implement cần verify | `docs/AI/tasks/identity/task_idp8_admin_mfa.md` |

**Gap:**
- **G-ID-1 (Wrong design risk):** SRS §7.4 `PersonalEvidenceProfiles` **trùng chức năng** với `VerifiedChannel`/`PhoneVerifiedAt`/`IdentityLevel`/`Birthday` đã có trên Customer + DemoUser. → **Modify SRS:** Personal Evidence phải là *lớp tham chiếu/metadata* (source, verificationStatus, privacyNoticeVersion), không copy giá trị PII đã có — đúng note cuối §7.4 nhưng cần nói rõ map với field nào.
- **G-ID-2 (Missing):** ATRS profile gắn với **canonical identity nào?** Customer là per-tenant (IMustHaveTenant) — một khách hàng có thể có N Customer rows ở N tenant (đã thấy `LoyaltyRewards` per-tenant silo). "Một UserId một ATRS profile" của SRS **không map được** khi customer identity phân mảnh theo tenant + DeviceId. → Cần quyết định: profile key = `CustomerId`? `DeviceId`-cluster? phone-hash? Đây là **open decision #1 cho Product Owner**.
- **G-ID-3 (Missing):** Cross-tenant customer view. Dispute "điểm thưởng" của khách có điểm ở nhiều tenant — ai là authority? `ILoyaltyPointLedgerService.GetBalanceAsync(customerId, tenantId)` là per-tenant.

---

## 4. Loyalty / Wallet ledger hiện hữu (dispute target)

| Thành phần | Hiện trạng | File |
|---|---|---|
| `ILoyaltyPointLedgerService` | **PG single source of truth** mọi write: `AwardAsync`/`SpendAsync`/`RefundAsync`/`RevertOrderAsync`; idempotency key; `LoyaltyIssuanceRecord` per-order double-award guard; "luật Silo" per-tenant | `3_CoreHub/Services/ILoyaltyPointLedgerService.cs` |
| `IAllianceWalletService` | Cross-tenant wallet, `AllianceTransaction` append-only; **`AdjustPointsAsync` signed ADJUST row** (positive/negative, idempotent, bypass budget) | `1_Shared/Services/IAllianceWalletService.cs:98` |
| Internal API | `POST /api/internal/loyalty/points/{add,deduct,adjust,refund}` + `/award` — `X-Internal-Api-Key`, đã có `points/adjust` cho admin | `2_Gateway/Controllers/InternalLoyaltyController.cs` |
| `LoyaltyReconcileCase` | Case audit cho double-failure (trừ điểm OK nhưng voucher+refund đều fail) — SystemAdmin resolve tại `/admin/loyalty-reconcile` | `1_Shared/Domain.cs:3579`, `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyReconcile.razor` |
| Metering wallet | Ví phí nền tảng riêng (`CreditLedgerEntry`, `WalletService`) — **KHÔNG phải** điểm khách hàng | `8_Metering/vanan-metering/` |
| HTTP proxy | ShopERP → Gateway: `LoyaltyPointLedgerServiceHttpProxy`, `AllianceWalletServiceHttpProxy`, `LoyaltyConfigApiClient` | `5_WebApps/ShopERP/Services/` |

**Đánh giá vs SRS FR-012/FR-013:**
- ✅ Ledger đúng chuẩn SRS muốn: append-only (`AllianceTransaction`, `LoyaltyIssuanceRecord`), adjustment = bản ghi mới có reason + idempotency, không sửa số dư trực tiếp.
- ✅ **`AdjustPointsAsync` đã là "điều chỉnh có phê duyệt" primitive** — dispute resolution chỉ cần gọi nó qua internal API với reason + reference case.
- ⚠️ **Modify:** Chưa có **maker-checker** trên adjust — `points/adjust` là single-admin-call. Cần thêm approval state (case → approved → execute adjust với `IdempotencyKey = disputeCaseId:adjust:n`).
- ⚠️ **Modify:** `LoyaltyReconcileCase` chỉ cover system-error, không phải user dispute — **có thể mở rộng** (thêm CaseType/ClaimedIssue/Evidence) thay vì tạo `DisputeCases` song song. Quyết định modeling.
- ❌ **Missing:** User-facing "transaction selector" data source — cần API list transactions/ledger entries của customer (hiện ledger đọc qua `GetBalanceAsync`, chưa có customer-facing transaction history API cho dispute picker).
- ❌ **Missing:** Ledger read API cho investigator (chi tiết entry theo reference) — `ILoyaltyPointLedgerService` chỉ có balance/award-spend-refund, chưa có `GetEntriesAsync(customerId, tenantId, range)`.

---

## 5. Audit / Event / Outbox hiện hữu

| Thành phần | Hiện trạng | File |
|---|---|---|
| `AuditLog` | Immutable append-only entity, factory methods (`ForCreate/ForUpdate/.../ForSecurityEvent`), có `CorrelationId`, `IpAddress`, `UserAgent`, `EntityType` enum (16 loại, có `SecurityEvent`) | `1_Shared/Domain/Audit/AuditLog.cs` |
| `AuditTrailService` + `AuditLogBackgroundWriter` | Ghi audit bất đồng bộ qua queue — đúng pattern SRS muốn (không kéo dài request) | `3_CoreHub/Services/AuditTrailService.cs`, `AuditLogBackgroundWriter.cs` |
| `IOutboxRepository`/`OutboxEvent` | Outbox pattern + NATS publish (đã chạy cho CustomerCreated, order sync) | `3_CoreHub/` (`IOutboxRepository`) |
| `INotificationService` | Email/SMS/push; `CompositeNotificationService` + `OutboxNotificationService` | `3_CoreHub/Services/INotificationService.cs` |
| `INotificationCenterService` | Inbox đã đọc/chưa đọc (`TenantNotification`, `PushNotificationDelivery`) — kênh in-app | `3_CoreHub/Services/INotificationCenterService.cs` |
| OTP infra | Rate limit/resend cooldown/daily limit đã có trong `MerchantSignupOtpService`; `OtpService` customer cần hardening (non-CSPRNG) | `3_CoreHub/Services/Otp/`, `5_WebApps/ShopERP/Services/OtpService.cs` |

**Gap:**
- ✅ Audit append-only **tái dùng trực tiếp** — chỉ cần thêm `AuditableEntityType` values (AccountTrustProfile, RecoveryRequest, DisputeCase...) hoặc bảng `AccountSecurityEvents` riêng nếu cần schema giàu hơn `AuditLog` (FR-009 có ~15 fields, AuditLog có sẵn phần lớn: Action/EntityType/EntityId/OldValues/NewValues/Reason/CorrelationId/UserId/Ip/UserAgent/Timestamp — **có thể map, thiếu ActorType/DeviceId/SessionId/Outcome/ReasonCode/EvidenceReferences** → Modify: mở rộng AuditLog hoặc event table riêng).
- ⚠️ **Modify:** `AccountSecurityEvents` của SRS ≠ `AuditLog` — cần quyết định: mở rộng `AuditLog` (thêm cột nullable) hay bảng riêng. Khuyến nghị bảng riêng `AccountSecurityEvents` vì FR-009 schema khác biệt và volume lớn hơn audit CRUD.
- ✅ Notification đa kênh sẵn — ATRS chỉ cần định nghĩa templates + precedence, không xây kênh mới. **Nhưng** customer hiện chỉ có phone (SMS chưa rõ provider) + email (nếu có) — "kênh liên hệ đã đăng ký" cần verify thực tế.

---

## 6. RBAC / Staff portal hiện hữu

| Thành phần | Hiện trạng |
|---|---|
| `UserRole` (DemoUser) | Owner/StoreKeeper/Guard/Staff/Masterchef/ChiefAccountant/DebtAccountant — tenant-scoped, **không có** role recovery-reviewer/dispute-investigator/auditor/security-admin riêng |
| `SystemAdmin` | Platform-level policy, cross-tenant — phù hợp Super Admin/break-glass |
| Staff portal | ShopERP Blazor Server admin pages (`/admin/*`, vd `LoyaltyReconcile.razor`, membership admin) |
| Maker-checker | **Chưa có primitive** — chỉ có convention trong một số flow |

**Gap:**
- ❌ **Missing:** Permission groups của UI SRS §4.2 (Support agent / Recovery reviewer / approver / Dispute investigator / approver / Auditor / Security admin / System admin) — `UserRole` hiện tại không cover. Cần hoặc (a) mở rộng enum (append-only theo convention), hoặc (b) permission table mới. **Open decision.**
- ❌ **Missing:** Toàn bộ staff screens A14-A22 — chưa có queue/review/investigation/audit-explorer/policy UI nào cho domain này.
- ❌ **Missing:** Break-glass workflow (SRS có nhắc, chưa định nghĩa).

---

## 7. WebAuthn / Passkey

- **Grep `WebAuthn|Passkey|Fido2` toàn repo = 0 file.** Không có thư viện, không có flow, không có DB schema.
- .NET ecosystem: `Fido2NetLib` (MIT) là ứng viên chuẩn; cần verify version ổn định ≥7 ngày (theo dependency policy).
- **Gap:** toàn bộ FR-004, FR-008 (recovery codes — cũng chưa có), U03/U05, §7.3 `DeviceCredentials` là **Missing 100%**.
- Recovery codes thực ra không cần WebAuthn — có thể làm trước (Phase 2 tách được).

---

## 8. Deployment / hosting decision (B3 từ review)

Pattern hiện có (3 lựa chọn đều có precedent):

| Option | Precedent | Ưu/nhược cho ATRS |
|---|---|---|
| **A. Module trong ShopERP/Gateway** | CoreHub services in-proc; `LoyaltyReconcile.razor` trong ShopERP | Ít ops nhất; nhưng ATRS là cross-app (KhachLink customer + staff) → Gateway là nơi customer token validate sẵn |
| **B. Service riêng** (metering/voice/tenantai) | `8_Metering`, `9_TenantAI`, `10_Voice` — sln riêng, Dockerfile, port riêng, PG/SQLite riêng, CI matrix | Đúng chuẩn "module độc lập attachable by contract" như voice; tách blast radius; tốn thêm VPS/compose |
| **C. Trong Gateway** | DeviceRegistration, InternalLoyalty, community entities đã ở Gateway PG | Gateway đã giữ "source of truth" cho orders/tenants/users; nhưng Gateway đang nặng, thêm domain mới vào PG Gateway cần cân nhắc |

**Khuyến nghị phân tích:** Option B (service riêng, pattern `vanan-voice`) phù hợp nhất với tầm nhìn "shared account security profile across apps" + đã có `StaffJwtValidator` clone pattern + internal API key pattern. **Nhưng** cần PO/Architect chốt — đưa vào open decisions.

Lưu ý governance: `3_CoreHub` là class library — SRS §4.1 "Gateway chuyển ATRS đến CoreHub" chỉ đúng nếu ATRS là service layer in-proc; nếu Option B thì CoreHub chỉ chứa domain entities/contracts (`1_Shared/Domain.cs` single source of truth vẫn bắt buộc).

---

## 9. Bảng gap tổng hợp theo SRS

### Existing — tái dùng nguyên trạng
- `AuditLog` append-only + `AuditTrailService`/`AuditLogBackgroundWriter` (cần extend schema hoặc song song `AccountSecurityEvents`)
- `ILoyaltyPointLedgerService` + `IAllianceWalletService.AdjustPointsAsync` (idempotent, append-only) — dispute adjustment primitive
- `DeviceRegistration` + `DeviceRegistrationService` + admin endpoints (extend, không tạo mới)
- `INotificationService`/`INotificationCenterService`/`OutboxNotificationService` — kênh thông báo
- `IOutboxRepository` + NATS — event publishing
- `IdentityLevel`/`UserTrustLevel`/`VerifiedChannel`/`CustomerMergeService` — trust baseline
- `DemoUser` MFA TOTP fields + `MfaService`
- `MerchantSignupOtpService` rate-limit pattern; `InternalApiKey`/`StaffJwtValidator` auth patterns
- `FeatureFlagService`/`ShopFeatureSettings` — gating UI theo profile/tenant
- `LoyaltyReconcileCase` + admin page — precedent cho case-management UI

### Modify — có nhưng cần sửa/mở rộng
- `DeviceRegistration` entity: thêm status lifecycle (`PENDING/ACTIVE/SUSPICIOUS/REVOKED/LOST`), `FriendlyName`, `RegistrationMethod`, link `DeviceCredentialId`, last-seen semantics — **Gate 5, cần duyệt**
- `AuditLog`/`AuditableEntityType`: thêm entity types cho trust/recovery/dispute HOẶC tạo `AccountSecurityEvents` riêng (schema FR-009 giàu hơn)
- `InternalLoyaltyController.points/adjust`: thêm maker-checker wrapper (case approval → execute với idempotency key = case ref)
- `CustomerIdentityController`/`OtpService`: hardening (CSPRNG, phone lookup) — đã nằm trong card P4, ATRS phụ thuộc
- `LoyaltyReconcileCase`: quyết định mở rộng thành `DisputeCase` tổng quát hay giữ riêng + tạo mới
- `UserRole`/RBAC: thêm permission groups cho staff screens (hoặc permission table mới)
- JWT/customer-token: thêm revocation/`tokenVersion` mechanism

### Missing — phải xây mới
- `AccountTrustProfile` aggregate (bind canonical identity)
- `DeviceCredentials`/WebAuthn (Fido2NetLib hoặc tương đương) + `RecoveryCodes`
- `RecoveryRequest` workflow + state machine + policy engine (deterministic rules FR-010)
- `DisputeCase`/`DisputeEvidenceItems`/`DisputeDecisions` (+ attachment storage)
- `PersonalEvidenceProfiles` (dạng metadata/reference, không copy PII)
- `SecretChallenges` (nếu PO giữ U04 — khuyến nghị omit)
- `AccountSecurityEvents` (nếu không extend AuditLog)
- Toàn bộ user UI U01-U13 + staff UI A14-A22
- Customer-facing transaction/ledger history API cho dispute picker + investigator read API
- Break-glass procedure
- Consent records (PDPL evidence)

### Wrong/Remove — cần loại bỏ hoặc tránh
- **SRS §4.1 "ATRS đến CoreHub"** nếu chọn service riêng → sửa thành contract/API boundary
- **SRS §7.4 copy PII** → phải là reference + provenance metadata (map `VerifiedChannel`/`PhoneVerifiedAt`/`IdentityLevel`)
- Fingerprint làm định danh chính — `DeviceRegistration` hiện dùng `FingerprintHash` làm khóa tra cứu; SRS cho phép làm risk signal nhưng `DeviceId` Vạn An cấp phải là identity chính → **Modify semantics, không xóa cột**
- `X-Dev-OTP`/dev-token patterns — đã xử lý (secret-gated), ATRS không được tái tạo lỗ hổng tương tự

---

## 10. Open decisions cần PO/Architect chốt (đề xuất đưa vào SRS §18)

| # | Quyết định | Ảnh hưởng | Đề xuất mặc định |
|---|---|---|---|
| D1 | Canonical identity cho ATRS: `Customer`(per-tenant), `DeviceId`-cluster, hay phone-verified cluster? | Toàn bộ data model | Cluster theo verified phone/email hash + link các Customer rows (tận dụng `CustomerMergeService` precedent) |
| D2 | Deployment: service riêng (pattern voice) vs module ShopERP/Gateway | Infra, CI, RV | Service riêng `vanan-atrs` (port mới), PG riêng |
| D3 | Token revocation mechanism | FR-011 feasibility | `tokenVersion` claim + version check cache (Redis/DB), TTL rút ngắn |
| D4 | `DisputeCase` mới vs mở rộng `LoyaltyReconcileCase` | Schema | Mở rộng + rename concept "Case" chung (reconcile case là 1 loại dispute) |
| D5 | RBAC: mở rộng `UserRole` vs permission table mới | Staff screens | Permission table mới (roles hiện tại quá thô) |
| D6 | WebAuthn library: Fido2NetLib? | Phase 2 | Fido2NetLib latest stable ≥7 ngày |
| D7 | U04 secret question: giữ hay omit? | Scope | **Omit** (cả 2 SRS đều khuyến nghị; recovery codes + passkey đủ) |
| D8 | `AccountSecurityEvents` bảng riêng vs extend `AuditLog` | Schema | Bảng riêng (schema khác, volume khác) |
| D9 | Evidence attachment storage | Phase 4 | Object storage (R2 đã có `R2StorageController`) + signed URL |
| D10 | ATRS có cover staff (DemoUser) recovery không, hay chỉ customer? | Scope lớn | Phase 1 chỉ customer; staff dùng MFA-1 hiện có |

---

## 11. Rủi ro phát hiện thêm trong codebase

1. **`Customer.PhoneNumber` encrypted at rest nhưng lookup in-memory** — mọi recovery flow "tìm customer theo SĐT/email" sẽ chậm/quét full table khi scale. Cần deterministic lookup key (HMAC hash của phone) — **phải giải trước khi recovery-by-contact hoạt động**.
2. **Customer token 30 ngày + không revoke** — chiếm token = 30 ngày access. Recovery phải có cách "kill token"; hiện chỉ rotate DataProtection key (kill all).
3. **`DeviceRegistration` max-3 + FraudFlag** — recovery flow "đăng ký device mới" sẽ thường xuyên chạm limit → cần policy "recovery-approved device bypass/replace" được định nghĩa.
4. **Hai khái niệm "ví"**: AllianceWallet (điểm liên minh, customer-facing) vs Metering wallet (phí nền tảng, tenant-facing). Dispute category "ví" trong UI SRS phải nói rõ chỉ cover AllianceWallet/điểm — metering wallet là billing nội bộ, không phải dispute của khách.
5. **Loyalty data per-tenant silo + SQLite mirror** — dispute cần đọc PG ledger (source of truth); ShopERP SQLite `LoyaltyRewards` chỉ là mirror, không được dùng làm evidence.

---

## 12. Khuyến nghị bước tiếp

1. Trình bày D1-D10 cho Product Owner (đa số đã có đề xuất mặc định an toàn).
2. Sau khi chốt D1/D2/D4 → sửa 2 SRS (canonical identity, deployment, status mapping, tenancy model) → mới vào Phase 1 (Trust Profile + Device Registry extension).
3. Song song: card P4 OTP hardening + ID-P1 trust fields đang chạy — ATRS Phase 1 nên **sau hoặc cùng wave** để tránh đụng `Customer`/`DemoUser` schema.

---

*Audit performed by read-only inspection of repo @ 2026-11-15. Mọi claim đều kèm file reference; không có thay đổi code nào được thực hiện.*
