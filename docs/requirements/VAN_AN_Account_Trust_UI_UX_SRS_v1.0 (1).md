# VAN AN Account Trust & Recovery Service

## UI/UX Software Requirements Specification (SRS) v1.0

-   **Document type:** UI/UX implementation specification
-   **Product:** Vạn An Account Trust & Recovery Service
-   **Version:** 1.0
-   **Status:** Ready for technical review and implementation planning
-   **Audience:** Devin, frontend/backend developers, QA, product owner
-   **Primary language:** Vietnamese UI; code identifiers and technical
    labels may use English
-   **Platform:** Existing Vạn An PWA / responsive web application

------------------------------------------------------------------------

## 1. Purpose

This document converts the approved screen and user-flow design into
implementable UI/UX requirements for the Vạn An Account Trust & Recovery
Service.

The service provides: 1. A shared account-security profile across Vạn An
applications. 2. Device and credential registration/management. 3.
Account recovery when a user loses access to a device or credential. 4.
Dispute submission and tracking for loyalty points, wallet balances, and
ecosystem benefits. 5. Staff workflows for reviewing recovery requests
and investigating disputes. 6. Audit and policy administration.

The interface must help users protect and recover accounts without
making a Device ID, personal profile data, or secret questions appear to
be conclusive proof of identity.

## 2. Product and UX principles

### 2.1 Core principles

-   **One account security profile:** Security settings should be shared
    across Vạn An applications wherever the existing identity
    architecture supports it.
-   **Progressive security:** Do not force every security setup step
    during initial registration. Explain the benefit and allow users to
    complete optional steps later.
-   **No OTP as the default authentication path:** Prefer
    passkeys/WebAuthn and recovery codes where supported. Do not design
    the UI as if every browser/device has a stable, reliable Device ID.
-   **Device ID is a signal, not proof:** Device recognition can support
    risk evaluation but must not be the sole factor for login, recovery,
    or dispute decisions.
-   **No secret-answer exposure:** Secret answers must never be
    displayed back to users or staff. If the legacy design retains
    secret questions, treat them only as a supplementary signal, not a
    primary recovery factor.
-   **Separate account recovery from transaction disputes:** Losing
    access to an account and disputing a points/wallet transaction are
    different processes with different evidence and decision paths.
-   **Explain consequences before destructive actions:** Revoking a
    device or credential must show what will happen and how the user can
    regain access.
-   **Auditability:** Security-sensitive actions and staff decisions
    must generate auditable events.
-   **Reuse existing Vạn An UI:** Inspect the current codebase and reuse
    its layout, navigation, components, styles, validation patterns, and
    authentication conventions. Do not introduce a parallel design
    system without approval.
-   **Mobile-first:** The primary user flows must work comfortably on a
    phone-sized viewport and remain usable on desktop.
-   **Accessible and understandable:** Use plain Vietnamese, clear
    status labels, readable contrast, keyboard support, and explicit
    error messages.

### 2.2 Non-goals

This UI/UX SRS does not authorize: - Building a new identity provider or
replacing the existing authentication architecture. - Treating browser
fingerprinting as a stable device identity. - Storing or displaying
plaintext secret answers, passwords, private keys, or recovery-code
values after initial display. - Implementing a financial wallet ledger
or loyalty ledger inside this service. The dispute UI consumes
references and statuses from the authoritative business services. -
Automatically approving high-risk recovery or financial adjustments
solely from profile matches. - Creating a new UI framework or design
system.

## 3. Scope

### 3.1 User-facing scope

-   Security onboarding
-   Personal evidence/profile review
-   Passkey registration and management
-   Optional supplementary security challenge setup
-   Recovery-code generation and regeneration
-   Security center
-   Device registry and device detail
-   Account recovery wizard and recovery status
-   Dispute center, submission, and tracking

### 3.2 Staff/admin scope

-   Operations dashboard
-   Recovery request queue and review
-   Dispute queue, investigation, decision, and approval
-   Audit explorer
-   Security policy view/edit (permission controlled)
-   Staff role and permission management

### 3.3 Screen inventory

The 22 logical screens are grouped below. A logical screen may be
implemented as a route, tab, modal, drawer, wizard step, or panel. Devin
must map these requirements to the existing navigation architecture
rather than create unnecessary routes.

  ID    Logical screen                     User
  ----- ---------------------------------- ------------------
  U01   Set up account protection          End user
  U02   Personal profile / evidence        End user
  U03   Set up passkey                     End user
  U04   Supplementary security challenge   End user
  U05   Recovery codes                     End user
  U06   Security center                    End user
  U07   Device registry                    End user
  U08   Device detail                      End user
  U09   Account recovery wizard            End user
  U10   Recovery request status            End user
  U11   Dispute center                     End user
  U12   Submit dispute                     End user
  U13   Dispute detail                     End user
  A14   Operations dashboard               Staff
  A15   Recovery queue                     Staff
  A16   Recovery review                    Staff
  A17   Dispute queue                      Staff
  A18   Dispute investigation              Staff
  A19   Decision and approval              Staff / approver
  A20   Audit explorer                     Auditor / admin
  A21   Security policy                    Security admin
  A22   Roles and permissions              System admin

## 4. Roles and access control

### 4.1 End-user roles

-   **Account owner:** Can view and manage their own security profile,
    devices, credentials, recovery requests, and disputes.
-   **Delegated user (if supported by the existing Vạn An account
    model):** Can access only explicitly delegated business functions.
    Must not automatically inherit personal account recovery or security
    administration rights.

### 4.2 Staff roles

The backend must remain authoritative for authorization. Hiding a button
in the UI is not sufficient access control.

