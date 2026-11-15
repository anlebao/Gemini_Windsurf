# Task ATRS-6: Personal Evidence Metadata + Consent + Secret Challenge (conditional)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-2 · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-006, FR-007 (CONDITIONAL — D7 đề xuất OMIT), U02, U04(cond), §11 privacy
> **Nguyên tắc cốt lõi:** Evidence = metadata/provenance/reference — KHÔNG copy PII đã có trong `Customer`/`DemoUser`.

## 1. OBJECTIVE

Lớp "personal evidence" cho recovery/dispute: user khai báo thông tin hỗ trợ xác minh, có nguồn gốc + trạng thái verify + privacy notice — tái dùng `VerifiedChannel`/`IdentityLevel`/`PhoneVerifiedAt`/`Birthday` hiện có thay vì tạo kho PII thứ hai.

## 2. THIẾT KẾ

### 2.1 `PersonalEvidenceProfiles` (§7.4 — MODIFY theo gap G-ID-1)
- `Id, TrustSubjectId, FieldKey (FULL_NAME/DOB/CONTACT_PHONE/RECOVERY_HINT...), ValueRef (pointer tới field gốc hoặc encrypted value nếu thật sự cần store riêng), Source(USER_DECLARED/VERIFIED_PROVIDER/STAFF_REVIEWED/MIGRATED), VerificationStatus(UNVERIFIED/VERIFIED/REJECTED/EXPIRED), CollectedAt, UpdatedAt, PrivacyNoticeVersion, ConsentRecordId`.
- Map rõ: `CONTACT_PHONE.verified` → đọc từ `Customer.PhoneNumber`+verify state/`DemoUser.VerifiedChannel` — **không duplicate giá trị**.
- DOB: chỉ thu thập nếu PO chốt cần (SRS §18 Q1 — đề xuất omit v1.0 vì `Customer.Birthday` đã có cho loyalty mission).

### 2.2 Consent (nếu chưa nằm trong ATRS-2)
- `ConsentRecords` đã spec ở ATRS-2 §2.4 — card này wire vào evidence writes + privacy notice version.

### 2.3 Secret Challenge (FR-007) — CHỈ KHI PO GIỮ U04
- Nếu D7 = OMIT: card này chỉ làm evidence + consent; đánh dấu FR-007/U04 = out-of-scope v1.0 trong SRS đã patch (ATRS-1).
- Nếu giữ: `SecretChallenges` entity (QuestionCode, AnswerHash Argon2id+salt, HintEncrypted, FailedAttempts, LockedUntil), rate limit/lockout, step-up để đổi, KHÔNG BAO GIỜ là factor duy nhất (FR-007 rules), UI U04 "supplementary only".

### 2.4 API
- `GET/PUT /account-trust/personal-evidence` — user xem/sửa field được phép; audit mọi thay đổi field nhạy cảm (AccountSecurityEvents).
- Masking: response mask giá trị nhạy cảm (vd phone `+84***123`).

## 3. FILES (dự kiến)

- Domain entities + service `IPersonalEvidenceService`
- Map/adapter đọc Customer/DemoUser fields (per-tenant → aggregate về TrustSubject)
- Controller + DTOs

## 4. TESTS

- [ ] User chỉ đọc/sửa evidence của mình (IDOR test)
- [ ] Self-declared KHÔNG BAO GIỜ set VerificationStatus=VERIFIED
- [ ] Field map đúng nguồn (phone → VerifiedChannel provenance)
- [ ] Consent withdraw → evidence purpose ngừng xử lý (theo policy)
- [ ] Nếu SecretChallenge implement: Argon2id hash, không log answer, lockout, step-up đổi

## 5. ACCEPTANCE

- [ ] U02 data contract sẵn sàng; không duplicate PII (verify bằng code review + test)
- [ ] Privacy notice version ghi được per-field

## 6. RISKS

- Per-tenant Customer → cùng 1 người nhiều row: evidence "của ai" phải resolve qua TrustSubject links (D1).
- Legal: DOB/identity docs cần legal review trước collect (§11.15 UI SRS).
