# W0 DECISION: ASSISTANT MANAGEMENT HOST (Q1)

> Date: 2026-11-22 · Câu hỏi: Assistant entity/config/publish-states/domains/links nằm ở service nào?

## Evidence thu thập

1. **CoreHub PG (`VanAnDbContext`)** đã chứa `ShopFeatureSettingsEntity` — per-tenant shop config với `AIChat_Enabled`, sync 2 chiều ShopERP SQLite ↔ PG qua outbox/NATS. Gateway inject trực tiếp `VanAnDbContext` trong `PublicTenantAiController`.
2. **ShopERP** đã có full TenantAI admin UI (`/admin/tenant-ai/*`) + `/settings/shop-features` — owner-facing surface tồn tại.
3. **Gateway** là public facade: đã resolve tenant, sign ctx, rate-limit, forward tới TenantAI. Không nên nhồi CRUD phức tạp vào.
4. **TenantAI** là engine thuần (PostgreSQL+pgvector): auth = caller key + scope + signed tenant ctx. KHÔNG chứa tenant/account identity — đúng boundary, không nhồi management vào.
5. Chưa có `Assistant`/`AssistantLink`/`WebsiteDomain` entity ở bất kỳ đâu.

## Options đánh giá

| Option | Mô tả | Ưu | Nhược | Verdict |
|---|---|---|---|---|
| **A. CoreHub module (Khuyến nghị)** | Entities `TenantAssistant`, `AssistantLink`, `WebsiteDomain`, `WidgetInstance` trong `3_CoreHub` (PG, `VanAnDbContext`, multi-tenant filter sẵn); service layer `AssistantManagementService`; Gateway expose public resolution endpoints đọc trực tiếp DbContext (pattern `PublicTenantAiController` đã làm); ShopERP UI quản lý | Tái dùng đúng chỗ per-tenant business config đang nằm; PG source of truth phù hợp Gateway routing; sync convention có sẵn nếu cần ShopERP local; không service mới | CoreHub đang lớn — cần giữ module gọn | **RECOMMENDED** |
| B. Gateway module | Entities trong Gateway DbContext | Gần public facade | Gateway PG đã là source of truth cho Orders/Tenants/Users — thêm assistant config vào có thể phình; management API authenticated cũng phải đặt đâu đó | Không chọn — trộn public facade với management |
| C. Extend TenantAI | Assistant entities trong TenantAI PG | Gần knowledge/policy | Vi phạm boundary: TenantAI không biết tenant identity/account; management cần auth owner — sai layer | Loại |
| D. Service mới | `vanan-assistant` riêng | Ranh giới sạch | Ops cost mới; data vẫn phải sync về CoreHub cho tenant context; vi phạm "không microservice khi chưa cần" | Chỉ khi A chứng minh không đủ |

## Quyết định đề xuất

**Option A — CoreHub module:**

```text
ShopERP (owner UI) ──► Gateway management API ──► CoreHub AssistantManagement
                              │                        (VanAnDbContext, PG)
Public surface ──► Gateway PublicAssistantController ──► đọc Assistant/Link/Domain
                              │                        từ cùng DbContext
                              ▼
                     TenantAiForwarder → vanan-ai /v1/chat (signed ctx)
```

- `TenantAssistant` gắn `TenantId` (BaseEntity, IMustHaveTenant) — single-identity rule áp dụng.
- `AIChat_Enabled` flag: **derived** = `assistant.Status == Published` (backward compat: giữ flag đọc được nhưng source of truth chuyển sang assistant; migration path trong W1 task).
- Public resolution: `AssistantLink.Slug` / `WidgetInstance.PublicWidgetKey` / storefront host → `TenantAssistant` → tenantId → sign ctx → TenantAI.
- AssistantLink/WidgetInstance/WebsiteDomain cũng là CoreHub entities (cùng aggregate boundary, tránh cross-service join).

## Rủi ro & mitigation

- CoreHub schema growth → giữ entities nhỏ, config JSON có kiểm soát; migration theo convention.
- ShopERP SQLite sync: assistant config có cần replicate xuống ShopERP không? **Không bắt buộc** — assistant phục vụ public surface qua Gateway; ShopERP chỉ cần management UI gọi Gateway API. Chỉ sync nếu offline-need chứng minh được.

## Cần PO confirm

- [ ] Chấp nhận Option A (CoreHub module, không service mới)
