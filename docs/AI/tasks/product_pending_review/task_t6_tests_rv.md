# Task T6 — E2E spec + guard + deploy + RV

> **Status:** PENDING · **Created:** 2026-11-25 · **Phase:** T6 (cuối)
> **Plan:** `docs/AI/plans/product-pending-review-master-plan.md` — Acceptance criteria tổng

## Mục tiêu

Verify end-to-end trên prod: extract→pending→hidden→bulk approve→visible.

## E2E spec mới

`6_Testing/e2e-tests/product-pending-review.spec.ts` + config `playwright-rv-productpending.config.ts` (pattern `playwright-rv-mobileui.config.ts`):

1. **API level:** `GET /api/products?shopId={tenant}` (public, qua YARP `shoperp/api/products`) — product Pending không có trong list; Ready có.
2. **Flow level (nếu auth khả dụng):** login owner test tenant → tạo product Pending qua `POST /api/products` `{PendingReview:true}` → assert ẩn khỏi public catalog → `POST /api/products/bulk-status {status:"ready"}` → assert hiện lại → cleanup (delete/deactivate).
3. **Storefront:** `/store/{slug}` product grid không chứa pending item (có thể skip nếu test tenant không có storefront).

## Verify checklist

- [ ] `dotnet build VanAn.sln` 0 err
- [ ] `guard-check.ps1` ALL PASSED
- [ ] vanan-ai build + tests PASS (T3)
- [ ] Commit → push → **CD Multi-VPS SUCCESS** (chú ý vanan-ai có deploy riêng trong CD không — check workflow)
- [ ] RV L1: public `GET shoperp/api/products` không trả pending
- [ ] RV L2: ProductManagement UI badge/filter/bulk (manual hoặc Playwright auth)
- [ ] RV L3: extract URL trên TenantAiKnowledge → product pending xuất hiện
- [ ] Update `docs/AI/project_state.md`

## Known risks / notes

- vanan-ai deploy: check `cd-multivps.yml` có job vanan-ai không — nếu service riêng thì cần deploy đúng thứ tự (vanan-ai trước ShopERP dùng field mới — nhưng field optional nên thứ tự không critical).
- PG migration `AddProductStatus` chạy trên Gateway DB khi deploy — verify migration applied trong CD logs.
- `ProductManagement` là OwnerOnly — sysadmin cũng cover (policy bao gồm SystemAdmin theo pattern hiện có — verify).
- POS: pending vẫn hiện trong manage/POS — chủ đích (D2), note trong RV report.
