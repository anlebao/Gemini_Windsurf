# TASK CARD: [SPRINT 3] - [CLEANUP] - E-Invoice Dead Code + Missing API/UI

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Dọn dead code EInvoice E2E tests, fix WebhookController route/body mismatch, quyết định architecture cho HKDElectronicInvoiceController (tạo lại hoặc thay thế), và plan EInvoice UI layer (6 Razor pages + 3 Playwright specs).
- **Nghiệp vụ áp dụng:** Hóa đơn điện tử multi-provider (Viettel, MISA) — Sprint 3 backend đã done, nhưng API layer + UI layer + E2E tests chưa hoàn thiện hoặc là dead code.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (cần approval trước khi IMPLEMENT)

### Chiến lược thực thi: JIT Planning + Pure Execution (Micro-phases)

Mỗi Session chạy 2 Micro-phases LIÊN TỤC trong 1 phiên:

```
[Session N]
  ├── Phase 1: JIT Planning
  │     Đọc boundary files 1 lần duy nhất → chốt: file cần sửa/tạo,
  │     tên test case, method signature, cấu trúc hàm.
  │     KHÔNG đọc ngoài boundary. KHÔNG giải thích dài.
  └── Phase 2: Pure Execution
        Bám chặt Phase 1 → viết thẳng.
        Token chỉ chi cho output code, không suy luận/re-explore.
```

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/task_sprint3_einvoice.md` (card gốc — SUPERSEDED cho backend, reference cho UI/E2E plan)
  - `docs/AI/tasks/task_sprint3b_provider_integration.md` (card Sprint 3B — backend done)
  - `6_Tests/VanAn.E2E.Tests/EInvoiceE2ETests.cs` — DEAD CODE, cần delete hoặc rewrite
  - `2_Gateway/Controllers/WebhookController.cs` — fix route + body shape
  - `2_Gateway/Controllers/ProviderController.cs` — verify (đã tồn tại)
  - `3_CoreHub/Services/Orchestration/EInvoiceOrchestrator.cs` — reference cho API contract
  - `3_CoreHub/Services/Orchestration/IWebhookService.cs` — reference
  - `5_WebApps/ShopERP/Components/Pages/` — reference cho UI Platform pattern
  - `6_Testing/e2e-tests/` — reference cho Playwright spec pattern
- **Boundary Rules (Nghiêm cấm):**
  - CẤM sửa Domain.cs (không có modeling defect)
  - CẤM sửa backend services (Sprint 3B đã done — chỉ đọc reference)
  - CẤM tạo controller mới nếu chưa có architecture decision (xem Open Question Q1)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Không sửa Domain layer.
- [ ] **Immutability:** AccountingEntry append-only — không liên quan task này.
- [ ] **UI Compliance:** 100% sử dụng linh kiện chuẩn từ `UI.Platform` (VanAnButton, VanAnCard, VanAnAlert, VanAForm, VanATable, VanALayout). Cấm viết custom HTML/CSS.
- [ ] **Gateway Purity:** Gateway MUST remain stateless reverse proxy. Nếu tạo HKDElectronicInvoiceController trong Gateway — chỉ forward, không business logic.
- [ ] **Legal Standards:** NĐ 123/2020/NĐ-CP, TT 78/2021/TT-BTC, TT 32/2025/TT-BTC — hóa đơn điện tử.
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Phase A — Dead Code Cleanup (P0)
- [ ] **SC1:** `EInvoiceE2ETests.cs` — DELETE file (5 tests dead code, endpoints không tồn tại) HOẶC rewrite sau khi SC3 done.
- [ ] **SC2:** `WebhookController.cs` — fix route `api/webhook` → `api/webhooks` (plural, consistent với E2E tests + provider convention) HOẶC update tất cả references sang singular.
- [ ] **SC3:** `WebhookController.cs` — fix body shape: hỗ trợ raw provider payload (Viettel/MISA DTO) thay vì chỉ `WebhookRequest` wrapper, HOẶC update E2E tests gửi `WebhookRequest` wrapper.
- [ ] **SC4:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

### Phase B — API Controller Decision (P0, cần approval)
- [ ] **SC5:** Architecture decision: tạo lại `HKDElectronicInvoiceController` trong `2_Gateway` (stateless forward) hay `5_WebApps/ShopERP` (có DbContext)? — cần User approval.
- [ ] **SC6:** Implement controller theo SC5 decision — CRUD + submit + status endpoints (`POST /api/einvoice`, `POST /api/einvoice/{id}/submit`, `GET /api/einvoice/{id}/status`).
- [ ] **SC7:** Controller có DTOs đầy đủ (InvoiceItemDto, InvoiceDto, CreateInvoiceRequest, v.v.) — không ghost files.

### Phase C — EInvoice UI Layer (P1, cần approval)
- [ ] **SC8:** 6 Razor pages tạo trong `5_WebApps/ShopERP/Components/Pages/EInvoice/`:
  - `EInvoiceDashboard.razor`
  - `ProviderManagement.razor`
  - `ProviderConfiguration.razor`
  - `HealthMonitoring.razor`
  - `InvoiceManagement.razor`
  - `AlertManagement.razor`
- [ ] **SC9:** Tất cả UI dùng VanAnButton, VanAnCard, VanAnAlert, VanAForm, VanATable, VanALayout — 0 custom HTML/CSS.
- [ ] **SC10:** Mobile-first responsive (≤640px, 641-1024px, ≥1025px).

### Phase D — E2E Playwright Specs (P1, sau SC8)
- [ ] **SC11:** 3 Playwright specs tạo trong `6_Testing/e2e-tests/`:
  - `einvoice-dashboard.spec.ts`
  - `provider-management.spec.ts`
  - `invoice-management.spec.ts`
- [ ] **SC12:** E2E specs test real UI flow (render, create, submit, retry) — không stub assertions.
- [ ] **SC13:** Re-enable E2E in CI (`if: false` → conditional) sau khi specs pass.

### Phase E — Validation
- [ ] **SC14:** `guard-check.ps1` → 0 errors.
- [ ] **SC15:** `dotnet test 6_Tests/VanAn.Core.Tests --configuration Release` → pass.
- [ ] **SC16:** `dotnet test 6_Tests/VanAn.Integration.Tests --configuration Release` → pass.
- [ ] **SC17:** Architecture tests VA1001-VA1005 → pass.

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration`
- `ui-platform-compliance-review`
- `ci-build-debug`

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 14 verified facts (từ REVIEW 2026-06-18 sprint3 + sprint3b cards)
- **Verified Facts:**
  - Fact 1: `HKDElectronicInvoiceController` đã DELETE — tạo `e4904a9`, xóa `e89b6c6` "purge dead code". `find_file_by_name` → No files found.
  - Fact 2: `EInvoiceE2ETests.cs` 5 tests gọi `/api/einvoice*` endpoints không tồn tại (controller đã delete).
  - Fact 3: `EInvoiceE2ETests.cs` gọi `/api/webhooks/viettel` (plural) — `WebhookController` route là `api/webhook` (singular). Route mismatch.
  - Fact 4: `WebhookController` expect `WebhookRequest(string ProviderInvoiceNumber, string CallbackData)` wrapper — E2E tests gửi raw Viettel/MISA payload. Body shape mismatch.
  - Fact 5: TC-E2E-04 stub assertion `OK || NotFound` — always passes, không verify behavior.
  - Fact 6: E2E tests disabled in CI — `.github/workflows/e2e.yml:115` `if: false`, `pr-check.yml:159` filter `Category!=E2E` + `|| true`.
  - Fact 7: 6 EInvoice Razor pages — 0 files exist (`grep *.razor` cho "EInvoiceDashboard|ProviderManagement" → No matches).
  - Fact 8: 3 E2E Playwright specs — 0 files exist (`find_file_by_name **/einvoice*.spec.ts` → No files found).
  - Fact 9: POS providers (KiotViet, Sapo) — STUBS, deferred Sprint 4 (không liên quan task này).
  - Fact 10: Backend Sprint 3B đã done — Domain, Providers, Orchestrator, WebhookService, CircuitBreaker, Outbox, DI wiring, appsettings.json.
  - Fact 11: `WebhookController.cs` đã tồn tại (59 lines) — chỉ cần fix route + body shape.
  - Fact 12: `ProviderController.cs` đã tồn tại trong `2_Gateway/Controllers/` — cần verify content.
  - Fact 13: Sprint 3 card plan 6 Razor pages (line 189-194) + 3 Playwright specs (line 249-253).
  - Fact 14: `docs/design/EInvoice UI Layout Design.md` + `docs/design/EInvoice multi provider integration.md` tồn tại — reference cho UI.
