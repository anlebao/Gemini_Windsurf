# IMPLEMENT STRATEGY: Zero-Cost Identity — W0→W3 (approved 2026-11-10)

> **Plan:** `docs/AI/plans/van-an-identity-tiered-trust-master-plan.md` (§8 decisions) · **Cards:** ID-P1 `docs/AI/tasks/identity/task_idp1_demo_user_trust_fields.md` · MFA-1 `docs/AI/tasks/identity/task_idp8_admin_mfa.md` · P4/ID-P3 `docs/AI/tasks/self_serve_onboarding/task_p4_otp_phone_verification.md` · AF1 `docs/AI/tasks/self_serve_onboarding/task_af1_signup_antiflood.md`
> **User directive (2026-11-10):** "đề xuất chiến lược implement an toàn và đầy đủ nhanh chóng" → ✅ chọn (a): lưu strategy + implement W0/W1 ngay.

## Nguyên tắc xương sống

1. **TDD trước** — test domain/service trước → code → chạy → push. Mỗi wave = 1 commit batch → 1 CD → 1 RV sweep.
2. **Additive-only** — migration nullable/default, enum không renumber (thêm `Rank()`), DTO trailing optional params → rollback no-op, deploy 2 chiều.
3. **Migration cả 2 DB ngay** — `DemoUserConfiguration` ăn cả PG + SQLite → `dotnet ef migrations add` cho CẢ `VanAnDbContext` LẪN `ShopERPDbContext` (bài học Regime-Lock).
4. **Gate mỗi wave** — `guard-check.ps1` + build 0 errors + Core.Tests + ShopERP.Tests PASS → FAST PUSH → CD poll `gh run list` (không `gh run watch`) → RV L1→L4, STOP ở fail đầu.
5. **Cấm** — đụng AccountingEntry · Domain có EF using · bật `RequireOtp=true` trước khi có kênh thật.

## Wave plan

| Wave | Nội dung | Gộp vì | Gate đặc biệt |
|---|---|---|---|
| **W0** | Hardening `OtpService` cũ (Random→CSPRNG + attempt lock 5/15' + purpose namespace) | Lỗ hổng sống production — fix 30' | Không đổi `IOtpService` interface |
| **W1** | **ID-P1 + MFA-1 gộp** — DemoUser += Phone/VerifiedChannel/TrustLevel + MfaEnabled/MfaSecret · IdentityLevelRank · VerifyAsync populate · downgrade hook · MfaService · login 2 bước · MfaSettings · UserSyncSubscriber | Cùng `DemoUser` + cùng config → 1 migration `AddDemoUserTrustAndMfa` + 1 CD | Gate 5 đã duyệt · banner MFA fail-safe · JWT/API không đổi |
| **W2** | P4/ID-P3 email-first — channels + MerchantSignupOtpService + UI OTP + payload OtpChannel | Cần ID-P1 payload field | `RequireOtp=false` default → deploy an toàn trước Brevo key; RV email thật khi có key |
| **W3** | AF1 anti-flood — cooldown + daily cap + caps + whitelist | Không đụng Domain/file W1/W2 | Silent dedup generic · LIKE-scan RawJson MVP-only |

## RV bắt buộc mỗi wave (user: "verify được khi chạy rv test")

- L1 markers DLL + migration applied (PG + SQLite) → L2 health/routes → L3 Playwright prod (spec `playwright-rv-*.config.ts`) → L4 check DB/manual → cleanup + ghi project_state.
- W1 L3: TOTP tính bằng node `otplib` + secret từ setup flow; tạo/reject tenant test verify TrustLevel + downgrade.
- W2 L3: gửi OTP email test → verify → badge; sai 5 lần → khóa; RequireOtp=false regression.
- W3 RV nhẹ: submit trùng SĐT → 1 Pending duy nhất.

## Log

- **2026-11-10:** Strategy approved (a). Bắt đầu W0 → W1.