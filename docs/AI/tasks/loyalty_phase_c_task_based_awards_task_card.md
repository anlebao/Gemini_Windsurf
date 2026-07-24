# TASK CARD: LOYALTY-C - Task-Based Awards (Gamification Framework)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Build gamification framework — khách tích điểm qua nhiệm vụ (không chỉ mua hàng). Hiện tại 0% complete (audit 2026-07-23).
- **Nghiệp vụ áp dụng:**
  - **Cài app (PWA install)** — khách install PWA → +50 điểm (one-time).
  - **Xác thực OTP** — khách upgrade Social → Verified via OTP → +100 điểm (one-time).
  - **Nhập ngày sinh** — khách nhập birthday → +30 điểm (one-time) + sinh nhật hàng năm auto +100 điểm.
  - **Share/like Facebook** — khách share campaign page lên Facebook → +20 điểm (daily cap 1 lần).
  - **Share/like TikTok** — khách share campaign page lên TikTok → +20 điểm (daily cap 1 lần).
  - Admin cấu hình nhiệm vụ + điểm thưởng + điều kiện (configurable per tenant).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Prerequisites:**
  - Phase 5.6 COMPLETE (cùng sửa `Profile.razor` + `PWAService.cs` — Phase 5.6 thêm push toggle, L-C thêm birthday input + task list).
  - Loyalty Phase A COMPLETE (configurable formula + guard fix — task awards cũng dùng `AddPointsAsync`, cần consistent formula).

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `1_Shared/Domain.cs` — thêm `Mission` entity + `MissionCompletion` entity + Customer fields (Birthday, PWAInstalledAt, OtpVerifiedAt, FacebookShareCount, TikTokShareCount)
  - `3_CoreHub/Infrastructure/Configurations/MissionConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/MissionCompletionConfiguration.cs` (NEW)
  - `3_CoreHub/Domain/Repositories/IMissionRepository.cs` (NEW)
  - `3_CoreHub/Infrastructure/Repositories/MissionRepository.cs` (NEW)
  - `3_CoreHub/Services/IMissionService.cs` (NEW) + `1_Shared/Services/IMissionService.cs` (NEW — contract)
  - `3_CoreHub/Services/MissionService.cs` (NEW) — mission CRUD + complete mission + award points
  - `5_WebApps/ShopERP/Controllers/MissionsController.cs` (NEW) — admin CRUD missions + customer complete mission
  - `2_Gateway/Controllers/MissionsController.cs` (NEW) — forward to ShopERP
  - `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — thêm award points khi OTP verify (line 79-85)
  - `5_WebApps/KhachLink/Services/PWAService.cs` — thêm award points khi PWA install (line 98-124, 234-239)
  - `5_WebApps/KhachLink/Pages/Profile.razor` — thêm birthday input + mission list + progress
  - `5_WebApps/KhachLink/Pages/Missions.razor` (NEW) — `/missions` page danh sách nhiệm vụ + complete button
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` — thêm social share functions (Facebook SDK share, TikTok share URL)
  - `5_WebApps/ShopERP/Components/Pages/Admin/MissionsAdmin.razor` (NEW) — admin mission management
  - `5_WebApps/ShopERP/Migrations/` — migration tạo 2 tables + alter Customer table (add fields)
  - `6_Tests/` — unit + integration tests
