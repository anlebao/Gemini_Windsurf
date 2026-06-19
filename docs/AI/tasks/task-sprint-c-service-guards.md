# TASK CARD: [SPRINT C] — Service Layer Guards (Duplicate Detection + Period Closing + COGS)

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Thêm 2 server-side guards vào `AccountingEntryService`: (1) chặn duplicate entries trong 5 phút, (2) chặn entries vào kỳ đã đóng sổ. Task C-3 (COGS từ CostPrice) bị blocked do Domain Modeling Defect.
- **Nghiệp vụ áp dụng:**
  - **C-1:** Ngăn nhập kép do lỗi người dùng hoặc double-click (TT 152/2025 — sổ sách phải chính xác).
  - **C-2:** Kỳ đã đóng sổ là immutable về mặt kế toán — không thể thêm entry mới (vi phạm audit trail).
  - **C-3:** COGS tính 70% hardcode không phản ánh đúng giá vốn thực tế — cần `Product.CostPrice`.
- **Root cause đã verify (2026-06-20):**
  - `AccountingEntryService.CreateRevenue/ExpenseEntryAsync()` không có duplicate check — client-only (`_recentEntries` list trong Razor)
  - `AccountingEntryService` không inject `IPeriodClosingService` — không check period status trước khi tạo entry
  - `OrderService.cs:119`: `decimal cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS for MVP`
  - `Product` entity trong `Domain.cs` không có `CostPrice` field → **Domain Modeling Defect DMD-2**

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**

| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `3_CoreHub/Services/AccountingEntryService.cs` | **Edit** | Thêm duplicate check + period closing guard |
| `3_CoreHub/Services/IAccountingService.cs` | **Edit** | Có thể cần inject `IPeriodClosingService` qua constructor |
| `3_CoreHub/Services/IPeriodClosingService.cs` | Read only | Verify `GetPeriodStatusAsync()` signature |
| `3_CoreHub/Repositories/IAccountingEntryRepository.cs` | Read only | Verify có query method phù hợp cho duplicate check |
| `3_CoreHub/Services/OrderService.cs` | ⚠️ C-3 only (BLOCKED) | Line 119 — COGS hardcode — chỉ đọc nếu C-3 unblocked |
| `1_Shared/Domain.cs` | ⚠️ C-3 only (BLOCKED) | `Product` entity — chỉ sửa nếu C-3 approved |
| `6_Tests/` | **Edit** | Unit tests cho guards |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `AccountingEntry` entity (immutable)
- CẤM thêm `CostPrice` vào `Product` entity mà không có Tech Lead approval (C-3 blocked)
- CẤM implement C-3 trước khi có approval
- CẤM sửa KhachLink hay ShopERP Razor pages trong sprint này
- CẤM thêm duplicate check vào Gateway Controller layer (business logic phải ở Service layer)

## 4. DOMAIN CONSTRAINT (C-3) ⚠️ BLOCKED

**Domain Modeling Defect DMD-2:** `Product` entity không có `CostPrice` field.

Để fix `OrderService.cs:119` (`order.TotalPrice * 0.7m`):
1. Cần thêm `public decimal CostPrice { get; protected set; }` vào `Product` entity
2. Cần EF Core migration
3. Cần update Product seeding/creation logic
4. Cần update `OrderService.GenerateAccountingEntriesAsync()` — `COGS = SUM(item.Quantity × product.CostPrice)`

**→ C-3 BLOCKED cho đến khi Tech Lead approve thêm `Product.CostPrice` vào `1_Shared/Domain.cs`.**

Sprint C sẽ implement **C-1 + C-2 only**. C-3 nhận status `BLOCKED` và sẽ tạo task card riêng sau khi domain change được approve.

## 5. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

- [ ] **Domain Purity:** Duplicate check và period guard là pure Service layer — không ở Controller, không ở Domain.
- [ ] **Immutability:** `AccountingEntry` append-only — guard chỉ throw exception TRƯỚC khi tạo entry, không modify entries đã tồn tại.
- [ ] **Performance:** Duplicate check query phải efficient — filter theo `TenantId + Amount + TransactionDate >= now.AddMinutes(-5)` có index.
- [ ] **Error clarity:** Exceptions phải có message rõ ràng để UI display đúng: `"Bút toán trùng lặp trong 5 phút vừa qua"`, `"Kỳ kế toán đã đóng sổ — không thể thêm bút toán mới"`.
- [ ] **Circular dependency:** `AccountingEntryService` inject `IPeriodClosingService` — verify không có circular DI (PeriodClosingService có inject IAccountingService không?).
- [ ] **Legal:** TT 152/2025/TT-BTC — kỳ đóng sổ là locked period, không cho phép thêm/sửa bút toán.
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Task C-1: Server-side Duplicate Detection

