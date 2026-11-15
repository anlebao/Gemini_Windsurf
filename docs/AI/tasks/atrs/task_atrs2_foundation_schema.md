# Task ATRS-2: Foundation Schema — TrustProfile, TrustSubject, DeviceRegistry extension, SecurityEvents, Consent

> **Master plan:** §4 · **Phụ thuộc:** ATRS-1 (D1, D2, D8) · **Mode:** IMPLEMENT sau duyệt · **Gate 5:** CÓ — Domain entities mới
> **SRS coverage:** FR-001, FR-002, FR-003 (phần registry), FR-009, §7.1/7.2/7.8, NFR-002 (consent), §11

## 1. OBJECTIVE

Dựng nền dữ liệu ATRS: profile gắn canonical identity, device registry nâng cấp, append-only security events, consent records — trên PG của service `vanan-atrs` (D2) hoặc Gateway PG nếu D2 chọn khác.

## 2. DOMAIN ENTITIES (Single Source of Truth → `1_Shared/Domain.cs` hoặc `1_Shared/Domain/` theo convention mới)

> Lưu ý governance: entity mới PHẢI theo Single-Identity Pattern (Id = business key, `builder.Ignore` VO) + `BaseEntity` + TenantId rule. Trust profile là **cross-tenant** → quyết định `TenantId` semantics trong ATRS-1 (đề xuất: `TenantId = system/empty` + scope qua TrustSubject links).

### 2.1 `TrustSubject` + `AccountTrustProfile`
- `TrustSubject`: canonical identity cluster — `Id`, `PrimaryContactHash` (HMAC-SHA256 của phone/email đã verify, key từ secret manager), `CreatedAt`.
- `AccountTrustProfile`: `Id`, `TrustSubjectId` (unique), `Status` (PENDING_SETUP/ACTIVE/RESTRICTED/CLOSED), `AssuranceState` (BASIC/PROTECTED/RECOVERY_READY), `RowVersion`, timestamps.
- `TrustSubjectLink`: map subject ↔ (`CustomerId` per-tenant | `DemoUserId` | `DeviceId`) — nguồn cho mọi truy vấn "profile của tôi".

### 2.2 Extend `DeviceRegistration` (existing — MODIFY, Gate 5)
Thêm (không xóa cột cũ — fingerprint vẫn làm risk signal):
- `VanAnDeviceId` (string/UUID random do server cấp — identity chính)
- `Status`: PENDING/ACTIVE/SUSPICIOUS/REVOKED/LOST (+ `IsActive`/`IsVerified` giữ cho compat)
- `FriendlyName`, `RegistrationMethod` (WEBAUTHN/INSTALLATION_ID/CUSTOMER_TOKEN)
- `FirstSeenAt`, `LastSeenAt`, `RevokedAt`, `RevokedReason`
- `DeviceCredentialId` (nullable — link sang ATRS-5)
- Unique (`CustomerId`/`TrustSubjectId`, `VanAnDeviceId`); giữ max-3 rule + FraudFlag hiện có.

### 2.3 `AccountSecurityEvents` (mới — append-only, D8)
Fields theo FR-009: `EventId, TrustSubjectId/UserId, EventType, OccurredAt, ReceivedAt, ActorType(USER/SYSTEM/STAFF/SERVICE), ActorId, SourceApplication, DeviceId, SessionId/CorrelationId, Outcome, ReasonCode, EvidenceReferences(JSON), SchemaVersion, MinimalMetadata(JSON)`.
- Index: (TrustSubjectId, OccurredAt), (EventType, OccurredAt), CorrelationId.
- **Không** lưu secret/PII trong payload.

### 2.4 `ConsentRecords`
- `Id, TrustSubjectId, Purpose, PrivacyNoticeVersion, GrantedAt, WithdrawnAt, EvidenceRef` — chứng minh consent theo §11.2.

### 2.5 Migration
- PG migration cho `vanan-atrs` DB (EF Core, theo pattern `8_Metering`/`9_TenantAI` — raw DDL idempotent `EnsureP7Schema`-style hoặc EF migrations, KHÔNG `EnsureCreated` cho prod).
- Backfill strategy: TrustSubject tạo lazy khi user đầu tiên gọi `/account-trust/me` (FR-002 PENDING_SETUP) — không bulk migration v1.0.

## 3. FILES (dự kiến)

- `1_Shared/Domain.cs` (+ có thể `1_Shared/Domain/Trust/` mới theo convention aggregate)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` + Configurations + Migrations (nếu đặt ở CoreHub/Gateway PG)
- HOẶC `vanan-atrs/src/VanAn.Atrs.Domain|Infra` (nếu service riêng — theo pattern voice: Domain/Infra/Api/Contracts/Tests)
- `6_Tests/VanAn.Core.Tests` hoặc `vanan-atrs/tests`

## 4. TESTS

- [ ] TrustSubject unique per contact-hash; 1 profile/subject
- [ ] DeviceRegistration: VanAnDeviceId unique, status transitions hợp lệ, không cho REVOKED→ACTIVE trực tiếp
- [ ] AccountSecurityEvents append-only (không update/delete path)
- [ ] Consent withdraw set WithdrawnAt, không xóa row
- [ ] Multi-tenancy: staff tenant A không đọc event/evidence tenant B (theo scope rule ATRS-1)

## 5. ACCEPTANCE

- [ ] `dotnet build VanAn.sln` 0/0 + `guard-check.ps1` PASS
- [ ] Migration apply sạch PG (và SQLite nếu entities sync — quyết định trong card)
- [ ] Domain pure (no EF), AccountingEntry không đụng, Gate 5 checklist pass

## 6. RISKS

- `DeviceRegistration` đang PG-only (community entities) — extend phải giữ PG-only, không sync SQLite.
- TrustSubject backfill: customer cũ chưa có contact verify → subject tạo với `PrimaryContactHash = null` tạm, upgrade sau — phải test case này.
