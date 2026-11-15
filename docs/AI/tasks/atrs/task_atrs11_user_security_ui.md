# Task ATRS-11: User Security UI — U01-U08 (onboarding → security center → devices)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-4 (recovery codes), ATRS-5 (passkey), ATRS-6 (evidence) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** UI U01-U08, F01-F02, §9.1 status vocab, §10 global reqs, §11-13, §14.1, §15 scenarios 1-8
> **Surface:** KhachLink (customer, WASM PWA) + ShopERP account area (staff, nếu D10 mở rộng sau)

## 1. OBJECTIVE

Bộ màn hình user-facing nền tảng: giới thiệu bảo mật → hồ sơ → passkey → recovery codes → security center → device registry/detail. Progressive, không bắt buộc, không oversell.

## 2. SCREENS → IMPLEMENTATION NOTES

| Screen | Key requirements (UI SRS) | Backend dep |
|---|---|---|
| U01 Set up account protection | Giải thích mục đích; "Để sau" không trap; progress không imply 100%=identity; persist dismiss theo preference hiện có | — |
| U02 Personal profile/evidence | Chỉ field backend hỗ trợ; show why; reuse data đã có (không hỏi lại); mask sensitive; DOB optional theo policy | ATRS-6 |
| U03 Set up passkey | Capability detection THẬT; gọi WebAuthn flow thật; handle cancel/timeout/unsupported/duplicate/server-reject; không claim success trước server confirm; synced-passkey wording | ATRS-5 |
| U04 Secret challenge | **OMIT nếu D7** — nếu giữ: supplementary-only wording, không mandatory | ATRS-6(cond) |
| U05 Recovery codes | Show 1 lần sau generate; copy/download nếu supported; regenerate warn invalidate; không show lại plaintext; step-up cho regenerate | ATRS-4 |
| U06 Security center | Status tổng (map AssuranceState BASIC/PROTECTED/RECOVERY_READY → "Chưa thiết lập/Cơ bản/Đã tăng cường"); passkey/recovery-code/device count/last activity; entry recovery + dispute; không lộ technical IDs | ATRS-2 status API |
| U07 Device registry | List devices: nickname, category, first/last seen (timezone-aware), status (active/revoked/pending/unknown), current-device marker nếu reliable; rename/revoke/report-lost; DeviceId = risk signal không phải proof badge | ATRS-2 |
| U08 Device detail | Metadata an toàn + credential status + recent events; revoke confirmation dialog (identify device, consequences, recovery path); không revoke credential cuối cùng khả dụng mà không warning | ATRS-2/3 |

## 3. GLOBAL RULES (áp mọi screen)

- Entry: Tài khoản → Bảo mật tài khoản; theo nav hiện có, không duplicate security center mỗi app.
- Feature gating: tôn trọng `KhachLinkProfile`/`ShopFeatureSettings`/`FeatureContextService` — browse-only profiles ẩn security center entries (precedent W2-5/W3-2).
- States bắt buộc mọi data screen: loading/empty/loaded/error+retry/permission-denied/stale.
- Status vocabulary map §9.1 — bảng mapping đã chốt ở ATRS-1.
- UI Platform components (`VanAnPublicSearchBar` pattern, `VanAnAlert`…) — KHÔNG custom HTML/CSS bypass.
- Mobile-first; a11y §10.5; không sensitive value trong console/URL/localStorage (§12.1-2).
- "Device ID không chứng minh danh tính" — wording audit trên mọi screen.

## 4. FILES (dự kiến)

- `5_WebApps/KhachLink/Pages/Security/*.razor` (hoặc `Components/`) + `SecurityHttpService` + DTOs
- NavMenu/Account menu entries + feature-flag wiring
- `UI.Platform` components nếu thiếu (vd `VanAnSecurityStatusBadge`)
- `6_Tests` bUnit/component tests + `6_Testing/e2e-tests` Playwright specs

## 5. TESTS

- Component: mỗi screen render states; U03 mock WebAuthn success/fail/cancel; U05 generate-once flow.
- Playwright (local trước, prod sau): scenarios §15.1-8 (no-methods, skip onboarding, passkey success/unsupported/cancel/timeout, recovery codes generate/one-time/regenerate, device list, revoke confirm).
- Negative: device revoke chỉ usable credential → warning path; không fabricated data.

## 6. ACCEPTANCE

- [ ] UI §14.1 + §14.5 pass; F01/F02 E2E pass
- [ ] Không screen nào fake backend (prohibited shortcuts §17.2)

## 7. RISKS

- WebAuthn trên iOS Safari/WASM — capability detection phải trung thực; fallback path rõ.
- Nhiều KhachLink instances/profiles → gating sai = lộ security UI cho browse-only tenant.
