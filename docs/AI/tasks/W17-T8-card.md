# W17-T8 — Update project_state.md

**Wave:** 17 — KhachLink Retention & Loyalty
**Master plan:** `docs/AI/tasks/KHACHLINK_RETENTION_PLAN.md` § T8
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟢 LOW — housekeeping, không block merge
**Conflict risk:** NONE
**Depends on:** W17-T7 PASS (tất cả scripts green)
**Estimated effort:** 0.1 session

---

## Mục tiêu

Sau khi W17-T7 xác nhận Wave 17 hoàn chỉnh, cập nhật `docs/AI/project_state.md` để:
1. AI context sessions tiếp theo load đúng trạng thái
2. Không lặp lại analysis đã làm
3. Ghi lại các quyết định kiến trúc quan trọng của Wave 17

---

## Nội dung cần cập nhật

### Section: Current Wave
```markdown
## Current Wave
**Wave 17 — KhachLink Retention & Loyalty** → COMPLETE ✅
**Wave 18** → PLANNING
```

### Section: Completed Features (thêm vào list)
```markdown
## Completed — Wave 17
- [x] Customer Identity: Phone OTP login, zero-friction → upgrade flow
- [x] CustomerToken: IDataProtector JWT-lite (30 ngày), X-Customer-Token header
- [x] OTP Service: IMemoryCache TTL 5 phút, dry-run mode (X-Dev-OTP header)
- [x] Loyalty Dashboard: /my-loyalty — tier badge, point balance, progress bar, history 20 entries
- [x] Order History: /my-orders — filter tabs, pagination, link đến OrderTracking
- [x] PWA Bug Fixes: IAsyncDisposable, dismiss persist, CSS transition, CancellationToken
- [x] Push Subscription: POST /api/notifications/push/subscribe endpoint
- [x] Store Finder: /stores — Shop.Latitude/Longitude (approved Domain change), Haversine sort
- [x] Google Maps: API key từ config (không còn AIzaSyDummyKey)
- [x] NavMenu: 6 retention items + mobile bottom tab bar, scaffold items removed
- [x] EF Migration: AddShopCoordinates
```

### Section: Architecture Decisions (thêm mới)
```markdown
## Architecture Decisions — Wave 17

| Decision | Lý do | Alternative rejected |
|---------|-------|---------------------|
| CustomerToken = IDataProtector (không phải JWT) | Tránh JWT library mới, không cần Identity infrastructure | ASP.NET Identity — quá nặng cho customer-facing |
| OTP storage = IMemoryCache (không phải DB table) | TTL built-in, không cần migration, OTP là transient data | Redis — over-engineering cho quy mô hiện tại |
| Tier calc on-the-fly từ PointBalance | Domain không cần biết tier rules, rules thay đổi không cần migration | Persist CustomerTier trong DB — stale data risk |
| Shop.Latitude/Longitude trên entity (không phải ShopConfig) | Store Finder cần query địa lý từ DB | ShopConfig only — không queryable |
| Push subscription lưu tạm trong log (Wave 17) | Domain field PushSubscriptionJson chờ Wave 18 approve | Tạo bảng mới ngay — premature |
| NavMenu mobile = bottom tab bar | UX pattern chuẩn cho mobile PWA | Side drawer — che nội dung |
```

### Section: Known Tech Debt (thêm Wave 17 items)
```markdown
## Tech Debt — Wave 17 → Wave 18

- [ ] **W18-TD1:** `Customer.PushSubscriptionJson` field chưa có trong Domain → push notification chưa hoạt động end-to-end
- [ ] **W18-TD2:** `LoyaltyRewards.History` là JSON blob string → không queryable (không filter/sort trên DB). Cần migrate sang `LoyaltyHistoryEntry` entity riêng trong Wave 18
- [ ] **W18-TD3:** Tier rules hardcode trong `LoyaltyController.CalcTier()` switch expression → cần config-driven (Wave 18: `LoyaltyTierConfig` table hoặc appsettings section)
- [ ] **W18-TD4:** `CustomerOrdersController` query chỉ match `CustomerId` — không fallback `CustomerDeviceId` khi user chưa upgrade identity
- [ ] **W18-TD5:** `GoogleMaps.razor` vẫn hiển thị 1 shop trên existing component — `StoreFinder.razor` là page riêng. Cần merge hoặc refactor để tránh duplication
```

### Section: Next Actions
```markdown
## Next Actions — Wave 18 (Roadmap)

Priority tiers:
1. **W18-TD1** — Customer.PushSubscriptionJson + VAPID setup → push notification hoạt động thật
2. **W18-TD2** — LoyaltyHistoryEntry entity riêng → queryable history
3. Referral program — customer refer → bonus points
4. Analytics dashboard (ShopERP) — customer behavior, order trends
5. Seasonal campaigns — limited-time offers, flash sales
```

### Section: Last Updated
```markdown
**Last Updated:** [session date]
**By:** Wave 17 completion
**Next review:** Before Wave 18 kickoff
```

---

## Entry criteria
- [ ] W17-T7 PASS — tất cả scripts green, manual checklist 10/10

## Success criteria
- [ ] `project_state.md` Section "Current Wave" → Wave 18 planning
- [ ] Tất cả 11 Wave 17 completed features được ghi lại
- [ ] 6 architecture decisions documented
- [ ] 5 tech debt items cho Wave 18 được ghi lại
- [ ] Last Updated timestamp cập nhật
