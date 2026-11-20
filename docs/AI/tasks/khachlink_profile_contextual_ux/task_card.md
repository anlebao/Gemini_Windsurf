# Task Card: KhachLink Customer Profile — Contextual UX v1

> **Status:** ⏳ DRAFT (awaiting IMPLEMENT)
> **Depends on:** Profile review + user-approved direction (rule-based, explainable, no ML/AI in v1)

## Objective

Giảm information overload trên `/profile` của customer trong KhachLink: thay vì hiển thị
tuyến tính ~10 sections, hiển thị **tối đa 2–3 "next best action" cards** được chọn từ
tín hiệu có sẵn trong session, + quick-access grid + cài đặt thu gọn.

## Current state (verified)

`5_WebApps/KhachLink/Pages/Profile.razor` renders linearly: header → menu items →
push toggle → notif prefs → birthday → identity upgrade → community roles →
fraud flags table → device info → logout. Loads in parallel:
`GET /api/customers/me`, `/api/notifications/push/status`, PWAService prefs,
`CommunityHttpService` roles/flags.

## Context signals → actions (rule engine)

| Signal | Source (existing) | Card action |
|---|---|---|
| `IdentityLevel` = Social/Guest | `/api/customers/me` | Nâng cấp xác thực |
| Active order (pending/shipping) | `/api/customerorders?status=...` | Theo dõi đơn |
| Push disabled | `/api/notifications/push/status` | Bật thông báo |
| `Birthday == null` | `/api/customers/me` | Nhập sinh nhật |
| Points near next tier | `tier` + `pointBalance` | "Còn X điểm lên hạng Y" |
| Mission in progress | `/api/missions/my/progress` | Xem mission |
| Alliance wallet / loyalty | `/api/loyalty/my`, AllianceWallet | Đổi điểm / xem ví |
| No signal | — | Neutral: khám phá cửa hàng gần bạn |

## Proposed structure

```
HEADER      — avatar + name + tier + points + identity badge (compact)
VIỆC CẦN LÀM — max 2–3 prioritized ContextCard (rule-sorted, explainable)
TRUY CẬP NHANH — compact grid: Điểm · Đơn · Ví · Nhiệm vụ · Cửa hàng · Khiếu nại
CÀI ĐẶT       — collapsed: push, sound/vibrate, birthday, security, logout
```

## Design constraints

- Pure, testable builder: `BuildContextCards(profile, orders, missions, pushStatus)`
  → `List<ContextCard>` sorted by priority. Stable IDs, icon, title, reason text,
  destination. Max 3 shown.
- No backend changes in v1 — reuse existing endpoints, parallel load.
- No ML/LLM — rules are explainable ("vì sao tôi thấy cái này").
- UI Platform components only (`VanAnCard`, `VanAnButton`, `VanAnAlert`...).
- Hide raw device-token; fraud flags → collapsed "advanced" section, user-friendly.
- Preserve all existing routes/auth/token behavior.

## Out of scope (v1)

ML model, LLM personalization, behavioral tracking DB, new domain entities/migrations,
auth redesign, `/api/customers/me/context` aggregation endpoint (defer to v2 if needed).

## Files

| File | Action |
|---|---|
| `5_WebApps/KhachLink/Pages/Profile.razor` | REWRITE layout into 4 zones |
| `5_WebApps/KhachLink/Services/ProfileContextCardBuilder.cs` (or similar) | NEW — pure rule engine |
| `5_WebApps/KhachLink/Services/Http/*` | reuse existing; possibly 1 small client for orders/missions |
| `6_Testing/e2e-tests/profile-contextual.spec.ts` | NEW — E2E (max cards, priority, logged-out, nav) |

## Verification

1. `dotnet build` 0 errors (KhachLink + sln)
2. Unit tests: rule ordering, max-3, empty-signal fallback, identity/push/birthday conditions
3. E2E: card count ≤3, priority order, action navigation, logged-out state
4. RV on live KhachLink instance after deploy (RV protocol — API → assets → Playwright → manual)

## Governance checklist

- [ ] KhachLink HTTP-only — no DB access, all data via existing HTTP endpoints
- [ ] Domain layer untouched
- [ ] UI Platform components only
- [ ] No new .csproj
- [ ] Multi-tenancy preserved (all endpoints already tenant-scoped)
- [ ] E2E added for UI layout change (Gate 4)
