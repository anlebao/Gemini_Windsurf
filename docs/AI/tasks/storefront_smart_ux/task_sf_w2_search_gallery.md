# Task SF-W2 — Storefront Smart UX Đợt 2: Semantic search bar + Gallery quick-view + Micro-motion

> **Status:** 🟡 DRAFT 2026-11-07 — chờ user review
> **Gate:** storefront UX polish — master plan `docs/AI/plans/storefront-smart-modern-master-plan.md` · **main sln + 1 public read endpoint mới**
> **Phụ thuộc:** SF-W1 DONE (`f6877634` + fix `b5e2223b`) · P6b admin API · `KnowledgeRetriever` sẵn có
> **Scope:** `UI.Platform` + `5_WebApps/KhachLink` + `2_Gateway` (1 endpoint) — KHÔNG đụng vanan-ai

## Objective

(A2) khách gõ câu hỏi tự nhiên vào search bar trên storefront → trả answer ngắn + product cards (không cần mở bubble); (B3) gallery sản phẩm có quick-view modal; (B5) micro-motion scroll.

## Deliverables

### 1. Semantic search bar (A2)

- **Endpoint mới** `GET /api/public/tenant-ai/search?query=` trên `PublicTenantAiController` (Gateway → vanan-ai):
  - Anonymous + rate-limited (reuse `ip:chat` pattern), tenant resolve qua `KhachLinkInstance` host-bound (T6 precedent).
  - Impl: gọi `/v1/chat` với synthetic session `search:{tenant}:{hash}` hoặc retrieval-only — **Quyết định khi implement: prefer `/v1/chat` (đã grounded + refused) giữ fail-open**; response trả `{ answer, products[], refused }` mirror widget chat.
  - Không persist session vào `ConversationSession` nếu dùng retrieval-only; nếu `/v1/chat` thì session bình thường (đã metered).
- **UI** `StoreSearchBar` section trong `Store.razor` (sau StoreNav, trên sections):
  - Input + nút tìm, placeholder "Hỏi gì về quán? (VD: đồ chay, giá massage...)".
  - Kết quả: answer text + `AiProductCard` list (reuse markup product card của widget) + "Hỏi AI thêm" → `AskAi(query)` (reuse `_pendingQuestion`).
  - Chỉ render khi `_features.AIChat_Enabled`; degraded/fail → ẩn phần kết quả, không lỗi trang.
- `CommunityHttpService` + `SearchAiAsync(tenantId, query)` → `GET /api/public/tenant-ai/search`.

### 2. Gallery quick-view (B3) — `VibeShowcase` host-level

- **KHÔNG đụng VibeShowcase internals** (ranh giới W1). Thay vào đó: `Store.razor` thêm `ProductQuickView` modal — click product card → mở modal (ảnh lớn + mô tả + giá + "Thêm giỏ" + "Hỏi AI về món này" → `AskAi($"Món {name} có gì nổi bật?")`).
- Quick-view cần hook từ VibeShowcase: thêm `[Parameter] public EventCallback<Product> OnProductClick` (additive vào `VibeShowcase.razor` — 1 dòng invoke trên card click). Nếu VibeShowcase quá khó chạm → fallback: overlay click-capture ở Store.razor (ghi deviation).
- Badges "chay/cay/bán chạy": bỏ qua đợt này nếu knowledge chưa expose per-product — **ghi vào phạm vi loại trừ** (đẩy W3).

### 3. Micro-motion (B5)

- `.reveal-on-scroll` CSS + 1 tiny JS `vananReveal` IntersectionObserver trong `pwa.js` (fade-up 0.3s); áp cho `.store-section` headers + campaign/product cards.
- `@media (prefers-reduced-motion: reduce)` → hiện ngay, không animate.
- Không library mới — vanilla IO ~15 dòng.

## Ranh giới (HARD)

- Search endpoint: retrieval/chat read-only — KHÔNG expose raw chunks (G5/G15 precedent), KHÔNG tạo order.
- Search bar chỉ gợi ý — AI vẫn không tự động hành động; add-to-cart qua product card existing flow.
- Modal/lightbox: markup + scoped css theo convention, không thêm JS library.

## Tests bắt buộc

- [ ] bUnit: search bar render khi `AIChat_Enabled` · `OnProductClick` invoke từ VibeShowcase · modal mở/đóng
- [ ] `CommunityHttpService.SearchAiAsync` mapping + fail→null (MockHttpClient pattern)
- [ ] E2E spec +1 case: search → answer + product card render (Gate 4)
- [ ] Gateway endpoint test: anonymous OK · invalid query → 400/fail-open

## Acceptance

- [ ] Tests xanh · build 0 err · guard PASSED · RV script `rv-sf-w2.cjs` (search → answer → click product → modal → add cart; reveal animation không vỡ layout)

## Không được làm

- Không autocomplete/personalization (W3) · không sửa VibeShowcase layout internals
- Không search endpoint trả raw knowledge chunks · không đụng MessagingHub

## Delivered

_(điền sau implement)_
