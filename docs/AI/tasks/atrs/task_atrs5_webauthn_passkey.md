# Task ATRS-5: WebAuthn / Passkey + DeviceCredential (FR-004, FR-005)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-3 (step-up), ATRS-4 (recovery codes — UX pair) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-004, FR-005, U03, §7.3, AC-003, §15 security tests (replay/origin/RP ID)
> **Gap nguồn:** WebAuthn = 0 trong toàn repo (grep verified). Library: `Fido2NetLib` (D6 — verify stable ≥7 ngày).

## 1. OBJECTIVE

Passkey/WebAuthn thật cho customer (KhachLink PWA) — registration + authentication ceremony, credential storage, revocation — làm recovery factor chính thay OTP.

## 2. THIẾT KẾ

### 2.1 Schema `DeviceCredentials` (§7.3)
`Id, TrustSubjectId, DeviceRecordId(FK), CredentialId (unique, base64url chuẩn), PublicKey, SignCount, Aaguid?, Transports(JSON), BackupEligible?, BackupState?, Status(ACTIVE/REVOKED), CreatedAt, LastUsedAt`.
- **KHÔNG có PrivateKey field** — enforce bằng review + test.
- `builder.Ignore` business VOs theo Single-Identity Pattern.

### 2.2 WebAuthn service `IWebAuthnService`
- `BeginRegistration(subjectId, deviceId)` → `PublicKeyCredentialCreationOptions` (challenge random CSPRNG, one-time, TTL ngắn ~5', store server-side chờ verify — bảng `WebAuthnChallenges` hoặc cache+DB).
- `FinishRegistration(...)` → verify attestation bằng Fido2NetLib `Fido2` class; origin/RP ID check; lưu credential.
- `BeginAuthentication` / `FinishAuthentication` → verify assertion signature + signCount (counter không tin cậy → không dùng làm tiêu chí duy nhất, FR-004).
- Challenge binding: session + origin + RP ID + purpose; replay → reject.
- Nhiều credential/account; revoke per-credential và per-device.

### 2.3 RP ID / Origin config
- RP ID = domain gốc phù hợp (vd `timlathay.com` cho multi-subdomain — quyết định trong card; passkey share cross-subdomain nếu RP ID = apex).
- Origin allowlist = các KhachLink domains (vanan/commienphi/diemthuong2/hoptacxa.timlathay.com).

### 2.4 FR-005 fallback
- `RegistrationMethod = INSTALLATION_ID`: App Installation ID random lưu storage + signed cookie (HMAC) — chỉ làm device signal, KHÔNG phải credential.
- UI wording: passkey có thể đồng bộ (synced) — không khẳng định gắn 1 thiết bị vật lý.

## 3. FILES (dự kiến)

- `vanan-atrs` service: `WebAuthnService`, `DeviceCredential` entity, challenges store
- `1_Shared` contracts/DTOs cho client
- KhachLink WASM: WebAuthn JS (`navigator.credentials.create/get`) — client gọi API qua Gateway
- Tests: unit (challenge lifecycle, verify mocks) + integration (Fido2NetLib test vectors nếu có) + Playwright (UV platform authenticator — virtual authenticator CDP)

## 4. TESTS

- [ ] Registration: challenge one-time, hết hạn reject, origin/RP sai reject
- [ ] Assertion: signature sai reject, replay challenge reject, revoked credential reject
- [ ] Nhiều credential cùng account OK; revoke 1 không ảnh hưởng cái khác
- [ ] Không private key trong DB/log/API response (grep + test)
- [ ] BackupEligible/BackupState lưu đúng metadata
- [ ] Playwright: virtual authenticator create + get flow trên Chromium

## 5. ACCEPTANCE

- [ ] Passkey login/step-up hoạt động trên ≥1 platform thật (Chrome/Android, Safari/iOS nếu feasible)
- [ ] AC-003 toàn bộ pass
- [ ] Security tests §15.3 (replay, origin mismatch, RP mismatch) pass

## 6. RISKS

- iOS Safari WebAuthn quirks (SRS voice có note) — cần RV thật trên device.
- Fido2NetLib attestation formats — dùng `none` attestation policy cho consumer (ghi ADR).
- Sync passkey (iCloud/Google Password Manager) → credential tồn tại trên nhiều thiết bị — UI phải nói đúng (FR-004).
