# Task ATRS-4: Recovery Codes (FR-008)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-2 · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-008, U05, §6.1 bước 8, §9.3, AC
> **Ghi chú:** Recovery codes KHÔNG cần WebAuthn — có thể ship trước passkey. Đây là recovery factor rẻ nhất.

## 1. OBJECTIVE

Bộ recovery codes dùng một lần cho customer (và staff nếu D10 mở rộng): generate → hiển thị 1 lần → lưu hash → verify single-use → revoke/regenerate sau step-up.

## 2. THIẾT KẾ

### 2.1 Domain/Schema
- `RecoveryCodeSet`: `Id, TrustSubjectId, CodesHash (JSON array hashes hoặc table riêng), GeneratedAt, RegeneratedAt, Status(ACTIVE/REVOKED/EXHAUSTED)`.
- `RecoveryCode` (nếu table riêng): `Id, SetId, CodeHash (Argon2id/BCrypt — CÙNG chuẩn password hashing), UsedAt, UsedFromDeviceId`.
- Format code: `XXXXX-XXXXX` từ `RandomNumberGenerator` (CSPRNG — KHÔNG `Random`), ~80-128 bits entropy.
- Lưu ý `OtpService` non-CSPRNG bug → ATRS TUYỆT ĐỐI dùng `RandomNumberGenerator`.

### 2.2 Service `IRecoveryCodeService`
- `GenerateSetAsync(subjectId)` → plaintext codes trả về ĐÚNG 1 lần (không log, không lưu plaintext).
- `VerifyAndConsumeAsync(subjectId, code)` → rate limit + lockout tăng dần; sai → generic error (không lộ gần đúng); đúng → mark used + trả `StepUpToken`/recovery-grant ngắn hạn.
- `RevokeSetAsync` / `RegenerateAsync` → yêu cầu step-up (ATRS-3).
- Mỗi code single-use; regenerate → invalidate set cũ.

### 2.3 API (theo convention `/api/v1/account-trust/...` — điều chỉnh theo D2)
- `POST /recovery-codes/generate` (step-up required)
- `POST /recovery-codes/verify` (public-ish, rate-limited — dùng trong recovery wizard)
- `POST /recovery-codes/regenerate` (step-up)
- `GET /recovery-codes/status` → {count remaining, generatedAt} — KHÔNG trả codes

### 2.4 Events
- Emit `AccountSecurityEvents`: `recovery_codes.generated|regenerated|used|revoked` (không log code/hash).

## 3. FILES (dự kiến)

- Domain entities (ATRS-2 schema hoặc migration mới trong card này)
- `vanan-atrs` service hoặc CoreHub service + controller
- ShopERP/KhachLink client sau (ATRS-11/12)

## 4. TESTS

- [ ] Generate → codes khác nhau, entropy đủ, hash verify được
- [ ] Verify đúng → consumed; verify lại → fail (single-use)
- [ ] Sai code → generic error, rate limit kick-in, lockout tăng dần
- [ ] Regenerate → set cũ vô hiệu
- [ ] Không code/hash/plaintext trong log/response sau generate
- [ ] Step-up required cho generate/regenerate

## 5. ACCEPTANCE

- [ ] UI contract sẵn sàng cho U05 (status + generate-once flow)
- [ ] Dùng được trong recovery wizard (ATRS-8/F04) như 1 recovery method
- [ ] Security tests: brute-force không khả thi (rate limit + lockout + CSPRNG)

## 6. RISKS

- Recovery code bị đánh cắp → vẫn chỉ là 1 factor; phải kết hợp step-up/risk (FR-008 note + §9.1 "Trung bình-cao").
- UX "lưu ngoại tuyến" — UI phải cảnh báo rõ (U05 rules).
