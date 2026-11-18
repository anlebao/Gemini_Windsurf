# Task ATRS-19: Old-Device Step-Up Approval trong Recovery Flow (FR-011)

> **SRS:** `docs/requirements/VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` — FR-011 ("ưu tiên approve từ thiết bị cũ step-up") + tiered proof §6.4
> **Parent:** `task_atrs8_recovery_workflow.md` · liên quan `atrs-decision-support.md` (tiered proof: ① recovery code → ② verified-contact OTP → ③ manual R3)
> **Traceability gap:** `atrs_srs_traceability_review_2026-11-17.md` gap #3 (② approve-ưu-tiên-từ-thiết-bị-cũ)
> **Depends on:** ATRS-5 WebAuthn/passkey (`IWebAuthnService` begin/finish **authentication** đã có), ATRS-3 `IStepUpService` (JWT 10' `amr=passkey`), ATRS-7 RiskPolicyEngine, W4 customer recovery UI (U09/U10)
> **Mode:** IMPLEMENT (khi duyệt) · **Layer:** 11_Atrs + 5_WebApps/KhachLink

## 1. OBJECTIVE

Thêm nhánh proof mạnh nhất vào recovery: nếu subject còn **thiết bị cũ tin cậy** (passkey ACTIVE), user có thể xác nhận recovery **ngay từ thiết bị đó** (WebAuthn authentication ceremony) → tính là step-up proof → ưu tiên auto-approve / hạ risk tier so với manual review. Đây là "proof of possession" mạnh hơn recovery code lẫn contact OTP.

## 2. VERIFIED FACTS

- `IWebAuthnService` đã có cả `BeginAuthenticationAsync`/`FinishAuthenticationAsync` (ATRS-5) — phát hành step-up token `amr=passkey` khi assert thành công (`WebAuthnController.cs:76-91`).
- `RecoveryRequest` state machine đã có `PENDING_USER_ACTION` — đúng chỗ "chờ user hành động" (chờ device challenge / chờ thêm info).
- `RecoveryCaseManager.VerifyCodePathAsync` là pattern sẵn cho "user cung cấp proof → risk engine → quyết định" — device-challenge là proof path thứ 2 song song.
- Customer devices list đã có API (`/devices`, U07) → UI biết có/không passkey cũ.
- Tiered proof hiện tại: ① recovery code (`VerifyCodePathAsync`) · ② contact OTP/manual review · **thiếu nhánh device-possession**.

## 3. DESIGN (đề xuất — chốt khi implement)

### 3.1 Flow mới

```
Submit recovery (SUBMITTED → AUTOMATED_CHECK)
  → risk engine đánh giá
  → nếu subject có ≥1 passkey ACTIVE VÀ policy `recovery.device_stepup.enabled`:
      status = PENDING_USER_ACTION + challenge pending
      user (trên thiết bị cũ HOẶC bất kỳ device nào — xem Q1) mở recovery status
      → POST /recovery/{id}/device-challenge/begin → WebAuthn authentication options
      → navigator.credentials.get trên thiết bị cũ → POST .../finish
      → FinishAuthenticationAsync OK → issue step-up token amr=passkey gắn requestId
      → POST /recovery/{id}/device-challenge/verify (hoặc finish trả luôn)
      → risk engine re-evaluate với tín hiệu device-possession → APPROVED | PENDING_REVIEW
```

### 3.2 Quyết định thiết kế cần chốt (open questions)

1. **Challenge trên thiết bị nào?** WebAuthn authentication phải chạy trên thiết bị CÓ private key = thiết bị cũ. User đang ở thiết bị mới/mất máy → không assert được → flow này chỉ hữu ích khi user còn giữ thiết bị cũ nhưng mất credential khác (passkey vẫn trong máy?). Thực tế phổ biến: user có passkey trên điện thoại cũ vẫn còn → đăng nhập web trên máy tính → challenge QR/deep-link sang điện thoại? **v1 đề xuất đơn giản:** challenge chạy trên bất kỳ browser nào, user tự chuyển sang thiết bị có passkey (PWA cùng origin → platform authenticator). Ghi rõ UX trong card khi ANALYZE.
2. **Tier trong proof chain:** device-step-up xếp ngang recovery code (cùng "proof mạnh") hay cao hơn? Đề xuất: **cao hơn** — possession + biometric/PIN của chính thiết bị tin cậy.
3. **Risk signal mới:** `RiskContext` += `DevicePossessionVerified` → rule mới (vd: device-possession + không high-value → ALLOW; + high-value → ALLOW_WITH_STEP_UP thay vì REQUIRE_MANUAL_REVIEW).
4. **Auto-approve hay chỉ giảm tier?** Đề xuất: device-possession verified → skip manual review khi risk ≤ R2; R3/R4 vẫn manual (giữ maker-checker cho takeover signals).
5. **Anti-replay:** WebAuthn challenge one-time TTL đã có sẵn trong `WebAuthnChallenge` — reuse, không phát minh lại.

### 3.3 atrs changes

- `RecoveryRequest` += `DeviceChallengeState` (hoặc tái dùng `PENDING_USER_ACTION` + field `PendingChallengeJson`) — migration additive.
- `IRecoveryCaseManager` += `BeginDeviceChallengeAsync(requestId, subject)` + `FinishDeviceChallengeAsync(requestId, assertion)` — gọi `IWebAuthnService` authentication path (KHÔNG registration).
- Risk re-evaluation sau verify: set flag rồi chạy lại `Evaluate` với `RiskContext` mở rộng.
- Notification: `recovery.device_challenge` in-app (ATRS-13 writer sẵn) khi vào PENDING_USER_ACTION.
- Audit: `AccountSecurityEvent` `recovery.device_stepup.success|fail`.

### 3.4 KhachLink UI (U09/U10 mở rộng)

- Recovery detail khi `PENDING_USER_ACTION` + có passkey: nút **"Xác nhận từ thiết bị tin cậy"** → gọi `webauthn.js` `navigator.credentials.get` (đã có sẵn từ U03!) → submit assertion.
- Copy hướng dẫn: "Mở trang này trên thiết bị đã đăng ký passkey".
- Fallback text: nếu không có thiết bị cũ → đường recovery code/contact OTP/manual như hiện tại.

## 4. FILES (dự kiến)

| File | Change |
|---|---|
| `RecoveryRequest.cs` | +`DeviceChallengeJson`/state fields (Domain — cần duyệt Gate 5? entity additive field, theo domain rules cần approval) |
| `RiskPolicyEngine.cs` | `RiskContext` += `DevicePossessionVerified`; rule mới |
| `RecoveryCaseManager.cs` | begin/finish device challenge + re-evaluate |
| `RecoveryRequestsController.cs` | +2 endpoints (customer, subject-scoped) |
| atrs EF migration | additive column(s) |
| `SecurityPolicy` defaults | `recovery.device_stepup.*` keys |
| KhachLink recovery page + `webauthn.js` reuse | nút + flow |
| Tests | challenge begin/finish · wrong-device assertion fail · replay reject · risk re-eval · state transitions |

## 5. ACCEPTANCE CRITERIA

- [ ] Recovery vào `PENDING_USER_ACTION` khi subject có passkey ACTIVE + policy bật
- [ ] WebAuthn authentication thành công trên thiết bị cũ → proof ghi nhận → risk re-eval → quyết định đúng (auto-approve ≤R2, manual ≥R3)
- [ ] Assertion sai/thiết bị không đăng ký → fail + audit + vẫn fallback code/OTP path
- [ ] Challenge replay/expired → reject (reuse `WebAuthnChallenge` TTL)
- [ ] Subject không có passkey → flow y hệt hiện tại (không regression)
- [ ] Notification in-app `recovery.device_challenge` + audit events đầy đủ
- [ ] Tests + build + guard PASS

## 6. RISKS / OPEN QUESTIONS

1. **UX cross-device** — WebAuthn get() phải chạy trên máy có authenticator; mobile browser khác máy tính không share được (trừ ca hybrid transport QR — Fido2 lib có support nhưng phức tạp). **Đề xuất v1:** hướng dẫn user mở link trên thiết bị cũ (cùng origin), chấp nhận friction; hybrid QR = phase 2.
2. **Device cũ đã REVOKED/lost** → không tính passkey (chỉ ACTIVE credentials).
3. **Guest subject** (ATRS-16, không passkey) → skip hoàn toàn.
4. Nhiều passkey → `allowCredentials` giới hạn credentials ACTIVE của subject (privacy: không leak credId ngoài subject — đã handle trong WebAuthnService).
5. Có nên yêu cầu **recovery case phải ở state cụ thể** (vd sau AUTOMATED_CHECK, trước manual review) — tránh user spam challenge khi case đã APPROVED.

## 7. VERIFICATION + RV

- Local: build + guard + atrs tests + KhachLink build.
- RV prod: customer test có passkey (đã có từ W2 probe `7ce9c1d1` hoặc tạo mới) → submit recovery → vào PENDING_USER_ACTION → device challenge trên thiết bị có passkey → verify → case auto-approve/review đúng risk → audit + notification. Negative: assertion sai → fail path.
- Playwright: spec recovery-device-challenge nếu UI flow đủ lớn (theo Gate 4 — UI change → E2E).

## 8. TRACEABILITY

| FR-011 clause | Cover |
|---|---|
| approve ưu tiên từ thiết bị cũ (step-up) | toàn bộ card |
| tiered proof §6.4 | chèn nhánh possession-proof vào chain |
| anti-enumeration | giữ nguyên — response generic như hiện tại |
| revoke token cũ | không đổi — vẫn wire ở CompleteAsync |

**Status:** REVIEW_ONLY — chờ user duyệt + trả lời §6 (đặc biệt Q1 cross-device UX scope).
**Naming:** ATRS-19. **Dependency note:** nên bundle implement cùng ATRS-18 (cùng `RecoveryCaseManager` + migration) thành 1 wave để giảm conflict — hoặc tuần tự 19 → 18 vì 19 thay đổi chỗ 18 hook vào.
**Ước lượng:** trung bình — phần lớn primitive (WebAuthn auth, step-up, state machine, notification) đã có; rủi ro chính là UX cross-device.