- **Boundary Rules:**
  - **Domain modifications (2 entity mới + Customer fields THÊM):** `Mission`, `MissionCompletion` entity + Customer thêm `Birthday`, `PWAInstalledAt`, `OtpVerifiedAt`, `FacebookShareCount`, `TikTokShareCount`. Approved as part of feature plan.
  - KHÔNG sửa `LoyaltyRewards` entity (dùng `AddPointsAsync` có sẵn).
  - KHÔNG sửa `Order` entity.
  - Multi-tenancy: missions tenant-scoped (mỗi tenant cấu hình nhiệm vụ riêng).
  - **Customer entity modification:** THÊM fields (Birthday, PWAInstalledAt, etc.) — không sửa field hiện có. Cần migration alter table.

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Mission entity:** TenantId, MissionType [PWAInstall/OtpVerify/BirthdayEntry/FacebookShare/TikTokShare/Custom], Title, Description, PointsReward, IsOneTime (bool), DailyCap (nullable int), IsActive, SortOrder, Config (JSON — mission-specific params).
- [ ] **MissionCompletion entity:** TenantId, CustomerId, MissionId, CompletedAt, PointsAwarded, Metadata (JSON — e.g., Facebook post URL, TikTok video URL).
- [ ] **Customer fields mới:** `DateTime? Birthday`, `DateTime? PWAInstalledAt`, `DateTime? OtpVerifiedAt`, `int FacebookShareCount`, `int TikTokShareCount`.
- [ ] **Mission completion flow:** Customer trigger mission → verify condition (e.g., PWA installed, OTP verified, birthday entered, share URL submitted) → check one-time/daily cap → `AddPointsAsync` → tạo MissionCompletion record.
- [ ] **PWA install trigger:** `PWAService.HandleInstallStateChanged(true)` → call MissionService.CompleteMissionAsync(customerId, MissionType.PWAInstall).
- [ ] **OTP verify trigger:** `CustomerIdentityController.VerifyOtp` (line 79-85) → call MissionService.CompleteMissionAsync(customerId, MissionType.OtpVerify).
- [ ] **Birthday entry trigger:** Profile.razor birthday input save → MissionService.CompleteMissionAsync(customerId, MissionType.BirthdayEntry).
- [ ] **Birthday annual bonus (chốt 2026-07-23 — Auto scheduled job):** Scheduled job (daily, HostedService) check customers có birthday today → AddPointsAsync (100 points) + tạo MissionCompletion (MissionType.Custom, Metadata="Birthday bonus {year}"). Auto award, KHÔNG require customer claim.
- [ ] **Social share (chốt 2026-07-23 — Require share URL):** Khách click share button → JS open Facebook/TikTok share dialog → return share URL → submit to server → verify URL format (Facebook post URL / TikTok video URL pattern) → CompleteMissionAsync.
  - **Facebook share verification:** Require Facebook post URL (pattern `facebook.com/*/posts/*` hoặc `fb.com/*`). KHÔNG verify thật (Facebook KHÔNG có callback API). Filter URL rỗng/sai format.
  - **TikTok share verification:** Require TikTok video URL (pattern `tiktok.com/@*/*` hoặc `tiktok.com/*/video/*`). Tương tự Facebook.
  - **Daily cap:** Max 1 share reward per platform per day per customer (prevent abuse).
- [ ] **Admin UI (chốt 2026-07-23 — Per tenant):** MissionsAdmin.razor — CRUD missions (type, title, points, one-time/daily cap, active toggle). Missions configurable **per tenant** (mỗi tenant cấu hình nhiệm vụ + điểm thưởng riêng).

## 5. SUCCESS CRITERIA (14)
- [ ] SC1: `Mission` entity + migration (ShopERP SQLite).
- [ ] SC2: `MissionCompletion` entity + migration.
- [ ] SC3: Customer entity thêm 5 fields (Birthday, PWAInstalledAt, OtpVerifiedAt, FacebookShareCount, TikTokShareCount) + migration alter table.
- [ ] SC4: `MissionService.CompleteMissionAsync(customerId, missionType)` — verify condition → check cap → AddPointsAsync → tạo MissionCompletion.
- [ ] SC5: PWA install → auto-complete PWAInstall mission + award points.
- [ ] SC6: OTP verify → auto-complete OtpVerify mission + award points.
- [ ] SC7: Birthday entry → auto-complete BirthdayEntry mission + award points.
- [ ] SC8: Birthday annual bonus — scheduled job award 100 points on birthday.
- [ ] SC9: Facebook share → complete FacebookShare mission (daily cap 1) + award points.
- [ ] SC10: TikTok share → complete TikTokShare mission (daily cap 1) + award points.
- [ ] SC11: KhachLink `Missions.razor` (`/missions`) — danh sách nhiệm vụ + progress + complete button.
- [ ] SC12: Profile.razor — birthday input field + save.
- [ ] SC13: ShopERP `MissionsAdmin.razor` — admin CRUD missions.
- [ ] SC14: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS + tests PASS.

