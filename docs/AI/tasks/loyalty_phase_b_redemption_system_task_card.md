# TASK CARD: LOYALTY-B - Redemption System (Catalog + Voucher + Fulfillment)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Build redemption system thật — khách đổi điểm lấy sản phẩm/voucher, có tracking, fulfillment, admin quản lý catalog. Hiện tại chỉ có "deduct points" mechanism (audit 2026-07-23, 25% complete).
- **Nghiệp vụ áp dụng:**
  - Admin tạo catalog sản phẩm đổi điểm (cà phê miễn phí 500 điểm, trà sữa 1000 điểm, voucher 50k 2000 điểm).
  - Khách browse catalog → chọn sản phẩm → redeem → nhận voucher/QR → đến quán nhận hàng.
  - Admin xem redemption history + fulfillment status (pending → fulfilled / cancelled).
  - Optional: Pay order with points (thanh toán order bằng điểm — partial hoặc full).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Prerequisite:** Phase 5 COMPLETE (cho clean Domain.cs — tránh merge conflict khi thêm entities). KHÔNG conflict file với Phase 5 (toàn file mới).

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `1_Shared/Domain.cs` — thêm 3 entity mới: `RedemptionCatalogItem`, `RedemptionRecord`, `Voucher`
  - `3_CoreHub/Infrastructure/Configurations/RedemptionCatalogItemConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/RedemptionRecordConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/VoucherConfiguration.cs` (NEW)
  - `3_CoreHub/Domain/Repositories/IRedemptionRepository.cs` (NEW)
  - `3_CoreHub/Infrastructure/Repositories/RedemptionRepository.cs` (NEW)
  - `3_CoreHub/Services/IRedemptionService.cs` (NEW) + `1_Shared/Services/IRedemptionService.cs` (NEW — contract)
  - `3_CoreHub/Services/RedemptionService.cs` (NEW) — catalog CRUD + redeem flow + fulfillment
  - `5_WebApps/ShopERP/Controllers/RedemptionController.cs` (NEW) — admin catalog CRUD + customer redeem + fulfillment
  - `2_Gateway/Controllers/RedemptionController.cs` (NEW) — forward to ShopERP
  - `5_WebApps/ShopERP/Migrations/` — migration tạo 3 tables (SQLite)
  - `2_Gateway/Migrations/` — migration tạo 3 tables (PG, nếu Gateway lưu catalog)
  - `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` — update redeem UI: browse catalog + redeem + voucher display
  - `5_WebApps/KhachLink/Pages/RedemptionCatalog.razor` (NEW) — `/rewards` page browse redeemable items
  - `5_WebApps/ShopERP/Components/Pages/Admin/RedemptionCatalogAdmin.razor` (NEW) — admin catalog management
  - `5_WebApps/ShopERP/Components/Pages/Admin/RedemptionHistory.razor` (NEW) — admin redemption history + fulfillment
  - `6_Tests/` — unit + integration tests
- **Boundary Rules:**
  - **Domain modifications (3 entity mới — THÊM, không sửa entity hiện có):** `RedemptionCatalogItem`, `RedemptionRecord`, `Voucher`. Approved as part of feature plan.
  - KHÔNG sửa `LoyaltyRewards` entity (chỉ dùng `SubtractPointsAsync` có sẵn).
  - KHÔNG sửa `Order` entity (pay-with-points sẽ dùng Order metadata, không thêm field).
  - Multi-tenancy: catalog items + redemption records tenant-scoped.
  - **Storage decision (chốt 2026-07-23):** RedemptionCatalogItem + RedemptionRecord + Voucher lưu ở **ShopERP SQLite** (cùng PushSubscription — per-tenant business data, admin quản lý qua ShopERP).

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **RedemptionCatalogItem entity:** TenantId, ProductName, Description, ImageUrl, PointsRequired, IsActive, StockCount (nullable = unlimited), ValidFrom, ValidTo.
- [ ] **RedemptionRecord entity:** TenantId, CustomerId, CatalogItemId, VoucherId, PointsSpent, Status [Pending/Fulfilled/Cancelled/Expired], RedeemedAt, FulfilledAt, CancelledAt, Notes.
- [ ] **Voucher entity:** TenantId, RedemptionRecordId, VoucherCode (unique), QRCodeData, Status [Active/Used/Expired], IssuedAt, UsedAt, ExpiresAt.
- [ ] **Redeem flow:** Customer chọn catalog item → verify đủ điểm → SubtractPointsAsync → tạo RedemptionRecord (Pending) → tạo Voucher (Active) → return voucher code + QR.
- [ ] **Fulfillment flow:** Admin scan voucher QR → mark RedemptionRecord.Fulfilled + Voucher.Used.
- [ ] **IdentityLevel gate:** Redeem yêu cầu IdentityLevel >= Verified (đã có trong SubtractPointsAsync — reuse).
- [ ] **Pay-with-points (optional, Phase B-2 — chốt 2026-07-23):** Checkout.razor thêm option "Thanh toán bằng X điểm" → deduct points → giảm TotalAmount. **Optional** — implement sau core L-B nếu user request. Hỗ trợ partial (điểm + tiền) lẫn full.
- [ ] **QR generation:** Dùng `IQrCodeService` có sẵn (đã có trong codebase per project_state.md).