- **Assumptions:** 0
- **Open Questions:** 2
  - **Q1:** `HKDElectronicInvoiceController` nên tạo trong `2_Gateway` (stateless forward tới ShopERP) hay `5_WebApps/ShopERP` (có DbContext trực tiếp)? Gateway purity rule nói Gateway MUST remain stateless — nhưng hiện tại `WebhookController` đã inject `IWebhookService` (CoreHub service) trực tiếp. Cần clarify architecture.
  - **Q2:** WebhookController route — đổi sang `api/webhooks` (plural) hay giữ `api/webhook` (singular) + update E2E tests? Convention REST thường dùng plural.
- **Recommended Action:** **Investigate** — Open Questions (2) < 3, Assumptions (0) << Facts (14). Đủ evidence để bắt đầu ANALYZE, nhưng cần User approval cho Q1 + Q2 trước khi IMPLEMENT Phase B (controller).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| DELETE `EInvoiceE2ETests.cs` | Mất 5 test (đã dead code, không chạy CI) | ✅ Zero impact — tests không pass được |
| Fix `WebhookController` route | E2E tests + bất kỳ client nào gọi webhook | ⚠️ Update tất cả references |
| Fix `WebhookController` body shape | E2E tests + provider callback contracts | ⚠️ Verify provider real webhook format |
| Tạo `HKDElectronicInvoiceController` | Thêm endpoints `/api/einvoice*` | ⚠️ Cần DI wiring + DTOs đầy đủ |
| Tạo 6 Razor pages | UI navigation, menu, routing | ⚠️ Cần E2E test (Gate 4) |
| Tạo 3 Playwright specs | CI E2E pipeline | ⚠️ Cần re-enable E2E in CI |