- [ ] **SC1:** `AccountingEntryService.CreateRevenueEntryAsync()` query: tìm entry có cùng `TenantId + Amount + AccountCode + TransactionDate.Date` trong vòng 5 phút trước → nếu found → throw `DuplicateEntryException` (hoặc `InvalidOperationException` với message rõ)
- [ ] **SC2:** `AccountingEntryService.CreateExpenseEntryAsync()` có cùng logic duplicate check
- [ ] **SC3:** Duplicate window là 5 phút (configurable via constant `DuplicateWindowMinutes = 5`)
- [ ] **SC4:** Unit test: tạo 2 entries giống nhau trong 5 phút → second call throws exception
- [ ] **SC5:** Unit test: tạo 2 entries giống nhau nhưng cách nhau > 5 phút → cả 2 entries đều được tạo thành công (không false-positive)
- [ ] **SC6:** Unit test: tạo 2 entries cùng `Amount` nhưng khác `AccountCode` → cả 2 thành công (khác account = không duplicate)

### Task C-2: Period Closing Guard

- [ ] **SC7:** `AccountingEntryService` inject `IPeriodClosingService` (constructor injection)
- [ ] **SC8:** `CreateRevenueEntryAsync()` gọi `IPeriodClosingService.GetPeriodStatusAsync(period, tenantId)` TRƯỚC khi tạo entry
- [ ] **SC9:** `GetPeriodStatusAsync()` trả `PeriodClosingStatus.Closed` → throw `InvalidOperationException("Kỳ kế toán {year}/{month} đã đóng sổ. Không thể thêm bút toán mới.")`
- [ ] **SC10:** `CreateExpenseEntryAsync()` có cùng period check
- [ ] **SC11:** Unit test: create entry vào kỳ `Closed` → exception với message đúng
- [ ] **SC12:** Unit test: create entry vào kỳ `Open` → entry được tạo thành công (không regression)
- [ ] **SC13:** Unit test: create entry vào kỳ `Open` sau khi một kỳ khác đã `Closed` → thành công (chỉ check period của entry, không phải period mới nhất)

### Task C-3 (BLOCKED)

- [ ] **SC14:** _(BLOCKED — Tech Lead approval required)_ `Product.CostPrice` thêm vào Domain.cs
- [ ] **SC15:** _(BLOCKED)_ `OrderService.GenerateAccountingEntriesAsync()` tính COGS từ `SUM(item.Quantity × product.CostPrice)` thay vì `order.TotalPrice * 0.7m`
- [ ] **SC16:** _(BLOCKED)_ Fallback: nếu `product.CostPrice == 0` → dùng `item.UnitPrice * 0.7m` (backward compat)

### Build & Quality

- [ ] **SC17:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC18:** `guard-check.ps1` → PASS
- [ ] **SC19:** `AccountingEntryService` không có circular DI (verify DI registration)

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 5 verified facts, 2 open questions

**Verified Facts (2026-06-20):**
- Fact 1: `AccountingEntryService.CreateRevenueEntryAsync()` — không có duplicate check, không inject `IPeriodClosingService` (đã đọc file)
- Fact 2: `IPeriodClosingService` có `GetPeriodStatusAsync(period, tenantId, CancellationToken)` method (đã đọc file)
- Fact 3: `IAccountingEntryRepository` — cần verify có `GetByTenantAndDateRangeAsync()` cho duplicate query (đã thấy usage trong `GetTodayRevenueAsync`)
- Fact 4: `OrderService.cs:119` — `decimal cogsAmount = order.TotalPrice * 0.7m;` (đã đọc file)
- Fact 5: `Product` entity không có `CostPrice` field (đã đọc Domain.cs)

**Open Questions:**
- Q1: `PeriodClosingService` có inject `IAccountingService` không? (Cần check để avoid circular DI trước khi inject `IPeriodClosingService` vào `AccountingEntryService`)
- Q2: `IAccountingEntryRepository` có method nào trả entries theo `TenantId + Amount + DateRange` không, hay phải dùng `GetByTenantAndDateRangeAsync` rồi filter in-memory? (Performance implication)

**Recommended Action:** Resolve Q1/Q2 trong JIT Planning (đọc `PeriodClosingService.cs` + `IAccountingEntryRepository.cs`). Sau đó IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS

| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `AccountingEntryService` inject `IPeriodClosingService` | DI registration cần update trong `Program.cs` (Gateway + ShopERP) | Verify `IPeriodClosingService` đã registered — add if missing |
| Duplicate check: throw exception | Client code (Razor pages) cần handle exception gracefully | `RevenueEntry.razor` + `ExpenseEntry.razor` đã có try/catch — verify message display |
| Period closing guard: throw exception | Existing integration tests tạo entry vào past periods có thể fail | Review test data — đảm bảo test periods là Open |
| Circular DI risk | App crash at startup nếu circular | Check `PeriodClosingService` constructor trước khi inject |

## 9. TDD & E2E TESTING STRATEGY

**Unit tests BẮT BUỘC (TDD — viết test trước):**

```csharp
// C-1 Duplicate Detection Tests
[Fact]
async Task CreateRevenueEntry_DuplicateInWindow_ThrowsException()
{
    // Arrange: first entry created 2 min ago, same amount/accountCode
    // Act: create second entry with same amount/accountCode
    // Assert: throws DuplicateEntryException (or InvalidOperationException)
}

[Fact]
async Task CreateRevenueEntry_DuplicateOutsideWindow_Succeeds()
{
    // Arrange: first entry created 10 min ago
    // Act: create entry with same amount/accountCode
    // Assert: no exception — entry created
}

[Fact]
async Task CreateRevenueEntry_SameAmountDifferentAccountCode_Succeeds()
{
    // Arrange: entry with AccountCode "511" created 1 min ago
    // Act: create entry with AccountCode "515", same amount
    // Assert: no exception — different account = not duplicate
}

// C-2 Period Closing Guard Tests
[Fact]
async Task CreateRevenueEntry_ClosedPeriod_ThrowsInvalidOperation()
{
    // Arrange: period 2025/12 is Closed
    // Act: create entry for period 2025/12
    // Assert: throws InvalidOperationException with "đã đóng sổ" message
}

[Fact]
async Task CreateRevenueEntry_OpenPeriod_Succeeds()
{
    // Arrange: period 2026/06 is Open
    // Act: create entry for period 2026/06
    // Assert: no exception — entry created
}
```

**E2E (post-Sprint C):**
- Không bắt buộc — server-side guards verifiable via unit tests
- Optional: `accounting-flow.spec.ts` — verify API returns 400/422 for duplicate entry

## 10. JIT PLANNING + PURE EXECUTION

### Micro-phase breakdown Sprint C

| Micro-phase | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **MP-C1** | Đọc `IPeriodClosingService.cs` + `PeriodClosingService.cs` constructor → chốt: có circular DI không, `GetPeriodStatusAsync` return type | Write unit tests C-2 (TDD first — failing) |
| **MP-C2** | Đọc `IAccountingEntryRepository.cs` + `AccountingEntryService.GetTodayRevenueAsync()` → chốt: query method available, duplicate check query design | Write unit tests C-1 (TDD first — failing) |
| **MP-C3** | _(JIT đã done)_ | Implement C-2 period guard trong `AccountingEntryService` — inject `IPeriodClosingService`, add check trước create |
| **MP-C4** | _(JIT đã done)_ | Implement C-1 duplicate check trong `AccountingEntryService` — query + window logic |
| **MP-C5** | Đọc `Program.cs` (Gateway + ShopERP) → verify `IPeriodClosingService` registered | Fix DI registration nếu thiếu, run `dotnet build` + unit tests |

### Rules
- JIT Planning: MAX 10 phút đọc per micro-phase
- TDD: viết test trước mỗi implement step
- Resolve Q1 (circular DI) trong MP-C1 TRƯỚC khi implement bất cứ điều gì

---

## 11. C-3 UNBLOCK PATH (Sau khi Tech Lead approve)

Khi Tech Lead approve `Product.CostPrice`:

1. **Tạo task card riêng:** `task-sprint-c3-cogs-costprice.md`
2. **Domain change:**
   - Thêm `public decimal CostPrice { get; protected set; }` vào `Product` entity trong `Domain.cs`
   - Thêm `CostPrice` vào `Product()` constructor
3. **EF Core migration:** `Add-Migration AddProductCostPrice` trong `3_CoreHub`
4. **OrderService fix:**
   ```csharp
   // Thay dòng 119:
   decimal cogsAmount = order.Items.Sum(i => i.Quantity * (product?.CostPrice ?? i.UnitPrice * 0.7m));
   ```
5. **Product seeding:** Update dev seed data với `CostPrice` cho test products

---

**Implementation Date:** _(để trống — điền khi bắt đầu)_
**Branch:** `feat/sprint-c-service-guards`
**Depends on:** Sprint B merged to `main`
**Status:** ⬜ C-1/C-2 READY (resolve Q1 trong JIT Planning) | 🚫 C-3 BLOCKED (awaiting Domain approval)
