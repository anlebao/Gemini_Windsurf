# Coding Plan: KhachLink Customer Profile — Contextual UX v1

> **Task card:** `task_card.md` (same folder)

## Phase 1 — Context card builder (pure, testable)

Create `5_WebApps/KhachLink/Services/ProfileContext/ProfileContextCardBuilder.cs`
(+ `ProfileContextCard.cs`, `ProfileContextSnapshot.cs` DTOs in same folder).

- `ProfileContextSnapshot` — flat input DTO: profile fields (IdentityLevel, Tier,
  PointBalance, Birthday), push enabled flag, active order count / best active order,
  mission count near completion, loyalty/membership state.
- `ProfileContextCard` — output: `Id` (stable string), `Priority` (int, lower = higher),
  `Icon` (bootstrap icon class), `Title`, `Reason` (explainable one-liner),
  `Href` (destination), `CtaLabel`.
- `Build(snapshot)` → evaluates rules in fixed order, returns `IReadOnlyList` sorted
  by Priority, capped at 3. Fallback card ("Khám phá cửa hàng gần bạn" → `/stores`)
  when list is empty.

Rules v1 (priority order):

1. **ActiveOrder** — snapshot has order in Pending/Confirmed/Shipping → `/my-orders`
2. **PushOff** — push status known-off → `/profile` settings anchor (or enable inline)
3. **MissionNearDone** — mission progress ≥80% → `/missions`
4. **TierProgress** — pointBalance within next-tier threshold → `/my-loyalty`
5. **BirthdayMissing** — Birthday null → settings birthday field
6. **BirthdaySoon** — birthday within 14 days → `/campaigns`
7. **IdentityUpgrade** — IdentityLevel Social/Guest → `/profile` verify section
8. **LoyaltyEmpty** — zero point balance + has loyalty → `/rewards`

Rules are pure functions over the snapshot — no HTTP, no JS, no EF. Unit-testable.

## Phase 2 — Data loading (reuse existing APIs)

In `Profile.razor` `OnAfterRenderAsync(firstRender)` → `LoadProfileAsync`:

- Keep existing parallel loads: `/api/customers/me`, push status, PWA prefs,
  community roles/flags.
- Add parallel loads (best-effort, failure-tolerant):
  - `GET /api/customerorders?status=active&pageSize=1` (Gateway `CustomerOrdersController`
    forwards `X-Customer-Token` + `X-Customer-Device-Id`; verify exact status filter value
    against ShopERP order status enum before wiring — see Open Questions).
  - `GET /api/missions/my/progress` (verify exact route/shape used by `Missions.razor`).
- Map responses into `ProfileContextSnapshot`; call builder; render top 3 cards.

## Phase 3 — Page restructure

Rewrite `Profile.razor` markup into 4 zones (still inside `<KhachLinkLayout>`):

1. **Header card** — avatar initial, FullName, PhoneNumber, tier badge,
   PointBalance, IdentityLevel badge. Compact single card.
2. **"Việc cần làm"** — `@foreach` over `_contextCards` → `VanAnCard` with icon,
   title, reason text, `VanAnButton` → `Navigation.NavigateTo(card.Href)`.
   Section hidden entirely when logged out (existing login prompt preserved).
3. **"Truy cập nhanh"** — compact grid (VanAnButton variant or card links):
   Điểm thưởng `/my-loyalty` · Đơn hàng `/my-orders` · Ví `/qr/wallet` ·
   Nhiệm vụ `/missions` · Cửa hàng `/stores` · Khiếu nại `/disputes`.
   Respect `NavFlags` where the layout already gates these routes.
4. **"Cài đặt"** — collapsed `<details>` or existing collapsible pattern:
   push toggle, sound/vibrate prefs, birthday edit, security/identity,
   community roles + fraud flags (moved here verbatim — no data loss),
   device info (keep, but inside settings), logout button.

Preserve: `X-Customer-Token` header pattern, `customer_token` localStorage key,
login/logout flow, all existing JS interop (PWAService), `OnAfterRenderAsync`
interactive-render safety.

## Phase 4 — Tests

- Unit tests (KhachLink.Tests or existing test project — verify location):
  - ordering by priority, max-3 cap, empty → fallback card
  - each rule's trigger/non-trigger condition
  - birthday-within-14-days boundary (13 vs 15 days)
- E2E `6_Testing/e2e-tests/profile-contextual.spec.ts`:
  - logged-out `/profile` → login prompt, no context cards
  - logged-in → ≤3 cards, priority order respected
  - card CTA navigates to expected route
  - settings section collapsed by default, expands
  - mobile viewport smoke (375px)

## Phase 5 — Verify & ship

1. `dotnet build VanAn.sln` — 0 errors
2. `dotnet test` — new unit tests pass, no regressions
3. guard-check.ps1 + pre-push CI
4. Deploy → RV per `.devin/rules/runtime-verification.md` on live KhachLink
   instance (shopai.timlathay.com or diemthuong2.khachvip.online)

## Open questions — RESOLVED (Đợt 0, 2026-…)

1. **Order status** ✅ — `OrderStatusId` values: `pending`, `confirmed`, `preparing`,
   `ready`, `delivering`, `completed`, `cancelled` (Domain.cs OrderStatuses.Default).
   Endpoint only accepts ONE status string → fetch `pageSize=5` unfiltered +
   `X-Customer-Token` + `X-Customer-Device-Id` (localStorage `customer_device_id`),
   filter client-side in builder input. Active set = pending/confirmed/preparing/ready/delivering.
2. **Missions progress** ✅ — `GET /api/missions/my/progress` returns
   `List<MissionProgressDto>` with `IsCompleted`, `CompletionCount`, `DailyCap`,
   `IsOneTime`, `PointsReward` — NO progress %. Rule uses `!IsCompleted` count +
   max `PointsReward`.
3. **Test project** ✅ — `6_Tests/VanAn.Core.Tests` already references
   `VanAn.KhachLink` → tests at `6_Tests/VanAn.Core.Tests/KhachLink/`.
4. **NavFlags** ✅ — `KhachLinkLayout` cascades `NavFlags` (`Name="NavFlags"`);
   Profile.razor can add `[CascadingParameter(Name = "NavFlags")]` same as NavMenu.

## Deviations from plan (decided during Đợt 1)

- Tier-progress rule gated: only shows when `PointBalance >= nextThreshold/2`
  (avoids "còn 9.990 điểm" noise for low-engagement accounts).
- `PushEnabled == null` (status API failed) → no push card (fail-safe).
- Identity card fires for `Social`/`Guest`/`null` — mirrors `NeedsUpgrade`.
- Cards with `Href` starting `#` render as plain anchor → native scroll to
  `#profile-settings` section (birthday + push toggles live there).
