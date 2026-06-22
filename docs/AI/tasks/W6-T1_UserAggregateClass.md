# TASK CARD: DOMAIN - WAVE 6 - User Aggregate Class (Rich Domain Model)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `DemoUser` class (Rich Domain Model, Aggregate Root) trong `1_Shared/Domain/Aggregates/UserAggregate/` — upgrade từ `class DemoUser : BaseEntity` trong `Domain.cs` thành aggregate đầy đủ domain methods và domain events. Giữ tên `DemoUser` trong Domain layer (Phán quyết D3).
- **Nghiệp vụ áp dụng:** Quản lý người dùng trong một Tenant của VanAn ERP. Mỗi DemoUser thuộc về 1 Tenant, có Role (Owner/StoreKeeper/Guard/Staff/Masterchef), và có vòng đời: Active → Deactivated → Reactivated. Password thay đổi tạo audit event. Role assignment tạo audit event.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` — TẠO MỚI
  - `1_Shared/Domain/Aggregates/UserAggregate/UserEvents.cs` — TẠO MỚI
  - `1_Shared/Domain/Common.cs` — ĐỌC để xác nhận `AggregateRoot` definition (từ W5-T1)
  - `1_Shared/Domain.cs` — ĐỌC để xem `class DemoUser : BaseEntity` (line 930), `enum UserRole` (line 399) — KHÔNG SỬA
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` — `class DemoUser` tại line 930 vẫn tồn tại nguyên vẹn
  - KHÔNG import EF Core, DataAnnotations, BCrypt, hoặc bất kỳ infrastructure library vào Domain layer
  - KHÔNG hash password trong domain class — `bcryptHash` parameter đã là hash khi truyền vào
  - KHÔNG inject dependencies vào `DemoUser` class — phải pure domain object
  - KHÔNG tạo `UserRole` enum file mới trong task này (W6-T2 sẽ làm)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** `DemoUser.cs` và `UserEvents.cs` KHÔNG import bất kỳ infrastructure namespace. Chỉ `System.*` và `VanAn.Shared.Domain.*`.
- [ ] **Namespace:** `VanAn.Shared.Domain` — giống Domain.cs để không cần thêm using directive downstream.
- [ ] **BCrypt Pre-Hashed:** Constructor và `ChangePassword(string newBcryptHash)` nhận hash đã có sẵn — KHÔNG gọi BCrypt.HashPassword trong domain. Validation chỉ: `string.IsNullOrWhiteSpace(newBcryptHash)` → throws.
- [ ] **AggregateRoot Inheritance:** `DemoUser : AggregateRoot` (từ W5-T1) — KHÔNG `DemoUser : BaseEntity` trực tiếp.
- [ ] **Deactivate Last Owner:** `Deactivate()` method KHÔNG check last-owner logic trong domain — đó là Service layer concern (W6-T4). Domain method chỉ: guard `IsActive == false` → throw, else set `_isActive = false` + phát event.
- [ ] **W5-T1 Prerequisite:** Task này CHỈ được bắt đầu sau khi W5-T1 hoàn thành.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `new DemoUser(tenantId, "alice", "bcrypt$hash", "Alice Nguyen", UserRole.Owner)` → `IsActive == true`, `DomainEvents` empty (chưa phát CreatedEvent — tạo qua factory).
- [ ] **SC-2:** `user.Deactivate()` khi `IsActive == true` → `IsActive == false` + `DomainEvents` contains `UserDeactivatedEvent`.
- [ ] **SC-3:** `user.Deactivate()` khi `IsActive == false` → throws `InvalidOperationException`.
- [ ] **SC-4:** `user.Reactivate()` khi `IsActive == false` → `IsActive == true`.
- [ ] **SC-5:** `user.Reactivate()` khi `IsActive == true` → throws `InvalidOperationException`.
- [ ] **SC-6:** `user.ChangePassword("")` → throws `ArgumentException`.
- [ ] **SC-7:** `user.ChangePassword("newHash$abc")` → `PasswordHash == "newHash$abc"` + `DomainEvents` contains `UserPasswordChangedEvent`.
- [ ] **SC-8:** `user.AssignRole(UserRole.StoreKeeper)` → `Role == StoreKeeper` + `DomainEvents` contains `UserRoleChangedEvent`.
- [ ] **SC-9:** `class DemoUser : BaseEntity` tại `Domain.cs` line 930 vẫn tồn tại không thay đổi.
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors. Architecture tests 7/7 PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce domain purity, pre-hashed password contract, no infrastructure imports
- `system-refactor-safety` — Tạo parallel với `class DemoUser` cũ trong Domain.cs, không phá vỡ existing references
- `build-error-analysis` — Handle namespace ambiguity giữa `DemoUser` cũ (Domain.cs line 930) và `DemoUser` mới

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `class DemoUser : BaseEntity` tại `1_Shared/Domain.cs` line 930: `Username, PasswordHash, DisplayName, Role (UserRole), IsActive`
  - Fact 2: `enum UserRole` tại `1_Shared/Domain.cs` line 399: `None, Owner, StoreKeeper, Guard, Staff, Masterchef`
  - Fact 3: `AggregateRoot` abstract class có trong `Common.cs` sau W5-T1 — `DemoUser` phải kế thừa `AggregateRoot`
  - Fact 4: `IDomainEvent` interface: `DateTime OccurredAt { get; }` (từ W5-T1)
  - Fact 5: Phán quyết D3: Giữ `DemoUser` tên trong Domain, DTO boundary dùng `UserDto`
  - Fact 6: Governance: Domain layer pure — NO EF Core, NO BCrypt library
  - Fact 7: `bcryptHash` parameter đã là BCrypt hash — domain KHÔNG hash lại
