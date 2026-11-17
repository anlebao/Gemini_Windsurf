# Task ATRS-9: Dispute Workflow + Ledger Integration + Maker-Checker (FR-012, FR-013, FR-014)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-2, ATRS-7, ATRS-10 (RBAC — maker-checker enforcement) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-012, FR-013, FR-014, FR-015(part), U11-U13 backend, A17-A19 backend, F05/F07 backend, AC-007
> **Gap nguồn:** `LoyaltyReconcileCase` chỉ cover system-error; chưa có user dispute, transaction history API, ledger read API cho investigator.

## 1. OBJECTIVE

DisputeCase đầy đủ: user khiếu nại điểm/ví/giao dịch → evidence → staff investigate → decision → maker-checker → adjustment qua ledger chính thức (KHÔNG sửa số dư trực tiếp).

## 2. THIẾT KẾ

### 2.1 Entities (§7.7)
- `DisputeCases`: `Id, TrustSubjectId, CaseType, RelatedTransactionIds(JSON), RelatedLedgerReferences(JSON), ClaimedIssue, Status(OPEN/TRIAGE/WAITING_FOR_USER/UNDER_REVIEW/WAITING_FOR_EXTERNAL_EVIDENCE/DECIDED/RESOLVED/REJECTED/CLOSED), AssignedReviewer?, SubmittedAt, ClosedAt`.
- `DisputeEvidenceItems`: `Id, DisputeCaseId, EvidenceType, SourceSystem, SourceReference, CapturedAt, RecordedAt, IntegrityHash?, SubmittedBy, ReliabilityClass, Description, AccessClassification`.
- `DisputeDecisions`: `Id, DisputeCaseId, Decision, DecisionReason, PolicyVersion, DecidedBy, ApprovedBy?, DecidedAt, ResolutionReference` — tách decision khỏi case (append-only decisions).
- Case types (FR-012): account-takeover, lost device, điểm thiếu/thừa/sai, ví/quyền lợi sai, giao dịch phủ nhận, điều chỉnh/hoàn điểm, tranh chấp quyền sở hữu.

