# TASK CARD: [SPRINT 3B] - [PHASE 5] - E-Invoice Provider Integration (Viettel + MISA)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Hoàn tất Sprint 3 E-Invoice — kết nối thực tế với 2 nhà cung cấp Viettel SInvoicer và MISA meInvoice, bao gồm authentication, submit invoice, xử lý webhook callback, circuit breaker và retry policy.
- **Nghiệp vụ áp dụng:** Hóa đơn điện tử theo Nghị định 70/2025/NĐ-CP, Thông tư 32/2025/TT-BTC, Nghị định 123/2020/NĐ-CP, Thông tư 78/2021/TT-BTC.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.windsurf/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT

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
  - `docs/AI/tasks/sprint3b_provider_detailed_plan.md` (kế hoạch chi tiết session-by-session)
  - `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs`
  - `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs`
  - `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs`
  - `3_CoreHub/Services/Providers/EInvoice/EInvoiceProviderFactory.cs`
  - `3_CoreHub/Services/Providers/EInvoice/EInvoiceProviderRegistry.cs`
  - `3_CoreHub/Services/Orchestration/EInvoiceOrchestrator.cs`
  - `3_CoreHub/Services/Orchestration/InvoicePolicyService.cs`
  - `3_CoreHub/Services/Orchestration/WebhookService.cs`
  - `3_CoreHub/Services/Orchestration/WebhookDtos.cs`
  - `3_CoreHub/Services/Resilience/CircuitBreakerService.cs`
  - `3_CoreHub/Services/Orchestration/RetryPolicyService.cs`
  - `3_CoreHub/Program.cs`
  - `1_Shared/Domain.cs` (chỉ phần ElectronicInvoice/InvoiceStatus/InvoiceItem ~line 1500-1650)
  - `6_Tests/VanAn.Core.Tests/Services/EInvoiceOrchestratorTests.cs`
  - `6_Tests/VanAn.Core.Tests/Services/EInvoiceProviderTests.cs`
  - `6_Tests/VanAn.Core.Tests/Services/InvoicePolicyServiceTests.cs`
  - `6_Tests/VanAn.Core.Tests/Services/WebhookServiceTests.cs`
  - `6_Tests/VanAn.Integration.Tests/Services/WebhookServiceTests.cs`
