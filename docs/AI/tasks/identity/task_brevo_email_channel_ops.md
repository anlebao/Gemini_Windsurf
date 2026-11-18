# Task BREVO-OPS: Brevo Email Channel — Ops Wiring + Production RV (P4 unblock)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-2/FR-15/FR-16 (P4 = ID-P3, L2 channel)
> **Parent:** `docs/AI/tasks/self_serve_onboarding/task_p4_otp_phone_verification.md` (code DONE `b0a5f2f0` — card này chỉ cover phần OPS còn thiếu)
> **Consumers:** P4 merchant-signup OTP · MFA-1 email OTP fallback (ShopERP) · ATRS-13 email notification channel (residual — card `task_atrs13_notifications_observability.md`)
> **Mode:** IMPLEMENT (khi được duyệt) — phần lớn là workflow YAML + secrets + RV, KHÔNG đụng Domain
> **Created:** 2026-11-18 — REVIEW_ONLY phân tích, CHƯA implement

## 1. OBJECTIVE

Làm cho kênh email Brevo (0đ, free 300 mail/ngày) **thực sự gửi được trên production**, unblock:
- P4/ID-P3 OTP merchant signup (`RequireOtp=true` khi cần)
- MFA-1 email OTP fallback cho admin mất authenticator
- ATRS-13 email notification channel (việc nhỏ tiếp theo sau khi channel proven)

## 2. VERIFIED FACTS (đã kiểm chứng 2026-11-18)

| Hạng mục | Trạng thái |
|---|---|
| GH secret `BREVO_API_KEY` | ✅ TỒN TẠI (set 2026-11-11T01:20:36Z) |
| `BrevoEmailService` (IEmailService) + tests | ✅ Code xong từ Wave 1 (`3_CoreHub/Services/BrevoEmailService.cs`) |
| `IIdentityOtpChannel` + `BrevoEmailOtpChannel` + `FallbackOtpChannel` | ✅ `3_CoreHub/Services/Otp/` |
| `MerchantSignupOtpService` + controller `otp/send`/`otp/verify`/`config` | ✅ `b0a5f2f0` |
| KhachLink `Onboard.razor` OTP UI (flag-gated) | ✅ |
| ShopERP `OtpService` hardening §7 (CSPRNG + attempt lock + purpose) | ✅ `RandomNumberGenerator` verified |
| Compose mapping `Brevo__ApiKey=${BREVO_API_KEY:-not-configured}` | ✅ `docker-compose.gateway.yml:110`, `docker-compose.shoperp.yml:59`, `docker-compose.prod.yml:151` |
| `cd-multivps.yml` → `.env.gateway` BREVO wiring | 🟡 **DỞ** — `envs:` list + preserve-block `FINAL_BREVO_KEY` đã thêm; THIẾU `BREVO_API_KEY=` trong printf + `DEPLOY_BREVO_API_KEY` env mapping |
| `cd-multivps.yml` → `.env.shoperp` BREVO wiring | ❌ CHƯA CÓ (cần cho MFA-1 fallback + ShopERP IEmailService) |
| `MerchantSignup__RequireOtp` trên prod | ❌ `false` (default — OTP UI ẩn) |
| SPF/DKIM sender `noreply@vanan.vn` trong Brevo dashboard | ❓ CHƯA VERIFY — user check tay |
| RV §8 P4 card (gửi OTP thật + check delivery log) | ❌ CHƯA CHẠY — block bởi wiring |
| `project_state.md` §4 dòng P4 | ⚠️ STALE — đang `[ ]` dù code merged |

## 3. CHANGES CẦN LÀM (khi implement)

### 3.1 `cd-multivps.yml` — Gateway job (deploy-gateway)

- [ ] `envs:` list (~line 460): thêm `DEPLOY_BREVO_API_KEY` — ✅ ĐÃ LÀM
- [ ] Script: preserve-block `EXISTING_BREVO_KEY`/`FINAL_BREVO_KEY` — ✅ ĐÃ LÀM (user thêm)
- [ ] Script `printf .env.gateway`: thêm `"BREVO_API_KEY=${FINAL_BREVO_KEY}"` (gần `GODADDY_API_KEY=`)
- [ ] Step `env:` mapping: thêm `DEPLOY_BREVO_API_KEY: ${{ secrets.BREVO_API_KEY }}` (gần `DEPLOY_GODADDY_API_KEY`)

### 3.2 `cd-multivps.yml` — ShopERP job (deploy-shoperp)

Cùng pattern (ShopERP dùng Brevo cho MFA-1 email fallback + customer-facing email):

- [ ] `envs:` list (~line 870): thêm `DEPLOY_BREVO_API_KEY`
- [ ] Script: preserve-block đọc `BREVO_API_KEY=` từ `.env.shoperp` → `FINAL_BREVO_KEY`
- [ ] `printf .env.shoperp`: thêm `"BREVO_API_KEY=${FINAL_BREVO_KEY}"`
- [ ] Step `env:` mapping: thêm `DEPLOY_BREVO_API_KEY: ${{ secrets.BREVO_API_KEY }}`

### 3.3 Ops tay (user / dashboard — không code)

- [ ] Brevo dashboard: verify sender `noreply@vanan.vn` + thêm SPF/DKIM DNS records cho `vanan.vn` nếu chưa — nếu không mail vào spam/spam folder
- [ ] (Optional) Test gửi tay 1 mail từ Brevo dashboard tới địa chỉ kiểm soát → confirm không spam

### 3.4 Deploy + RV (theo `.devin/rules/runtime-verification.md`)

