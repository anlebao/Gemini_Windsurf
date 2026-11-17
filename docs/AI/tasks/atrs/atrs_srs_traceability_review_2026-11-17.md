# ATRS — SRS Requirement Traceability Review (2026-11-17, REVIEW_ONLY)

> Rà soát độc lập: implementation (W1–W6) vs `SRS_VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` (đã patch Wave 0) + UI/UX SRS.
> **Kết luận: KHÔNG phải 100%.** FR bắt buộc core ✅, nhưng có **4 GAP thật** + nhiều mục partial/exception đã chốt — chi tiết dưới.

## 1. FR Matrix (service SRS)

| FR | Yêu cầu cốt lõi | Trạng thái | Bằng chứng / Ghi chú |
|---|---|---|---|
| **FR-001** | TrustSubjectId canonical, map identity, ownership check, KHÔNG silo | ✅ phần lớn | `TrustSubject`+`TrustSubjectLink` (Customer/DemoUser/DeviceCluster); ownership mọi endpoint; anti-enum. **GAP ⚠️:** "account bị khóa/xóa → ATRS nhận event sync trạng thái" — chưa có subscriber cập nhật `AccountTrustProfile` từ khóa/xóa (UserSyncSubscriber chỉ sync trust level, không TrustProfile) |
| **FR-002** | Tạo profile PENDING_SETUP, hoàn tất từng phần, consent | ✅ | `TrustProfileService` lazy-create + `ConsentRecord` + privacy notice version |
| **FR-003** | Device registry: DeviceId ngẫu nhiên, states PENDING/ACTIVE/SUSPICIOUS/LOST/REVOKED, history, đặt tên | ✅ | `UserDevice` + `DeviceService`; history qua AccountSecurityEvents; rename/revoke/mark-lost |
| **FR-004** | WebAuthn passkey: challenge 1-lần/TTL/origin/RP-ID/purpose, verify attestation, không private key, nhiều credential, revoke, mô tả sync | ✅ | `WebAuthnService` (Fido2), `WebAuthnChallenge`, `DeviceCredential` (KHÔNG private key); challenge TTL 5' + origin/RP-ID binding; revoke cascade; prod ceremony CDP verified |
| **FR-005** | Installation ID fallback, cookie/storage signed, không quyền rủi ro cao từ installation | ✅ | `RegistrationMethod.INSTALLATION_ID`; device = risk signal, không proof; browser-clear → new device + verify |
| **FR-006** | Personal profile: fields + metadata (source/verificationStatus/collectedAt/privacyNoticeVersion), sửa + audit | ✅ partial | `PersonalEvidenceItem` (field/source/verificationStatus/privacyNoticeVersion) + API (W4) + audit mọi thay đổi; **UI xem/sửa trên KhachLink chưa có** (API-level; ghi residual) |
| **FR-007** | Secret questions (hash, pepper, lockout, thay đổi sau step-up) | ✅ N/A | **D7 OMIT (chốt Wave 0)** — thay bằng passkey/recovery code an toàn hơn; documented |
| **FR-008** | Recovery codes: hash-only, single-use, hiển thị 1 lần, thu hồi sau step-up, không qua log/email | ✅ | `RecoveryCodeService` (BCrypt normalized, single-use, regenerate revoke set, generate yêu cầu step-up); notification KHÔNG chứa mã |
| **FR-009** | Activity evidence: đủ event types + fields, không secret | ✅ | `AccountSecurityEvent` (append-only, fields đủ, sanitized payloads); event types phủ list |
| **FR-010** | Risk engine deterministic + reason code + policy version; signals | ✅ partial | `RiskPolicyEngine` R4>R3>R2>R1>R0 + reason codes + `atrs-risk-v1`; **chỉ 4/9 signal đang được caller truyền** (IsNewDevice/HighAccountValue…); "điều chỉnh điểm/ví, thay đổi thông tin bảo mật, đăng nhập bất thường" chưa wire thành signal → engine chưa nhìn thấy |
| **FR-011** | Recovery request: state machine 9 status, hủy, idempotency, **giới hạn rate**, **thông báo kênh đăng ký**, **ưu tiên approve từ thiết bị cũ step-up**, **delay giao dịch nhạy cảm**, **revoke token cũ** | ✅ partial | State machine ✅ + idempotency ✅ + token revoke (TokenVersion bump on COMPLETED) ✅ + notification in-app ✅. **GAP ⚠️:** ① giới hạn request/time-window chưa có (chỉ chống duplicate active); ② approve-ưu-tiên-từ-thiết-bị-cũ chưa có; ③ **delay/restrict giao dịch nhạy cảm sau recovery chưa implement** (hook đã comment "chưa wire" từ W1); ④ kênh email/SMS chưa (chờ P4) |
| **FR-012** | Dispute: nhóm case, evidence nguồn/integrity/reliability, audit xem, maker-checker, không tự phê duyệt, 2-người vượt ngưỡng | ✅ | `DisputeCaseManager` full; maker-checker; evidence integrity hash + access class; adjust qua ledger idempotent. **Export hồ sơ dispute chưa có** (residual nhỏ) |
| **FR-013** | Ledger = source of truth, không sửa số dư, adjust = giao dịch mới có duyệt, ledger không nhất quán → chờ đối soát | ✅ | `ILoyaltyGatewayClient.AdjustPointsAsync` qua Gateway internal; adjust fail → FAILED + UNDER_REVIEW; `GetEntriesAsync` đọc additive |
| **FR-014** | RBAC + scope tenant, tách quyền, front-line không xem secret, staff action có actor/reason/timestamp, **MFA/step-up staff**, maker-checker, xung đột lợi ích | ✅ partial | `AtrsPermissions`+`IAtrsAuthorizationService` (scope fail-closed) ✅; front-line không thấy secret ✅; actor/reason/audit ✅; maker-checker ✅. **GAP ⚠️:** **staff MFA/step-up cho tác vụ nhạy cảm chưa có** (residual từ W3, ghi card ATRS-10) |
| **FR-015** | User transparency: devices list/last-used/rename/lost/revoke, recovery/dispute status, notification thay đổi bảo mật, xem/sửa personal data, **rút lại consent** | ✅ partial | Devices + recovery mine/cancel + disputes list/detail + notifications (W5d) ✅; **consent withdraw ✅ (W4)**; personal data xem/sửa UI chưa (FR-006) |

