# TASK CARD: Profile UX Upgrade — Loyalty Hero + dọn Zone 4 + retention surfaces

> **DONE — `c478e8e0`** (approved + implemented 2026-11-28 · guard ALL PASSED)
> Created: 2026-11-28 · Mode: IMPLEMENT (sau approve) · TDD · Sweep-first (đã xong trong review)
> Nguồn: review UX `Profile.razor` cùng session — tách riêng khỏi
> `task_my_stores_profile.md` theo yêu cầu user (card kia giữ nguyên scope
> "Cửa hàng của tôi"; card này là phần còn lại của UX proposal).
> Rule: **KHÔNG chạm Domain/DB/migration — không cần Gate 5** (UI + 1 reuse
> endpoint hiện hữu). UI Platform components only.

## OBJECTIVE

1. **P0 Loyalty Hero** — biến Zone 1 thành hero điểm thưởng (điểm là tài sản
   giữ chân lớn nhất, hiện bị dìm thành 1 dòng text nhỏ): số điểm to + tier +
   **progress bar tới tier kế** + CTAs.
2. **P1 Dọn Zone 4** — xóa bảng Fraud Flags (leak cơ chế anti-fraud cho chính
   khách), ẩn device token raw, xóa nút "Nâng cấp xác thực" trùng lặp.
3. **P2 Retention surfaces** — referral card cho collaborator + guest-mode
   continue-shopping + nhóm quick-access.

## CURRENT STATE — SWEEP (verified 2026-11-28)

### Zone map hiện tại (`Profile.razor`)

| Zone | Nội dung | Vấn đề |
|---|---|---|
| 1 header | initial-letter avatar + tên + SĐT + tier badge + `"N điểm"` + IdentityLevel badge | Điểm = text nhỏ, không progress — mất cơ hội retention lớn nhất |
| 2 context | `ProfileContextCardBuilder` — đã có `active-order`/`voucher-expiring`/`identity-upgrade` | Tốt — giữ |
| 3 quick-access | 6 nút Outline giống hệt | Không thứ bậc, không nhóm |
| 4 settings (collapsed) | push+sound/vibrate · sinh nhật · **Nâng cấp xác thực (TRÙNG context card)** · community roles · **FRAUD FLAGS bảng Risk Score** · **device_token raw** · logout | Leak anti-fraud + phô token + duplicate CTA |

### Fact nền đã verify

- `GET /api/loyalty/my` trả `{ Tier, PointBalance, NextTierThreshold,
  ProgressPercent, History }` — **đủ data cho hero, 0 backend change**
  (LoyaltyCard.razor:129-133 đang dùng). Cần 1 call thêm song song với
  context load trong `LoadProfile`.
- `ProfileContextCardBuilder` (pure static, unit-testable —
  `ProfileContextCardBuilderTests` hiện hữu) **đã có** `identity-upgrade`
  card (IdentityLevel Social/Guest → CTA /my-loyalty) → nút upgrade trong
  Zone 4 là **duplicate, xóa được an toàn**.
- `_myFraudFlags` load qua `LoadCommunityAsync` → chỉ phục vụ bảng Zone 4 →
  xóa bảng = xóa luôn query (giảm 1 request).
- `_deviceToken[..12]` raw hiện trong card "Thiết bị đang sử dụng".
- `_communityRoles[].SalesmanCode` đã có → referral card chỉ cần render
  (data đã load sẵn, không thêm request).
- PWA install **đã cover**: `KhachLinkLayout` header icon + menu +
  `PWAInstallPrompt.razor` + `_canInstallApp` trên Home — **không làm lại**.
- `BackToStoreChip` đã tồn tại — guest mode chỉ cần drop vào.
- `/my-stores` (card kia) render trong zone riêng — **không xung đột**:
  card này chỉ đụng Zone 1/3/4 + guest branch.

## IMPACT-NGUỢC GUARDS (không được vỡ)

- Hero vẫn hiển thị đúng khi `/api/loyalty/my` lỗi → fallback dùng
  `profile.Tier` + `profile.PointBalance` (từ `/api/customers/me`), progress
  bar ẩn. Fail-open, không block page.
- `NeedsUpgrade` (Social/Guest) vẫn hiện được CTA — qua context card
  `identity-upgrade` sẵn có (đã verify trong builder). Xóa nút Zone 4 nhưng
  KHÔNG xóa `_identityLevel` badge ở hero.
- Push/birthday/device settings giữ nguyên chức năng — chỉ đổi presentation.
- Community roles section giữ (collaborator cần thấy code của mình).
- Guest branch (`!_isLoggedIn`): giữ CTA đăng nhập, CHỈ THÊM BackToStoreChip
  — không đổi flow login.
- Fraud flags: xóa UI + xóa fetch `_myFraudFlags` trong `LoadCommunityAsync`
  — KHÔNG đụng backend endpoint/entity (admin có thể còn dùng).
- `data-testid` hiện hữu giữ (`identity-level-badge`, `settings-toggle` …)
  — check spec `profile-contextual` / `contextual-ai-entries` không vỡ.

## IMPLEMENTATION CHECKLIST

### Phase A — Loyalty Hero (Zone 1 redesign)

- [x] `LoadProfile`: sau `/api/customers/me` OK → fire parallel
  `client.GetAsync("/api/loyalty/my")` (cùng client đã gắn X-Customer-Token)
  → `_loyaltyProgress` {NextTierThreshold, ProgressPercent} (DTO private
  trong file — mirror `LoyaltyData` của LoyaltyCard).
