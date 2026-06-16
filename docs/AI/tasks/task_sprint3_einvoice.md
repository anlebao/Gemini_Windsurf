# TASK CARD: [SPRINT 3] - [PHASE 5] - E-Invoice Multi-Provider Integration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement E-Invoice multi-provider integration với domain models, provider interfaces, circuit breaker và outbox pattern cho hệ thống kế toán HKD.
- **Nghiệp vụ áp dụng:** Hóa đơn điện tử theo Nghị định 70/2025/NĐ-CP và Thông tư 32/2025/TT-BTC.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** .windsurf/workflows/newfeaturebuild.md
- **Execution Mode:** ANALYZE -> IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/plan_MVP/RoadMap/MVP plan Account M.md` (chứa Design Reference & UI Reference)
  - `1_Shared/Domain.cs` (sẽ thêm E-Invoice domain models)
  - `3_CoreHub/Services/EInvoice/` (thư mục mới cho E-Invoice services)
- **Boundary Rules (Nghiêm cấm):**
  - CẤM đọc lại source code của Audit Trail hay Period Closing trừ khi có lỗi build/test phát sinh.
  - CẤM đọc file roadmap dài hạn trừ khi cần xác định chi tiết E-Invoice requirements.
  - CẤM chỉnh sửa Domain Layer ngoại trừ file `1_Shared/Domain.cs` khi có modeling defect công nhận.

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Domain entity không chứa EF Core, DbContext, DataAnnotations.
- [ ] **Immutability:** `AccountingEntry` phải 100% append-only, thay đổi qua Reversal Entry.
- [ ] **UI Compliance:** 100% sử dụng linh kiện chuẩn từ `UI.Platform`. Cấm viết custom HTML/CSS.
- [ ] **Legal Standards:** Nghị định 123/2020/NĐ-CP và Thông tư 78/2021/TT-BTC về hóa đơn điện tử.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] Domain models cho E-Invoice được tạo trong `1_Shared/Domain.cs`
- [ ] Provider interfaces được định nghĩa trong `3_CoreHub/Services/EInvoice/`
- [ ] Circuit breaker pattern được implement
- [ ] Outbox pattern được implement
- [ ] Unit tests cho E-Invoice components pass
- [ ] Chạy `guard-check.ps1` đạt kết quả 0 errors

## 6. ACTIVE SKILLS (MAX 3)
- einvoice-integration
- ui-platform-compliance-review
- domain-integrity-validation

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 0
- **Verified Facts:**
  - Fact 1: File `1_Shared/Domain.cs` hiện chưa có Entity EInvoice
  - Fact 2: Thư mục `3_CoreHub/Services/EInvoice/` chưa tồn tại
  - Fact 3: RoadMap đã xác định Sprint 3 - Phase 5 với Design Reference và UI Reference
  - Fact 4: Circuit breaker pattern và outbox pattern đã được thiết kế trong plan
  - Fact 5: Legal requirements theo Nghị định 123/2020/NĐ-CP đã được xác định
- **Assumptions:** 0
- **Open Questions:** 0
- **Recommended Action:** Continue — Start Sprint 3

---

## 8. KẾ HOẠCH THEO WORKFLOW NEWFEATUREBUILD.MD

### Phase: ANALYZE (Steps 1-4) - Không sửa code

#### Step 1: Use Case & Business Design

**Đã đọc Design Reference và UI Reference**
**Cần xác định Use Cases chính cho E-Invoice**

**Use Cases Chính:**

1. **Multi-Provider Invoice Submission**
   - Actor: Kế toán viên, System
   - Description: Submit hóa đơn điện tử đến multiple E-Invoice providers (Viettel, BKAV, MISA)
   - Preconditions: Provider configured, Order completed
   - Flow:
     - System tạo Invoice entity với status Draft
     - User submit invoice → status PendingSend
     - Orchestrator chọn provider (primary/fallback)
     - Submit đến provider → status SentToProvider
     - Provider callback → status TaxApproved/Failed
   - Postconditions: Invoice stored in database, Outbox event created, Audit trail logged

2. **HKD Revenue Classification**
   - Actor: System (automatic)
   - Description: Phân loại doanh thu HKD theo 4 nhóm (≤500M, >500M-1B, >1B-3B, >3B)
   - Preconditions: AccountingEntry exists
   - Flow:
     - System aggregate revenue theo period
     - Apply TT152-2025 thresholds
     - Assign HKDRevenueGroup
   - Postconditions: HKD classification updated, Compliance validated

3. **Provider Failover & Circuit Breaker**
   - Actor: System (automatic)
   - Description: Auto-switch provider khi failure
   - Preconditions: Primary provider failed, Circuit breaker open
   - Flow:
     - Track SubmitAttempt (Provider, Timestamp, Status)
     - Retry same provider (max N times)
     - Switch to fallback provider if retry exhausted
     - Circuit breaker prevents cascade failures
   - Postconditions: Invoice submitted to backup provider, SLA maintained

4. **Idempotency & Duplicate Prevention**
   - Actor: System (enforcement)
   - Description: Prevent duplicate invoice submission (legal compliance)
   - Preconditions: UNIQUE constraint on (TenantId, OrderId)
   - Flow:
     - Check duplicate before create
     - Throw exception if exists
     - Return existing invoice if duplicate request
   - Postconditions: 0 duplicate invoices, Legal compliance maintained

5. **Async Outbox Processing**
   - Actor: Background worker
   - Description: Non-blocking invoice submission with atomic transaction
   - Preconditions: Invoice created, Outbox event created
   - Flow:
     - Atomic save: Invoice + Outbox in same transaction
     - Background worker processes Outbox events
     - Submit to provider asynchronously
     - Update Invoice status on callback
   - Postconditions: Non-blocking API, Reliable delivery, Audit trail complete

6. **Webhook Callback Handling**
   - Actor: Provider → System
   - Description: Receive provider callback for invoice status
   - Preconditions: Provider configured, Webhook endpoint registered
   - Flow:
     - Receive webhook from provider
     - Validate idempotency (check if already processed)
     - Update Invoice status
     - Create accounting entry if TaxApproved
     - Log audit trail
   - Postconditions: Invoice status updated, Accounting entry created, Audit logged

**Business Rules:**
- Domain Purity: Domain entities trong Domain.cs KHÔNG chứa EF Core, DbContext
- Immutability: AccountingEntry 100% append-only, Reversal Entry pattern
- State Machine: InvoiceAggregate enforce state transitions (Draft → PendingSend → SentToProvider → TaxApproved/Failed)
- Atomic Transaction: Invoice + Outbox must be saved in same transaction
- Multi-tenancy: Mọi query phải filter theo TenantId
- Legal Compliance: TT152-2025 requirements enforced (5-year storage, digital signatures, tax authority submission)

**Success Criteria:**
- Domain models cho E-Invoice trong Domain.cs
- Provider interfaces trong 3_CoreHub/Services/EInvoice/
- Circuit breaker pattern implemented
- Outbox pattern implemented
- Unit tests pass
- guard-check.ps1 0 errors

---

#### Step 2: Reverse Impact Analysis + TDD Plan

**Impact Analysis by Layer:**

1. **Domain Layer (Domain.cs)**
   - Impact: HIGH - Thêm mới domain entities
   - Changes:
     - Add ElectronicInvoice entity
     - Add InvoiceAggregate with state machine
     - Add OutboxEvent entity
     - Add HKDRevenueClassification value object
     - Add ProviderConfiguration entity
     - Add enums: InvoiceStatus, InvoiceType, HKDRevenueGroup, ProviderStatus
     - Add domain events: InvoiceSubmitted, InvoiceConfirmed, InvoiceRejected
   - Risk: Domain purity violation (EF Core, DbContext) → Gate 5: Domain Integrity

2. **Application Layer (Services)**
   - Impact: HIGH - Thêm mới services
   - Changes:
     - Services/Providers/POS/ - POS provider interfaces & implementations
     - Services/Providers/EInvoice/ - E-Invoice provider interfaces & implementations
     - Services/Orchestration/ - Orchestrator, Retry, Fallback, Compliance, Webhook services
     - Services/Resilience/ - Circuit breaker service
     - Services/Monitoring/ - Provider monitoring service
     - Infrastructure/Repositories/ - TenantProviderConfiguration, Outbox repositories
     - Infrastructure/Messaging/ - Outbox processor, background worker
   - Risk: God service anti-pattern → Gap 6: Orchestrator becoming God service (FIXED in design)

3. **API Layer (Controllers)**
   - Impact: MEDIUM - Thêm mới controllers
   - Changes:
     - HKDElectronicInvoiceController.cs - Invoice CRUD + submit
     - ProviderController.cs - Provider management
     - WebhookController.cs - Provider callbacks
   - Risk: Idempotency not enforced → Gap 3: Idempotency not clear (FIXED in design)

4. **UI Layer (ShopERP)**
   - Impact: MEDIUM - Thêm mới UI components
   - Changes:
     - Components/Pages/EInvoice/EInvoiceDashboard.razor
     - Components/Pages/EInvoice/ProviderManagement.razor
     - Components/Pages/EInvoice/ProviderConfiguration.razor
     - Components/Pages/EInvoice/HealthMonitoring.razor
     - Components/Pages/EInvoice/InvoiceManagement.razor
     - Components/Pages/EInvoice/AlertManagement.razor
   - Risk: UI Platform compliance → Gate 4: UI Layout → E2E Test requirement

5. **Infrastructure Layer**
   - Impact: LOW - Configuration updates
   - Changes:
     - appsettings.json - Provider configurations
     - DI container - Service registrations
     - Database schema - New tables for Invoice, Outbox, ProviderConfiguration
   - Risk: Transaction boundary not locked → Gap 8: Transaction boundary not locked (FIXED in design)

**TDD Plan:**

**Phase 1: Domain Tests (Days 1-2)**
- Location: 6_Tests/VanAn.Core.Tests/Domain/EInvoice/
- Test Cases:
  - InvoiceAggregateTests.cs: CreateInvoice, SubmitInvoice, MarkAsSent, MarkAsApproved, MarkAsFailed
  - ElectronicInvoiceTests.cs: CreateInvoice, DuplicateTenantOrderId, InvalidAmount
  - OutboxEventTests.cs: CreateOutboxEvent, MarkAsProcessed
  - HKDRevenueClassificationTests.cs: ClassifyRevenue_Group1-4

**Phase 2: Provider Tests (Days 3-4)**
- Location: 6_Tests/VanAn.Core.Tests/Providers/
- Test Cases:
  - POSProviderFactoryTests.cs: CreateProvider_KiotViet, Sapo, Unknown
  - EInvoiceProviderFactoryTests.cs: CreateProvider_Viettel, MISA
  - ProviderRegistryTests.cs: RegisterProvider, GetProvider

**Phase 3: Orchestration Tests (Days 5-6)**
- Location: 6_Tests/VanAn.Core.Tests/Orchestration/
- Test Cases:
  - EInvoiceOrchestratorTests.cs: SubmitInvoice, ProviderFailure, RetryExhausted
  - RetryPolicyServiceTests.cs: ShouldRetry_FirstFailure, MaxRetryExceeded
  - FallbackServiceTests.cs: SelectFallbackProvider, AllFailed
  - ComplianceServiceTests.cs: ValidateInvoice_Compliant, NonCompliant
  - WebhookServiceTests.cs: ProcessWebhook_Valid, Duplicate

**Phase 4: Resilience Tests (Day 9)**
- Location: 6_Tests/VanAn.Core.Tests/Resilience/
- Test Cases:
  - CircuitBreakerServiceTests.cs: RecordFailure, ThresholdExceeded, IsOpen, Reset

**Phase 5: Integration Tests (Days 10-11)**
- Location: 6_Tests/VanAn.E2E.Tests/EInvoice/
- Test Cases:
  - EInvoiceIntegrationTests.cs: SubmitInvoice_EndToEnd, WithFallback
  - OutboxIntegrationTests.cs: InvoiceWithOutbox, OutboxProcessor

**Phase 6: API Tests (Days 10-11)**
- Location: 6_Tests/VanAn.E2E.Tests/Controllers/
- Test Cases:
  - HKDElectronicInvoiceControllerTests.cs: CreateInvoice, Duplicate, Submit
  - WebhookControllerTests.cs: ReceiveWebhook_Valid, Duplicate

**Phase 7: E2E UI Tests (Week 4)**
- Location: 6_Testing/e2e-tests/einvoice-flow.spec.ts
- Test Cases (Gate 4: UI Layout → E2E Test requirement):
  - einvoice-dashboard.spec.ts: render dashboard, provider status, activity feed
  - provider-management.spec.ts: display provider list, configure credentials, test connection
  - invoice-management.spec.ts: display invoice list, create invoice, submit invoice, retry failed

---

#### Step 3: Detailed Coding Plan + Namespace Strategy

**Detailed Coding Plan (Days 1-11):**

**Day 1: Domain Models & Invoice Aggregate**
- Location: Domain.cs
- Priority: CRITICAL - Domain first
- Tasks:
  - Add enums: InvoiceStatus, InvoiceType, HKDRevenueGroup, ProviderStatus
  - Add value objects: ElectronicInvoiceId, ProviderId, InvoiceIdempotencyKey
  - Add entities: ElectronicInvoice, InvoiceAggregate, OutboxEvent, SubmitAttempt, HKDRevenueClassification, ProviderConfiguration
  - Add domain events: InvoiceSubmitted, InvoiceConfirmed, InvoiceRejected
- Validation: Domain purity, State machine, Idempotency

**Day 2: Provider Interfaces**
- Location: Providers
- Priority: HIGH
- Tasks:
  - Create IPOSProvider.cs with methods and capabilities
  - Create IEInvoiceProvider.cs with methods and capabilities
  - Create raw request/response DTOs
- Validation: Stateless providers, Generic interface pattern, Capability normalization

**Day 3: Provider Factory & Registry**
- Location: Providers
- Priority: HIGH
- Tasks:
  - Create POS provider factory and registry
  - Create E-Invoice provider factory and registry
  - Add ProviderAttribute for auto-registration
- Validation: Factory pattern, Registry with auto-discovery, DI setup

**Day 4: Provider Manager & Configuration**
- Location: Services + Infrastructure/Repositories/
- Priority: HIGH
- Tasks:
  - Create IProviderManager and ProviderManager
  - Create ITenantProviderConfigurationService and implementation
  - Implement multi-tenant provider management
  - Add configuration caching and health checking
- Validation: Multi-tenancy, Caching strategy, Health check

**Day 5: HKD Revenue Classification**
- Location: Orchestration
- Priority: MEDIUM
- Tasks:
  - Create IHKDRevenueClassificationService and implementation
  - Implement 4-level revenue classification logic
  - Add TT152-2025 compliance validation
- Validation: Revenue group detection, TT152-2025 compliance

**Day 6: Split Services (Anti-God Service)**
- Location: Orchestration
- Priority: CRITICAL - Prevent God Service
- Tasks:
  - Create IEInvoiceOrchestrator (ONLY coordination)
  - Create focused services: InvoicePolicy, RetryPolicy, Fallback, Compliance, Webhook
- Validation: Service separation, Orchestrator coordination only, Focused interfaces

**Day 7: Atomic Outbox Pattern**
- Location: Messaging
- Priority: CRITICAL - Transaction Safety
- Tasks:
  - Create IOutboxRepository and OutboxRepository
  - Create EInvoiceWorker background worker
  - Implement ATOMIC Invoice + Outbox save
  - Add Dead Letter Queue, retry mechanisms, webhook idempotency
- Validation: Atomic transaction, Transaction rollback, Dead Letter Queue

**Day 8: Example POS Providers**
- Location: POS
- Priority: MEDIUM
- Tasks:
  - Create KiotVietProvider and SapoProvider
  - Implement provider-specific mappings and error handling
- Validation: Stateless implementation, Interface compliance

**Day 9: Example E-Invoice Providers**
- Location: EInvoice
- Priority: MEDIUM
- Tasks:
  - Create ViettelProvider and MISAProvider
  - Implement digital signature integration and tax authority submission
- Validation: Stateless implementation, Interface compliance, Digital signature

**Day 10: Circuit Breaker & Safe Failover**
- Location: Resilience
- Priority: CRITICAL - Resilience
- Tasks:
  - Create ICircuitBreakerService and CircuitBreakerService
  - Implement circuit breaker pattern (Closed → Open → Half-Open → Closed)
  - Add provider fallback logic with SubmitAttempt tracking
  - Add safe retry logic and SLA monitoring
- Validation: Circuit breaker state transitions, SubmitAttempt tracking, SLA metrics

**Day 11: API Controllers + Webhook**
- Location: Controllers
- Priority: HIGH
- Tasks:
  - Create HKDElectronicInvoiceController (CRUD + submit)
  - Create ProviderController (management + health)
  - Create WebhookController (provider callbacks)
  - Add validation, error handling, Swagger, idempotency
- Validation: REST API compliance, Idempotency enforcement, Swagger

**Namespace Strategy:**

```csharp
// Domain Layer
using VanAn.Shared.Domain;
using static VanAn.Shared.Domain.HKDRevenueGroup;
using static VanAn.Shared.Domain.InvoiceStatus;
using static VanAn.Shared.Domain.InvoiceType;

