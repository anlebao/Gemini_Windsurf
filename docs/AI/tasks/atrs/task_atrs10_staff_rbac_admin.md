# Task ATRS-10: Staff RBAC + Admin Screens A14-A22 (backend + ShopERP UI)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-8, ATRS-9 · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-014, §13 phân quyền, UI §4 roles/permissions + §4.3 least-privilege/maker-checker, A14-A22, §15 Playwright staff scenarios
> **Gap nguồn:** `UserRole` 8 giá trị quá thô; không có permission groups reviewer/investigator/auditor/security-admin; maker-checker chưa có primitive.

## 1. OBJECTIVE

Hệ phân quyền staff cho ATRS (8 nhóm quyền UI §4.2) + toàn bộ staff screens: operations dashboard, recovery queue/review, dispute queue/investigation/decision+approval, audit explorer, security policy, roles admin — backend-authoritative.

## 2. THIẾT KẾ

### 2.1 `AtrsPermissions` (D5 — permission table mới)
- Entity `StaffPermissionGrant`: `Id, DemoUserId, Permission, ScopeTenantId?, GrantedBy, GrantedAt, ExpiresAt?`.
- 8 permission groups map UI §4.2: `recovery.view_assigned`, `recovery.review`, `recovery.approve`, `dispute.view_assigned`, `dispute.investigate`, `dispute.approve_adjust`, `audit.read`, `policy.manage`, `roles.manage`, `case.assign`, `note.write`…
- Helper `IAtrsAuthorizationService.HasAsync(userId, permission, caseId?)` — check grant + scope (tenant/queue) + case assignment.
- Super Admin = break-glass path (dual-control, time-boxed, audit) — định nghĩa rõ trong card này (gap G-breakglass).

### 2.2 Enforcement
- Mọi staff endpoint ATRS check permission server-side — ẩn nút UI KHÔNG phải security (UI §4.3).
- Maker-checker primitive: `AdjustmentProposal { ProposedBy, ApprovedBy, Status }` — approve reject nếu cùng người (khi policy yêu cầu).
- Mọi staff access sensitive case → `AccountSecurityEvents` (ActorType=STAFF, ActorId, reason).
- Staff MFA step-up cho tác vụ nhạy cảm (dùng ATRS-3 step-up + MFA-1 TOTP).

### 2.3 Screens (ShopERP Blazor, UI Platform components — reuse `LoyaltyReconcile.razor` precedent)
- **A14 Operations dashboard**: widgets từ data thật (new recovery, awaiting review, needs-info, open disputes, SLA overdue, recent incidents) — không hardcode.
- **A15 Recovery queue**: server-side search/filter/pagination; masked account id; SLA indicator.
- **A16 Recovery review**: 9 sections theo UI spec; actions request-info/recommend/approve/reject/escalate/assign/note; hiển thị policy version; KHÔNG lộ secret answers/private keys/recovery codes.
- **A17 Dispute queue**: tương tự.
- **A18 Dispute investigation**: claim + account context tối thiểu + transaction từ ledger read-only + device/auth timeline (authorized) + attachments + notes + evidence timeline.
- **A19 Decision & approval**: decision/reason/user-facing explanation/internal rationale/proposed adjustment/approver identity; maker-checker enforce; không mark RESOLVED trước ledger confirm.
- **A20 Audit explorer**: read-only, server-side search, filters (date/actor/category/case/result/app), export chỉ khi authorized + audited.
- **A21 Security policy**: read/edit policy groups (passkey rules, recovery limits, thresholds, maker-checker, SLA) — chỉ security admin; show current/proposed/effective/policy version; high-impact change cần approval; không lộ fraud thresholds cho unauthorized.
- **A22 Roles & permissions**: staff account ↔ grants ↔ scope; prevent removing last sysadmin; log mọi change.

### 2.4 APIs
- `/api/admin/atrs/*` — permission-filtered, SystemAdmin policy cho roles.manage, scope check cho phần còn lại.

## 3. FILES (dự kiến)