- [x] Zone 1 markup mới: avatar initial (giữ) + **viền màu theo tier**
  (`bronze: #cd7f32 · silver: #94a3b8 · gold: #eab308 · platinum: #38bdf8`
  — border 3px trên `.rounded-circle`) · tên + SĐT (giữ) · badge tier +
  IdentityLevel (giữ) · **hàng hero**: `display-6` số điểm + "điểm" ·
  progress bar (`div.progress` + `progress-bar` Bootstrap, width
  `ProgressPercent%`) + caption "còn {threshold - balance} điểm lên {nextTier}"
  (ẩn khi `_loyaltyProgress == null` — fallback state).
- [x] CTA row trong hero: `VanAnButton` Outline sm "Đổi điểm" → `/rewards` ·
  "Lịch sử" → `/my-loyalty` (gate `_navFlags.ShowLoyaltyHistory`).
- [x] Next tier name: từ `NextTierThreshold` map ngược ladder
  {1000→Silver, 5000→Gold, 20000→Platinum} — mirror `TierLadder` trong
  `ProfileContextCardBuilder` (comment keep-in-sync như builder đang làm).

### Phase B — Dọn Zone 4

- [x] Xóa block `@if (_myFraudFlags.Count > 0)` (bảng fraud) + xóa field
  `_myFraudFlags` + nhánh fetch trong `LoadCommunityAsync` — **security
  fix**: không expose risk-score/anti-fraud internals cho customer.
- [x] Card "Thiết bị đang sử dụng": bỏ `<code>@_deviceToken[..12]</code>`
  → text "Thiết bị này · đã đăng ký" (giữ note "tối đa 3 thiết bị").
- [x] Xóa `@if (NeedsUpgrade)` alert + `VanAnButton` upgrade ở Zone 4
  (duplicate `identity-upgrade` context card); GIỮ method
  `NavigateToLoyaltyUpgrade` chỉ nếu context card dùng chung — kiểm tra call
  site trước khi xóa.
- [x] `GetIdentityLevelBadgeClass/Icon/Label` + badge hero: GIỮ.

### Phase C — Retention surfaces (P2)

- [x] **Referral card**: `@if (_communityRoles.Any(r => r.IsActive &&
  !string.IsNullOrEmpty(r.SalesmanCode)))` → `VanAnCard` trong Zone 4
  "Mã giới thiệu của bạn" + code + nút copy (`JSRuntime` clipboard —
  pattern `navigator.clipboard` đã dùng trong codebase?) — check trước,
  nếu không có pattern thì hiện code thôi.
- [x] **Guest branch**: thêm `<BackToStoreChip />` dưới nút Đăng nhập
  ("Tiếp tục mua tại {last store}") — component đã tồn tại, 1 dòng.
- [x] **Quick-access grouping**: 6 nút chia 2 nhóm có header nhỏ —
  "Mua sắm" (Đơn hàng · Lịch sử tích điểm · Cửa hàng) / "Tài khoản"
  (Nhiệm vụ · Bảo mật · Khiếu nại) — chỉ markup, không đổi flags.
- [x] Skeleton: `_loading` branch → 3 `placeholder-glow` skeleton cards
  (Bootstrap `placeholder` class — check pattern skeleton hiện có trong
  Home/index.html để mirror, không tự chế CSS).

### Phase D — Ship

- [x] `dotnet build` KhachLink 0 err · bUnit relevant tests PASS
- [x] Cập nhật/viết test: `ProfileContextCardBuilderTests` không đổi;
  thêm bUnit nhẹ nếu có harness render Profile (check `StoreFinderTests`
  pattern — nếu không có, note "UI-only, verify bằng E2E").
- [x] E2E spec `6_Testing/e2e-tests/profile-ux.spec.ts` (Gate 4 — UI layout
  thay đổi): hero có số điểm + progress · không còn bảng fraud · guest có
  chip · referral card khi có code.
- [x] guard-check ALL PASSED → commit → push → CD → RV probe (screenshot
  profile logged-in + guest trên `commienphi.timlathay.com`).
- [x] Update card + `project_state.md` (Python — KHÔNG PowerShell).

## OUT OF SCOPE

- "Cửa hàng của tôi" section — task `task_my_stores_profile.md` riêng.
- "Ưu đãi của tôi" voucher wallet đầy đủ — cần endpoint active-vouchers
  (wave sau; hiện chỉ có `voucher-expiring` context card).
- Cross-device merge, avatar upload, theme personalization.
- PWA install — ĐÃ CÓ (layout + PWAInstallPrompt + Home).
- Backend mới — card này 100% UI/reuse `/api/loyalty/my` hiện hữu.

## DECISIONS (đã chốt trong review — user duyệt card = duyệt luôn)

- D1: progress bar data từ `/api/loyalty/my` (server-truth), fallback
  `/api/customers/me` khi fail — KHÔNG hardcode ladder mới ở client ngoài
  mapping next-tier-name (mirror comment TierLadder).
- D2: fraud flags + raw device token = **remove khỏi customer UI** (security
  rationale — anti-fraud internals không phơi ra cho subject bị flag).
- D3: upgrade CTA single-source = context card `identity-upgrade` (đã có);
  xóa duplicate Zone 4.
- D4: guest mode = đăng nhập CTA + BackToStoreChip (không thêm my-stores —
  data server yêu cầu deviceId nhưng section đó thuộc card kia).