Suggested permission groups (map to existing RBAC if available):

  -----------------------------------------------------------------------
  Permission group                    Typical capabilities
  ----------------------------------- -----------------------------------
  Support agent                       View assigned recovery/dispute
                                      cases; request additional evidence;
                                      add internal notes

  Recovery reviewer                   Review recovery evidence; recommend
                                      outcome

  Recovery approver                   Approve/reject/escalate recovery
                                      within authorized scope

  Dispute investigator                View transaction references and
                                      evidence; propose a dispute outcome

  Dispute approver                    Approve financial/points
                                      adjustments where policy requires

  Auditor                             Read-only access to audit events
                                      and case history

  Security admin                      Manage security policy within
                                      authorized scope

  System admin                        Manage staff roles and permissions
  -----------------------------------------------------------------------

### 4.3 Least privilege and maker-checker

-   A person who proposes a high-impact adjustment must not approve
    their own adjustment when maker-checker is required.
-   Staff must see only the personal data necessary for their assigned
    task.
-   Staff UI must not expose passkey private material, recovery-code
    plaintext, or secret answers.
-   All staff access to sensitive case details must be logged.
-   If the current backend cannot enforce these rules, Devin must report
    the gap and not simulate security through frontend-only
    restrictions.

## 5. Navigation and information architecture

### 5.1 End-user navigation

Entry point: **Tài khoản** → **Bảo mật tài khoản**

Security center sections: 1. Trạng thái bảo vệ 2. Passkey / phương thức
đăng nhập 3. Thiết bị đã đăng ký 4. Mã khôi phục 5. Thông tin hỗ trợ xác
minh 6. Lịch sử hoạt động bảo mật 7. Khôi phục tài khoản 8. Tranh chấp
điểm thưởng / ví

The exact placement must follow the existing Vạn An application
navigation. Do not duplicate the same security center in every product
if a shared account area already exists.

### 5.2 Staff navigation

Staff portal: - Tổng quan - Khôi phục tài khoản - Tranh chấp - Nhật ký
kiểm toán - Chính sách bảo mật - Vai trò và quyền

Menu items must be permission-filtered and backend-authorized.

## 6. End-user screen requirements

### U01 --- Set up account protection

**Purpose:** Introduce account protection after sign-up or from the
security center.

**Content:** - Title: "Bảo vệ tài khoản Vạn An" - Short explanation of
why security methods matter. - Summary of available methods: passkey,
recovery codes, recognized devices, supplementary profile information. -
Clear explanation: device recognition helps detect unusual access but
does not independently prove account ownership.

**Actions:** - "Thiết lập ngay" → U02 or U03 based on the chosen setup
sequence. - "Để sau" → return to the originating page. - "Tìm hiểu thêm"
→ explanatory content.

**Rules:** - Do not block ordinary account use solely because optional
setup is incomplete, unless a separate business/security policy requires
it. - Show completion progress without implying that a 100% profile
guarantees identity. - Persist onboarding completion/dismissal according
to existing account preference mechanisms.

**States:** Initial, partially configured, completed, loading, service
unavailable.

### U02 --- Personal profile / evidence

**Purpose:** Let the user review the minimum profile information that
may support account recovery.

**Fields (only if supported and justified by backend policy):** -
Display name - Date of birth (optional; collect only if there is a
legitimate need) - Contact/recovery channel status (do not imply it is
verified unless verified) - Additional recovery hints or evidence
metadata, if approved

**Actions:** - Edit permitted fields - Save - Cancel

**Validation and privacy:** - Show why each field is requested. - Mark
required vs optional. - Do not treat a matching name or date of birth as
sufficient proof of ownership. - Mask sensitive values where
practical. - Do not collect excessive personal data merely to make
recovery appear stronger. - If the user has already supplied the field
in the account profile, reuse it rather than asking again.

**States:** View, edit, saving, saved, validation error, permission
denied, service error.

### U03 --- Set up passkey

**Purpose:** Register a passkey using supported browser/device WebAuthn
capabilities.

**Content:** - Explain passkeys in plain Vietnamese. - Explain that
availability depends on the user's device/browser and account
configuration. - Show supported/unsupported state based on actual
capability detection from the implementation.

**Actions:** - "Tạo Passkey" - "Thử lại" - "Để sau" - "Tìm hiểu phương
thức khác"

**Rules:** - The frontend must call the existing/backend-approved
WebAuthn registration flow; do not create a visual-only passkey setup. -
Handle user cancellation, timeout, unsupported browser, duplicate
credential, and server rejection. - Never expose private key material. -
On success, show credential nickname, registration date, and
device/browser metadata where available. - Do not claim a passkey is
registered until the server confirms successful credential registration.

**States:** Checking support, supported, unsupported, registration
pending, success, cancelled, timed out, failed.

### U04 --- Supplementary security challenge

> **Cập nhật 2026-11-15 (quyết định D7 đã chốt):** **OMIT khỏi scope v1.0.** Sản phẩm KHÔNG triển khai secret question; U04 bị loại khỏi navigation; tính năng được đánh dấu "not implemented, documented" (không thay thế bằng factor yếu khác). Recovery dựa vào passkey (U03) + recovery codes (U05) + manual review (U09/F04). Giữ mục này để tham chiếu nếu PO yêu cầu re-evaluate sau v1.0.

**Purpose:** Optional supplementary recovery signal, only if the product
owner and security review retain this feature.

**Important security constraint:** Secret questions are weak, guessable,
reusable, and susceptible to social engineering. This screen must not
position them as a primary authentication or recovery method.

