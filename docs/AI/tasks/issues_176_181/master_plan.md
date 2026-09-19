# Master Plan — Issue Batch #176 → #181 (2026-09-19)

**Created:** 2026-09-19
**Status:** Batch 1 ✅ FIXED + DEPLOYED + RV PRODUCTION PASS 2026-09-19 (`49d07bd5` + `7a32a9c7`): #180 200/slug chuẩn hóa · #178 201 rate=0 · #177 deploy WASM (UI test pending). Batch 2 pending.
**Branch target:** `main`
**Source:** 6 GitHub issues mở ngày 2026-09-18/19 (anlebao/Gemini_Windsurf)

## Summary

| Issue | Title | Verify kết quả | Root cause (verified file:line) | Severity |
|---|---|---|---|---|
| #176 | VanAnButton `disabled` render crash (WASM) | ✅ **ĐÃ FIX + ĐÃ DEPLOY** (`c76c0b4d`, trong HEAD `79a8f39e`) | `RealtimeChatPanel.razor` truyền `disabled="_loadingHistory"` (literal string, thiếu `@`) → match case-insensitive `BaseComponent.Disabled` (bool) → InvalidCastException | Cao (WASM crash) — cần RV đóng issue |
| #177 | Đơn hàng free/charity bị lỗi | ⚠️ 2 gap code xác định + cần RV bắt lỗi chính xác | (a) `Scan.razor:289-299` bỏ qua `IsFree` từ `ReferralScanDto`; (b) product QR fast-path `Scan.razor:372` skip giá 0 + ShopERP product API không có `ProductType` → free/charity không featured → Tier 0 từ chối | Cao — chặn đặt hàng free/charity |
| #178 | Referral config + QR list | ✅ Phần 1 verified; phần 2 = feature mới | `Domain.cs:4400/4414` — `ProductReferralConfig.CommissionRate` bắt buộc 0.02–0.05 → rate=0 (free/charity) → `ArgumentOutOfRangeException` → 400 | Trung bình (config) + Feature |
| #179 | Quick-setup không lưu menu/product | ❓ Cần RV production (app2) | Wizard success screen dùng số **hardcoded** từ template (`QuickSetup.razor:468-470`); exception bị nuốt (`QuickSetup.razor:475-478` chỉ Console.WriteLine) → UI báo thành công dù seed fail | Trung bình — onboarding bị vỡ |
| #180 | Verify Pending Tenant → 400 | ✅ **Root cause xác định** | `TenantOnboardingService.cs:231-234` — `Slugify()` giữ dấu tiếng Việt (`.NET \w` = Unicode) → `Tenant.UpdateSlug` regex `^[a-z0-9-]+$` (`Tenant.cs:285`) ném `ArgumentException` → không được controller catch → `UnifiedErrorHandler` → **400** | Cao — chặn toàn bộ crawl-onboard |
| #181 | Luồng đặt hàng: 2 giỏ hàng | ✅ Root cause xác định | `KhachLinkLayout.razor:143-152` — floating `CartDrawer` global hiển thị trên MỌI trang (kể cả /cart + /checkout) → trùng giỏ | Trung bình — UX |

## Verify log (2026-09-19)

### #176 — VanAnButton `disabled` crash
- Error: `Unable to set property 'disabled' on object of type 'VanAn.UI.Platform.Components.Atomic.VanAnButton'` — InvalidCastException khi render WASM.
- Cơ chế: Razor attribute `disabled="_loadingHistory"` (KHÔNG có `@`) → value = literal string `"_loadingHistory"` → Blazor match parameter case-insensitive (`BindingFlags.IgnoreCase`) tới `BaseComponent.Disabled` (bool, `BaseComponent.razor:21`) → string→bool cast fail.
- Fix đã tồn tại: `c76c0b4d` (2026-09-18 23:20 +0700) — sửa `RealtimeChatPanel.razor` dùng `Disabled` (PascalCase). **Issue #176 tạo lúc 23:06 +0700 — fix 14 phút sau, đã trong HEAD/origin/main `79a8f39e` + CD deploy.**
- Residual (chưa fix, hiện không crash vì value là bool): `VanAOrderTable.razor:86,92`, `VanAStatusForm.razor:103`, `VanAStaffForm.razor:74`, `Checkout.razor:255` vẫn dùng lowercase `disabled="@..."` — nên đổi PascalCase cho nhất quán + chống tái phát.

