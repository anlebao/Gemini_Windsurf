# TASK CARD: [INFRASTRUCTURE] - [PHASE 1-2] - Guard Check Upgrade

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Nâng cấp guard-check.ps1 từ string matching thô sơ sang Roslyn Analyzers + Integration Tests với real NATS service để đảm bảo chất lượng kiến trúc và CI reliability.
- **Nghiệm vụ áp dụng:** Infrastructure Quality Gate - Đảm bảo Clean Architecture, Domain Purity, và Outbox Pattern reliability cho hệ thống Kế toán HKD và Hóa đơn điện tử.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** .windsurf/workflows/newfeaturebuild.md
- **Execution Mode:** ANALYZE -> IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `guard-check.ps1` (Phase 1: warning classification, report generation)
  - `windsurf-guard.js` (Phase 1: improved regex patterns)
  - `architecture-guard.ps1` (Phase 1: dynamic pattern discovery)
  - `.github/workflows/ci.yml` (Phase 1: Node.js setup, Phase 2: NATS service)
  - `docs/IMPLEMENTATION/Guard_Check_Upgrade_Plan.md` (Reference plan)
  - `docs/IMPLEMENTATION/Phase2_Coding_Plan.md` (Phase 2 detailed plan)
  - `VanAn.Accounting/VanAn.Accounting.Analyzers/` (Phase 2: Roslyn Analyzer project - NEW)
  - `6_Tests/VanAn.Integration.Tests/Services/CircuitBreakerIntegrationTests.cs` (Phase 2: NEW)
  - `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` (Phase 2: NATS URL configurable)
  - `Directory.Build.props` (Phase 2: analyzer integration)
- **Boundary Rules (Nghiêm cấm):**
  - CẤM đọc lại các module không liên quan để tránh phình context window.
  - CẤM chỉnh sửa Domain Layer ngoại trừ file `1_Shared/Domain.cs` khi có modeling defect công nhận.
  - CẤM sử dụng Mock NATS cho Integration Tests - phải dùng real NATS service.

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Domain entity không chứa EF Core, DbContext, DataAnnotations.
- [ ] **Immutability:** `AccountingEntry` phải 100% append-only, thay đổi qua Reversal Entry.
- [ ] **Architecture Compliance:** Domain entities chỉ được định nghĩa trong `1_Shared/Domain.cs`.
- [ ] **Dependency Direction:** Service layer không được reference API layer.
- [ ] **Integration Test Quality:** Circuit Breaker và Outbox Pattern phải test với real NATS service (không mock).
- [ ] **CI Reliability:** GitHub Actions phải chạy Integration Tests với NATS service container.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **Phase 1 Complete:** windsurf-guard.js improved regex, architecture-guard.ps1 dynamic patterns, guard-check.ps1 warning classification + report generation, CI Node.js setup.
- [ ] **Phase 2.1 Complete:** NATS service added to CI, NATS URL configurable, Circuit Breaker Integration Tests with real NATS (5 test cases).
- [ ] **Phase 2.2 Complete:** Roslyn Analyzer project created with 5 rules (VA1001-VA1005).
- [ ] **Phase 2.3 Complete:** Analyzers integrated into build, guard-check.ps1 runs analyzers.
- [ ] **Phase 2.4 Complete:** Integration Tests added to guard-check.ps1 fast gate.
- [ ] **Phase 2.5 Complete:** CI workflows updated with integration-tests job, guard report updated.
- [ ] **CI Stability:** All CI jobs pass (build, architecture-tests, integration-tests, guard-check).
- [ ] **Test Coverage:** Circuit Breaker state transitions, Outbox Worker pause/resume, NATS reconnection message integrity all tested.

## 6. ACTIVE SKILLS (MAX 3)
- build-error-analysis
- pattern-based-fixing
- domain-integrity-validation

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 0 (Chưa bắt đầu implementation)
- **Verified Facts:**
  - Fact 1: Phase 1 đã hoàn thành (windsurf-guard.js improved, architecture-guard.ps1 dynamic, guard-check.ps1 warning classification, CI Node.js setup).
  - Fact 2: Integration tests hiện tại dùng In-Memory Database (không file locking issue).
  - Fact 3: guard-check chạy trên Windows (không case sensitivity issue).
  - Fact 4: WebApplicationFactory đã có DbContext override (không critical DI debt).
  - Fact 5: NATS dependency cần resolution cho Sprint 3 E-Invoice Outbox Pattern.
- **Assumptions:**
  - GitHub Actions Services (NATS) sẽ stable trong CI environment.
  - Roslyn Analyzers sẽ không gây false positives cho existing codebase.
  - Circuit Breaker Integration Tests với real NATS sẽ chạy reliably trong CI timeout.
- **Open Questions:**
  - Question 1: CI timeout hiện tại (10 minutes) có đủ cho Integration Tests với NATS không?
  - Question 2: Có cần thêm Redis service cho Circuit Breaker distributed state không?
- **Recommended Action:** Continue - Phase 1 completed, Phase 2 plan ready, assumptions reasonable, proceed with Phase 2.1 (NATS dependency resolution).