**Content:** - Explain that this is supplementary only. - Allow
selection from approved question templates or another approved
mechanism. - Answer must be submitted securely and stored using a
backend-approved one-way protection scheme; never store/display
plaintext. - Do not reveal the correct answer in hints or staff tools.

**Actions:** Set, update, disable.

**Rules:** - Do not make this step mandatory during registration. -
Apply rate limits and lockout/risk policy server-side. - Do not reveal
whether a guessed answer is close. - If security review rejects secret
questions, omit U04 from navigation and mark the feature as not
implemented; do not silently replace it with another weak factor.

### U05 --- Recovery codes

**Purpose:** Generate and manage one-time recovery codes.

**Content:** - Explain that each code can be used once. - Display codes
only after successful generation and only at the time the backend
authorizes their display. - Provide copy and download/print options only
if supported by the existing product security requirements. - Explain
that users should store codes somewhere private and not share them.

**Actions:** - Generate codes - Copy codes - Confirm saved - Regenerate
codes (must warn that previous unused codes will be invalidated) -
Return to security center

**Rules:** - Never show previously generated plaintext codes again. -
Store only server-approved protected representations. - Regeneration
must require re-authentication/step-up as defined by backend policy. -
Log generation, regeneration, and use events without logging the code
values. - UI must not claim codes are saved by the user merely because
they were displayed.

**States:** Not generated, displaying once, user confirmation required,
generated, regenerating, expired/invalidated, error.

### U06 --- Security center

**Purpose:** Single overview of account protection.

**Content:** - Overall status: "Chưa thiết lập", "Cơ bản", "Đã tăng
cường" or equivalent policy-defined states. - Passkey status. -
Recovery-code status (available/not generated/needs regeneration; never
show code values). - Registered device count. - Last security
activity. - Recovery request entry. - Dispute entry.

**Actions:** - Manage passkeys - Manage devices - Manage recovery
codes - Edit recovery profile - View security history - Start account
recovery - Open dispute center

**Rules:** - Status wording must not imply absolute account security. -
Show warnings for stale or revoked credentials. - Do not show sensitive
technical identifiers in the main overview.

### U07 --- Device registry

**Purpose:** Show devices/sessions recognized or registered for the
account.

**Each item may show:** - User-defined device name - Device/browser
category (only when reliably available) - First registered date - Last
seen date/time (timezone-aware) - Status: active, revoked, pending
verification, unknown - Current-device marker, if reliably known

**Actions:** - Open device detail - Rename device - Revoke
device/credential (where supported) - Report lost device

**Rules:** - Device ID is an internal identifier/risk signal, not a
proof-of-identity badge. - Avoid presenting unstable browser
fingerprints as permanent device identity. - If device metadata is
unavailable, show "Không xác định" rather than guessing. - Revocation
must call the backend and update UI only after confirmation.

### U08 --- Device detail

**Purpose:** Inspect and manage one registered device record.

**Content:** - Device nickname - Device/browser metadata available from
backend - Registered at - Last seen - Status - Associated credential(s),
if the backend exposes safe metadata - Recent events relevant to this
device

**Actions:** - Rename - Revoke - Report lost - Back to device list

**Confirmation dialog for revoke:** - Identify the selected device. -
Explain that access/credential associated with it may stop working. -
Explain how to use another passkey or recovery method. - Require
explicit confirmation.

**Rules:** Never allow the user to revoke the only usable credential
without a clear recovery warning and policy-compliant confirmation path.

### U09 --- Account recovery wizard

**Purpose:** Guide a user who cannot access their account or registered
credential.

**Wizard steps:**

1.  **Describe the access problem**
    -   Lost device
    -   Device damaged/reset
    -   Passkey unavailable
    -   Recovery code unavailable
    -   Suspected unauthorized access
    -   Other
2.  **Choose available recovery methods**
    -   Use another registered passkey
    -   Use an unused recovery code
    -   Use another approved recovery method
    -   "I cannot use any of these"
3.  **Provide supporting information**
    -   Prefill known account information where possible.
    -   Ask only for information needed by the applicable recovery
        policy.
    -   Allow attachments only if supported, necessary, and protected.
    -   Explain that personal details alone do not guarantee approval.
4.  **Review and submit**
    -   Summary of submitted information
    -   Privacy notice
    -   Confirmation checkbox
    -   Submit request

**Rules:** - Do not ask users to re-enter information already held and
authorized for reuse. - Do not disclose which specific evidence would
allow an attacker to pass the check. - Use risk-based server decisions.
UI must not calculate or invent approval outcomes. - High-risk cases
must be queued for human review according to policy. - Prevent duplicate
submissions or show the existing active request. - Do not lock out the
user indefinitely because one method failed.

**States:** Step loading, validation errors, submission pending,
submitted, duplicate active request, service unavailable.

### U10 --- Recovery request status

**Purpose:** Let the user track an account recovery request.

**Content:** - Request reference - Created date/time - Current status -
Current processing stage - Last update - User action required, if any -
Secure support/help instructions

**Suggested statuses:** - Submitted - Under automated checks - Waiting
for staff review - Additional information required - Approved -
Rejected - Cancelled - Expired/closed

**Actions:** - Provide requested information - View request details -
Cancel request where policy allows - Return to security center

**Rules:** - Status comes from backend; do not infer it from elapsed
time. - Rejection reasons must be safe and not reveal anti-fraud
thresholds. - If more information is required, show exactly what the
user is allowed to provide and the deadline if one exists.

### U11 --- Dispute center

**Purpose:** Separate business transaction disputes from account
recovery.

