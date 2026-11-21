# Task Card: KhachLink AI/Chat UI Unification — P0 + P1 + P2

> **Status:** ✅ **P0 DONE + RV 3/3** (`ece7e0e5`, CD `44895953223`) · ✅ **P1 DONE + DEPLOYED `efecb6b6` + CD `44901852483` SUCCESS + RV prod PASS 4/4** (2026-11-21, `playwright-rv-chatui.config.ts` trên An Tâm `e2d91cdf`) — P2 pending
> **Depends on:** Review 2026-11-21 (xem dưới) — user approved direction
> **Scope:** `UI.Platform/Components/Ai/AiChatWidget.razor(.css)` · `UI.Platform/Components/Realtime/RealtimeChatPanel.razor(.css)` · `UI.Platform/Components/Realtime/VoiceCallPanel.razor(.cs/.css)` · `5_WebApps/KhachLink/Pages/Store.razor` (mount params only)

## 1. Objective

Đồng bộ hoàn toàn trải nghiệm chat trong `AiChatWidget` (1 bubble, 2 mode: **Trợ lý AI** / **Chủ quán**) và voice call UI — hiện 2 mode dùng 2 hệ visual khác nhau (custom `vanan-ai-*` vs RealtimeChatPanel Bootstrap), double header, font-size rải rác, scroll direction ngược nhau, call UI thiếu timer/waveform.

Mục tiêu: **1 design system chat duy nhất** — user switch mode không cảm thấy "đổi app".

## 2. Verified current state (đã đọc code)

| Thành phần | File | Trạng thái |
|---|---|---|
| Floating bubble | `AiChatWidget.razor.css:3-26` | 56px tròn `bi-robot`, z-1050, right/bottom 16px, badge unread tĩnh, không safe-area |
| Mode tabs | `AiChatWidget.razor:46-64` | Segmented tabs AI/Chủ quán — giữ nguyên |
| AI messages | `AiChatWidget.razor:107` | **Newest-first** (`for i=count-1..0`) — ngược convention chat |
| Human messages | `RealtimeChatPanel.razor:102` | Oldest-first + bubble `bg-primary`/`bg-white`, không avatar, timestamp riêng |
| Double header | `AiChatWidget.razor:22-41` + `RealtimeChatPanel.razor:23-59` | Widget header (tên+chevron) + chat header (tên+call+refresh+conn badge) chồng nhau |
| Input | AI: pill+icon send (`:182-200`) · Human: VanAInput+mic+nút "Gửi" text (`RealtimeChatPanel.razor:157-181`) | Khác nhau |
| Font sizes | bubble 0.9rem · chip 0.8rem · voice-panel **13px hardcode** · sub 0.78rem · badge 0.65rem · chat ~1rem | Không có scale |
| Voice call | `VoiceCallPanel.razor` card 38px avatar gradient; in-call expand full-frame (Zalo-like, `RealtimeChatPanel.razor.css:75-127`) | Thiếu **call timer**, waveform; incoming chưa đủ nổi bật; **đóng bubble = mất incoming call UI** |
| Call entry | Header phone icon trong RealtimeChatPanel (`:28-37`) | Chỉ có ở Human mode; AI mode không có voice call (by design — AI là text+STT) |

## 3. Requirements per phase

### P0 — Design system unification (impact lớn nhất, risk thấp)

- [x] **R1. CSS vars chung** — `--chat-*` tokens trên `.vanan-ai-widget` + `.chat-panel` root (scoped CSS không share :root → duplicate có chủ đích)
- [x] **R2. Single header** — `ShowHeader` param (default `true`); `false` → slim toolbar giữ call+refresh+conn badge, không title. Store.razor truyền `false`
- [x] **R3. Unified bubbles** — `.chat-msg`/`.chat-bubble--own|--peer` + `.chat-avatar` + `.chat-meta` thay markup ad-hoc; `KnowledgeSuggestButton` giữ nguyên vị trí
- [x] **R4. Unified input bar** — send icon-only cả 2 mode; input row luôn horizontal mọi viewport (bỏ column mobile + media query)
- [x] **R5. Scroll convention** — oldest-first cả 2 mode; AI loop đảo `i=0..count-1`, typing indicator xuống cuối, `scrollToTop`→`scrollToBottom`

### P1 — Floating bubble + call UX

- [x] **R6. Bubble polish** — 60px, gradient primary, hover scale + shadow deepen, unread badge pulse (`prefers-reduced-motion` tôn trọng), `bottom: calc(16px + env(safe-area-inset-bottom))`, icon đổi theo mode gần nhất (`bi-robot`/`bi-person-fill`) — RV verified 60px + icon switch
- [x] **R7. Call timer** — `VoiceCallPanel`: `PeriodicTimer` 1s khi `connected` → mm:ss hiển thị "Đang gọi · mm:ss"; survives reconnecting; stop/reset trên terminal state + `DisposeAsync`. Unit tests 39/39
- [x] **R8. Speaking/active visual** — pulse-ring avatar khi `connected` (ring animation, reduced-motion safe); WebAudio level deferred → P2
- [x] **R9. Incoming call nổi bật** — ringing: emphasized card + avatar pulse + accept/reject ≥48px + "Cuộc gọi đến" label
- [x] **R10. Floating mini call-bar** — `RealtimeChatPanel.VoiceCallStateChanged` → `Store.razor` → `AiChatWidget.ExternalCallState`; bubble đóng + call active/ringing → `[data-testid="ai-mini-callbar"]` (state + Mở → human mode). RV verified hidden khi không có call

