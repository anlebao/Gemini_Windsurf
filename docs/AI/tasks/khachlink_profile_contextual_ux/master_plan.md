# Master Plan: KhachLink Customer Profile — Contextual UX v1

> **Task card:** `task_card.md` · **Coding plan:** `coding_plan.md`

## Mục tiêu sản phẩm

Customer mở `/profile` trên KhachLink PWA thấy ngay **điều họ cần làm tiếp theo**
(1–3 hành động có ngữ cảnh), thay vì một trang dài liệt kê mọi tính năng.
Trang vẫn giữ đủ chức năng — phần ít dùng chuyển vào mục cài đặt thu gọn.

## Tại sao rule-based trước, không phải AI

- Tín hiệu cần thiết **đã có sẵn** trong session: profile, đơn hàng, missions,
  loyalty, push status, sinh nhật, identity level.
- Rule engine = deterministic, explainable ("vì sao hiện card này"), test được
  bằng unit test, zero latency, zero chi phí API.
- AI/ML cá nhân hóa là **v2** — chỉ nên thêm khi rule-based chứng minh thiếu
  (vd: engagement thấp dù đủ tín hiệu). Endpoint tổng hợp
  `/api/customers/me/context` cũng là v2 nếu multi-call client-side không ổn.

## Nguyên tắc thiết kế

1. **Tối đa 3 action cards** — nhiều hơn = lại overload.
2. **Mỗi card giải thích được** — `Reason` text nói rõ lý do hiển thị.
3. **Không mất chức năng** — mọi route/capability hiện có vẫn truy cập được
   qua quick-access grid hoặc cài đặt.
4. **Fail-safe** — mọi API phụ (orders, missions) best-effort: lỗi → bỏ qua
   tín hiệu đó, profile chính vẫn render.
5. **Privacy** — device token, fraud flags không còn prominent; chỉ trong
   mục cài đặt nâng cao.

## Phạm vi

### Trong scope (v1)

- Rewrite layout `Profile.razor` thành 4 zones.
- `ProfileContextCardBuilder` pure service + unit tests.
- Tải thêm orders + missions progress (best-effort, song song).
- Ẩn/thu gọn device info + fraud flags vào cài đặt.
- E2E spec mới.

### Ngoài scope (defer)

- `/api/customers/me/context` aggregation endpoint (v2).
- ML/LLM personalization (v2+).
- Behavioral tracking / new telemetry.
- Domain entity/migration changes.
- Redesign các trang khác (orders, wallet, missions...).

## Rủi ro & giảm thiểu

| Rủi ro | Giảm thiểu |
|---|---|
| Multi-call chậm (thêm 2 API) | Load song song, timeout ngắn, fail → bỏ tín hiệu |
| Order status filter sai → card sai | Verify status strings ở Phase 2 trước khi wire; fallback filter client-side |
| Mất chức năng khi collapse settings | Checklist diff toàn bộ element hiện tại trước khi rewrite |
| Blazor hydration/interactivity regression | Giữ `OnAfterRenderAsync(firstRender)` pattern; build + E2E verify |
| Card sai ngữ cảnh (vd push card khi user không quan tâm) | Rule đơn giản, priority rõ ràng; user feedback loop ở v2 |

## Tiêu chí hoàn thành

- [ ] `/profile` render 4 zones đúng thứ tự
- [ ] ≤3 context cards, sort đúng priority, có reason text
- [ ] Quick-access grid điều hướng đúng routes
- [ ] Settings collapsed chứa đủ: push, prefs, birthday, security,
      community, fraud flags, device, logout
- [ ] Logged-out state giữ nguyên login prompt
- [ ] Unit tests pass; E2E spec pass
- [ ] Build 0 errors; RV trên prod OK

## Related

- Precedent task-card format: `docs/AI/tasks/khachlink_profile_transition_ux/`
- Profile page: `5_WebApps/KhachLink/Pages/Profile.razor`
- Gateway order forward: `2_Gateway/Controllers/CustomerOrdersController.cs`
