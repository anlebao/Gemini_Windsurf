# MASTER PLAN: Product Pending-Review — Extract → Catalog có gate owner duyệt

> Ngày: 2026-11-25 · Trạng thái: APPROVED direction (user chọn "Status field mới" + duyệt đề xuất), chờ implement
> Liên quan: `universal-ai-assistant-master-plan.md` · `w3c-w3d-extract-voice-master-plan.md`
> Task cards: `docs/AI/tasks/product_pending_review/task_t1..t6_*.md`

## Bối cảnh

`TenantAiKnowledge.razor` (`/admin/tenant-ai/knowledge`) đã có pipeline: URL website → `WebContentExtractorService` (deep crawl same-origin, SSRF-guarded) → vanan-ai `KnowledgeExtractor` → items `{kind, title, content}` → knowledge doc pending-review.

**Gap:** items `kind=product|service` chỉ thành text cho AI knowledge — **không vào danh mục sản phẩm** (`Products` SQLite per-tenant). User yêu cầu: sản phẩm/dịch vụ trích xuất được **push vào catalog ở trạng thái Pending**, chỉ khi owner chuyển **Ready** mới dùng được (show storefront, buyer add giỏ, booking). Hỗ trợ thao tác 1 lúc nhiều sản phẩm.

## Quyết định đã chốt

| # | Quyết định | Lựa chọn |
|---|---|---|
| D1 | Trạng thái sản phẩm | **`ProductStatus` enum mới** trên `Product` entity: `Ready = 0` (default, existing rows không đổi) / `Pending = 1`. KHÔNG tái dùng `IsActive` (tránh lẫn "ngừng bán" thủ công). |
| D2 | Phạm vi gating | Pending ẩn khỏi **mọi public/anonymous API** (`GetProducts`, `GetProduct`, `grouped-by-tenant`, `recommended`, `{id}/qr`, `validate-price`). Management + POS vẫn thấy kèm badge (staff bán tay được). Order path không re-validate (giống `IsActive` hiện tại — catalog là gate duy nhất). |
| D3 | Nguồn giá | **vanan-ai extract schema += `price` (number, optional)** — LLM tách giá VND; không có giá → `null` → product giá 0, owner sửa sau. Backward-compat (field optional). |
| D4 | Đường vào catalog | `CreateProductRequest.PendingReview` (bool) → `ProductService` set `Status=Pending`. `ProductImportRequest.AsPending` cho endpoint `api/products/import` (self-serve import có thể opt-in; wizard OCR giữ Ready vì sysadmin đã review bảng giá trị). |
| D5 | UI extract | Checkbox "Đưa vào danh mục sản phẩm (chờ owner duyệt)" (default ON) trong preview — items kind=product/service tạo Product Pending ngay khi confirm (cả single-extract lẫn extract-groups menu group). Category mặc định "Menu". |
| D6 | Bulk approve | `ProductManagement.razor`: badge "Chờ duyệt" + filter + checkbox (pattern `_selectedForPrint` có sẵn) + "Duyệt đã chọn" / "Duyệt tất cả" → endpoint mới `POST /api/products/bulk-status`. |
| D7 | Booking offerings | **Ngoài scope** — `AppointmentOffering` (PG) bắt buộc `DurationMinutes`, extract không có → phase 2 riêng. |

## Kiến trúc đụng tới

- **Domain:** `1_Shared/Domain.cs` — `ProductStatus` enum + `Product.Status` + `SetStatus`/`MarkPending`/`MarkReady` (Single Source of Truth, Domain modification đã được user approve qua lựa chọn D1).
- **EF:** `ProductConfiguration` map `Status` int; migrations **ShopERP SQLite** (runtime products) + **PG `VanAnDbContext`** (model-sync, `DbSet<Product>` tồn tại).
- **vanan-ai:** `KnowledgeExtractor` schema/prompt/parse + contracts `KnowledgeExtractItem.Price` — deploy riêng trong CD.
- **ShopERP:** DTOs + `ProductService` + `ProductsController` (6 public filters + bulk endpoint) + `ProductImportController` + 2 razor pages.
- **DTO contract:** `TenantAiExtractItem` (ShopERP) += `Price`.

## Phases / Task cards

| Task | Nội dung | TDD trọng tâm |
|---|---|---|
| **T1** `task_t1_domain_status.md` | Enum + entity + EF config + DTOs + service + 2 migrations | Entity status transitions · CreateProductAsync PendingReview→Status=Pending · DTO mapping |
| **T2** `task_t2_gating_bulk.md` | Public catalog filters + `POST /api/products/bulk-status` | Pending bị loại khỏi 6 public queries · bulk endpoint tenant-scope + status validate |
| **T3** `task_t3_extract_price.md` | vanan-ai `price` field + ShopERP DTO | Extractor parse price · never-throw giữ nguyên · DTO round-trip |
| **T4** `task_t4_extract_catalog_ui.md` | `TenantAiKnowledge.razor` checkbox + tạo pending products | UI logic chọn items · dedupe/skip invalid · groups flow |
| **T5** `task_t5_product_mgmt_bulk.md` | `ProductManagement.razor` badge/filter/checkbox/bulk approve | Filter pending · bulk select · gọi bulk-status |
| **T6** `task_t6_tests_rv.md` | E2E spec + guard + CD + RV prod | Pending→hidden storefront · approve→hiện · bulk hoạt động |

## Thứ tự implement

T1 → T2 → T3 → T4 → T5 → T6 (T3 độc lập với T1/T2 — có thể song song; T4 phụ thuộc T1+T3).

## Acceptance criteria tổng

- [ ] Item `kind=product|service` từ extract (URL hoặc paste) → Product `Status=Pending` trong catalog tenant, KHÔNG hiện `/store/{slug}` / cart / product detail / QR / price-validate.
- [ ] Owner `/products` thấy badge "Chờ duyệt", filter riêng, tick nhiều → "Duyệt đã chọn"/"Duyệt tất cả" → Status=Ready → hiện storefront ngay.
- [ ] Product tạo tay qua UI giữ Ready (không regression). Import menu OCR wizard giữ Ready.
- [ ] vanan-ai trả `price` khi có giá; UI preview hiển thị giá; product tạo với đúng giá.
- [ ] guard-check ALL PASSED · CD SUCCESS · RV prod pass.
