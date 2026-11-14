# Task W3-1 — CONTEXTUAL FEATURE SURFACING (FR-V8/V9/V10)

> **Status:** PENDING · **Wave:** W3
> **Phụ thuộc:** W2-5 (`FeatureContextService`), W2-2 (portal member cards), W2-1 (member list).

## Mục tiêu giá trị

Trên portal hoptacxa + Directory timlathay + Home diemthuong2: member/shop/product cards có entry "Hỏi AI" / "Gọi cửa hàng" / "Chat" **chỉ khi tenant đó bật toggle và có context** — biến các flagship features thành thứ user nhìn thấy.

## Scope

1. **FR-V8 — Public feature exposure:** thêm `AiChatEnabled`, `VoiceCallEnabled` (anonymous-safe bools) vào `TenantStoreDto` (Directory search) + cooperative member DTO (W2-1). Nguồn: `ShopFeatureSettings` per tenant — expose 2 bool này thôi, không lộ config khác.
2. **FR-V10 — Per-card AI entry:**
   - Directory `Home.razor` store card: nút "Hỏi AI" khi `AiChatEnabled` → mở chat với `tenantId` context (Store page chatbox pattern — deep link `/store/{slug}?chat=open` hoặc panel).
   - Portal member cards (W2-2): tương tự.
   - Home featured products: "Hỏi AI về món này" trên `RecommendedCatalogItem` (`item.TenantId` đã có).
3. **FR-V9 — Voice call có context:** entry "Gọi cửa hàng" khi `VoiceCallEnabled` → mở/ensure conversation với tenant (reuse `EnsureOrderParticipantsAsync`/chat flow) rồi nút gọi hiện có trong ChatPanel. Counterpart resolver: most-recent participant + reachable-first (đã có S1–S9).
4. Render rule FR-V7: mọi entry mới check `toggle ON && context` qua `FeatureContextService`.

## Files

- `2_Gateway/Controllers/` — DTO changes (`TenantStoreDto` + coop member DTO) + feature fields source
- `5_WebApps/Directory/Components/Pages/Home.razor` (store card entries)
- `5_WebApps/KhachLink/Pages/Home.razor` (featured card "Hỏi AI") + portal component (W2-2)
- `5_WebApps/KhachLink/Components/` — shared `ContextualFeatureButtons` component nếu tái dùng nhiều chỗ (UI Platform)

## Tests

- DTO serialization + feature-flag mapping tests.
- E2E: member card với tenant bật AI → nút "Hỏi AI" hiện; tenant tắt → không hiện (Gate 4).
- guard-check + build + Core.Tests.

## RV checklist

- [ ] Timlathay store card: tenant AIChat ON → "Hỏi AI" mở chat đúng tenant.
- [ ] hoptacxa member card: voice-call entry → conversation + call button (nếu voice service OK).
- [ ] Featured product "Hỏi AI về món này" → chat context product+tenant.
- [ ] Tenant toggle OFF → không render nút nào.
