# TASK CARD: DOMAIN - WAVE 5 - AggregateRoot Foundation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `IDomainEvent` interface và `AggregateRoot` abstract class vào `1_Shared/Domain/Common.cs` — cung cấp nền tảng domain event infrastructure cho toàn bộ Wave 5 và Wave 6 aggregate classes.
- **Nghiệp vụ áp dụng:** Kiến trúc nền tảng (Domain Infrastructure). Không có nghiệp vụ trực tiếp — đây là building block cho TenantAggregate (W5-T2), UserAggregate (W6-T1), và tất cả future aggregates trong VanAn ERP.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `1_Shared/Domain/Common.cs` — SỬA (append only, không xóa gì)
  - `6_Tests/VanAn.Architecture.Tests/` — đọc để verify architecture tests vẫn PASS
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG xóa `BaseEntity`, `IMustHaveTenant`, `IAuditableEntity` hoặc bất kỳ class/interface hiện có trong `Common.cs`
  - KHÔNG sửa `1_Shared/Domain.cs` (God File) trong task này
  - KHÔNG thêm EF Core annotations, DataAnnotations, hoặc infrastructure imports vào Domain layer
  - KHÔNG tạo file mới nào ngoài việc sửa `Common.cs`
  - KHÔNG thay đổi namespace `VanAn.Shared.Domain.Common`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Layer Purity:** `Common.cs` KHÔNG được import EF Core, DbContext, DataAnnotations. Chỉ `System.*` và `VanAn.Shared.Domain.*`.
- [ ] **Append-Only:** Chỉ APPEND code vào cuối file (hoặc sau `BaseEntity` class). Không xóa, không modify existing code.
- [ ] **TenantId Constructor:** `AggregateRoot(TenantId tenantId)` phải gọi `base(tenantId)` — `BaseEntity` constructor nhận `TenantId` đã tồn tại.
- [ ] **Thread Safety:** `_domainEvents` là `List<IDomainEvent>` private — không cần concurrent collection (domain events cleared sau mỗi Unit of Work).
- [ ] **Architecture Tests:** `6_Tests/VanAn.Architecture.Tests` — 7/7 tests phải PASS sau khi append.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới (chỉ có thể có warnings từ code hiện có).
- [ ] **SC-2:** `IDomainEvent` interface có property `DateTime OccurredAt { get; }` — resolvable từ namespace `VanAn.Shared.Domain`.
- [ ] **SC-3:** `AggregateRoot` abstract class kế thừa `BaseEntity` — resolvable từ `3_CoreHub` project.
- [ ] **SC-4:** `AggregateRoot.DomainEvents` trả về `IReadOnlyList<IDomainEvent>` — consumers không thể mutate danh sách trực tiếp.
- [ ] **SC-5:** `AggregateRoot.AddDomainEvent()` là `protected` — Service layer không thể gọi trực tiếp, chỉ domain methods bên trong aggregate.
- [ ] **SC-6:** `AggregateRoot.ClearDomainEvents()` là `public` — Infrastructure/UoW có thể clear events sau dispatch.
- [ ] **SC-7:** Architecture tests 7/7 PASS — `guard-check.ps1` PASS.
- [ ] **SC-8:** `BaseEntity` và tất cả existing types trong `Common.cs` không bị thay đổi (verify bằng git diff).

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Đảm bảo domain layer purity, không import infrastructure concerns
- `build-error-analysis` — Phân tích compile errors nếu `BaseEntity` constructor signature không match
- `system-refactor-safety` — Append-only change, verify existing tests không bị break

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `abstract class BaseEntity` tồn tại tại `1_Shared/Domain/Common.cs` với properties: `Id, TenantId, CreatedAt, UpdatedAt, IsDeleted` và methods `UpdateAudit()`, `MarkAsDeleted()`
  - Fact 2: `BaseEntity` có constructor nhận `TenantId tenantId` (theo phán quyết D1 và codebase facts)
  - Fact 3: Namespace của `Common.cs` là `VanAn.Shared.Domain` (hoặc `VanAn.Shared.Domain.Common` — cần verify khi đọc file)
  - Fact 4: Architecture tests tại `6_Tests/VanAn.Architecture.Tests` — 7/7 phải PASS
  - Fact 5: `TenantId` là strong-typed ID (record/struct) đã tồn tại trong codebase — `AggregateRoot(TenantId tenantId)` constructor cần import đúng type
  - Fact 6: Governance rule: Domain layer KHÔNG được chứa EF Core, DbContext, DataAnnotations