### #177 — Free/Charity order
- Gateway đã xử lý server-side (D9 fix `96e733a6`): Tier 0/1 dùng `FeaturedProducts.ProductType` làm authoritative (`PublicOrdersController.cs:127-148, 191-217`).
- **Gap A** — `Scan.razor:289-299` (`ProcessReferralCodeAsync`): build `ProductDto` từ `ReferralScanDto` (có field `IsFree` — `CommunityHttpService.cs:784`, Gateway populate đúng `SalesmanService.cs:472`) nhưng **KHÔNG gán `IsFree`/`ProductType`** → cart item `IsFree=false` → nếu sản phẩm không nằm trong FeaturedProducts → Tier 0 reject "giá không hợp lệ"; kể cả khi featured, UI C2/C3 (ẩn payment selector, bước donation) hỏng.
- **Gap B** — product QR flow: fast-path `Scan.razor:372` `if (qrPayload.UnitPrice > 0)` loại QR giá 0 → legacy path `GetProductsAsync` (ShopERP API không có ProductType) → `IsFree=false` → free/charity không featured → Tier 0 reject.
- **Cần RV**: issue body trống — reproduce trên production để bắt lỗi thực tế reporter gặp (xác nhận path nào).

### #178 — Referral config
- `ProductReferralConfig` constructor + `Update` (`Domain.cs:4400-4415`): `CommissionRate` phải 0.02–0.05 → free/charity cần rate=0 → `ArgumentOutOfRangeException` → controller `ProductReferralConfigController.cs:50-52` → **400** → save fail. Đúng y như issue.
- Phần 2 (feature): QR composite là **deterministic** (`SalesmanService.cs:148-149`: `$"{SalesmanCode}|{ProductShortCode}"`) → "lưu lại QR" không cần storage mới, chỉ cần trang list "Gian hàng của tôi" cho salesman (list sản phẩm đã config + QR + add/remove + link đồng bộ giá).

### #179 — Quick-setup
- Flow: `QuickSetup.razor:442-484` → in-process `OnboardingService.ApplyTemplateAsync(templateId, shopId)` → seed Products/Ingredients/Recipes/Inventories vào `IVanAnDbContext` (**= ShopERPDbContext SQLite** trong ShopERP — `Program.cs:152-154`) → đúng chỗ products cư trú (Option C).
- Vấn đề: (a) exception bị nuốt `catch { Console.WriteLine }` + (b) màn hình thành công dùng số hardcoded từ template metadata (`QuickSetup.razor:468-470`) — UI báo thành công ngay cả khi seed fail/blank.
- Cần RV trên app2: chạy quick-setup cho tenant test → check SQLite rows (`Products`/`Ingredients`) + ShopERP container logs (`ApplyTemplateAsync` log line `OnboardingService.cs:96-98, 135-138`).

### #180 — Verify Pending Tenant 400
- Chain verified: `VerifyAsync` (`TenantOnboardingService.cs:231-234`) → `Slugify(tenant.Name)` giữ dấu TV (`Regex.Replace(..., @"[^\w\s-]", "")` — .NET `\w` = Unicode letters) → slug "quán-cà-phê" → `Tenant.UpdateSlug` (`Tenant.cs:285`) `^[a-z0-9]+(?:-[a-z0-9]+)*$` → **`ArgumentException`** → controller chỉ catch KeyNotFound(404)/InvalidOperation(409) → rơi vào `UnifiedErrorHandler` (`Middleware/UnifiedErrorHandler.cs:82-84`) → **400 Bad Request**.
- Hầu hết tenant crawl (tên tiếng Việt có dấu) đều dính → chặn toàn bộ verify pipeline.
- Secondary: `SendAndReadAsync` (`GatewayAdminApiClientBase.cs:97-102`) không đọc body lỗi → user chỉ thấy "400 (Bad Request)" vô nghĩa.