- `AtrsPermissions` entity + `IAtrsAuthorizationService`
- `2_Gateway` hoặc `vanan-atrs` admin controllers
- `5_WebApps/ShopERP/Components/Pages/Admin/Atrs/*.razor` (9 pages) + NavMenu nhóm "Bảo mật tài khoản"
- Tests: authorization matrix (mỗi role × mỗi action), maker-checker, masking

## 4. TESTS

- [ ] Permission matrix: staff không quyền → 403 cả API lẫn UI route
- [ ] Scope: reviewer tenant A không xem case tenant B
- [ ] Maker-checker self-approval → reject
- [ ] Sensitive data masking trong queue/list responses
- [ ] Audit explorer read-only (không update/delete endpoint)
- [ ] Policy change → audit + version bump; unauthorized → 403
- [ ] Playwright: staff without permission không access review (UI §15 scenario 16-22)

## 5. ACCEPTANCE

- [ ] A14-A22 hoạt động với data thật, server-paginated
- [ ] FR-014 + §13 pass; UI §14.4 pass
- [ ] Break-glass procedure documented + audited

## 6. RISKS

- Lộ PII cho staff → masking + AccessClassification + audit mọi view (FR-012).
- Permission table mới vs mở rộng UserRole — D5 chốt trong ATRS-1; nếu user chọn extend enum → điều chỉnh card.
- Queue performance khi case lớn → server-side pagination bắt buộc (UI §A15 rule).

## 7. IMPLEMENTATION STATUS (Wave 3 subset — 2026-11-16/17) — ✅ DEPLOYED + RV PASS

**Status: SUBSET CLOSED (deployed + RV verified).** Subset còn lại (A14/A20-A22, MFA step-up, permission groups UI đầy đủ) thuộc wave sau.

Đã làm (Wave 3 + Wave 5 W5a):
- `StaffPermissionGrant` entity + `AtrsPermissions` constants (recovery.view/review/approve, dispute.view/investigate/propose_adjustment/approve_adjustment, case.assign) + migration.
- `IAtrsAuthorizationService`/`AtrsAuthorizationService`: grant active (chưa revoke/expire) + scope tenant fail-closed + SystemAdmin break-glass + legacy Owner→recovery.* map (transition).
- Enforcement server-side trên mọi staff endpoint: `AdminDisputesController`, `AdminRecoveryController`, `AdminPermissionsController` (grant/revoke chỉ SystemAdmin), `InternalRecoveryController` (legacy route nâng cấp từ role-check → permission-check).
- `StaffPrincipal` helper (sub→DemoUserId, roles từ JWT).
- Audit: mọi staff view/action dispute + recovery → `AccountSecurityEvent` ActorType=Staff.
- ShopERP `AtrsAdminApiClient` mint JWT **giữ role thật** (không SystemAdmin hoá) → RBAC atrs enforce đúng; grant được cấp qua `POST /admin/permissions`.
- **Wave 5 W5a (2026-11-17, commit `5ed78d34`, deployed CD `44034164443`, RV PASS):** A14 ops dashboard (`AdminOpsController` + `/admin/atrs` 7 widgets data thật) · A15 recovery queue full (paged + total + filter status/type/assignee/unassigned + SLA 24h) · A16 recovery review full (`/admin/recovery/{id}`: 9 sections — risk policy version, devices, identity links, sanitized timeline, notes/recommend, actions assign/request-info/escalate/return-to-review/approve/reject; KHÔNG evidence payload) · `RecoveryCaseNote` entity (append-only) · escalate = status `ESCALATED` + `ReturnToReview` · audit staff events mọi action. Sweep 41/41 + probe 10/10 + Playwright 6/6.

Chưa làm:
- A20 audit explorer, A21 security policy, A22 roles UI (Wave 5 W5b/W5c); staff MFA step-up cho tác vụ nhạy cảm; permission groups UI (8 nhóm đầy đủ — Wave 3 chỉ subset cần cho dispute/recovery).
