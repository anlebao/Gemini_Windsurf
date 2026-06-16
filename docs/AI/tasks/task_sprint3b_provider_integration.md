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
- [ ] `EInvoiceProviderTests.cs` — tất cả test mock HTTP pass (Viettel + MISA submit/status/cancel)
- [ ] `EInvoiceOrchestratorTests.cs` — CreateInvoice, GetInvoice, SubmitInvoice flow pass
- [ ] `InvoicePolicyServiceTests.cs` — ValidateInvoice, CanSubmit, B2B/B2C rules pass
- [ ] `WebhookServiceTests.cs` (Core.Tests) — Viettel/MISA webhook idempotency pass
- [ ] `WebhookServiceTests.cs` (Integration.Tests) — DB integration pass
- [ ] `CircuitBreakerTests.cs` (nếu chưa có, tạo mới) — Open/HalfOpen/Closed transitions pass
- [ ] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] DI registration trong `3_CoreHub/Program.cs` đầy đủ (ViettelConfig, MisaConfig, named HttpClients)
- [ ] `RetryPolicyService` submitAction wired với real provider (fix TODO F4)
- [ ] Config placeholders trong `appsettings.json` (credentials = `__PLACEHOLDER__`, không commit secret)

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration`
- `ci-build-debug`
- `domain-integrity-validation`

## 7. AI HEALTH CHECK MATRIX

**Cập nhật lần cuối:** 2026-06-14 — Full codebase audit hoàn tất

- **Evidence Count:** 20+ files đã đọc, audit đầy đủ tất cả layers
- **Verified Facts:**
  - Fact 1: `ViettelEInvoiceProvider.cs` — REAL: HTTP auth (JWT cache 55'), submit, status, cancel, healthcheck
  - Fact 2: `MisaEInvoiceProvider.cs` — REAL: HTTP auth (JWT cache 55'), submit, status, cancel, healthcheck
  - Fact 3: `EInvoiceOrchestrator.cs` — REAL: CreateInvoice (Outbox transaction), GetInvoice, SubmitInvoice, ProcessWebhook
  - Fact 4: `InvoicePolicyService.cs` — REAL: amount (1K–100B VND), B2B/B2C check, VAT (10%/8%), TT152-2025
  - Fact 5: `WebhookService.cs` — REAL: idempotency L1 (ConcurrentDictionary) + L2 (DB), Viettel/MISA typed parsing
  - Fact 6: `CircuitBreakerService.cs` — REAL: Closed→Open (5 failures)→HalfOpen→Closed, 5' cooldown
  - Fact 7: `RetryPolicyService.cs` — REAL: 3 retries, backoff 1s/2s/4s, logging. **Nhưng submitAction trong DI = Task.CompletedTask (TODO F4)**
  - Fact 8: `ComplianceService.cs` — REAL: CustomerName, TaxCode ≥10, TotalAmount >0, TT152-2025
  - Fact 9: `FallbackService.cs` — REAL: chọn provider không failed từ ProviderManager
  - Fact 10: `EInvoiceProviderFactory.cs` / `EInvoiceProviderRegistry.cs` — REAL code, nhưng **chưa được register vào DI**
  - Fact 11: `WebhookController.cs` — REAL: POST /api/webhook/{provider} endpoint
  - Fact 12: `HKDElectronicInvoiceController.cs` — REAL: CRUD + submit endpoints
  - Fact 13: `WebhookDtos.cs` (`1_Shared/DTOs/`) — **✅ CREATED 2026-06-14** — ViettelWebhookDto + MisaWebhookDto
  - Fact 14: `ViettelDTOs.cs` / `MisaDTOs.cs` — tồn tại trong Providers/EInvoice/ — ViettelConfig, MisaConfig records
  - Fact 15: `3_CoreHub/Program.cs` — **ViettelConfig/MisaConfig chưa Configure<T>(), named HttpClients chưa AddHttpClient()**
  - Fact 16: `EInvoiceWorker.cs` — REAL: BackgroundService, 30s cycle, batch 50, dead-letter queue ≥5 retries
  - Fact 17: Unit tests — 35+ cases across EInvoiceOrchestratorTests, InvoicePolicyServiceTests, WebhookServiceTests, EInvoiceProviderTests
  - Fact 18: Integration tests — 12+ cases: WebhookServiceTests (Integration), EInvoiceDISmokeTests
  - Fact 19: Build Release → 0 errors (confirmed 2026-06-14 trước khi tạo WebhookDtos.cs)
  - Fact 20: Build Release cần verify lại sau khi tạo WebhookDtos.cs
- **Assumptions:** 0
- **Open Questions:** 0
- **Recommended Action:** Continue — Bắt đầu Session S1: DI Wiring (4 P0 items còn lại)

## 8. DETAILED PLAN (REFERENCE)
- **Kế hoạch chi tiết:** [sprint3b_provider_detailed_plan.md](./sprint3b_provider_detailed_plan.md)