- **Assumptions:**
  - `BaseEntity` có parameterless protected constructor (cần verify — `AggregateRoot` cần `protected AggregateRoot()` để EF Core work)
  - `TenantId` là trong cùng namespace `VanAn.Shared.Domain` — không cần thêm using statement
- **Open Questions:**
  - Q1: `BaseEntity` constructor signature chính xác là gì? Có `protected BaseEntity()` và `protected BaseEntity(TenantId tenantId)` không? (Cần đọc `Common.cs` trước khi implement)
  - Q2: File `Common.cs` hiện tại có `using` directives gì? (Để đảm bảo không cần thêm using mới)
- **Recommended Action:** IMPLEMENT — đọc `Common.cs` → append 2 blocks (IDomainEvent + AggregateRoot) → build → verify

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `1_Shared/Domain/Common.cs` | Tất cả projects reference `Common.cs` sẽ thấy types mới — không breaking change | Types mới chỉ được add, không modify — backward compatible 100% |
| `6_Tests/VanAn.Architecture.Tests/` | Architecture tests kiểm tra layer dependencies — thêm `AggregateRoot` không vi phạm nếu không import infra | Verify domain purity: chỉ `System.*` imports |
| `3_CoreHub` projects | `AggregateRoot` resolvable → W5-T2 có thể build on top | Không có impact tiêu cực — chỉ enrich API |
| `1_Shared/Domain.cs` (God File) | Không bị sửa trong task này → no impact | Boundary rule được enforce |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — Domain Event Mechanics:**
  - Test: `AggregateRoot` có thể add domain event → `DomainEvents.Count == 1`
  - Test: `ClearDomainEvents()` → `DomainEvents.Count == 0`
  - Test: `DomainEvents` trả về `IReadOnlyList` — không thể cast thành `List` và mutate
- **Compile-Time Verification:**
  - `IDomainEvent` với `OccurredAt` property → build thành công
  - `AggregateRoot : BaseEntity` → không circular dependency
  - `protected AggregateRoot()` → EF Core có thể instantiate
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Architecture.Tests/` — verify 7/7 pass; thêm 1-2 unit test cho domain event add/clear nếu test project tồn tại
  - Integration tests: N/A cho task này (infrastructure foundation only)
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này là SINGLE-SESSION do phạm vi nhỏ (append 2 code blocks vào 1 file). JIT Planning: đọc `Common.cs` để xác nhận namespace + BaseEntity constructor. Pure Execution: append code.

### Micro-phase breakdown cho W5-T1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `Common.cs` → xác nhận namespace, BaseEntity constructor signature, existing using directives | Append `IDomainEvent` interface + `AggregateRoot` abstract class vào `Common.cs` |
| **S1 (cont)** | Verify code appended đúng syntax | Run `dotnet build VanAn.sln` → fix bất kỳ compile errors (nếu có) → run architecture tests |

### Rules
- Đọc file trước, append sau — không modify existing code
- Nếu architecture tests fail → đọc test file → identify vi phạm → fix (likely là namespace/import issue)
- Task hoàn thành khi: build 0 errors + 7/7 architecture tests PASS

## 11. ESTIMATED EFFORT
- 1 session (30-45 phút)
- **Tiên quyết cho:** W5-T2 (TenantAggregateClass), W6-T1 (UserAggregateClass), và mọi aggregate trong hệ thống
- **BLOCKER:** Nếu `BaseEntity` không có `protected BaseEntity(TenantId tenantId)` constructor → báo cáo Tech Lead, không tự thêm
