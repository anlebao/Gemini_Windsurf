# Master Plan — Storefront Smart & Modern UX (KhachLink `/store/{slug}`)

> **Status:** DRAFT 2026-11-07 — chờ user review
> **Nguồn:** đề xuất session 2026-11-07 (sau P8 unified widget + P9 inbox AI review)
> **Phạm vi:** KhachLink storefront (`5_WebApps/KhachLink/Pages/Store.razor`) + UI.Platform — **additive, flag-gated, không merge backend, không phá Option C**

## 1. Mục tiêu

Biến trang storefront từ "brochure tĩnh + bubble chat" thành điểm chạm thông minh: AI chủ động hiện diện ở từng section (không chỉ sau bubble), UI hiện đại chuẩn PWA 2026 (sticky nav, skeleton, motion, tokens), giữ nguyên ranh giới kiến trúc.

## 2. Hiện trạng (verified 2026-11-07)

| Thành phần | File | Vai trò |
|---|---|---|
| Storefront page | `5_WebApps/KhachLink/Pages/Store.razor` | hero + 5 section (booking/campaigns/products/maps/shop-chat/social) + unified `AiChatWidget` · themes chỉ đổi 3 biến gradient |
| Unified chat | `UI.Platform/Components/Ai/AiChatWidget.razor` | bubble 2 mode AI/Human (P8) — entry duy nhất của AI trên trang |
| Human chat | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` | section inline + bubble human mode |
| AI backend | vanan-ai + Gateway `PublicTenantAiController` | `/v1/chat`, widget-config, sessions — grounded, refusal→handoff |
| Knowledge | vanan-ai PG pgvector | products/documents đã ingest (An Tâm 65 dịch vụ + 5 docs) |

**Khoảng trống:** (a) AI chỉ chờ khách mở bubble — không chủ động theo context section; (b) trang dài không có nav; (c) loading = spinner thô, ảnh không lazy/aspect → CLS; (d) themes nông; (e) không unread cho khách.

## 3. Wave plan — 3 đợt tuần tự

| Wave | Task card | Nội dung | Backend mới? | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| **W1** | `task_sf_w1_storefront_smart_ux.md` | A1 AI quick-chips theo section · B1 sticky glass navbar · B2 skeleton shimmer + lazy/aspect images · B6 unread badge trên bubble | **Không** (chỉ UI.Platform + KhachLink) | P8 done ✅ | 🟡 CARD READY — chờ review |
| **W2** | `task_sf_w2_search_gallery.md` | A2 semantic search bar (retrieval-only endpoint) · B3 gallery quick-view modal + badges từ knowledge · B5 micro-motion scroll | 1 endpoint đọc (`/api/public/tenant-ai/search` hoặc reuse `/v1/chat`) | W1 + P6b | ⬜ |
| **W3** | `task_sf_w3_personal_themes.md` | A3 section "Gợi ý cho bạn" (popularity + intent-aware) · A4 upsell post-add-to-cart · A5 store status thông minh · B4 theme v2 design tokens + dark variant · B7 OG meta | đọc popularity từ PG order mirror; theme schema mở rộng | W2 | ⬜ |

**Gate rule (giữ nguyên như tenant-ai):** implement tuần tự theo card — card xong = tests xanh + deviation ghi vào card. Deploy checkpoint tự nhiên: push/CD per wave (W1 chỉ 2 app — ShopERP không đổi nên chỉ KhachLink VPS).

## 4. Ranh giới cứng (áp cho cả 3 waves)

- UI Platform components only — không custom CSS bypass (hard stop).
- KhachLink WASM HTTP-only — không inject DbContext (Option C).
- AI KHÔNG tạo order — chỉ gợi ý + add-to-cart qua card snapshot (`OnAddToCart` host-owned).
- Không merge AI/human history (P8 boundary).
- Flag-gated: mọi AI surface mới phải tự tắt khi `AIChat_Enabled`/widget-config off (pattern `aiEnabled` hiện có). UI thuần (navbar/skeleton/theme) không cần flag.
- Mọi endpoint mới (W2/W3): anonymous + rate-limited, tenant resolve server-side (T6 precedent — KhachLinkInstance bound host), fail-open.

## 5. Rủi ro & mitigations

| Rủi | Mitigation |
|---|---|
| Quick-chips spam AI call (metering cost) | Chips chỉ mount khi `aiEnabled=true`; 1 chip = 1 turn chat bình thường (đã metered); không auto-fire khi load trang |
| Unread badge poll tốn request | Poll 45s chỉ khi trang visible (`visibilitychange`), stop khi human mode mở; localStorage lastSeen |
| Skeleton/CLS regression trên mobile cũ | CSS thuần, không JS layout; `prefers-reduced-motion` tắt shimmer |
| W2 search endpoint lộ knowledge | Retrieval-only: chỉ trả facts đã qua `GetPublicChunksAsync` filter (G5/G15 precedent) — KHÔNG expose raw chunks |
| Theme v2 vỡ tenant hiện hữu | Tokens mới fallback về giá trị hiện tại khi tenant chưa set (additive CSS vars) |

## 6. Metrics quan sát sau deploy (informal)

- % session storefront có ≥1 AI turn (từ `ConversationSession`) — baseline An Tâm pilot ~20 sessions.
- Click-through quick-chips (đếm client-side, log qua metering `store.visit` hook mở rộng hoặc bỏ — W1 không thêm tracking mới).
- CLS/LCP trên storefront (thủ công Lighthouse — không thêm tooling).

## 7. Task cards

- W1: `docs/AI/tasks/storefront_smart_ux/task_sf_w1_storefront_smart_ux.md` (READY)
- W2, W3: viết sau khi W1 merged + user approve wave tiếp.
