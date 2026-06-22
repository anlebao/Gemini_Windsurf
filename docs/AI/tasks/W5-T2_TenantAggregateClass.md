# TASK CARD: DOMAIN - WAVE 5 - Tenant Aggregate Class (Rich Domain Model)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `Tenant` class (Rich Domain Model, Aggregate Root) trong `1_Shared/Domain/Aggregates/TenantAggregate/` — thay thế `record Tenant` read-only trong `Domain.cs` bằng một aggregate đầy đủ domain methods, lifecycle management, và domain events.
- **Nghiệp vụ áp dụng:** Quản lý đơn vị kinh doanh (Tenant) trong VanAn ERP. Một Tenant là một đơn vị kinh doanh (cửa hàng, nhà hàng, HKD) với vòng đời: Pending → Active → Suspended/Inactive → Terminated. Mỗi chuyển đổi trạng thái có business rule nghiêm ngặt.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `1_Shared/Domain/Common.cs` — đọc để lấy `AggregateRoot`, `BaseEntity`, `TenantId` definitions (từ W5-T1)
  - `1_Shared/Domain.cs` — đọc để xem `record Tenant` (line 156), `BusinessType`, `HKDGroup`, `TenantId` — KHÔNG SỬA
  - `1_Shared/Domain/Aggregates/TenantAggregate/TenantStatus.cs` — TẠO MỚI
  - `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — TẠO MỚI
  - `1_Shared/Domain/Aggregates/TenantAggregate/TenantEvents.cs` — TẠO MỚI
  - `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — TẠO MỚI
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` — `record Tenant` tại line 156 vẫn tồn tại nguyên vẹn (W5-T4 sẽ xử lý [Obsolete])
  - KHÔNG import EF Core, DataAnnotations, hoặc bất kỳ infrastructure namespace vào Domain layer
  - KHÔNG inject dependencies vào domain class — domain methods phải pure
  - KHÔNG dùng `InvalidOperationException` string literals magic numbers — dùng named constants hoặc message rõ ràng
  - KHÔNG sửa `Common.cs` (chỉ đọc để reference `AggregateRoot`)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Tất cả 4 files mới KHÔNG được import EF Core (`Microsoft.EntityFrameworkCore.*`), DbContext, hoặc DataAnnotations.
- [ ] **Namespace Đồng nhất:** Tất cả files trong `TenantAggregate/` dùng namespace `VanAn.Shared.Domain` — giống `Domain.cs` hiện tại để downstream code không cần thêm using.
- [ ] **Immutable State via Private Set:** Tất cả properties của `Tenant` class phải có `private set` — state chỉ thay đổi qua domain methods.
- [ ] **Lifecycle Guard Clauses:** `Deactivate(reason)` từ `Suspended` trạng thái → phải throw `InvalidOperationException` với message rõ ràng. `Activate()` từ trạng thái khác `Pending` → throw.
- [ ] **AggregateRoot Inheritance:** `Tenant` class phải kế thừa `AggregateRoot` (từ W5-T1) — KHÔNG kế thừa `BaseEntity` trực tiếp.
- [ ] **W5-T1 Prerequisite:** Task này CHỈ được bắt đầu sau khi W5-T1 hoàn thành và `AggregateRoot` đã có trong `Common.cs`.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `dotnet build VanAn.sln` → 0 errors (chỉ có thể có warnings về ambiguous reference giữa `record Tenant` cũ và `class Tenant` mới — acceptable ở Wave 5).
- [ ] **SC-2:** `Tenant.Create(id, "Test", BusinessType.Restaurant)` → trả về `Tenant` với `Status == TenantStatus.Pending`.
- [ ] **SC-3:** `tenant.Activate()` khi `Status == Pending` → `Status == Active` (no exception).
- [ ] **SC-4:** `tenant.Activate()` khi `Status != Pending` → throws `InvalidOperationException`.
- [ ] **SC-5:** `tenant.Suspend("audit")` khi `Status == Active` → `Status == Suspended` + `DomainEvents` contains `TenantSuspendedEvent`.
- [ ] **SC-6:** `tenant.Deactivate("close")` khi `Status == Suspended` → throws `InvalidOperationException("Cannot deactivate a suspended tenant directly...")`.
- [ ] **SC-7:** `tenant.UpdateProfile(name, email, address)` khi `Status == Inactive` → throws guard exception.
- [ ] **SC-8:** `tenant.UpdateSettings(newSettings)` với `settings == null` → throws `ArgumentNullException`.
- [ ] **SC-9:** `record Tenant` tại `Domain.cs` line 156 vẫn tồn tại không thay đổi (verify bằng git diff `Domain.cs`).
- [ ] **SC-10:** Architecture tests 7/7 PASS — `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce domain purity, lifecycle state machine correctness
- `system-refactor-safety` — Tạo mới parallel với record cũ, không phá vỡ existing references
- `build-error-analysis` — Handle potential namespace ambiguity giữa `record Tenant` (Domain.cs) và `class Tenant` (Tenant.cs)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `record Tenant` tại `1_Shared/Domain.cs` line 156: properties `Id (TenantId), Name, BusinessType, HKDGroup, IsActive` — immutable record, NO domain methods
  - Fact 2: `AggregateRoot` abstract class sẽ có trong `Common.cs` sau W5-T1 (prerequisite task)
  - Fact 3: `BusinessType` enum và `HKDGroup` enum đã tồn tại trong `Domain.cs` (namespace `VanAn.Shared.Domain`)
  - Fact 4: `TenantId` là strong-typed ID trong `VanAn.Shared.Domain` namespace
  - Fact 5: Governance: Domain layer pure — NO EF Core, NO DbContext, NO DataAnnotations
  - Fact 6: Phán quyết D1 (Tech Lead đã approve): Tenant record → class, AggregateRoot, tách ra file riêng, namespace `VanAn.Shared.Domain`
  - Fact 7: `1_Shared/Domain.cs` có 2,050+ lines, 79 types — God File, KHÔNG được sửa trong task này