**Categories:** - Loyalty points - Wallet/balance (only if an
authoritative wallet service exists) - Voucher/discount/benefit - Other
ecosystem transaction

**Content:** - Open disputes - Recently resolved disputes - Link to
relevant transaction history where supported

**Actions:** - Create a dispute - Open dispute detail - Filter by
status/category

**Rules:** - Do not describe loyalty points as cash or an e-wallet
unless the legal/product model explicitly supports that. - The
authoritative service owns transaction balances and ledger data. - The
dispute service must reference, not rewrite, the underlying ledger.

### U12 --- Submit dispute

**Purpose:** Capture a user complaint about a specific transaction or
benefit.

**Fields:** - Dispute category - Related transaction selector (prefill
from the authoritative service) - Issue reason (category list controlled
by backend) - Description - Supporting attachment(s), optional and
policy-controlled - Preferred contact channel, only if supported and
necessary

**Actions:** - Select transaction - Submit - Save draft only if draft
persistence is supported - Cancel

**Rules:** - When a transaction is selected, display authoritative
transaction date, reference, points/amount, and status from the source
service. - Do not ask the user to manually retype amount or points if
the transaction can be selected. - Do not allow the frontend to alter
ledger values. - Validate file type/size server-side and client-side. -
Show a review step before submission.

### U13 --- Dispute detail

**Purpose:** Track the investigation and outcome of a dispute.

**Content:** - Dispute reference - Category - Related transaction
reference - Submitted date - Current status - Timeline of status changes
visible to the user - User-provided description and attachments -
Requests for additional information - Final decision and explanation
appropriate for the user

**Actions:** - Provide additional information - View attachment -
Cancel/withdraw if policy permits - Return to dispute center

**Rules:** - Do not expose internal staff notes, fraud signals, or
private audit metadata. - A financial/points correction must be
reflected by the authoritative ledger service, not by editing a display
field.

## 7. Staff and administration screen requirements

### A14 --- Operations dashboard

**Purpose:** Give authorized staff an operational overview.

**Widgets (only where supported by real data):** - New recovery
requests - Recovery requests awaiting review - Requests requiring user
information - Open disputes - Cases overdue against configured SLA -
Recent security incidents

**Actions:** Open filtered queue.

**Rules:** - Do not hardcode sample counts in production. - Apply role
and tenant/organizational scope. - Avoid exposing unnecessary personal
data on dashboard cards.

### A15 --- Recovery queue

**Purpose:** Search and triage recovery cases.

**Columns:** - Case reference - Masked account identifier - Submitted
time - Risk/status category as authorized - Assigned reviewer - Last
update - SLA indicator (if configured)

**Filters:** - Status - Created date range - Assigned reviewer - Risk
category (permission-controlled) - Case type

**Actions:** - Open case - Assign/reassign (permission-controlled) - Add
internal note (permission-controlled)

**Rules:** Pagination or virtualized loading is required for large
queues. Search and filters must be server-side for production-scale
datasets.

### A16 --- Recovery review

**Purpose:** Allow authorized staff to review evidence and
recommend/decide an outcome.

**Sections:** 1. Case summary 2. Account profile (masked/minimized) 3.
Registered devices and safe device metadata 4. Credential status/events
(no secrets) 5. Recovery methods attempted 6. Submitted evidence 7.
Relevant security event timeline 8. Risk indicators with access control
9. Staff notes and decision history

**Actions:** - Request additional information - Recommend approve -
Recommend reject - Escalate - Assign case - Add internal note

**Decision safeguards:** - Display the applicable policy version or rule
reference. - Require a reason code and explanatory note for a
decision. - Enforce maker-checker if configured. - Do not expose
plaintext secret answers, passkey private keys, or recovery-code
values. - Backend must validate permission, case state, policy, and
approval authority.

### A17 --- Dispute queue

**Purpose:** Search and triage user disputes.

**Columns:** - Dispute reference - Masked account identifier -
Category - Transaction reference - Submitted time - Status - Assigned
investigator - SLA indicator if configured

**Filters:** Status, category, date range, assignee, transaction
reference.

**Actions:** Open investigation, assign, add internal note.

### A18 --- Dispute investigation

**Purpose:** Provide an evidence workspace for dispute investigators.

**Sections:** - User claim - Account context (minimum necessary) -
Transaction details from authoritative ledger - Relevant loyalty/wallet
events - Device/authentication event timeline where relevant and
authorized - User attachments - Internal notes - Evidence timeline

**Actions:** - Add investigation note - Request additional information -
Propose outcome - Escalate

**Rules:** - Ledger details must be read from the source service. -
Never allow staff to edit a displayed balance directly. - If a
correction is required, create a proposed adjustment command/reference
for the authoritative service. - Distinguish "investigation conclusion"
from "ledger adjustment completed".

### A19 --- Decision and approval

**Purpose:** Record the proposed outcome and obtain required approval.

**Fields:** - Decision: uphold / partially uphold / reject / request
more information / escalate - Reason code - User-facing explanation -
Internal rationale - Proposed points/amount adjustment, if applicable
and supported - Source transaction reference - Approver identity and
timestamp (system generated)

**Actions:** - Submit proposal - Approve - Reject proposal - Return for
more information

**Rules:** - Maker-checker must be enforced server-side. - The approver
must see who proposed the action. - Do not mark a dispute "resolved"
until the authoritative service confirms any required adjustment. -
Record idempotency/reference information for adjustment commands. -
Never fabricate a successful ledger adjustment based on a UI response
alone.

### A20 --- Audit explorer

**Purpose:** Search security and case audit events.

**Filters:** - Date/time range - Actor (user/staff/system) - Event
category - Account/case reference - Result - Source application

