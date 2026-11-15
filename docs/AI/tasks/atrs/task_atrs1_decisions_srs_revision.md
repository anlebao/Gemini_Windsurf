# Task ATRS-1: Chốt Open Decisions + SRS Revision + ADR

> **Master plan:** `docs/AI/plans/account-trust-recovery-master-plan.md` §3
> **Mode:** ANALYZE → IMPLEMENT (docs only — KHÔNG code)
> **Status:** 🟡 DRAFT — chờ user/PO duyệt D1-D10

## 1. OBJECTIVE

Chốt 10 open decisions (master plan §3), viết ADR, và sửa 2 SRS cho khớp codebase thực tế trước khi bất kỳ schema/code nào được viết.

## 2. DECISIONS CẦN CHỐT (đề xuất mặc định — user chỉ cần confirm hoặc override)

| # | Quyết định | Đề xuất | SRS §18/§18-UI mapping |
|---|---|---|---|
| D1 | Canonical identity: `TrustSubjectId` (UUID mới) map 1-nhiều `Customer`(per-tenant)/`DemoUser`/DeviceId-cluster qua verified contact hash | ✅ đề xuất | UI §18 Q1-Q2 |
| D2 | Deployment: service riêng `vanan-atrs` pattern `10_Voice` (sln riêng, Dockerfile, port mới ~5080, PG riêng) + contracts trong `1_Shared` | ✅ đề xuất | — |
| D3 | Token revocation: `tokenVersion` claim + check cache; customer token thêm version | ✅ đề xuất | — |
| D4 | `DisputeCases` bảng mới tổng quát; `LoyaltyReconcileCase` giữ (map thành dispute category system-discrepancy) | ✅ đề xuất | — |
| D5 | Staff RBAC: permission table `AtrsPermissions` mới (8 nhóm UI §4.2) | ✅ đề xuất | UI §4.2, §18 Q10-11 |
| D6 | WebAuthn lib: `Fido2NetLib` stable ≥7 ngày | ✅ đề xuất | UI §18 Q1 |
| D7 | **U04 secret question: OMIT** khỏi scope v1.0 | ✅ đề xuất (cả 2 SRS đều khuyến nghị omit) | SRS §18 Q2, FR-007, UI U04 |
| D8 | `AccountSecurityEvents` bảng riêng (không extend AuditLog) | ✅ đề xuất | FR-009 |
| D9 | Attachment: R2 object storage + signed URL + content-type/size validation | ✅ đề xuất | U12 rules |
| D10 | Scope v1.0: chỉ customer recovery; staff recovery = manual runbook + MFA-1 | ✅ đề xuất | §2.3, §13 |

**Câu hỏi PO bổ sung từ SRS §18 (cần trả lời khi review card này):**
1. Ngày sinh bắt buộc hay tùy chọn? → đề xuất: tùy chọn/omit v1.0.
2. Secret question → D7 (omit).
3. Số thiết bị tối đa → **đã có: 3** (`DeviceRegistrationService.MaxActiveDevicesPerCustomer`).
4. Recovery delay cho tài khoản có điểm/ví → đề xuất config, default 24h hạn chế giao dịch nhạy cảm.
5. Ngưỡng maker-checker → đề xuất config `Atrs:MakerCheckerThresholdPoints` (default 0 = mọi adjust cần 2 người? hay 500?).
6. Nhóm nhân viên dispute → map 8 permission groups UI §4.2.
7. Retention recovery/dispute/audit → đề xuất: case+evidence 5 năm (kế toán), security events 2 năm, personal evidence theo mục đích.
8. Kênh thông báo đã đăng ký → hiện có: email (nếu Customer.Email), in-app `TenantNotification`; SMS/ZNS phụ thuộc P4 — **verify thực tế trước ATRS-13**.
9. Apps tích hợp đợt đầu → đề xuất: KhachLink (customer) + ShopERP admin (staff). TimLaThay/Booking/Loyalty đọc trust state sau.
10. Session/token authority → JWT (staff) + customer token (ShopERP `/me`) — không đổi, ATRS thêm revocation layer.

## 3. CHANGES

| File | Change |
|---|---|
| `docs/AI/plans/account-trust-recovery-master-plan.md` | Cập nhật Status → APPROVED sau khi chốt; điền quyết định vào §3 |
| `docs/requirements/VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` | Sửa: §4.1 deployment (service riêng, không "đến CoreHub"), FR-001 canonical identity = TrustSubjectId, §7 PersonalEvidence → metadata/reference (không copy PII), thêm FR token-revocation, FR tenancy model, status mapping table, U04 → omit note |
| `docs/requirements/VAN_AN_Account_Trust_UI_UX_SRS_v1.0 (1).md` | Rename file bỏ "(1)"; align status vocabulary với backend enums; thêm feature-flag gating (KhachLinkProfile/ShopFeatureSettings); map trust subject |
| `docs/AI/plans/atrs-adr-001-deployment-and-identity.md` (mới) | ADR: deployment Option B + TrustSubjectId model + tenancy |
| `docs/AI/project_state.md` | Thêm objective ATRS |

## 4. ACCEPTANCE CRITERIA

- [ ] User xác nhận D1-D10 + 10 câu hỏi §2
- [ ] ADR viết xong, 2 SRS đã patch (không mâu thuẫn codebase)
- [ ] Status mapping table duy nhất: backend enums ↔ UI labels (Recovery: SUBMITTED/AUTOMATED_CHECK/PENDING_USER_ACTION/PENDING_REVIEW/APPROVED/REJECTED/CANCELLED/EXPIRED/COMPLETED; Dispute: OPEN/TRIAGE/WAITING_FOR_USER/UNDER_REVIEW/WAITING_FOR_EXTERNAL_EVIDENCE/DECIDED/RESOLVED/REJECTED/CLOSED)
- [ ] AssuranceState `BASIC/PROTECTED/RECOVERY_READY` ↔ UI "Chưa thiết lập/Cơ bản/Đã tăng cường" mapped

## 5. RISKS

- Nếu user không trả lời → card block, không được tự quyết (theo SRS §17.1 rule 7).