**Implementation Date:** _TBD_
**Branch:** `main` (sau Phase 5.6 + Loyalty Phase A)

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — 2 entity mới + Customer fields, verify không phá existing
- `accounting-ui-implementation` — admin + customer UI pattern
- `test-system-upgrade` — TDD cho mission completion flow

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8 (from audit 2026-07-23)
- **Verified Facts:**
  - Fact 1: KHÔNG có Mission/Quest/Task entity/service/controller nào.
  - Fact 2: `PWAService.ShowInstallPromptAsync` (line 98-124) + `HandleInstallStateChanged` (line 234-239) REAL — nhưng KHÔNG gọi AddPointsAsync.
  - Fact 3: `CustomerIdentityController.VerifyOtp` (line 79-85) REAL — upgrade IdentityLevel nhưng KHÔNG award points.
  - Fact 4: Customer entity KHÔNG có Birthday/DateOfBirth field.
  - Fact 5: `TenantSettings.SocialLinksFb` + `SocialLinksTiktok` có (link page) — nhưng KHÔNG có share reward logic.
  - Fact 6: `LoyaltyRewardsService.AddPointsAsync` REAL — có thể reuse cho mission awards.
  - Fact 7: `ActivateCustomerAsync` (LoyaltyRewardsService:210) đã award 100 points welcome bonus — precedent cho trigger-based awards.
  - Fact 8: KHÔNG có admin UI cho tasks/missions.
- **Assumptions:**
  - A1: Facebook/TikTok share KHÔNG thể verify thật (no callback API) — trust-based verification.
  - A2: Scheduled job cho birthday annual bonus — cần background service (verify HostedService pattern trong codebase).
- **Open Questions:**
  - Q1: Social share verification → **Require share URL** (chốt 2026-07-23 — verify URL format, không verify thật, daily cap 1 per platform).
  - Q2: Birthday annual bonus → **Auto scheduled job** (chốt 2026-07-23 — HostedService daily check, không require claim).
  - Q3: Mission config → **Per tenant** (chốt 2026-07-23 — mỗi tenant cấu hình nhiệm vụ + điểm riêng).
- **Recommended Action:** ANALYZE HostedService pattern → IMPLEMENT (sau Phase 5.6 + L-A).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs (thêm 2 entity + Customer 5 fields) | THÊM entity + THÊM Customer fields (alter table) | Domain integrity validation + migration |
| PWAService.cs (thêm award on install) | Sửa existing — thêm call sau install success | Phase 5.6 phải COMPLETE trước (cùng sửa file) |
| Profile.razor (thêm birthday + mission list) | Sửa existing — thêm section | Phase 5.6 phải COMPLETE trước (cùng sửa file) |
| CustomerIdentityController.cs (thêm award on OTP) | Sửa existing — thêm call sau upgrade | None (Phase 5 không sửa file này) |
| pwa.js (thêm social share functions) | THÊM functions, không sửa existing | None |
| New files (services, controllers, pages) | New, no impact existing | Isolated |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:** MissionService.CompleteMissionAsync — first completion → award points; one-time mission second completion → reject; daily cap exceeded → reject.
- **Unit test:** PWA install trigger → CompleteMissionAsync(PWAInstall) → points awarded.
- **Unit test:** OTP verify trigger → CompleteMissionAsync(OtpVerify) → points awarded.
- **Unit test:** Birthday entry → CompleteMissionAsync(BirthdayEntry) → points awarded + Customer.Birthday updated.
- **Unit test:** Birthday annual bonus — scheduled job → AddPointsAsync(100) on birthday.
- **Unit test:** Facebook/TikTok share → CompleteMissionAsync → daily cap enforcement.
- **Integration test:** Full mission flow — admin create mission → customer complete → points awarded → MissionCompletion record created.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | User chốt Q1-Q3 + ANALYZE HostedService pattern | Domain entities + Customer fields + EF config + migration |
| S2 | MissionService + repository | Code + unit tests |
| S3 | PWA install + OTP verify + birthday triggers | Code + unit tests |
| S4 | Social share (Facebook + TikTok) + daily cap | Code + pwa.js + unit tests |
| S5 | Birthday annual bonus scheduled job | Code + test |
| S6 | ShopERP + Gateway controllers | Code + integration tests |
| S7 | KhachLink Missions.razor + Profile.razor birthday | Code + browser test |
| S8 | ShopERP MissionsAdmin.razor | Code |
| S9 | Full test suite + build + guard-check + RV | Test + RV report |

## 12. ESTIMATED EFFORT
- 9 sessions. **NO BLOCKER** (Q1-Q3 đã chốt 2026-07-23). **Prerequisite:** Phase 5.6 + Loyalty Phase A COMPLETE.
- **Risk:** Social share verification require URL — vẫn có thể bị abuse (user submit URL fake). Mitigation: daily cap 1 per platform + admin can disable mission + URL format validation.
