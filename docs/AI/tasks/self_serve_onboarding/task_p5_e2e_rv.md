# Task P5: E2E + Deploy + Runtime Verification (Self-Serve Onboarding)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md`
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §9 RV Plan
> **Mode:** IMPLEMENT → deploy → RV
> **Prerequisite:** P0-P3 DONE (+P4 nếu RequireOtp bật); SRS §9 đã chốt
> **Layer:** 6_Tests + 6_Testing + production

## 1. OBJECTIVE

Chứng minh end-to-end trên production: merchant self-serve → Pending → sysadmin verify-signup → owner login → tenant hoạt động; đồng thời regression Track A wizard + crawl pipeline.

## 2. SCOPE

### 2.1 E2E specs (`6_Testing/e2e-tests/`)

**`merchant-onboarding.spec.ts`** (mới) — production config pattern hiện có (`playwright-rv-*.config.ts` + auth state sysadmin):

1. **Self-serve submit (Case B/Rong — không cần ảnh thật):**
   - `/onboard` → chọn "Shop nhỏ không GPKD" → điền tên/địa chỉ/HKDGroup(gợi ý) → bỏ qua ảnh (optional) → account (username/password/SĐT test) → review → submit (Turnstile: production widget cần token thật — **risk: E2E không solve được Turnstile** → phương án: test env bypass flag `Turnstile:SkipInE2E`? KHÔNG weaken security prod → thay vào đó: submit qua API trực tiếp trong spec setup (request context) rồi UI chỉ test phần hiển thị/status. Ghi rõ trong spec.)
   - Assert: màn kết quả + signupId lưu localStorage.
2. **Status page:** `/onboard/status` → nhập signupId+phone → "Đang chờ duyệt".
3. **Sysadmin review:** login sysadmin (storageState sẵn) → `/admin/tenants` → tab Pending → tìm tenant vừa tạo → "Hồ sơ" modal → assert ảnh/fields/menu/flags → "Duyệt" → verify-signup → assert slug + ownerUsername hiển thị.
4. **Owner login:** login app2/ShopERP bằng ownerUsername+password merchant đặt → vào dashboard tenant mới → (nếu productLines>0) import sản phẩm hoặc QuickSetup.
5. **Reject path:** signup thứ 2 → sysadmin Từ chối → status page hiện Rejected.
6. **Regression Track A:** `/admin/onboarding-ocr` load được + Case B điền tay submit (hoặc ít nhất wizard render + validation) — không cần tạo tenant thật thứ 2 (hoặc tạo rồi reject).

**`ocr-onboarding.spec.ts` mở rộng (nếu P3 đổi UI):** assert `PhotoUrls` upload call xảy ra (network intercept) + HKDGroup helper render.

### 2.2 Unit/integration bổ sung

- Core.Tests: đã cover P0 — thêm edge cases nếu review phát hiện.
- ShopERP.Tests: bUnit P2/P3.

### 2.3 Deploy

- FAST PUSH conditions: guard-check + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS trong session → `git push --no-verify origin main` → verify `gh api .../branches/main` sha == HEAD.
- CD Multi-VPS tự deploy gateway + shoperp + khachlink.
- **Secrets/env cần trước deploy:** `MerchantSignup__RequireOtp` (default false — không cần set), ESMS keys (chỉ nếu P4), Turnstile site key đã có (Register.razor dùng — reuse), ImageUpload R2 đã config production.

### 2.4 RV layers (theo `.devin/rules/runtime-verification.md`)

| Layer | Check |
|---|---|
| L1 | Markers production: `MerchantSignupService`+`MerchantSignupController` trong CoreHub.dll/Gateway.dll; `/onboard` route + `onboarding-ocr.js` assets trong KhachLink image; rate-limit policies trong Gateway.dll |
| L2 | `GET /onboard` → 200 public · `POST /api/v1/merchant-signups` no-turnstile → 400 · `verify-signup`/`signup-detail`/`reject-pending` no-auth → 401 · `status` endpoint hoạt động · image upload `folder=onboarding` → 200 (ảnh test nhỏ) · `GET /api/v1/platform/onboarding-flags` public → 4 bool · `PUT` no-auth → 401 · **toggle matrix**: master OFF → submit 503 + `/onboard` trang đóng (test bằng cách toggle → verify → toggle lại ON) |
| L3 | Playwright production spec trên (mục 2.1) |
| L4 | Manual browser mobile: `/onboard` thật → chụp ảnh GPKD/biển/menu → OCR → submit → sysadmin review ảnh → duyệt → owner login app2 → dashboard + import sản phẩm |
| L5 | Regression: `/admin/onboarding-ocr` sysadmin wizard end-to-end (1 tenant test → cleanup); `/claim` form vẫn hoạt động; crawl pending cũ không vỡ |

### 2.5 Cleanup

- Tenant test self-serve: sau RV → deactivate/xóa (theo pattern cleanup các RV trước — PG tenant + owner user + SQLite mirror + R2 ảnh test + pending rows).
- Draft localStorage test artifacts — n/a server-side.