// Application Layer - Core Services
using VanAn.CoreHub.Services.Providers.POS;
using VanAn.CoreHub.Services.Providers.EInvoice;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.CoreHub.Services.Resilience;
using VanAn.CoreHub.Services.Monitoring;

// Infrastructure
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Infrastructure.Messaging;

// Domain aliases for clarity
using CoreInvoiceId = VanAn.Shared.Domain.ElectronicInvoiceId;
using CoreTenantId = VanAn.Shared.Domain.TenantId;
using CoreOrderId = VanAn.Shared.Domain.OrderId;

// Provider aliases
using POSProvider = VanAn.CoreHub.Services.Providers.POS.IPOSProvider;
using EInvoiceProvider = VanAn.CoreHub.Services.Providers.EInvoice.IEInvoiceProvider;
```

**Source of Truth:**
- Domain Entities: VanAn.Shared.Domain (single source)
- Service Interfaces: VanAn.CoreHub.Services
- Provider Implementations: VanAn.CoreHub.Services.Providers.*
- Infrastructure: VanAn.CoreHub.Infrastructure

**File Structure Summary:**
```
c:\VibeCoding\Gemini_Windsurf\
├── 1_Shared\
│   └── Domain.cs (ADD: E-Invoice domain models)
├── 2_Gateway\
│   └── Controllers\
│       ├── HKDElectronicInvoiceController.cs (NEW)
│       ├── ProviderController.cs (NEW)
│       └── WebhookController.cs (NEW)
├── 3_CoreHub\
│   ├── Services\
│   │   ├── Orchestration\ (NEW)
│   │   ├── Providers\ (NEW)
│   │   ├── Resilience\ (NEW)
│   │   └── Monitoring\ (NEW)
│   └── Infrastructure\
│       ├── Repositories\ (NEW)
│       └── Messaging\ (NEW)
└── 6_Tests\
    ├── VanAn.Core.Tests\
    │   ├── Domain\EInvoice\ (NEW)
    │   ├── Providers\ (NEW)
    │   ├── Orchestration\ (NEW)
    │   └── Resilience\ (NEW)
    └── VanAn.E2E.Tests\
        ├── EInvoice\ (NEW)
        └── Controllers\ (NEW)
```

---

#### Step 4: Review & Approval

**User approve trước khi chuyển sang IMPLEMENT**
