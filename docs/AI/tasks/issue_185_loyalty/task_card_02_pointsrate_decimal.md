# Task Card 185-2: Tỷ lệ tích điểm (%) cho phép nhập 0.01

> **Status:** DONE 2026-09-23 — commit 8a47f40a — Option C (no Domain change): step 0.0001 + hint rõ 0.01%; rate giờ sync được lên PG nhờ Card 03
> **Priority:** P2
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** ~30min (UI hint only)

## Problem

`/admin/loyalty-config` → "Tỷ lệ tích điểm (%)" chỉ nhận số nguyên ≥1. User muốn nhập **0.01** (%) để thu nhỏ điểm thưởng trên doanh số đơn — 1% hiện tại là quá lớn.

## Root cause (VERIFIED — file:line)

- `Domain.cs:2310` — `LoyaltyGlobalConfig.PointsRate` là **`int`** percent (1 = 1% giá trị đơn).
- `Domain.cs:2343-2352` — `UpdatePointsFormula` validate `pointsRate < 0 || pointsRate > 100` → `ArgumentException`.
- `LoyaltyConfigAdmin.razor:62-65` — `int.TryParse` + `Math.Clamp(n, 0, 100)` + `Step="1"`.
- Consumers chia /100 đúng chuẩn: `Gateway/LoyaltyController.cs:77` (`globalConfig.PointsRate / 100m`).

**HARD STOP đã resolve:** KHÔNG đổi Domain (decision 2026-09-23).

## Solution (APPROVED — Option C)

Per-tenant `Loyalty_PointsRate` (`IShopFeatureSettingsService.cs:47`, decimal 0–1) tại `/settings/shop-features` **đã hỗ trợ 0.0001 (=0.01%)** — input `step="0.001"`, manual typing cho giá trị nhỏ hơn.

- [ ] A1: Update help text tại `ShopFeatures.razor` loyalty-rate field: ghi rõ "0.0001 = 0.01% (1 điểm / 1.000.000đ)". Để trống/0 = dùng global.
- [ ] A2: Update help text tại `LoyaltyConfigAdmin.razor` global field: "Rate toàn cục chỉ nhận % nguyên; để set 0.01% cho 1 tenant, dùng 'Tỉ lệ tích điểm' trong /settings/shop-features của tenant đó."
- [ ] A3 (phụ thuộc): per-tenant rate chỉ có tác dụng trên Gateway khi **Card 03-sync** chạy (settings PG/SQLite hiện drift — `GetSettingsAsync` Gateway đọc PG).

## Acceptance
- [ ] Tenant set `Loyalty_PointsRate=0.0001` → estimate/award dùng 0.01% (sau khi card 03-sync deployed)
- [ ] Help text chỉ rõ cách nhập 0.01%