### 2.2 Ledger integration (FR-013) — dùng primitive CÓ SẴN
- Read: cần **transaction history API mới** — `ILoyaltyPointLedgerService` hiện chỉ có balance/award-spend-refund; thêm `GetEntriesAsync(customerId, tenantId, range)` đọc `LoyaltyIssuanceRecord`/`AllianceTransaction` (PG source of truth — KHÔNG dùng SQLite mirror).
- Adjust: `IAllianceWalletService.AdjustPointsAsync` (signed, idempotent) + `RefundAsync`/`RevertOrderAsync` — gọi qua `InternalLoyaltyController` pattern (`X-Internal-Api-Key`) hoặc in-proc nếu cùng host.
- **Maker-checker:** proposal (investigator) → approval (approver ≠ proposer, D5 permissions) → execute adjust với `IdempotencyKey = "dispute:{caseId}:adjust:{n}"` → `ResolutionReference` = ledger tx id. Case chỉ `RESOLVED` sau khi ledger confirm (UI A19 rule).
- Wallet dispute: làm rõ scope = AllianceWallet/điểm (customer-facing); Metering wallet là billing nội bộ — loại khỏi customer dispute (gap analysis risk #4).

### 2.3 Evidence & attachments (D9)
- Upload → R2 object storage + signed URL; validate type/size server-side; `IntegrityHash` (SHA-256) khi lưu; `AccessClassification` per RBAC.
- User-facing dispute status timeline: chỉ trạng thái user-visible, không lộ fraud signals/internal notes.

### 2.4 APIs
- User: `POST /disputes`, `GET /disputes`, `GET /disputes/{id}`, `POST /disputes/{id}/evidence`, `POST /disputes/{id}/withdraw` (nếu policy).
- Transaction selector: `GET /account-trust/transactions?customer=&tenant=` → list từ ledger service.
- Staff internal endpoints → ATRS-10 wires RBAC.

## 3. FILES (dự kiến)

- Domain entities + configurations + migrations
- `DisputeCaseManager` service, controllers (user + internal)
- `ILoyaltyPointLedgerService` += read entries method (CoreHub — Modify, cần duyệt)
- Metering? KHÔNG — metering wallet out of scope customer dispute

## 4. TESTS

- [ ] Case lifecycle mọi transition; decision append-only (nhiều decision rows, không sửa)
- [ ] Maker-checker: proposer == approver → reject; threshold config
- [ ] Adjust chỉ execute sau approval; idempotent (retry không double-adjust)
- [ ] Case không RESOLVED khi ledger pending/fail
- [ ] Evidence: hash đúng, access classification enforce, user không thấy internal notes/fraud signals
- [ ] Duplicate dispute same transaction → detect/warn (UI §13)
- [ ] Ledger reversal trong lúc dispute mở → case chuyển WAITING_FOR_EXTERNAL_EVIDENCE/re-open path

## 5. ACCEPTANCE

- [ ] AC-007 pass; F05/F07 backend flows chạy end-to-end với ledger thật (test env)
- [ ] Audit mọi staff access dữ liệu nhạy cảm (FR-012/FR-014)

## 6. RISKS

- Ledger read API mới trên `ILoyaltyPointLedgerService` — đụng service đang stable (LoyaltyLedgerV2 flag) → additive only, không đổi write path.
- Cross-tenant dispute (điểm nhiều tenant) — scope theo TrustSubject links; "luật Silo" ảnh hưởng adjustment tenant nào → cần rule rõ trong card khi implement.
- Maker-checker bypass qua API trực tiếp → enforce ở service layer, không chỉ UI.

## 7. IMPLEMENTATION STATUS (Wave 3 — 2026-11-16/17) — ✅ DEPLOYED + RV PASS

**Status: CLOSED.** Code `ff6dc99c` + docs `d281e663` → CD `43916878678` SUCCESS (atrs+gateway+shoperp) → **prod RV `atrs-wave3-sweep.mjs` 48/48 PASS** (2026-11-17) + Playwright `atrs-disputes-admin.spec.ts` 5/5 (D6 skip — cần staff login thật) + migration applied prod (4 bảng dispute/staff_permission_grants).

Đã làm:
- Entities `DisputeCase`/`DisputeEvidenceItem`/`DisputeDecision` + enums + EF config + migration `20261116115723_DisputeCases_StaffGrants` (additive, snake_case).
- `DisputeCaseManager` (vanan-atrs Infra): submit idempotent + anti-duplicate theo transaction ref, evidence (user luôn USER_VISIBLE, IntegrityHash SHA-256-hex validate), withdraw, user-safe detail (không lộ AccessClassification/fraud/internal), staff queue/assign/transition, propose→approve maker-checker, adjust qua `ILoyaltyGatewayClient` → Gateway `points/adjust-customer`, `ResolutionReference` = AllianceTransaction id, case RESOLVED chỉ sau ledger confirm; ledger fail → decision FAILED + case về UNDER_REVIEW (re-propose được, key mới).
- `ILoyaltyPointLedgerService.GetEntriesAsync` (additive): union LoyaltyIssuanceRecord + AllianceTransaction + Silo history (SPEND only, tránh dup EARN); Gateway `GET /api/internal/loyalty/entries` + `POST /api/internal/loyalty/points/adjust-customer` (resolve `customer.DeviceId ?? customerId`); ShopERP proxy forward (fail-open rỗng).
- Staff APIs: `AdminDisputesController` (`/api/v1/account-trust/admin/disputes*`), `AdminRecoveryController`, `AdminPermissionsController`; customer `DisputesController` + `TransactionsController` (X-Customer-Token, anti-enumeration 404).
- Staff UI tối thiểu: `/admin/disputes` (A17 queue, filter+server paging, masked subject) + `/admin/disputes/{id}` (A18 evidence/decisions/ledger txs + A19 propose/approve/reject) qua `AtrsAdminApiClient` (JWT giữ role thật — SystemAdmin break-glass, còn lại check grant); NavMenu Owner + SystemAdmin.
- Tests: `DisputeCaseManagerTests` + `AtrsAuthorizationServiceTests` — 26 tests PASS (lifecycle, self-approval reject, idempotent adjust, ledger-fail→re-propose, evidence access class, permission matrix/scope/expiry).

Chưa làm (wave sau):
- Evidence attachments R2 signed-URL upload (D9) — hiện chỉ metadata + IntegrityHash; `AddDisputeEvidenceRequest` đã có chỗ cho file ref.
- `FlagExternalEvidencePending` chưa có trigger tự động khi phát hiện ledger reversal ngoài hệ thống (method sẵn, cần job/poll).
- Metering wallet vẫn out of scope (đúng card); transaction selector UI phía customer (KhachLink) chưa làm — chỉ backend `GET /transactions`.
- E2E authenticated staff flow cần `DISPUTE_STAFF_TOKEN` post-deploy RV.