### P2 — Polish (sau khi P0/P1 ổn định)

- [ ] **R11.** Date separator "Hôm nay/Hôm qua" cả 2 mode.
- [ ] **R12.** "Đang nhập..." typing indicator chiều customer→shop (infra SignalR `ReceiveTyping` cần check tồn tại — nếu chưa có backend thì defer).
- [ ] **R13.** Message entrance animation (fade+slide 150ms), skeleton loading thay spinner trần.
- [ ] **R14.** AI mode: product card polish (ảnh lớn hơn, giá bold, nút "Thêm" full-width mobile).

## 4. Execution strategy — đủ yêu cầu, nhanh

### Phase A — P0 (1 commit lớn, ~1 ngày)

```
A1. Thêm CSS vars + viết lại bubble/input CSS cho CẢ 2 component theo tokens mới
A2. RealtimeChatPanel: +ShowHeader param; widget: header động theo mode + relocate call/conn controls
A3. Store.razor: truyền ShowHeader=false + wire call controls lên widget level
A4. AI scroll: đảo loop → oldest-first + auto-scroll bottom (JS hook nếu cần)
A5. Unit tests (nếu có builder logic) + E2E spec chat-ui-unification.spec.ts
```

**An toàn:** tất cả trong UI.Platform scoped CSS + param mới default `true` → **zero breaking change** cho ShopERP ShopInbox/OrderTracking usages. Chỉ KhachLink Store mount đổi behavior.

### Phase B — P1 (1-2 commit)

```
B1. Bubble 60px + badge pulse + safe-area + icon theo mode (CSS-only, nhanh)
B2. VoiceCallPanel: call timer (PeriodicTimer) + connected pulse ring
B3. Incoming call card to hơn (CSS)
B4. Mini call-bar: lift CallStateChanged lên AiChatWidget level — phức tạp nhất,
    làm cuối; nếu vượt scope → tách commit riêng, không block B1-B3
```

### Phase C — P2 (tuỳ chọn, từng item 1 commit nhỏ)

Date separators → typing indicator (cần backend check trước) → animations → product card.

## 5. Risks & mitigations

| Risk | Mitigation |
|---|---|
| `ShowHeader=false` làm mất call button/conn badge | Relocate lên widget header TRƯỚC khi ẩn; E2E assert call btn vẫn click được ở human mode |
| Scroll order change phá AI history UX | Auto-scroll bottom + E2E assert tin mới cuối list; giữ `_historyLoaded` logic |
| VoiceCallPanel timer leak | `PeriodicTimer` dispose trong `DisposeAsync`/state exit; test terminal states |
| Scoped CSS `::deep` conflicts giữa 2 component | Tokens ở `:root`/layout level, không scoped; verify visual cả Store page lẫn ShopInbox |
| Mini call-bar overlap PWA install/cart | z-index dưới widget (1040), position bottom-left hoặc above bubble; manual check mobile |
| E2E flake do widget chỉ render khi `aiEnabled`/flag on | Spec skip khi widget absent (pattern sẵn có); RV prod cần tenant có AI+voice flags bật |

## 6. Verification

- [ ] `dotnet build VanAn.sln` 0 errors · guard-check ALL PASSED
- [ ] Unit tests hiện có `RealtimeChatPanel*Tests` vẫn pass (param mới default-on)
- [ ] E2E mới `chat-ui-unification.spec.ts`: tabs switch → header/avatar/tên đổi đúng · bubbles cùng style · input cùng pattern · scroll oldest-first · (P1) timer hiện khi connected — mock hoặc flag-gated skip
- [ ] RV prod `shopai.timlathay.com` tenant có `Voice_Call_Enabled` + AI enabled: visual check 2 mode + call flow (cần 2 thiết bị/account test)
- [ ] Regression: ShopERP `/admin` chat (ShopInbox) + OrderTracking chat không đổi (ShowHeader default true)

## 7. Files

| File | Action |
|---|---|
| `UI.Platform/Components/Ai/AiChatWidget.razor` | header động theo mode, bubble order, input unify, bubble 60px, mini call-bar (P1) |
| `UI.Platform/Components/Ai/AiChatWidget.razor.css` | tokens consume, bubble polish, badge pulse, safe-area |
| `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` | `ShowHeader` param, bubble/avatar unify, input pattern, scroll order |
| `UI.Platform/Components/Realtime/RealtimeChatPanel.razor.css` | tokens consume, remove hardcode sizes |
| `UI.Platform/Components/Realtime/VoiceCallPanel.razor(.cs/.css)` | timer, ringing emphasis, connected pulse |
| `5_WebApps/KhachLink/Pages/Store.razor` | pass `ShowHeader=false` + relocate call controls |
| `6_Testing/e2e-tests/chat-ui-unification.spec.ts` | NEW |
| Tokens: `UI.Platform/Tokens/` hoặc `VanAnLayout` css | shared `--chat-*` vars |

## 8. Out of scope

- Backend/SignalR protocol changes (trừ typing indicator P2 nếu infra đã có)
- Voice call **AI mode** (AI gọi voice — feature mới, không phải UI unify)
- ShopERP ShopInbox redesign (chỉ đảm bảo không regression)
- Dark mode chat (theme system xử lý riêng)
