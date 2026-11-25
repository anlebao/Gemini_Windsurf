# Task T3 — vanan-ai extract `price` field + ShopERP DTO

> **Status:** PENDING · **Created:** 2026-11-25 · **Phase:** T3 (độc lập T1/T2, có thể song song)
> **Plan:** D3 — `docs/AI/plans/product-pending-review-master-plan.md`
> **TDD:** extractor unit tests trước → implement → green

## Mục tiêu

LLM extract trả `price` (number, optional) cho item kind=product/service có giá; ShopERP nhận được field này trong preview.

## Files

| File | Thay đổi |
|---|---|
| `9_TenantAI/vanan-ai/.../Contracts` (tìm `KnowledgeExtractItem`) | `record KnowledgeExtractItem(string Kind, string Title, string Content, decimal? Price = null)` — trailing optional |
| `9_TenantAI/vanan-ai/src/VanAn.TenantAi.Api/Services/KnowledgeExtractor.cs` | SchemaHint += `"price":{"type":"number"}` trong item properties; prompt: "product/service có giá → price = số nguyên VND; không có → bỏ trống field"; `Parse`: đọc `price` (TryGetProperty, ValueKind Number → GetDecimal, else null); `KnowledgeExtractGroupsResponse` items dùng chung type → tự có |
| `5_WebApps/ShopERP/Services/TenantAiApiClient.cs` | `TenantAiExtractItem(string Kind, string Title, string Content, decimal? Price = null)` — JSON camelCase `price` tự map |
| vanan-ai tests | `KnowledgeExtractorTests` + `KnowledgeGroupExtractorTests` |

## Quyết định

- `price` optional → response cũ (không có field) parse vẫn OK; LLM không trả → null.
- Không đổi `content` — giá vẫn nằm trong content text cho knowledge doc; `price` chỉ phục vụ catalog import.
- Group extractor (`extract-groups`) dùng chung `KnowledgeExtractItem` → menu group tự có price.

## Tests (TDD)

1. `Extract_Parse_PriceNumber_ReturnsDecimal`
2. `Extract_Parse_NoPrice_ReturnsNull` / `PriceNonNumeric_ReturnsNull` (defensive)
3. `Extract_Parse_MissingItems_StillWorks` (regression)
4. `ExtractGroups_MenuGroup_PriceParsed` (nếu test group extractor sẵn)

## Acceptance

- [ ] vanan-ai unit tests PASS (mới + cũ)
- [ ] ShopERP build 0 err; `TenantAiExtractItem.Price` deserialize đúng từ JSON camelCase
- [ ] Không breaking: client cũ bỏ qua field mới