- [ ] Push → CD deploy gateway + shoperp → verify container env `Brevo__ApiKey` non-empty, không phải `not-configured` (KHÔNG echo giá trị)
- [ ] **L2:** `GET /api/v1/merchant-signups/config` trả `otpChannel=email`
- [ ] **L3:** bật tạm `MerchantSignup__RequireOtp=true` trên VPS `.env.gateway` (hoặc test qua OTP service path khác) → `otp/send` tới email test thật → check Brevo dashboard delivery log → `otp/verify` đúng → submit có `OtpVerified` → **revert flag về false** sau RV
- [ ] MFA-1: login admin → trigger "gửi email OTP fallback" → nhận mail thật (nếu MFA-1 đã deploy — check trạng thái `task_idp8_admin_mfa.md` trước)
- [ ] Regression: `RequireOtp=false` → submit signup không cần OTP (P0 behavior)

## 4. ACCEPTANCE CRITERIA

- [ ] `BREVO_API_KEY` xuất hiện trong `.env.gateway` + `.env.shoperp` trên VPS sau CD (giá trị khớp GH secret — verify bằng length/hash, KHÔNG echo)
- [ ] Container `Brevo__ApiKey` != `not-configured` trên cả gateway + shoperp
- [ ] 1 email OTP thật tới được inbox test (không spam — hoặc ghi nhận nếu SPF/DKIM pending)
- [ ] `FallbackOtpChannel` failover path không regression (primary email OK)
- [ ] Guard + build PASS; CD run SUCCESS

## 5. RISKS / NOTES

- **Free tier 300 mail/ngày** — đủ cho OTP MVP; theo dõi volume khi bật RequireOtp diện rộng (mỗi send = 1 mail; fallback chain có thể gửi 2 kênh).
- **KHÔNG bật `RequireOtp=true` mặc định** — giữ `false` cho tới khi có use-case + deliverability proven (theo card P4 §6.1).
- **atrs KHÔNG tự gọi Brevo** — sau khi channel proven, atrs email notification là task riêng (extend `IAtrsNotificationWriter`, tái dùng `IEmailService` pattern hoặc gọi qua Gateway — quyết định khi làm card đó).
- Preserve-pattern (`DEPLOY_*:-$EXISTING_*`) bắt buộc — tránh CD xoá key set tay trên VPS.
- `docker-compose.prod.yml` (Oracle/legacy path) cũng map `BREVO_API_KEY` — không nằm trong cd-multivps scope; nếu Oracle VPS còn sống cần set `.env` tay ở đó (verify trước khi bỏ qua).

## 6. FOLLOW-UP SAU CARD NÀY

1. Update `project_state.md` §4: P4 → DONE (code) + note ops card này
2. ATRS-13 email channel card (nhỏ — reuse proven channel)
3. MFA-1 email fallback RV (nếu MFA-1 đã deploy)
4. Slot ZNS 300đ khi có Zalo OA verified (phase sau, giữ nguyên quyết định P4)

## 7. TRACEABILITY

| Nhu cầu | Cover bởi |
|---|---|
| P4 OTP gửi được mail thật | §3.1 + §3.4 L3 |
| MFA-1 email fallback | §3.2 + §3.4 |
| ATRS-13 email notify | prerequisite — card riêng sau |
| Deliverability (không spam) | §3.3 SPF/DKIM + §3.4 inbox check |
| Không regression P0 | §3.4 RequireOtp=false regression |
| Secrets an toàn | GH secret + preserve-pattern + không echo |
| CD governance | `.devin/rules/cd-deployment.md` — `*_REMOTE_HOST`/secret wiring pattern |
| RV protocol | `.devin/rules/runtime-verification.md` — 5-layer, STOP at first fail |

**Status:** 🟡 WIRED + DEPLOYED — BLOCKED bởi GH secret `BREVO_API_KEY` sai giá trị.

**Progress 2026-11-19:**
- §3.1+§3.2 DONE — commit `5fcec6c3`: `BREVO_API_KEY=${FINAL_BREVO_KEY}` vào printf `.env.gateway` + `.env.shoperp`, `DEPLOY_BREVO_API_KEY` env mapping cả 2 job, preserve-block ShopERP. CD `44256578328` SUCCESS.
- AC verify: `.env.gateway` + `.env.shoperp` trên VPS đều có `BREVO_API_KEY` (len=90); container `Brevo__ApiKey` = **CONFIGURED** cả gateway (`vanan-gateway-1`) + shoperp (`vanan-shoperp-1`). L2 `GET /api/v1/merchant-signups/config` → `{"requireOtp":false,"otpChannel":"email"}` PASS.
- **BLOCKER:** `POST otp/send` tới `lebaoan81@gmail.com` → Brevo API **401 "Key not found"** (`[Brevo] API error 401` trong gateway log). Key length=90 nhưng **không có prefix `xkeysib-`** → GH secret chứa giá trị sai (không phải Brevo API key hợp lệ — có thể placeholder/key cũ đã revoke).
- **Cần user:** tạo API key mới trong Brevo dashboard (SMTP & API → API Keys, quyền `smtp` transactional) → update GH secret `BREVO_API_KEY` (`gh secret set BREVO_API_KEY`) → re-run CD (hoặc set tay `.env.*` + restart nếu muốn nhanh) → re-test `otp/send` → check inbox `lebaoan81@gmail.com`.
- Còn lại sau unblock: §3.3 SPF/DKIM sender `noreply@vanan.vn` (user check Brevo dashboard) + §3.4 L3 verify delivery + MFA-1 email fallback (chờ `task_idp8_admin_mfa.md` implement) + ATRS-13 email channel card.