- **Boundary Rules (Nghiêm cấm):**
  - CẤM đọc lại Sprint 1/2/UC1 code trừ khi có lỗi build/test phát sinh.
  - CẤM chỉnh sửa Domain Layer ngoại trừ `1_Shared/Domain.cs` khi có modeling defect công nhận.
  - CẤM tạo Controller mới trong `2_Gateway` — webhook endpoint đã có tại `WebhookController.cs`.

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** `ElectronicInvoice`, `InvoiceItem` không chứa EF Core, DbContext, DataAnnotations.
- [ ] **Idempotency:** Webhook processing phải idempotent (L1: in-memory ConcurrentDictionary, L2: DB check).
- [ ] **Circuit Breaker:** 5 failures → Open (5 phút cooldown) → Half-Open → Closed.
- [ ] **Retry Policy:** 3 lần retry, exponential backoff 2^n giây, jitter ±20%.
- [ ] **Auth Token Cache:** Token Viettel/MISA cache 55 phút (expire 60 phút), refresh proactive.
- [ ] **Legal Standards:** Trường bắt buộc B2B: CustomerTaxCode (NĐ 123/2020). VAT rate: 10% hàng hóa, 8% dịch vụ (TT 152/2025).
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Done khi tất cả pass:
- [x] `1_Shared/DTOs/WebhookDtos.cs` — `ViettelWebhookDto` + `MisaWebhookDto` ✅ CREATED 2026-06-14
- [ ] **`EInvoiceProviderTests.cs` — VIETTEL/MISA HTTP MOCK TESTS KHÔNG TỒN TẠI** ❌ (REVIEW 2026-06-18: file chỉ chứa Registry/Factory/DTO plumbing tests + MockEInvoiceProvider helper; 0 MockHttp/HttpMessageHandler; 9 cases S2 plan chưa viết)
- [ ] **`EInvoiceOrchestratorTests.cs` — PARTIAL** ⚠️ (REVIEW 2026-06-18: 9 test chỉ verify delegation/call-order/exception; KHÔNG có test `CreateInvoiceAsync` verify DB write + Outbox enqueue; KHÔNG có `GetInvoiceAsync` flow test)
- [ ] **`InvoicePolicyServiceTests.cs` (Core.Tests) — PARTIAL/MISLEADING** ⚠️ (REVIEW 2026-06-18: chỉ test failure path `WithoutDbContext` + pure logic `DetermineRecipientType`/`IsEInvoiceRequired`/`ValidateBusinessPolicy`; KHÔNG test `ValidateInvoiceAsync` happy/sad path ở unit level — bù đắp ở Integration.Tests)
- [ ] **`WebhookServiceTests.cs` (Core.Tests) — STUB/FALSE CONFIDENCE** ❌ (REVIEW 2026-06-18: dùng `new WebhookService()` parameterless → `_dbContext=null` → L2 DB idempotency OFF; test chỉ assert `Should not throw`; comment test author tự ghi "stub implementation"; KHÔNG test production path)
- [x] **`WebhookServiceTests.cs` (Integration.Tests) — DB integration REAL** ✅ (REVIEW 2026-06-18: 6 test REAL với DB — Viettel Approved/Rejected, MISA Approved, invalid JSON, duplicate idempotency, non-existent invoice)
- [ ] **`CircuitBreakerTests.cs` — KHÔNG TỒN TẠI** ❌ (REVIEW 2026-06-18: `find_file_by_name` → No files found; `CircuitBreakerService.cs` 0% test coverage)
- [ ] `dotnet build VanAn.sln --configuration Release` → 0 errors (claim 2026-06-14, CHƯA re-verify sau WebhookDtos.cs)
- [x] **DI registration trong `3_CoreHub/Program.cs` ĐỦ** ✅ (REVIEW 2026-06-18: `Program.cs:103-118` Configure<ViettelConfig/MisaConfig> + AddHttpClient named "viettel"/"misa"; `:121-128` Registry+Factory; `:190-192` Orchestrator+CircuitBreaker+Worker)
- [x] **`RetryPolicyService` submitAction wired với real provider** ✅ (REVIEW 2026-06-18: `Program.cs:132-183` đã wire `submitAction` → `factory.CreateProvider` + `provider.SubmitInvoiceAsync` + `breaker.RecordSuccess/Failure`; TODO F4 ĐÃ FIX)
- [x] **Config placeholders trong `appsettings.json`** ✅ (REVIEW 2026-06-18: `appsettings.json:25-39` ViettelConfig + MisaConfig sections, credentials = `__PLACEHOLDER__`)

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration`
- `ci-build-debug`
- `domain-integrity-validation`

## 7. AI HEALTH CHECK MATRIX

**Cập nhật lần cuối:** 2026-06-18 — REVIEW_ONLY audit (verify code thực tế vs claims cũ 2026-06-14)

- **Evidence Count:** 12 verified facts (đọc 8 file code + 4 file test + Program.cs + appsettings.json)
- **Verified Facts (REVIEW 2026-06-18):**
  - Fact 1: `ViettelEInvoiceProvider.cs` — REAL: HTTP auth (JWT cache 55'), submit `createInvoice`, status, cancel, healthcheck ✅
  - Fact 2: `MisaEInvoiceProvider.cs` — REAL: HTTP auth (JWT cache 55'), submit `einvoices`, status, cancel, healthcheck ✅
  - Fact 3: `EInvoiceOrchestrator.cs` — REAL: CreateInvoice (transaction + Outbox), GetInvoice, SubmitInvoice (policy→compliance→retry), ProcessWebhook ✅
  - Fact 4: `InvoicePolicyService.cs` — REAL: amount/B2B-B2C/VAT logic ✅
  - Fact 5: `WebhookService.cs` — REAL: L1 ConcurrentDictionary + L2 DB `ProcessedWebhookKeys`, typed Viettel/MISA DTO parsing ✅
  - Fact 6: `CircuitBreakerService.cs` — REAL code (3665 bytes) NHƯNG **0% test coverage** ❌
  - Fact 7: ~~`RetryPolicyService submitAction = Task.CompletedTask` (TODO F4)~~ → **OUTDATED/ĐÃ FIX**: `Program.cs:132-183` đã wire submitAction tới real provider + circuit breaker ✅
  - Fact 8: `ComplianceService.cs` — REAL ✅
  - Fact 9: `FallbackService.cs` — REAL ✅
  - Fact 10: ~~`Factory/Registry chưa register vào DI`~~ → **OUTDATED/ĐÃ FIX**: `Program.cs:121-128` đã register Registry (Singleton) + Factory (Scoped) + 2 providers ✅
  - Fact 11: `WebhookController.cs` — REAL ✅
  - ~~Fact 12: `HKDElectronicInvoiceController.cs` — REAL ✅~~ → **FALSE (REVIEW 2026-06-18 sprint3 card)**: File đã DELETE commit `e89b6c6` "purge dead code". `find_file_by_name **/HKDElectronicInvoiceController*` → No files found. Git log: tạo `e4904a9`, xóa `e89b6c6`. Endpoints `/api/einvoice*` không tồn tại — `EInvoiceE2ETests.cs` gọi endpoints này là DEAD CODE. ❌
  - Fact 13: `WebhookDtos.cs` — ✅ CREATED 2026-06-14
  - Fact 14: `ViettelDTOs.cs` / `MisaDTOs.cs` — tồn tại ✅
  - Fact 15: ~~`ViettelConfig/MisaConfig chưa Configure<T>(), named HttpClients chưa AddHttpClient()`~~ → **OUTDATED/ĐÃ FIX**: `Program.cs:103-118` đã Configure + AddHttpClient named "viettel"/"misa" ✅
  - Fact 16: `EInvoiceWorker.cs` — REAL, tồn tại tại `3_CoreHub/Infrastructure/Messaging/` ✅
  - Fact 17: ~~"35+ unit tests"~~ → **MISLEADING**: số lượng đúng nhưng chất lượng ảo — xem Facts 21-24
  - Fact 18: Integration tests — REAL: `Integration.Tests/WebhookServiceTests.cs` (6 test DB), `Integration.Tests/InvoicePolicyServiceTests.cs` (8 test DB), `EInvoiceDISmokeTests.cs` ✅
  - Fact 19: Build Release → 0 errors (claim 2026-06-14, CHƯA re-verify sau WebhookDtos.cs) ⚠️
  - **Fact 21 (MỚI):** `EInvoiceProviderTests.cs` — KHÔNG có HTTP mock tests cho Viettel/MISA. File chỉ chứa Registry/Factory/Capabilities/Request/Response DTO tests + MockEInvoiceProvider helper. 0 MockHttp/HttpMessageHandler. 9 cases S2 plan CHƯA viết. ❌
  - **Fact 22 (MỚI):** `Core.Tests/WebhookServiceTests.cs` — STUB-VALIDATING. Dùng `new WebhookService()` parameterless → `_dbContext=null` → L2 DB idempotency OFF. Test chỉ assert `Should not throw`. Comment test author tự ghi "stub implementation" (line 78, 154). False confidence. ❌
  - **Fact 23 (MỚI):** `EInvoiceOrchestratorTests.cs` — 9 test chỉ verify delegation/call-order/exception. KHÔNG có test `CreateInvoiceAsync` (DB write + Outbox enqueue) hay `GetInvoiceAsync` flow. ⚠️ PARTIAL
  - **Fact 24 (MỚI):** `Core.Tests/InvoicePolicyServiceTests.cs` — chỉ test failure path (`WithoutDbContext`) + pure logic (`DetermineRecipientType`/`IsEInvoiceRequired`/`ValidateBusinessPolicy` via reflection). KHÔNG test `ValidateInvoiceAsync` happy/sad path ở unit level. ⚠️ PARTIAL (bù đắp ở Integration.Tests)
  - **Fact 25 (MỚI):** `CircuitBreakerTests.cs` — KHÔNG TỒN TẠI (`find_file_by_name` → No files found). ❌
- **Assumptions:** 0
- **Open Questions:** 1 (build Release chưa re-verify sau WebhookDtos.cs — không chạy trong REVIEW_ONLY)
- **Recommended Action:** Code production đã done (DI/RetryPolicyService/config/providers/orchestrator/webhook REAL). **P0 tiếp theo là viết tests thật**: (1) EInvoiceProviderTests HTTP mock 9 cases, (2) CircuitBreakerTests, (3) EInvoiceOrchestratorTests.CreateInvoiceAsync flow. **P1 dọn false confidence**: xóa/rewrite Core.Tests/WebhookServiceTests stub, bổ sung Core.Tests/InvoicePolicyService happy path.

## 8. DETAILED PLAN (REFERENCE)
- **Kế hoạch chi tiết:** [sprint3b_provider_detailed_plan.md](./sprint3b_provider_detailed_plan.md)