- **Assumptions:**
  - `UserRole` enum trong `UserAggregate/` sẽ được tạo ở W6-T2 — trong task này dùng `UserRole` từ `Domain.cs` (sẽ gây dependency nhưng là acceptable short-term)
  - `DemoUser` mới cần `protected DemoUser()` parameterless constructor cho EF Core
- **Open Questions:**
  - Q1: `UserRoleChangedEvent` có cần carry `previousRole` và `newRole`, hay chỉ `newRole`? (Ảnh hưởng audit trail quality)
  - Q2: `DemoUser.UpdateProfile(string displayName)` cần phát event không? Hay chỉ domain methods quan trọng (password, role) mới phát event?
- **Recommended Action:** IMPLEMENT — 2 files, phạm vi rõ ràng, dependencies từ W5-T1 đã có

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `DemoUser.cs` (mới) | Namespace ambiguity với `class DemoUser` cũ trong Domain.cs | W6-T3 (tương tự W5-T4) sẽ [Obsolete] mark class cũ |
| `UserEvents.cs` (mới) | Không có impact — brand new file | N/A |
| `1_Shared/Domain.cs` | KHÔNG bị sửa trong task này | Boundary rule enforce |
| `3_CoreHub` consumer code | Sau W6-T4, service layer sẽ dùng DemoUser mới — hiện tại không có impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — DemoUser Lifecycle:**
  - Test: Constructor → IsActive=true, PasswordHash stored correctly
  - Test: Deactivate() → IsActive=false, UserDeactivatedEvent raised
  - Test: Deactivate() when already inactive → throws
  - Test: Reactivate() → IsActive=true
  - Test: Reactivate() when already active → throws
  - Test: ChangePassword("") → throws ArgumentException
  - Test: ChangePassword(validHash) → hash updated, UserPasswordChangedEvent raised
  - Test: AssignRole(StoreKeeper) → Role=StoreKeeper, UserRoleChangedEvent raised
  - Test: UpdateProfile("new name") → DisplayName updated
  - Test: ClearDomainEvents() after Deactivate → DomainEvents empty
- **Test boundary:**
  - Unit tests: `6_Tests/` — minimum 10 test cases
  - Integration tests: N/A (domain only)
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: Session 1 cho `UserEvents.cs` + `DemoUser` skeleton. Session 2 cho đầy đủ domain methods + unit tests.

### Micro-phase breakdown cho W6-T1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `Domain.cs` lines 390-410 (UserRole) và 925-960 (DemoUser) → lấy chính xác property names. Đọc `Common.cs` → verify AggregateRoot. Xác định UserRoleChangedEvent data (previousRole vs newRole). Tạo folder structure | Tạo folder `1_Shared/Domain/Aggregates/UserAggregate/`. Viết `UserEvents.cs`: 4 events (UserCreatedEvent, UserDeactivatedEvent, UserPasswordChangedEvent, UserRoleChangedEvent) implement IDomainEvent. Viết `DemoUser.cs` skeleton: constructor, properties (private set). Verify `dotnet build` |
| **S2** | Review domain method implementations. Xác nhận guard clause messages. Xác nhận `protected DemoUser()` cho EF | Implement 5 domain methods: Deactivate, Reactivate, ChangePassword, AssignRole, UpdateProfile. Viết 10 unit tests. Run `guard-check.ps1` |

### Rules
- `_passwordHash` field dùng `private string` backing field — property `PasswordHash` chỉ có `public string PasswordHash { get; private set; }`
- UserEvents phải carry đủ context: UserId, TenantId, OccurredAt — audit trail requirement
- Mỗi domain method: guard clause → state change → add event (nếu cần) → return

## 11. ESTIMATED EFFORT
- 2 sessions (60-90 phút total)
- **Phụ thuộc:** W5-T1 (AggregateRoot trong Common.cs)
- **BLOCKER:** Nếu namespace ambiguity giữa `DemoUser` cũ (Domain.cs) và mới gây compile error → báo cáo để xem xét [Obsolete] Domain.cs version trước