**Event categories may include:** - Passkey registration/removal -
Recovery-code generation/use/revocation - Device
registration/revocation - Recovery request submission/review/decision -
Dispute submission/decision - Staff access to sensitive records - Policy
and permission changes

**Display:** - Event timestamp - Actor - Action - Target reference -
Outcome - Correlation/request ID - Safe metadata

**Rules:** - Read-only UI. - No edit/delete controls. - Do not display
secrets, full recovery codes, private keys, or unnecessary personal
data. - Export only if authorized and audited. - Search must be
server-side.

### A21 --- Security policy

**Purpose:** Manage approved security policy settings.

**Potential policy groups:** - Passkey requirements - Recovery-code
rules - Recovery attempt/rate limits - Recovery review thresholds -
Device risk handling - Session/credential revocation behavior -
Maker-checker requirements - Case SLA and expiry - Dispute adjustment
approval limits

**Rules:** - Only authorized security administrators can access. - Show
current value, proposed value, effective date, and policy version. -
Require reason for changes. - High-impact policy changes require
approval where configured. - All changes must be audited. - Do not
expose sensitive fraud thresholds to ordinary users or unauthorized
staff. - If the backend policy API is not available, implement read-only
or hide this screen; do not fake policy persistence.

### A22 --- Roles and permissions

**Purpose:** Manage staff access.

**Content:** - Staff account - Role(s) - Scope
(organization/tenant/queue) - Permission summary - Status - Last updated

**Actions:** - Assign/revoke role - Change scope - Disable staff access
(if supported) - View permission history

**Rules:** - Enforce separation of duties. - Prevent removing the last
authorized system administrator unless an approved recovery process
exists. - Require confirmation for privilege elevation. - Log every
role/permission change. - Backend is authoritative; frontend must not
grant permissions locally.

## 8. End-to-end user flows

### Flow F01 --- New account protection setup

1.  User completes the existing Vạn An sign-up flow.
2.  User sees U01 introduction (if configured for this entry point).
3.  User may review profile information in U02.
4.  User is offered passkey registration in U03.
5.  User may generate recovery codes in U05.
6.  Optional supplementary challenge U04 appears only if approved.
7.  User returns to U06 security center.
8.  If user skips optional steps, account remains usable subject to
    existing policy; the security center shows incomplete status.

**Acceptance:** The flow must not falsely claim that a device ID alone
secures or verifies the account.

### Flow F02 --- Manage devices

1.  User opens Account → Security center (U06).
2.  User opens Device registry (U07).
3.  User selects a device (U08).
4.  User renames, reports lost, or revokes it.
5.  For revocation, user reviews consequences and confirms.
6.  Backend processes action; UI refreshes only after success.

### Flow F03 --- Recover account with a remaining credential

1.  User opens recovery entry (U09).
2.  User selects the issue type.
3.  System offers eligible existing recovery methods.
4.  User completes a supported passkey or recovery-code flow.
5.  Backend evaluates the request and returns the authoritative result.
6.  If approved, user follows the server-guided credential/device
    re-enrollment process.
7.  User can view the case/result in U10.

### Flow F04 --- Recover account after losing all credentials

1.  User opens U09 and declares that no registered method is available.
2.  UI explains that additional review may be required.
3.  User provides only policy-required supporting information.
4.  User reviews and submits.
5.  Backend performs risk evaluation.
6.  Case is auto-processed only if policy explicitly permits; otherwise
    it enters A15/A16.
7.  Staff may request more information, approve, reject, or escalate.
8.  User tracks progress in U10.
9.  On approval, the user registers a new credential/device through the
    backend-approved flow.
10. Old credentials/devices are revoked according to the approved
    recovery policy.

### Flow F05 --- Dispute loyalty points or wallet transaction

1.  User opens U11.
2.  User selects dispute category.
3.  User selects a transaction from the authoritative transaction list
    in U12.
4.  UI prepopulates transaction details.
5.  User chooses a reason, describes the issue, and optionally attaches
    evidence.
6.  User reviews and submits.
7.  User tracks status in U13.
8.  Staff investigates through A17/A18.
9.  Decision/approval occurs in A19.
10. If adjustment is required, the source ledger service processes it
    and confirms completion.
11. User sees the final user-facing explanation in U13.

### Flow F06 --- Staff recovery review

1.  Staff opens A14.
2.  Staff opens A15 and selects a case.
3.  Staff reviews the permitted evidence in A16.
4.  Staff may request more information, recommend a decision, or
    escalate.
5.  Approver completes any required maker-checker step.
6.  Backend records the decision and audit event.
7.  User sees the updated status in U10.

### Flow F07 --- Staff dispute investigation

1.  Staff opens A17.
2.  Investigator opens A18.
3.  Investigator compares the user claim with authoritative
    transaction/ledger events.
4.  Investigator adds internal notes and proposes an outcome.
5.  Authorized approver completes A19.
6.  If an adjustment is needed, the authoritative ledger service
    executes it.
7.  Case is closed only after the required downstream confirmation.

## 9. Shared UI components

Devin should reuse existing components where available. The following
are logical reusable components, not a mandate to create a new component
library:

-   Security status summary
-   Credential status row
-   Device list item
-   Device metadata panel
-   Recovery method selector
-   Multi-step wizard header/progress
-   Case status badge
-   Case timeline
-   Transaction selector
-   Evidence/attachment list
-   Confirmation dialog
-   Permission-aware action group
-   Empty state
-   Loading skeleton
-   Inline validation message
-   Error/retry panel
-   Audit event row

### 9.1 Status vocabulary