- **Assumptions:**
  - Folder `1_Shared/Domain/Aggregates/TenantAggregate/` chưa tồn tại — cần tạo
  - `TenantSettings` là owned entity (EF Value Object pattern) — không cần `Id` riêng
- **Open Questions:**
  - Q1: `BusinessType` và `HKDGroup` enums có `None` value không? (Ảnh hưởng đến constructor validation)
  - Q2: `TenantSuspendedEvent` cần carry theo `reason` string không, hay chỉ `TenantId` và `OccurredAt`?
- **Recommended Action:** IMPLEMENT — tạo folder → tạo 4 files theo thứ tự TenantStatus → TenantSettings → TenantEvents → Tenant → build verify

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `TenantStatus.cs` (mới) | Không có — brand new file | N/A |
| `TenantSettings.cs` (mới) | Không có — brand new file | N/A |
| `TenantEvents.cs` (mới) | Không có — brand new file | N/A |
| `Tenant.cs` (mới) | Namespace ambiguity: `using VanAn.Shared.Domain` → compiler thấy 2 types tên `Tenant` (record cũ + class mới) | Downstream code phải dùng fully-qualified name hoặc alias cho đến khi W5-T4 obsolete record cũ |
| `1_Shared/Domain.cs` | KHÔNG bị sửa — `record Tenant` vẫn tồn tại | Boundary rule enforce |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — Lifecycle State Machine:**
  - Test matrix: tất cả valid transitions (Pending→Active, Active→Suspended, Active→Inactive, Suspended→Inactive via Deactivate guard)
  - Test guard clauses: Activate từ Active → throws, Suspend từ Suspended → throws, Deactivate từ Suspended → throws
  - Test domain events: Suspend → DomainEvents có TenantSuspendedEvent; Clear → DomainEvents empty
- **Unit Test — Validation:**
  - UpdateProfile với `name = ""` → throws ArgumentException
  - UpdateProfile với `email = null` → throws ArgumentNullException
  - UpdateSettings với `null` → throws ArgumentNullException
  - Create với valid params → Tenant.Status == Pending, DomainEvents có TenantCreatedEvent
- **Test boundary:**
  - Unit tests: tạo file test trong `6_Tests/VanAn.Architecture.Tests/` hoặc dedicated unit test project — minimum 10 test cases cho Tenant lifecycle
  - Integration tests: N/A trong task này (EF mapping ở W5-T4)
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions do số lượng files mới (4 files) và complexity của state machine. Session 1: supporting types + TenantEvents. Session 2: Tenant aggregate class đầy đủ + verification.

### Micro-phase breakdown cho W5-T2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `Domain.cs` lines 1-200 → lấy chính xác `TenantId`, `BusinessType`, `HKDGroup` definitions. Đọc `Common.cs` → verify `AggregateRoot` đã có. Quyết định `TenantSettings` có validation logic không | Tạo folder `Aggregates/TenantAggregate/`. Viết `TenantStatus.cs` (5 enum values). Viết `TenantSettings.cs` (record với ContactEmail, BusinessAddress, LogoUrl?, PhoneNumber?). Viết `TenantEvents.cs` (TenantCreatedEvent, TenantSuspendedEvent, TenantDeactivatedEvent implement IDomainEvent). Verify `dotnet build` |
| **S2** | Review state machine transitions diagram. Xác định guard message strings. Xác nhận factory method pattern `Create()` | Viết `Tenant.cs`: constructor, properties (private set), domain methods (Activate, Suspend, Deactivate, UpdateProfile, UpdateSettings), static factory Create(). Run unit tests. Run `guard-check.ps1` |

### Rules
- Mỗi file mới phải compile trước khi sang file tiếp theo
- State transitions phải match business spec: Pending→Active→Suspended/Inactive, KHÔNG có Suspended→Inactive direct
- `protected Tenant()` parameterless constructor phải có (EF Core requirement)

## 11. ESTIMATED EFFORT
- 2 sessions (60-90 phút total)
- **Phụ thuộc:** W5-T1 phải PASS trước (AggregateRoot trong Common.cs)
- **BLOCKER:** Nếu namespace ambiguity giữa `record Tenant` và `class Tenant` gây compile error (không chỉ warning) → cần escalate W5-T4 (Obsolete) lên trước để giải quyết conflict
