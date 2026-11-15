# ADR-001: ATRS — Deployment, Canonical Identity & Tenancy

> **Status:** ✅ Accepted 2026-11-15 (user chốt theo `atrs-decision-support.md`)
> **Context:** SRS service v1.0 + UI/UX SRS v1.0 · gap analysis `BASE_CODE_GAP_ANALYSIS_ATRS.md`
> **Supersedes:** SRS §4.1 luồng "Gateway → CoreHub" (CoreHub là class library — hard stop, không host API)

---

## Quyết định 1 — Deployment: service riêng `vanan-atrs`

**Chọn:** Service độc lập theo pattern `10_Voice`/`8_Metering`/`9_TenantAI`.

- Thư mục: `11_Atrs/vanan-atrs/` (kế thừa numbering: 8 metering, 9 tenantai, 10 voice).
- Sln 4 projects: `VanAn.Atrs.Api` / `VanAn.Atrs.Contracts` / `VanAn.Atrs.Domain` / `VanAn.Atrs.Infra` + `tests/VanAn.Atrs.Tests`.
- Dockerfile port **5080**; PG riêng (trust registry cross-tenant — không SQLite per-tenant).
- Auth: JWT `StaffJwtValidator` clone (staff/admin) + customer token forward tới ShopERP `/api/customer-identity/me` (pattern `DeviceRegistrationController`).
- CI: `deployment-impact.yml` app `atrs` → `11_Atrs/**`; `cd-multivps.yml` matrix `atrs` → `ghcr.io/anlebao/vanan-atrs`.

**Lý do:** blast radius riêng; pattern proven 3× (CI/CD sẵn); cross-tenant profile cần PG; không phình Gateway; attachable by contract (user directive pattern voice).

**Hệ quả:** contracts dùng chung đặt trong `1_Shared` (hoặc `VanAn.Atrs.Contracts` referenced bởi consumer — quyết định theo pattern voice: voice contracts nằm trong service, Gateway clone/forward. Dùng pattern voice: contracts trong service, client HTTP proxy trong consumers).

## Quyết định 2 — Canonical identity: `TrustSubjectId`

**Chọn:** Profile key = `TrustSubjectId` (UUID mới, do ATRS tạo), KHÔNG dùng PII/device làm PK.

- `TrustSubjects` (1 row/người): `Id`, `PrimaryContactHash` (HMAC-SHA256, key từ secret manager — chỉ để lookup, KHÔNG PK), `CreatedAt`.
- `TrustSubjectLinks`: `(TrustSubjectId, IdentityType[Customer|DemoUser|DeviceCluster], IdentityId, TenantId?)` — map per-tenant Customer rows + DemoUser + device clusters.
- `AccountTrustProfiles`: 1-1 với TrustSubject; `Status`, `AssuranceState`, RowVersion.
- Lazy creation: first `/account-trust/me` call (FR-002 PENDING_SETUP) — không bulk v1.0.

**Lý do:** đúng nguyên tắc identity master plan ("UUID immutable là khóa nội bộ — không dùng SĐT/email làm PK"); 1 profile/người xuyên tenant/app; device chỉ là tín hiệu (SRS core principle); sẵn sàng IdP vision.

**Hệ quả:** cần cột `ContactHash` (HMAC) additive trên `Customer`/`DemoUser` để resolve — phối lịch ID-P1/P4 (cùng cần lookup theo contact; tránh conflict migration).

## Quyết định 3 — Tenancy

**Chọn:** Trust registry là **cross-tenant** (không `TenantId` bắt buộc trên `TrustSubjects`/`AccountTrustProfiles`/`AccountSecurityEvents`):
- `TenantId = null` cho entities account-level; scope staff access qua `AtrsPermissions` (scope theo tenant/queue — ATRS-10).
- Evidence/case entities per-tenant khi cần (vd `DisputeCases.TenantId` = tenant khiếu nại, nhưng `TrustSubjectId` là account-level).
- Mọi truy vấn staff: check `IAtrsAuthorizationService` scope, không dựa global query filter.

**Lý do:** customer là per-tenant nhưng profile bảo mật là xuyên tenant (1 người, nhiều cửa hàng); multi-tenancy enforce ở tầng authorization + staff scope, không ở tầng global filter.

## Hệ quả toàn diện

- SRS service §4.1/§12 phải sửa: luồng = app → Gateway (auth) → `vanan-atrs` API; CoreHub chỉ chứa contracts/Domain shared (Single Source of Truth `1_Shared/Domain.cs` cho entities dùng chung — device/ledger).
- Các entity ATRS đặt ở đâu: Domain của service (như voice) — KHÔNG đụng `1_Shared/Domain.cs` cho entities mới trừ khi dùng chung (vd `DeviceRegistration` extend vẫn ở `1_Shared`).
- Reviewers: Solution Architect + user (approved).

*Đã tham chiếu: `atrs-decision-support.md`, `account-trust-recovery-master-plan.md` §3.*