Use consistent Vietnamese labels across user and staff views. Suggested
mapping:

  Internal status       User-facing label
  --------------------- -------------------------
  Draft                 Bản nháp
  Submitted             Đã gửi
  PendingChecks         Đang kiểm tra
  PendingReview         Chờ xét duyệt
  NeedsInformation      Cần bổ sung thông tin
  Approved              Đã chấp thuận
  Rejected              Không được chấp thuận
  Escalated             Đang chuyển cấp xử lý
  Cancelled             Đã hủy
  Expired               Đã hết hạn
  Resolved              Đã xử lý
  AdjustmentPending     Đang cập nhật giao dịch
  AdjustmentCompleted   Đã cập nhật giao dịch

The backend status enum is authoritative. UI may map values to labels
but must not invent state transitions.

**Align backend enums (đã chốt 2026-11-15 — bảng mapping duy nhất):**
- Recovery (backend): `SUBMITTED → "Đã gửi"` · `AUTOMATED_CHECK → "Đang
  kiểm tra"` · `PENDING_USER_ACTION → "Cần bổ sung thông tin"` ·
  `PENDING_REVIEW → "Chờ xét duyệt"` · `APPROVED → "Đã chấp thuận"` ·
  `REJECTED → "Không được chấp thuận"` · `CANCELLED → "Đã hủy"` ·
  `EXPIRED → "Đã hết hạn"` · `COMPLETED → "Đã hoàn tất"`.
- Dispute (backend): `OPEN → "Đã mở"` · `TRIAGE → "Đang phân loại"` ·
  `WAITING_FOR_USER → "Cần bổ sung thông tin"` · `UNDER_REVIEW → "Đang
  xem xét"` · `WAITING_FOR_EXTERNAL_EVIDENCE → "Chờ đối soát"` ·
  `DECIDED → "Đã ra quyết định"` · `RESOLVED → "Đã xử lý"` ·
  `REJECTED → "Không được chấp thuận"` · `CLOSED → "Đã đóng"`.
- AssuranceState: `BASIC → "Chưa thiết lập"` · `PROTECTED → "Cơ bản"` ·
  `RECOVERY_READY → "Đã tăng cường"`.

## 10. Global interaction and visual requirements

### 10.1 Responsive behavior

-   Support narrow mobile viewport, tablet, and desktop.
-   On mobile, use single-column layouts and full-width primary actions.
-   On desktop, staff tables may use wider layouts; on mobile, use
    stacked case cards or horizontal scrolling only where appropriate.
-   Dialogs must fit the viewport and remain scrollable.
-   Do not hide critical warnings below the fold.

### 10.2 Form behavior

-   Label every field.
-   Mark required/optional clearly.
-   Validate on submit and provide inline feedback.
-   Preserve user-entered data after recoverable network errors where
    safe.
-   Prevent accidental double-submit.
-   Disable submit while a request is in progress.
-   Use server validation as the final authority.
-   Avoid asking for the same information twice.

### 10.3 Loading, empty, error, and success states

Every data-driven screen must define: - Loading state - Empty state -
Loaded state - Error state with retry where appropriate -
Permission-denied state - Stale-data or refresh state where relevant

Do not show empty state when the API failed. Distinguish "no records"
from "could not load records".

### 10.4 Notifications

-   Use inline confirmation for routine successful actions.
-   Use modal confirmation for destructive or high-impact actions.
-   Do not rely only on color to convey status.
-   Avoid showing sensitive recovery or dispute information in
    push/browser notifications.

### 10.5 Accessibility

-   Keyboard-accessible controls.
-   Visible focus states.
-   Semantic headings and labels.
-   Screen-reader names for icon-only controls.
-   Sufficient contrast.
-   Do not communicate success/failure by color alone.
-   Focus must move appropriately when dialogs open/close and when
    wizard steps change.

## 11. Data and API integration expectations

This is a UI/UX SRS; it does not prescribe new backend endpoints. Devin
must inspect the existing services and map the UI to available APIs.

Expected logical data objects: - AccountSecurityProfile - DeviceRecord -
PasskeyCredentialMetadata - RecoveryCodeStatus (never plaintext code
collection after initial display) - RecoveryRequest - RecoveryEvidence -
DisputeCase - TransactionReference - CaseTimelineEvent - AuditEvent -
SecurityPolicy - StaffRole / Permission

### 11.1 Integration rules

-   Account identity and authorization come from the existing
    identity/authentication service.
-   Passkey operations use the approved WebAuthn backend flow.
-   Device records come from the backend registry; browser metadata is
    best-effort.
-   Recovery decisions come from the recovery service/policy engine.
-   Loyalty points and wallet transaction details come from their
    authoritative services.
-   Staff permissions are checked by backend APIs.
-   Audit events are written by backend services; frontend logging is
    not a substitute.
-   The UI must handle unavailable integrations gracefully and clearly
    indicate unavailable functionality.

### 11.2 No fabricated data

-   No hardcoded production metrics, case records, transaction values,
    or approval outcomes.
-   Mock data may be used only in an explicitly marked development/demo
    environment.
-   Do not convert missing values into guessed device names, dates, risk
    levels, or transaction values.

## 12. Security and privacy requirements

1.  Never store credentials, private keys, recovery codes, or secret
    answers in browser local storage as plaintext.
2.  Never log secrets in console output, telemetry, analytics, crash
    reports, or URLs.
3.  Never display secret answers to staff.
4.  Mask account identifiers and personal data in queues and dashboards.
5.  Use server-side authorization for every sensitive operation.
6.  Apply CSRF/session protections according to the existing application
    architecture.