## 5. SUCCESS CRITERIA (12)
- [ ] SC1: `RedemptionCatalogItem` entity + migration (ShopERP SQLite).
- [ ] SC2: `RedemptionRecord` entity + migration.
- [ ] SC3: `Voucher` entity + migration.
- [ ] SC4: `RedemptionService.RedeemAsync(customerId, catalogItemId)` — verify đủ điểm → SubtractPointsAsync → tạo RedemptionRecord + Voucher → return voucher.
- [ ] SC5: `RedemptionService.FulfillAsync(voucherCode)` — admin scan → mark Fulfilled + Voucher.Used.
- [ ] SC6: `RedemptionService.CancelAsync(redemptionRecordId)` — cancel + refund points (AddPointsAsync).
- [ ] SC7: ShopERP `RedemptionController` — admin CRUD catalog + customer redeem + fulfillment endpoints.
- [ ] SC8: Gateway `RedemptionController` — forward to ShopERP.
- [ ] SC9: KhachLink `RedemptionCatalog.razor` (`/rewards`) — browse catalog + redeem + voucher/QR display.
- [ ] SC10: ShopERP `RedemptionCatalogAdmin.razor` — admin catalog management (CRUD).
- [ ] SC11: ShopERP `RedemptionHistory.razor` — admin redemption history + fulfillment UI.
- [ ] SC12: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS + tests PASS.

**Optional Phase B-2 (Pay-with-points):**
- [ ] SC13: Checkout.razor — "Thanh toán bằng điểm" option (partial hoặc full).
- [ ] SC14: Order total calculation with points deduction.

**Implementation Date:** _TBD_
**Branch:** `main` (sau Phase 5)

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — 3 entity mới, verify không phá existing
- `accounting-ui-implementation` — admin UI pattern
- `test-system-upgrade` — TDD cho redemption flow

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 10 (from audit 2026-07-23)
- **Verified Facts:**
  - Fact 1: `POST /api/loyalty/redeem` (ShopERP) REAL — chỉ deduct điểm, KHÔNG tạo voucher/record.
  - Fact 2: `SubtractPointsAsync` REAL — IdentityLevel >= Verified gate + balance check + DB persist.
  - Fact 3: `LoyaltyCard.razor` PARTIAL — có input số điểm, KHÔNG có catalog.
  - Fact 4: KHÔNG có `RedemptionCatalog` / `RedemptionRecord` / `Voucher` entity.
  - Fact 5: KHÔNG có admin UI redemption management.
  - Fact 6: KHÔNG có pay-with-points trong Checkout.razor.
  - Fact 7: `IQrCodeService` đã có (per project_state.md — QR generation cho products).
  - Fact 8: Gateway forward pattern đã có (LoyaltyController forward redeem).
  - Fact 9: `Customer.IdentityLevel` gate đã enforce trong SubtractPointsAsync.
  - Fact 10: Tests cover SubtractPointsAsync gate + balance (6 tests).
- **Assumptions:**
  - A1: `IQrCodeService` có thể generate QR cho voucher code (verify signature trước implement).
  - A2: Checkout.razor có thể thêm payment method option (verify UI structure).
- **Open Questions:**
  - Q1: Catalog storage → **ShopERP SQLite** (chốt 2026-07-23).
  - Q2: Pay-with-points → **Optional Phase B-2** (chốt 2026-07-23 — implement sau core nếu user request, hỗ trợ partial + full).
  - Q3: Voucher expiry — auto-expire sau bao lâu? → Default 30 days, configurable per catalog item.
- **Recommended Action:** ANALYZE `IQrCodeService` signature + Checkout.razor structure → IMPLEMENT (sau Phase 5).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs (thêm 3 entity) | THÊM entity, không sửa existing | Domain integrity validation |
| LoyaltyCard.razor (update redeem UI) | Sửa existing page — thay input số điểm bằng catalog browse | Keep old redeem-as-fallback nếu catalog empty |
| Checkout.razor (Phase B-2 only) | Sửa checkout flow — thêm points payment option | Feature flag default OFF |
| New files (services, controllers, pages) | New, no impact existing | Isolated |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:** RedemptionService.RedeemAsync — đủ điểm → tạo record + voucher; thiếu điểm → reject; IdentityLevel < Verified → reject.
- **Unit test:** RedemptionService.FulfillAsync — voucher code valid → mark fulfilled; invalid → 404; already used → 409.
- **Unit test:** RedemptionService.CancelAsync — cancel + refund points (AddPointsAsync).
- **Integration test:** Full redeem flow — customer redeem → voucher created → admin fulfill → status updated.
- **Integration test:** Pay-with-points (Phase B-2) — checkout with points → order total reduced.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | User chốt Q1-Q3 + ANALYZE IQrCodeService + Checkout.razor | Domain entities + EF config + migration |
| S2 | RedemptionService + repository | Code + unit tests |
| S3 | ShopERP + Gateway controllers | Code + integration tests |
| S4 | KhachLink RedemptionCatalog.razor + update LoyaltyCard.razor | Code + browser test |
| S5 | ShopERP admin UI (catalog + history) | Code |
| S6 | (Optional) Phase B-2 pay-with-points | Code + test |
| S7 | Full test suite + build + guard-check + RV | Test + RV report |

## 12. ESTIMATED EFFORT
- 6-7 sessions (core) + 1-2 sessions (Phase B-2 pay-with-points optional).
- **NO BLOCKER** (Q1 + Q2 đã chốt 2026-07-23: ShopERP SQLite + Optional Phase B-2).
- **Prerequisite:** Phase 5 COMPLETE (cho clean Domain.cs).
