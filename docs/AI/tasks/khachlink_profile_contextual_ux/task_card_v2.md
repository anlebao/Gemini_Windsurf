# Task Card v2: KhachLink Profile Contextual UX — Aggregation + Dismiss + Personalization

> **Status:** ✅ DONE — deployed + RV PASS (2026-11-20, commits `31622ddb` `8cf324f5` `ca5942e9`, CD `44771304822`)
> **Depends on:** v1 deployed + RV PASS (`50cd0627`, CD `44739587449`)
> **Scope approved by user 2026-11-20:** all 3 items — aggregation endpoint, dismiss persistence, personalization signals.

## Objective

1. **Aggregation endpoint** `GET /api/customer-context/me` (ShopERP) — 1 call thay 4 call song song (profile/orders/missions/push) trên `/profile`.
2. **Dismiss-card persistence** — customer ẩn context card, lưu server-side (cross-device), TTL 7 ngày.
3. **Personalization signals** — thêm voucher sắp hết hạn + win-back (lâu không ghé), vẫn rule-based/explainable.

## Architecture

```
KhachLink Profile.razor
  └─ GET /api/customer-context/me  (X-Customer-Token + X-Customer-Device-Id)
       └─ Gateway CustomerContextController (forward)
            └─ ShopERP CustomerContextController
                 ├─ ICustomerTokenService.ValidateToken
                 ├─ ICustomerRepository.GetByIdAsync
                 ├─ ILoyaltyRewardsService + LoyaltyReadRouter (balance/tier)
                 ├─ ShopERPDbContext.Orders (active count + last order)
                 ├─ IMissionService (progress + completions)
                 ├─ IPushSubscriptionRepository (push enabled)
                 ├─ IRedemptionService.GetCustomerVouchersAsync (expiring ≤7d)
                 └─ CustomerContextDismissals DbSet (dismissed card IDs)
  └─ POST /api/customer-context/me/dismiss { cardId }  → upsert dismissal (ExpiresAt = now+7d)
  └─ DELETE /api/customer-context/me/dismiss/{cardId}  → restore (best-effort)
```

## New/changed files

| File | Action |
|---|---|
| `3_CoreHub/Infrastructure/Entities/CustomerContextDismissalEntity.cs` | NEW — BaseEntity, fields CustomerId/CardId/DismissedAt/ExpiresAt |
| `3_CoreHub/Infrastructure/Configurations/CustomerContextDismissalConfiguration.cs` | NEW — IEntityConfiguration (unique idx TenantId+CustomerId+CardId) |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | + DbSet<CustomerContextDismissalEntity> |
| `5_WebApps/ShopERP/Migrations/*AddCustomerContextDismissals*` | NEW — EF migration |
| `5_WebApps/ShopERP/Controllers/CustomerContextController.cs` | NEW — GET /me, POST /me/dismiss, DELETE /me/dismiss/{cardId} |
| `5_WebApps/ShopERP/Services/LoyaltyTierCalculator.cs` | NEW — shared CalcTier (refactor 2 private copies) |
| `2_Gateway/Controllers/CustomerContextController.cs` | NEW — forward to ShopERP |
| `5_WebApps/KhachLink/Services/ProfileContext/ProfileContextSnapshot.cs` | + DismissedCardIds, ExpiringVoucherCount, VoucherDaysLeft, DaysSinceLastOrder, HasPastOrders |
| `5_WebApps/KhachLink/Services/ProfileContext/ProfileContextCardBuilder.cs` | + voucher-expiring (pri 15), win-back (pri 70), dismissal filter |
| `5_WebApps/KhachLink/Pages/Profile.razor` | 1-call context load + fallback legacy + dismiss button |
| `6_Tests/VanAn.Core.Tests/KhachLink/ProfileContextCardBuilderTests.cs` | + new rule tests |
| `6_Testing/e2e-tests/profile-contextual.spec.ts` | + dismiss + aggregation checks |

## Rules added (builder)

| Card | Id | Priority | Trigger |
|---|---|---|---|
| Voucher sắp hết hạn | `voucher-expiring` | 15 | `ExpiringVoucherCount > 0` → `/my-loyalty`, "Voucher của bạn sắp hết hạn — dùng ngay" |
| Win-back | `win-back` | 70 | `HasPastOrders && DaysSinceLastOrder >= 30 && ActiveOrderCount == 0` → `/campaigns` |

Dismissal: cards whose `Id ∈ snapshot.DismissedCardIds` are filtered BEFORE the max-3 cap. Fallback `discover-stores` also dismissible → zone may render empty (section hidden when 0 cards).

## Constraints

- Server endpoint best-effort per section: một section fail → trả default (không 500 toàn bộ).
- Dismissal TTL 7 ngày (`ExpiresAt`); re-dismiss refreshes.
- Không đổi Domain.cs (entity mới ở CoreHub.Infrastructure.Entities theo precedent ShopFeatureSettingsEntity).
- KhachLink vẫn HTTP-only; fallback legacy multi-call nếu context endpoint lỗi.
- Migration additive only (bảng mới).

## Verification

- `dotnet build` 0 errors; Core.Tests builder tests pass.
- RV: dev-token window → `/api/customer-context/me` 200 shape đúng → `/profile` 1 call → dismiss → reload vẫn ẩn → sau TTL quay lại (không test TTL trên prod, unit test cover).
