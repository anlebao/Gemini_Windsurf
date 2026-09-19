# Task Card #181: Luồng đặt hàng — ẩn giỏ hàng tại thanh toán + hết giỏ trùng

> **Status:** ✅ CODE DONE + DEPLOYED + RV PRODUCTION PASS (`6ab09cbc` + `a620d1de`, 2026-09-19)
> **Priority:** P2 — UX
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 2-3h

## Problem (issue #181)

1. Sau khi chọn sản phẩm + đặt mua, ở bước thanh toán cần **ẩn giỏ hàng phía trên** — không thừa, không rối mắt.
2. Khi khách quét QR referral → chuyển thẳng tới form thanh toán, ở đó **tồn tại 2 giỏ hàng** — hỏi có phải duplicate code luồng order không.

## Root cause (VERIFIED)

- `KhachLinkLayout.razor:143-152` — floating `CartDrawer` **global** render trên MỌI trang khi `_navFlags.ShowCart && _cartState.Items.Count > 0` (bao gồm `/cart` và `/checkout`).
- `/checkout` (bước thanh toán): user thấy floating cart (nút "Thanh toán") + card "Tóm tắt đơn hàng" + header cart icon → 2-3 giỏ.
- `/cart`: list giỏ hàng + floating drawer trùng.
- Trả lời "duplicate code?": **không** — 2 entry (search-buy → Cart→Checkout; QR-referral → Scan add→Cart→Checkout) là cùng flow, chỉ khác điểm vào. Vấn đề là hiển thị cart bị trùng, không phải logic order trùng.

## Solution

### Phase A — Ẩn cart chrome trên trang có sẵn giỏ
- [ ] A1: `KhachLinkLayout.razor` — bỏ điều kiện render floating `CartDrawer` + header cart icon khi route hiện tại ∈ {`/cart`, `/checkout`} (check `NavigationManager.Uri` hoặc flag từ page).
- [ ] A2: Xử lý cả `/scan` sau khi add sản phẩm (khách vừa scan → thấy 1 giỏ tại /cart thay vì drawer ngay tại /scan).

### Phase B — UI thanh toán gọn
- [ ] B1: Checkout — đảm bảo "Tóm tắt đơn hàng" là nơi duy nhất hiển thị giỏ; không render thêm bất kỳ cart nào khác.
- [ ] B2: Sau khi đặt hàng thành công (màn hình chi tiết đơn) — xác nhận giỏ đã clear + không còn drawer.

### Phase C — Test
- [ ] C1: Playwright/visual: tại `/checkout` và `/cart` chỉ 1 giỏ (không có floating cart).
- [ ] C2: RV luồng referral: scan QR → add → đặt hàng → thanh toán → tracking: không còn giỏ trùng ở bất kỳ bước nào.

## Acceptance
- [ ] Không còn 2 giỏ tại /checkout + /cart · luồng QR referral sạch UI
