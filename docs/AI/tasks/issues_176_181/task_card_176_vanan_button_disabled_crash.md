# Task Card #176: VanAnButton `disabled` render crash (WASM)

> **Status:** ✅ **ISSUE CLOSED 2026-09-19** — RV PRODUCTION PASS (store page 0 crash errors, sweep deployed `6ab09cbc` + `a620d1de`)
> **Priority:** P3 (đóng issue) — fix gốc đã có trong HEAD
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 0.5-1h (RV + sweep)

## Problem

Browser error trên `https://diemthuong2.khachvip.online/store/by-id/00000000-0000-0000-0000-000000000001`:
```
Unhandled exception rendering component: Unable to set property 'disabled' on object of type 'VanAn.UI.Platform.Components.Atomic.VanAnButton'. Arg_InvalidCastException
```

## Root cause (VERIFIED)

`UI.Platform/Components/Realtime/RealtimeChatPanel.razor` (bản trước fix) truyền **literal string** `disabled="_loadingHistory"` (thiếu `@`) → Blazor match parameter case-insensitive tới `BaseComponent.Disabled` (bool — `UI.Platform/Components/Base/BaseComponent.razor:21`) → string→bool cast fail → WASM crash. Nút "Gửi" có latent bug tương tự (`disabled="@_tokenExpired"` lowercase).

## Evidence

- Fix commit: `c76c0b4d` (2026-09-18 23:20 +0700) — "P6 manual-test round 2 — refresh button crash + Enter-to-send".
- Issue #176 tạo 2026-09-18 23:06 +0700 → **fix 14 phút sau, đã trong HEAD/origin/main `79a8f39e` + CD deploy**.
- `git merge-base --is-ancestor c76c0b4d HEAD` = TRUE.

## Solution

### Phase A — RV production (đóng issue)
- [ ] A1: Mở `https://diemthuong2.khachvip.online/store/by-id/00000000-0000-0000-0000-000000000001` (browser console) → xác nhận không còn lỗi `disabled`/InvalidCastException; chat panel render đầy đủ (nút refresh + nút Gửi).
- [ ] A2: Kiểm tra WASM bundle deployed chứa fix (RCL `_content/VanAn.UI.Platform/...` mới).
- [ ] A3: Comment kết quả + đóng issue #176.

### Phase B — Sweep residual (phòng tái phát)
- [ ] B1: Đổi lowercase `disabled="@..."` → `Disabled="..."` (PascalCase) tại: `UI.Platform/Components/VanAOrderTable.razor:86,92` · `UI.Platform/Components/VanAStatusForm.razor:103` · `UI.Platform/Components/VanAStaffForm.razor:74` · `5_WebApps/KhachLink/Pages/Checkout.razor:255`.
- [ ] B2: Grep toàn repo `VanAnButton[^>]*disabled=` → 0 match.
- [ ] B3: Build sln 0 errors + guard PASS.

## Acceptance
- [ ] RV store page không crash · issue #176 closed · sweep sạch