## 2. NFR Matrix

| NFR | Nội dung | Trạng thái |
|---|---|---|
| NFR-001 Security | §15.3 suite | ✅ report W6 (12/12 traceability) |
| NFR-002 Privacy | retention, DPIA, consent withdraw | ⏳ retention chờ PO Q7; DPIA note residual; consent withdraw ✅ |
| NFR-003 Availability | ATRS down → đọc cơ bản không sập; sensitive fail-closed | ⚠️ tại cấu trúc: atrs riêng, gateway 502 khi down; fail-closed cho admin ✅; "đọc cơ bản không sập" chưa có fallback/verify |
| NFR-004 Performance | load p95 | ❌ **chưa chạy load test** (residual) |
| NFR-005 Scalability | multi-instance | ⚠️ single instance hiện tại; cache in-memory (policy 30s, token version) — cần shared cache khi scale |
| NFR-006 Observability | logs + metrics | ✅ `/metrics` + structured logs; alerts chưa cấu hình (residual) |

## 3. UI/UX SRS (U-series)

| U | Trạng thái |
|---|---|
| U01-U03 (security center, passkey, devices) | ✅ W2/W4 Playwright 3/3 + 11/11 |
| U04 (secret question) | ✅ OMIT (D7) |
| U05-U08 (recovery codes, recovery UI) | ✅ W4 (mine/cancel) + W5a staff |
| U09-U13 (dispute UI customer + evidence) | ✅ W4 sweep 34/34 + W5 staff |
| In-app notification inbox (FR-015/§10.4) | ❌ **UI chưa có** (API đã sẵn W5d) |

## 4. KẾT LUẬN

**Không đạt 100% SRS.** Ước lượng: **~85-90%** requirement bắt buộc có implementation + test + RV. Phần còn lại = **4 GAP thật** + 5 residual đã chốt/documented:

### GAP THẬT (cần làm hoặc PO quyết định)
1. **FR-011 delay/restrict giao dịch nhạy cảm sau recovery rủi ro cao** — chưa implement (hook còn comment).
2. **FR-014 staff MFA/step-up** cho tác vụ nhạy cảm — chưa có (liên quan MFA-1 TOTP identity card).
3. **FR-011 rate limit request/time-window** + **approve-ưu-tiên-từ-thiết-bị-cũ** — chưa có.
4. **FR-001 account lock/delete → ATRS sync** — chưa có subscriber.

### RESIDUAL đã chốt/documented (không phải gap ẩn)
- FR-007 OMIT (D7) · FR-006/FR-015 personal-data UI · notification inbox UI · email/SMS kênh (P4) · retention (PO Q7) · load p95 · NFR-003 fallback · alerts · export dispute · signal FR-010 wire thêm.

Đề xuất: GAP 1-4 đưa vào **Wave 7 backlog** (hoặc gộp ATRS-14 hardening) + PO sign-off cho các exception; đánh dấu rõ trong card nếu chấp nhận defer.