### #181 — Order flow: 2 giỏ hàng
- `KhachLinkLayout.razor:143-152`: floating `CartDrawer` global render khi `ShowCart && Items.Count > 0` trên **mọi trang** — kể cả `/cart` (đã có list giỏ) và `/checkout` (bước thanh toán) → người dùng thấy 2 giỏ.
- Trả lời câu hỏi "duplicate code luồng order?": không — 2 entry khác nhau (search-buy vs QR-referral) là bình thường, nhưng hiển thị giỏ bị trùng.

## Fix plan (đề xuất thứ tự triển khai)

### Batch 1 — Fix nhanh, độc lập (1 session)
1. **#180** (P0 — chặn crawl-onboard):
   - `TenantOnboardingService.Slugify` → strip diacritics (NFD normalize + bỏ combining marks) + fallback `tenant-{guid}`.
   - Controller catch `ArgumentException` → `BadRequest(ex.Message)` để user thấy lý do.
   - Client `SendAndReadAsync` đọc body lỗi khi non-success (surface message thay vì HttpRequestException chung).
   - Test: Slugify("Quán Cà Phê") → "quan-ca-phe".
2. **#178** (P1 — config free/charity):
   - Domain `ProductReferralConfig` (IMPLEMENT mode — cần approval): range `CommissionRate` 0.00–0.50 (issue đề xuất default 0.01), `AppInstallBonus` >= 0.
   - Update UI form default + validation range + update tests `ProductReferralConfigServiceTests`.
3. **#177** (P1 — order free/charity):
   - `Scan.razor` truyền `IsFree` (+ `ProductType` khi có) từ `ReferralScanDto` vào `ProductDto`.
   - Product-QR legacy path: enrich `IsFree` từ catalog khi giá 0 (hoặc Gateway Tier 0 fallback: item `UnitPrice=0` + client `IsFree=true` + KHÔNG có FeaturedProduct → accept, log warning — cần duyệt tradeoff spoof).
   - RV production để bắt error path còn lại (nếu có).

### Batch 2 — UX + Feature (1-2 sessions)
4. **#181** (P2): layout ẩn floating cart + header cart icon trên `/cart`, `/checkout` (và `/scan` sau khi add) — qua route check hoặc flag CascadingValue.
5. **#178-phần2** (P2, feature): trang "Gian hàng của tôi" cho salesman — list sản phẩm referral đã config + QR (download), add/remove config, link "đồng bộ giá" (khi ProductRefConfig hiển thị giá hiện tại từ catalog).
6. **#179** (P2): RV app2 → tìm root cause thật; fix UI hiển thị lỗi seed (không nuốt exception); bỏ số hardcoded → đọc DB thật; fix root cause tìm được.
7. **#176** (P3 — đóng issue): RV production diemthuong2 `/store/by-id/...` xác nhận hết crash → đóng issue; sweep lowercase `disabled` → `Disabled` (PascalCase) 4 chỗ residual.

## Hard stop checks
- **#178 domain change** cần approval (Domain Modification By Mode: IMPLEMENT + user approval).
- **#177 Gateway Tier 0 fallback** có tradeoff spoof — cần duyệt trước khi chấp nhận client `IsFree` cho product không featured.
- **#179** không sửa nếu chưa có bằng chứng runtime (Gate 1: Anti-Guessing).

## Acceptance criteria
- [ ] Build full sln 0 errors · guard-check.ps1 PASS · Core.Tests PASS sau mỗi batch
- [ ] #176: RV store page ổn định + đóng issue
- [ ] #177: đặt mua free + charity (cả 3 path: store page, product QR, referral QR) tạo được đơn, C2/C3 UI đúng
- [ ] #178: lưu config rate=0 OK; salesman có trang list QR
- [ ] #179: quick-setup seed thật vào SQLite, product list hiển thị, UI báo lỗi nếu fail
- [ ] #180: verify Pending tenant tên tiếng Việt thành công (slug chuẩn hóa)
- [ ] #181: không còn 2 giỏ tại /checkout + /cart