## 9. TDD & E2E TESTING STRATEGY
- **Phase A (cleanup):** Không cần TDD — delete dead code + fix route.
- **Phase B (controller):** TDD — viết integration test cho mỗi endpoint trước, implement sau.
- **Phase C (UI):** Gate 4 — UI layout change → BẮT BUỘC viết E2E test tại `6_Testing/e2e-tests/`.
- **Phase D (E2E specs):** Playwright specs test real UI flow — không stub assertions (learn from TC-E2E-04 mistake).

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc WebhookController + E2E tests → chốt route fix + body shape fix | Phase A: DELETE/rewrite EInvoiceE2ETests + fix WebhookController route + body |
| **S2** | Architecture decision Q1 + Q2 → chốt controller location + DTOs | Phase B: Tạo HKDElectronicInvoiceController + DTOs + DI wiring + integration tests |
| **S3** | Đọc UI Platform guide + EInvoice UI design doc → chốt 6 page layouts | Phase C: Tạo 6 Razor pages với VanAn components |
| **S4** | Đọc Playwright spec pattern → chốt 3 spec flows | Phase D: Tạo 3 Playwright specs + re-enable CI |
| **S5** | Full validation | Phase E: guard-check + build + tests + arch tests |

## 11. REFERENCES
- Sprint 3 card (SUPERSEDED for backend, reference for UI/E2E plan): `docs/AI/tasks/task_sprint3_einvoice.md`
- Sprint 3B card (backend done): `docs/AI/tasks/task_sprint3b_provider_integration.md`
- Sprint 3B detailed plan: `docs/AI/tasks/sprint3b_provider_detailed_plan.md`
- EInvoice UI design: `docs/design/EInvoice UI Layout Design.md`
- EInvoice multi-provider design: `docs/design/EInvoice multi provider integration.md`
- Ghost files fix plan (history): `.devin/plans/fix-ghost-files-build.md`
- UI Platform guide: `docs/UI_Platform_Implementation_Guide.md`