7.  Protect file upload and download flows; enforce authorization and
    content restrictions server-side.
8.  Do not use invasive browser fingerprinting as a substitute for
    authentication.
9.  Device identifiers must be treated as revocable and potentially
    unreliable.
10. Provide privacy notices at the point of collection and collect only
    data necessary for the stated purpose.
11. Sensitive actions must require policy-defined
    re-authentication/step-up where available.
12. All staff review, approval, policy changes, and permission changes
    must be auditable.
13. Do not expose anti-fraud thresholds or detailed signals to end
    users.
14. Avoid retaining personal evidence longer than the approved retention
    policy.
15. Legal/privacy review is required before collecting date of birth,
    identity documents, biometrics, or other sensitive evidence.

## 13. Error handling and edge cases

The implementation must handle at least:

-   Browser does not support passkeys.
-   User cancels passkey registration/authentication.
-   Passkey operation times out.
-   Passkey credential already exists.
-   Device metadata is incomplete or unavailable.
-   Device ID changes after browser reset, app reinstall, or device
    reset.
-   User loses all registered devices and recovery codes.
-   Recovery request already exists.
-   Recovery request expires or is cancelled.
-   User submits a duplicate dispute for the same transaction.
-   Transaction source service is unavailable.
-   Ledger transaction is reversed/changed while a dispute is open.
-   File upload fails or file type/size is rejected.
-   Staff lacks permission or assigned scope.
-   Maker-checker approver is the same person as proposer.
-   Approval succeeds but downstream ledger adjustment is pending/fails.
-   Network interruption occurs during submit/approve/revoke.
-   User refreshes or closes the browser during a wizard.
-   Session expires while a sensitive action is in progress.

For retryable operations, use idempotency support where available. Never
display success before the backend confirms the operation.

## 14. Acceptance criteria

### 14.1 User security setup

-   [ ] User can reach the security center from the existing account
    area.
-   [ ] User can defer optional setup without being trapped in
    onboarding.
-   [ ] User can see whether passkey registration is supported.
-   [ ] Passkey registration reflects actual backend success/failure.
-   [ ] Recovery codes are shown only at authorized generation time.
-   [ ] Regeneration warns that prior codes will be invalidated.
-   [ ] Device registry distinguishes active/revoked/unknown states
    using backend data.
-   [ ] Device revocation requires explicit confirmation.
-   [ ] UI never states that Device ID alone proves identity.

### 14.2 Recovery

-   [ ] User can complete the recovery wizard on mobile.
-   [ ] User sees only recovery methods actually available to their
    account.
-   [ ] User can submit a recovery request when all credentials are
    lost, subject to policy.
-   [ ] Duplicate active requests are handled safely.
-   [ ] User can track authoritative status and required actions.
-   [ ] High-risk/manual review states are represented without
    frontend-invented decisions.
-   [ ] A new credential is registered only after the backend-approved
    recovery process.

### 14.3 Disputes

-   [ ] User can select a transaction from the authoritative service.
-   [ ] Transaction details are read-only in the dispute form.
-   [ ] User can submit a reason and description.
-   [ ] User can track status and user-visible timeline.
-   [ ] Staff can investigate without editing ledger balances in the UI.
-   [ ] A case is not marked resolved before required ledger
    confirmation.

### 14.4 Staff/admin

-   [ ] Queues are permission-scoped and server-paginated.
-   [ ] Sensitive values are masked and secrets are never exposed.
-   [ ] Staff actions are checked by backend authorization.
-   [ ] Maker-checker is enforced where configured.
-   [ ] Audit explorer is read-only.
-   [ ] Policy/role changes are permission-controlled and audited.

### 14.5 Quality

-   [ ] All screens have loading, empty, error, and permission-denied
    states as applicable.
-   [ ] Responsive layouts work on mobile and desktop.
-   [ ] Keyboard and screen-reader basics are supported.
-   [ ] No production screen relies on fabricated sample data.
-   [ ] No sensitive value appears in logs, URLs, or browser storage.

## 15. Playwright / E2E test scenarios

Minimum automated scenarios:

1.  User opens security center with no optional security methods
    configured.
2.  User skips onboarding and later returns to complete setup.
3.  Passkey registration success.
4.  Passkey unsupported/cancelled/timeout behavior.
5.  Recovery-code generation and one-time display behavior.
6.  Recovery-code regeneration invalidates previous codes.
7.  User views registered devices.
8.  User revokes a device after confirmation.
9.  User starts recovery using an available credential.
10. User starts recovery after losing all credentials.
11. User sees duplicate active recovery request handling.
12. User views recovery status and provides requested information.
13. User submits a loyalty-point dispute linked to a real test
    transaction.
14. User cannot edit transaction values in the dispute form.
15. User sees dispute status changes.
16. Staff without permission cannot access recovery review.
17. Authorized reviewer requests additional information.
18. Maker-checker prevents self-approval.
19. Investigator proposes dispute outcome.
20. Ledger adjustment pending/failure does not show as completed.
21. Auditor can search audit events but cannot edit/delete them.
22. Unauthorized staff cannot change security policy or roles.
23. Network failure during submit does not create duplicate requests on
    retry.
24. Sensitive values do not appear in browser console, URL, or local
    storage.

Use existing Playwright fixtures, test conventions, and environment
setup. Do not create a parallel test harness unless the current project
has none.

## 16. Implementation phases

### Phase 0 --- Codebase discovery and gap analysis

