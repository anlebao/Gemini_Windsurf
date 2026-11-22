# TASK W5: UNIVERSAL DISTRIBUTION — EXTENSION / ANDROID / MXH CHANNELS

> Wave: W5 · Mode: IMPLEMENT (sau W1 interfaces ổn định; song song một phần với W4)
> Master plan §W5 · SRS v2.0 §7 feasibility matrix, §9.2 continuity
> Depends on: W1 (assistant/session/identity); interfaces thiết kế từ W1 cho surface mới

## Sản phẩm giao được

> Trợ lý Vạn An đi theo người dùng: extension trên trình duyệt, floating trên Android, hiện diện được trên Zalo/FB theo API được phép — tất cả cùng MỘT assistant identity/knowledge/quota của shop.

## Scope IN (theo thứ tự ưu tiên trong wave)

### 5A. Conversation continuity foundation (làm trước trong wave)
- `ConversationId / ChannelId / VisitorRef / SessionTransferToken` theo SRS §9.2.
- Continuity opt-in: authenticated Vạn An user hoặc explicit transfer token; anonymous KHÔNG merge.

### 5B. Browser Extension (Chrome/Edge trước)
- User-triggered panel trên site được phép; per-site allow/deny.
- Page context chỉ khi user chọn/chia sẻ explicit; không đọc body mặc định; không password/cookie/payment fields.
- Store listing compliance + permission disclosure.

### 5C. Android Floating Assistant
- Overlay permission onboarding; bubble movable/dismissible; mic chỉ khi explicit; OS lifecycle compliant.
- Compatibility matrix + battery tests; không hứa iOS.

### 5D. MXH channels (theo API được phép)
- Zalo OA: link/QR trong OA auto-reply nếu API cho phép.
- Facebook: Messenger integration nếu Meta app review pass; fallback link/CTA.
- TikTok: bio link/QR/pinned — không scrape.

## Scope OUT

- Widget lên site người khác không được phép — không bao giờ
- Scraping MXH trái phép — tuyệt đối không
- iOS overlay

## Tasks

1. Continuity model + transfer token service + tests.
2. Extension: manifest, permissions, panel UI (reuse chat contract), per-site controls, store assets.
3. Android app: bubble, panel, permission flows, lifecycle.
4. Channel integrations theo feasibility audit (W0 bổ sung nếu cần).
5. Tests per surface: consent, isolation, revoke, store-policy compliance.

## Acceptance / Gate

- [ ] Mỗi surface: cùng assistant identity/policy/quota (AT-032)
- [ ] Continuity chỉ khi opt-in/authenticated; token single-use
- [ ] Extension: không password/cookie/page-body khi chưa consent
- [ ] Android: overlay revoke → app vẫn dùng; mic không tự bật
- [ ] Store review pass cho từng kênh phát hành
- [ ] Regression W1–W4 pass

## Risks

- Store rejection (Play/Chrome) → budget thời gian review; fallback deep-link.
- OEM Android fragmentation → compatibility matrix trước khi hứa.
- MXH API thay đổi → mỗi integration là task riêng có thể defer.