## 3. ACCEPTANCE

- [ ] Toggle matrix PASS: 4 toggles độc lập đúng behavior (FR-19..22); QR link quét thật trên điện thoại → `/onboard` prefill đúng; Directory CTA hiện/ẩn đúng flag
- [ ] E2E spec PASS trên production (hoặc documented deviation với lý do — vd Turnstile)
- [ ] L1-L5 PASS; mọi bug tìm được → fix trong session hoặc task card mới (không bỏ qua)
- [ ] CD Multi-VPS ALL SUCCESS + smoke PASS
- [ ] Không regression: Track A wizard, /claim, crawl pipeline, customer OTP login
- [ ] `docs/AI/project_state.md` cập nhật objective mới

## 4. VERIFICATION COMMANDS

```powershell
# Pre-push (bat buoc)
.\scripts\guard-check.ps1
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests
dotnet test 6_Tests/VanAn.ShopERP.Tests

# Push
git push --no-verify origin main   # FAST — sau khi CI da pass trong session
gh api repos/anlebao/Gemini_Windsurf/branches/main --jq '.commit.sha'
git rev-parse HEAD                 # 2 sha phai khop

# CD status — KHONG dung gh run watch (hang) — poll:
gh run list --limit 5 --json status,conclusion,displayTitle
```

## 5. RISKS

- **Turnstile trong E2E**: production widget không auto-pass → spec submit qua API request context (không qua UI submit) hoặc mock — quyết định khi viết spec, ghi deviation rõ.
- **Ảnh R2 test**: upload ảnh test → dọn sau RV (xóa objects hoặc chấp nhận orphan — R2 lifecycle rule là ghi nợ).
- **Owner login tenant resolution**: phụ thuộc PF1 P0 — nếu login cần slug subdomain, spec phải dùng đúng URL.
- **NATS sync timing**: verify-signup → tenant Active PG ngay nhưng SQLite mirror chậm vài giây → owner login/import có thể 409 thoáng qua → spec retry.

## 6. KẾT QUẢ (2026-11-09 — DONE `3c4d7384`)

- **E2E production `merchant-onboarding.spec.ts` + `playwright-rv-onboarding.config.ts`: 6/6 PASS**
  1. L2 flags public (all true) + admin guards auth-enforced.
  2. Case B/Rong submit qua API → `/onboard/status` Pending.
  3. Sysadmin `/admin/tenants` Pending tab → Hồ sơ modal → verify-signup → status Active.
  4. Owner login `app2/Login` username+password merchant → redirect khỏi /login.
  5. Case Rong submit → reject-pending (lý do) → status Rejected.
  6. Regression: `/onboard` 3 case cards + `/admin/onboarding-ocr` + flags card.
- **Bug production tìm được + fixed trong session:** owner user tạo bởi verify-signup nằm PG, ShopERP `/Login` đọc SQLite → "Email hoặc password không đúng". Fix: `UserUpserted` outbox event trong `TenantOnboardingService` (bcrypt hash only, không plaintext) → NATS `vanan.cloud.user.upserted` → `UserSyncSubscriber` (ShopERP hosted service) upsert `DemoUser`+`UserTenant` idempotent giữ cross-DB userId. Kèm latent bug: `TenantVerifiedEvent` enqueue sau `SaveChanges` cuối → mất → thêm SaveChanges persist cả 2 events.
- **Tests:** onboarding Core.Tests 67/67 (gồm test mới `VerifyAsync_EnqueuesOutboxEvents` — assert cả TenantVerified+UserUpserted, payload không chứa plaintext password) · ShopERP build 0 errors · guard-check PASSED sau commit.
- **CD:** push `3c4d7384` → CI + Accounting Tests + CD Multi-VPS ALL SUCCESS → image `3c4d7384` deployed gateway + shoperp.
- **Cleanup:** 6 tenant "RV Onboard*" — 2 `reject-pending` (204) + 4 `deactivate` (200) qua admin API.
- **Deviations (đã ghi trong spec header):** Turnstile prod sitekey rỗng → submit qua `context.request` (không qua UI submit); rate-limit merchant-signup 5/24h/IP; L4 manual mobile + L5 một phần (spot-check `/claim` `/login` `/onboard` 200, flags 200) còn lại cho user xác nhận tay.
- **Post-RV fix `002d2e72` (user manual test):** CTA "Đăng ký ngay" trên timlathay.com link `https://khachvip.online/onboard` (apex không host KhachLink → unreachable — cùng pattern bug Issue #166). Fix: `OnboardCta` fallback `KhachLink:DefaultDomain`+`/onboard` (prod = `diemthuong2.khachvip.online` qua `DIRECTORY_DEFAULT_KHACHLINK_DOMAIN`); `QrSignupCard` fallback `ExternalUrls:KhachLink`+`/onboard`; bỏ `Onboarding:OnboardUrl` chết khỏi appsettings ×2. CD #42730621505 SUCCESS → RV Playwright: click CTA timlathay.com → landed `https://diemthuong2.khachvip.online/onboard?src=directory` 200, 0 console errors.
