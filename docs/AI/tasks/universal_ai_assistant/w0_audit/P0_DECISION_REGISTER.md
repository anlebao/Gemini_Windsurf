# P0 DECISION REGISTER — Universal AI Assistant

> Date: 2026-11-22 · Cần PO approve trước khi W1 IMPLEMENT
> Mỗi quyết định có recommendation từ audit evidence.

## Architecture decisions

| ID | Quyết định | Recommendation | Evidence | Status |
|---|---|---|---|---|
| D1 | Assistant Management host | **CoreHub module** (entities trong `VanAnDbContext`, management API qua Gateway, UI trong ShopERP). Không service mới, không nhồi vào TenantAI/Gateway | ASSISTANT_HOST_DECISION | ✅ APPROVED (user 2026-11-22) |
| D2 | Public resolution model | Unified `assistantRef` resolution trong Gateway facade: storefront-host (giữ nguyên) + assistant slug + widget key → tenant → sign ctx → TenantAI | API_CONTRACT_GAP K1 | ✅ APPROVED (user 2026-11-22) |
| D3 | Session model W1 | Giữ client-generated sessionId; signed session token chỉ cho continuity W5 | API_CONTRACT_GAP K3 | ✅ APPROVED (user 2026-11-22) |
| D4 | `AIChat_Enabled` migration | Flag hiện có → derived từ `assistant.Status==Published`; transition fallback đọc cả hai | KHACHLINK_CHAT_RELATION | ✅ APPROVED (user 2026-11-22) |
| D5 | Hosted page host | Trang `/a/{slug}` trong KhachLink app (tái dùng Blazor widget + public API pattern); domain strategy theo PO-002 | IMPLEMENTATION_TASK_CARDS W1.4 | ✅ APPROVED (user 2026-11-22) |

## Pricing & quota

| ID | Quyết định | Recommendation | Evidence | Status |
|---|---|---|---|---|
| D6 | Rate card `ai.chat.message` | Verify seed trên prod; nếu thiếu seed qua `/v1/rate-cards` trước W1 | PRICING_AND_QUOTA_MODEL | ✅ APPROVED (user 2026-11-22) |
| D7 | Assistant budget model | `MonthlyBudgetMessages`/credits per assistant + ví tenant QuotaGate; vượt → degrade structured-only trước, hard stop sau | PRICING doc | ✅ APPROVED (user 2026-11-22) |
| D8 | Giá credit/message | Tính từ Gemini cost thật + margin — cần số từ TenantAI config model | PRICING doc §economics | ✅ APPROVED (user 2026-11-22) |
| D9 | Attribution metadata | Thêm channel/assistantRef/sessionRef-hash vào usage events | PRICING doc, SEC-025 | ✅ APPROVED (user 2026-11-22) |

## Product decisions

| ID | Quyết định | Recommendation | Status |
|---|---|---|---|
| D10 | W3 knowledge scope | W3a text/manual + LLM extraction trước; ảnh/PDF/crawl là increments | ✅ APPROVED (user 2026-11-22) |
| D11 | Handoff target W4 | Verify ShopInbox/lead capability; fallback email/Zalo notify | ✅ APPROVED (user 2026-11-22) |
| D12 | AI voice provider | Gemini Live primary (cùng vendor TenantAI, `vi` support, function calling); spike eval sớm W4; Soniox/Blaze/MetaConnect alternatives | ✅ APPROVED (user 2026-11-22) |
| D13 | Pilot shop | Shop Vạn An có knowledge sạch + owner nhiệt tình | ✅ APPROVED (user 2026-11-22) |
| D14 | Assistant pricing pilot | Free quota nhỏ trong pilot → thu phí từ W2/P2 | ✅ APPROVED (user 2026-11-22) |
| D15 | Link domain | Subdomain Vạn An kiểm soát (HTTPS), DNS review trước | ✅ APPROVED (user 2026-11-22) |

## Blocking items cần verify trong W1.0 (không cần PO)

- [ ] `/v1/config/widget` prod status (stub hay live)
- [ ] `ai.chat.message` rate card seeded + giá hiện tại
- [ ] MeteringClient có forward metadata fields chưa
- [ ] `TenantAiProfile` entity nội dung (tránh trùng Assistant)
- [ ] ShopInbox/lead receive path cho W4
- [ ] Booking API surface cho W4

## Sign-off

| Role | Name | Date | Decision |
|---|---|---|---|
| PO | User | 2026-11-22 | ☑ Approve all 15 decisions theo recommendation |
| Tech Lead | | | ☐ |
