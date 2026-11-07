# Task SF-W2 — Storefront Smart UX Đợt 2: Semantic search bar + Gallery quick-view + Micro-motion

> **Status:** ✅ IMPLEMENTED 2026-11-07 (worktree `Gemini_Windsurf-w2`, branch `feature/sf-w2`) — chờ CD + RV `6_Testing/rv-sf-w2.cjs` · **worktree sẵn `C:\VibeCoding\Gemini_Windsurf-w2` branch `feature/sf-w2`** — implement tại đó, merge về main khi xong (tránh đụng P10 đang WIP trên checkout chính)
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

**Implement 2026-11-07 — build 0 err · Core.Tests 25 tests mới (search bar 7 · quick-view 5 · vibe click 3 · client 3 · controller search 7) · PublicTenantAiControllerTests 18/18.**

- **A2 search:** `GET /api/public/tenant-ai/search` trên `PublicTenantAiController` — anonymous + class rate-limit `tenant-ai-public`, resolve tenant T6 (host-bound), flag `AIChat_Enabled`, forward `/v1/chat` với synthetic session deterministic `search:{tenant}:{sha24}` + messageId `search-{sha24}` (normalized: trim + collapse whitespace + lower → re-search idempotent, không double-meter). `IAiChatClient.SearchAiAsync` + `TenantAiPublicHttpService` impl (fail→null). `StoreSearchBar.razor` (KhachLink Components): pill input + Enter/search button + answer + product cards (reuse add-to-cart flow `AddAiProductToCartAsync`) + chip "Hỏi AI thêm" → `AskAi(query)`; fail/degraded → ẩn result (NFR-3). Mount đầu `.store-body`, gate `AIChat_Enabled`.
- **B3 quick-view:** `VibeShowcase` + `[Parameter] OnProductClick` (invoke trên col click, buttons bọc `@onclick:stopPropagation` span) — 5 theme branches. `ProductQuickView.razor` (VanAnModal — UI Platform): ảnh lớn + mô tả + giá + "Thêm vào giỏ" + "Hỏi AI về món này" → `AskAi($"Món {name} có gì nổi bật?")`, gate `AiEnabled`.
- **B5 micro-motion:** `.reveal-on-scroll` trên 5 `.section-header` + `.campaign-card` + col wrappers VibeShowcase; `vananReveal` IntersectionObserver trong `pwa.js` (fade-up 0.3s, threshold 0.12) — FOUC-safe `reveal-armed` pattern (no-JS/reduced-motion → luôn hiện), `Store.razor` gọi `scan()` 1 lần ở `OnAfterRenderAsync` sau load.
- **Tests:** `PublicTenantAiControllerTests` +7 (400/404/T6 forward+dSynthetic session/dedupe hash/flag-off 503/upstream 503/429 cap) · `StoreSearchBarTests` bUnit ×7 · `ProductQuickViewTests` ×5 · `VibeShowcaseTests` ×3 · `TenantAiPublicHttpServiceTests` ×3 · `FakeAiChatClient` +member.
- **E2E (Gate 4):** `ai-chat-widget.spec.ts` +2 cases W2 (search → answer + product card · click card → modal → add cart) · RV script `6_Testing/rv-sf-w2.cjs` (A2 search flow → widget handoff · B3 modal + cart + close · B5 armed→revealed sau scroll).

**Deviation:** `SearchAiAsync` đặt trên `IAiChatClient`/`TenantAiPublicHttpService` (card ghi `CommunityHttpService`) — cùng base path `/api/public/tenant-ai/*`, reuse `AiChatResult` + fail-safe pattern; CommunityHttpService là surface `/api/community/*` authenticated (X-Customer-Token), không hợp endpoint anonymous. Badges chay/cay/bán chạy → W3 (knowledge chưa expose per-product — theo card).
