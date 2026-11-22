# W0: IMPLEMENTATION TASK CARDS — refinement sau audit

> Date: 2026-11-22 · Đánh giá lại task_w1→w6 dựa trên evidence; chỉ ghi DELTA so với task cards gốc.

## W1 refinements (quan trọng nhất)

1. **Thêm task đầu wave: verify `/v1/config/widget` prod status** — comment trong Gateway nói "501 stub lands in P5"; nếu stub → implement config endpoint thật theo assistant entity (thay vì giả định có sẵn).
2. **Session model**: giữ client-generated sessionId (đã chạy, đơn giản) — KHÔNG build session-token service ở W1; ghi enhancement cho W5 continuity. → giảm scope W1.
3. **AIChat_Enabled migration path**: W1 phải định nghĩa rõ — assistant đầu tiên của tenant "adopt" flag hiện có; storefront widget check cả hai trong transition (C1).
4. **Hosted page host app**: đề xuất đặt `/a/{slug}` trong **KhachLink app** (đã có public pages + gọi Gateway public API + mobile-first sẵn) — cần confirm domain strategy (subdomain riêng hay path trên domain KhachLink hiện có). Alternative: Gateway-served static page — nhưng chat UI Blazor tái dùng tốt hơn ở KhachLink.
5. **Entities**: `TenantAssistant`, `AssistantLink` trong CoreHub (xem HOST_DECISION); `WebsiteDomain`/`WidgetInstance` để W2 nhưng có thể tạo schema sớm nếu migration gộp tiện.
6. **Metering**: verify `ai.chat.message` rate card seeded trên prod TRƯỚC khi code quota gate; nếu chưa → seed qua rate-card API (không code).
7. **Attribution**: kiểm tra MeteringClient có forward metadata; nếu chưa → thêm `channel`/`assistantRef` vào usage event metadata (thay đổi nhỏ, backward compatible).

## W2 refinements

- Shadow DOM vs iframe: spike 1 ngày đầu wave với 3 host mẫu (WP theme, custom HTML, SPA) trước khi commit.
- PublicWidgetKey resolution: tái dùng cùng `ResolveAssistantContextAsync` — widget key → assistant → tenant + origin check.

## W3 refinements

- Chia W3a (text/manual + LLM extraction → documents/products + confirm UI) làm core; W3b ảnh (Gemini vision), W3c PDF/Excel, W3d crawl là increments riêng — không block.
- Extraction output map ưu tiên `ProductsBatchRequest` cho item+giá (structured path tốt nhất), `DocumentIngestRequest` cho policy/FAQ text.

## W4 refinements

- Handoff đích đến: verify KhachLink inbox/lead capability trước (`ShopInbox.razor` tồn tại — check có nhận lead từ public không); fallback: tạo lead record + notify.
- Booking integration: kiểm tra Booking API surface hiện có trước khi thiết kế contract.

## W5/W6

- W5: continuity model đã spec; extension/android là builds độc lập lớn — có thể tách task cards riêng khi tới wave.
- W6: Gemini Live spike đề xuất sớm (W4 timeframe) — 1-2 ngày eval voice VN → quyết provider trước khi commit.

## Thứ tự thực thi đề xuất trong W1

```
W1.0 verify config/widget prod + rate card seed + metadata forward   (0.5 ngày)
W1.1 entities + migration (CoreHub)                                  
W1.2 assistant CRUD + publish/pause + wizard backend                 
W1.3 link/QR API + slug resolution                                   
W1.4 hosted page /a/{slug} (KhachLink) + chat wiring                  
W1.5 dashboard UI (ShopERP settings/assistant page)                  
W1.6 budget gate + attribution                                       
W1.7 tests (unit/integration/security/E2E) + RV                       
```