Before coding: - Inspect current account/profile/navigation patterns. -
Inspect authentication and session architecture. - Identify existing
WebAuthn/passkey support, if any. - Identify device registry and audit
capabilities. - Identify recovery/dispute APIs and source ledger APIs. -
Identify existing RBAC and staff portal. - Map current components and
styles to this SRS. - Produce a gap report: existing / reuse / modify /
missing / blocked by backend / out of scope.

**Gate:** Do not build UI flows that have no real backend contract
unless explicitly marked as a prototype.

### Phase 1 --- User security foundation

Implement: - U01 security onboarding - U02 profile/evidence view - U03
passkey setup (if backend ready) - U05 recovery codes (if backend
ready) - U06 security center - U07 device registry - U08 device detail

U04 is conditional on explicit security approval.

### Phase 2 --- Account recovery

Implement: - U09 recovery wizard - U10 recovery status - A14 operations
dashboard (minimum viable) - A15 recovery queue - A16 recovery review

### Phase 3 --- Dispute and governance

Implement: - U11 dispute center - U12 dispute submission - U13 dispute
detail - A17 dispute queue - A18 investigation - A19 decision and
approval - A20 audit explorer - A21 security policy - A22 roles and
permissions

Do not implement policy editing or role management until backend
authorization, audit, and approval behavior are verified.

## 17. Devin execution instructions

Devin must follow this sequence:

1.  Read this SRS and inspect the existing repository before changing
    code.
2.  Identify the actual frontend entry points, routes, layout, component
    library, API clients, auth state, RBAC, and test setup.
3.  Produce a screen-to-codebase mapping and gap analysis.
4.  Identify backend dependencies and API contracts that are missing. Do
    not invent endpoint names or silently stub security-sensitive
    operations.
5.  Implement one phase at a time.
6.  Preserve existing UX patterns and avoid unnecessary new
    screens/routes.
7.  Keep business rules and authorization on the backend.
8.  Add loading/empty/error/permission states for every implemented
    screen.
9.  Add unit/component tests and Playwright E2E tests using the current
    conventions.
10. Run build, lint, tests, and relevant E2E suites.
11. Report changed files, completed requirements, test evidence,
    unresolved backend dependencies, security gaps, and deferred items.

### 17.1 Required gap-analysis table

Devin must return this table before implementation:

  ---------------------------------------------------------------------------------
  Requirement /  Existing              Action         Backend        Risk /
  screen         implementation                       dependency     question
  -------------- --------------------- -------------- -------------- --------------
  Example: U06   Route/component/API   Reuse / Modify API or service Gap
  Security       status                / New /                       
  center                               Blocked                       

  ---------------------------------------------------------------------------------

### 17.2 Prohibited shortcuts

-   Do not create fake passkey success.
-   Do not use a random browser fingerprint as a trusted identity
    credential.
-   Do not store secrets in local storage/session storage.
-   Do not make approval decisions in frontend code.
-   Do not let staff UI directly mutate wallet/loyalty balances.
-   Do not bypass RBAC by hiding controls only.
-   Do not create placeholder endpoints and claim integration is
    complete.
-   Do not use hardcoded production-like data.
-   Do not refactor unrelated modules without necessity and explicit
    scope.

## 18. Open decisions requiring product/security confirmation

> **Cập nhật 2026-11-15 — đã chốt theo `docs/AI/plans/atrs-decision-support.md`:** Q1 passkey = chưa có backend, xây mới (Fido2NetLib — D6) · Q2 canonical identity = `TrustSubjectId` (ADR-001) · Q3 device registry = có (`DeviceRegistration`), extend · Q4 recovery codes = xây mới (ATRS-4) · Q5 secret question = **OMIT (D7)** · Q6 recovery policy khi mất hết = tiered proof + manual review (đã chốt) · Q7 retention = case 5y / events 2y (chờ legal finalize) · Q8 ledger authority = `ILoyaltyPointLedgerService`/`AllianceWallet` (Gateway PG) · Q9 adjustment API = `AdjustPointsAsync`/`RefundAsync` + maker-checker (D4/D9) · Q10 staff roles = `AtrsPermissions` 8 nhóm (D5) · Q11 staff portal = ShopERP admin · Q12 SLA/expiry = config, A21 policy screen.

Devin must surface these as blockers or decision items rather than
assume answers:

1.  Is passkey registration/authentication already supported by the
    current identity backend?
2.  What is the authoritative account identifier and cross-app identity
    mechanism?
3.  Does a device registry already exist, and what device metadata is
    legitimately available?
4.  Are recovery codes supported by the backend, including one-time use
    and revocation?
5.  Will supplementary secret questions be retained? Security
    recommendation: do not use as a primary recovery factor; preferably
    omit unless a reviewed use case exists.
6.  What evidence and review policy is legally and operationally
    approved for recovery when all credentials are lost?
7.  What are the retention periods for recovery evidence, disputes, and
    audit events?
8.  Which service is authoritative for loyalty points and wallet
    balances?
9.  What adjustment API and confirmation semantics exist for ledger
    corrections?
10. Which staff roles and maker-checker thresholds are approved?
11. Which staff portal and tenant/organizational scopes should these
    screens use?
12. What SLA, expiry, and escalation rules apply to recovery and dispute
    cases?

## 19. Definition of Done

A phase is complete only when: - Its screens are integrated into the
existing application navigation. - Real API contracts are used for all
security-sensitive actions. - Authorization is enforced by backend
services. - Responsive and accessibility requirements are met. -
Loading, empty, error, and permission states are implemented. -
Automated tests pass. - No prohibited shortcuts or secret exposure are
found. - Devin provides a completion report with evidence and unresolved
risks.

------------------------------------------------------------------------

**End of document --- VAN AN Account Trust & Recovery Service UI/UX SRS
v1.0**
