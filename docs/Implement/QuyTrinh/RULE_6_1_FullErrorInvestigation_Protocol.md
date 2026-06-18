VẠN AN ECOSYSTEM - ERROR FIX PROTOCOL v7.0
PATTERN-BASED APPROACH - Effective Date: May 6, 2026

## ERROR FIXING RULES (Per WINDSURF RULES v7.0)
- Hard Stop Rules ALWAYS override any fixing rule
- Small (<2 errors): Direct Fix + short report. Fix immediately if pattern is obvious.
- Medium (2-5 errors): Scan for shared patterns before fixing
- High (>5 errors): Full Investigation + Comprehensive Plan

**CRITICAL DISTINCTION:**
- **Compile Errors:** Apply error classification above
- **Architectural Violations:** ALWAYS trigger Hard Stop + Full Investigation regardless of error count

## FULL INVESTIGATION (For >5 errors)
When error count > 5, follow this process:

### STEP 1: ASSESSMENT
Run: `dotnet clean && dotnet build VanAn.sln --no-incremental`
Report: Total errors, Top 5 error codes

### STEP 2: ROOT CAUSE ANALYSIS
Focus on patterns, not individual errors

### STEP 3: IMPACT ANALYSIS
Check: Domain integrity, AccountingEntry immutability, rollback risk

### STEP 4: PLANNING
Create Detailed Coding Plan, wait for approval

### STEP 5: VALIDATION
After fix: Report new error count, domain integrity check

## ANTI-PANIC RULES (ZERO TOLERANCE)
- Never modify Domain.cs, BaseEntity, or AccountingEntry
- Always complete assessment before creating plan
- Must wait for approval before any code changes
- If error count increases >10% → STOP immediately and report
- Use systematic batch replacement only (no case-by-case)

**Goal:** Move from "fixing individual errors" to "fixing patterns" to increase speed and quality.

**31 Main Patterns to Eliminate:**
- Pattern 1: Direct Entity Creation
- Pattern 2: Incorrect Mock Setup  
- Pattern 3: Service Method Mismatch — ⚠️ OBSOLETE (2026-06-18)
- Pattern 4: Missing Test Dependencies
- Pattern 5: Type Mismatch Conversions
- Pattern 6: Property Access Errors — ⚠️ MISLEADING (2026-06-18)
- Pattern 7: Extension Method Issues
- Pattern 8: Constructor Parameter Mismatches
- Pattern 9: UI Platform Component Parameter Mismatch
- Pattern 10: Navigation Service Injection Architecture
- Pattern 11: Razor TagHelper Syntax Errors
- Pattern 12: Navigation Variable Mismatch (NEW)
- Pattern 13: CSS Media Query in Code Block (NEW)
- Pattern 14: Static Method Call Context (NEW)
- Pattern 15: Razor HTML Parsing Errors (NEW)
- Pattern 16: Blazor Event Handler Type Mismatch (NEW)
- Pattern 17: Security Vulnerability Dependencies (NEW)
- Pattern 18: Duplicate Package Central Management (NEW)
- Pattern 20: Razor Lambda Type Mismatch (NEW)
- Pattern 21: Missing DTO Definitions (NEW)
- Pattern 22: Blazor RenderFragment Method Call (NEW)
- Pattern 23: Dispose Pattern Violations (NEW)
- Pattern 24: Static Method Opportunities (NEW)
- Pattern 25: Performance Optimizations (NEW)
- Pattern 26: Culture-Dependent Operations (NEW)
- Pattern 27: xUnit Test Improvements (NEW)
- Pattern 28: Default Value Initializations (NEW)
- Pattern 29: Null Reference Warnings (NEW)
- Pattern 30: Duplicate Class/Type Definition (CS0101) (NEW)
- Pattern 31: Razor Generated File Namespace Resolution (CS0246 in .g.cs) (NEW)
- Pattern 32: Sync-over-Async Anti-Pattern (NEW 2026-06-18)

**Application Process:**
1. Choose 1 source file containing the error pattern.
2. Rewrite completely using Hybrid Pattern (TestEntityBuilder + Service/API call + Behavior assertion).
3. Create a template and apply it to other similar files.
4. Validate after each pattern: build + error count + domain integrity check.

## DETAILED PATTERN DEFINITIONS

### Pattern 1: Direct Entity Creation (50+ errors)
```
// WRONG:
new Customer { FullName = "...", PhoneNumber = "..." }
new JournalEntry() { TenantId = tenantId, Description = "..." }

// CORRECT:
TestEntityBuilder.CreateCustomer(tenantId, "...", "...")
new JournalEntry(tenantId, entryDate, description, referenceType, referenceId)
```

### Pattern 2: Incorrect Mock Setup (30+ errors)
```
// WRONG:
_notificationService.Setup(...).Verify(...).Times.Once

// CORRECT:
// Test business results instead
var customer = await _dbContext.Customers.FirstAsync();
Assert.Equal("Test Customer", customer.FullName);
```

### Pattern 3: Service Method Mismatch (20+ errors) — ⚠️ OBSOLETE (2026-06-18)
> **STATUS:** OBSOLETE. Cả `SendWelcomeMessageAsync` và `GetEntriesByPeriodAsync` đều là method **hợp lệ** trong interface hiện tại:
> - `ICustomerOnboardingService.SendWelcomeMessageAsync(Guid)` — `3_CoreHub/Services/ICustomerOnboardingService.cs:39`
> - `IAccountingService.GetEntriesByPeriodAsync(TenantId, AccountingPeriod)` — `1_Shared/Services/IAccountingService.cs:29`
>
> Pattern này dựa trên interface cũ đã được cập nhật. **KHÔNG áp dụng** để fix — có thể gây xóa method hợp lệ.
> Giữ lại làm historical record. Nếu gặp CS1061 trên service method, inspect interface thực tế thay vì apply pattern này.

```
// WRONG (historical — không còn đúng):
SendWelcomeMessageAsync (không có trong interface)
GetEntriesByPeriodAsync (method không exist)

// CORRECT (historical):
StartOnboardingAsync (tôn tai trong interface)
GetEntriesByTenantAndPeriodAsync (correct method name)
```

### Pattern 4: Missing Test Dependencies (15+ errors)
```
// WRONG:
[Fact] // but missing using Xunit;

// CORRECT:
using Xunit;
[Fact]
```

### Pattern 5: Type Mismatch Conversions (10+ errors)
```
// WRONG:
List<AccountingEntry> entries = new List<JournalEntry>();
var dto = new AccountingEntryDto { Id = entity.Id.Value };

// CORRECT:
List<AccountingEntry> entries = TestEntityBuilder.CreateAccountingEntry(...);
var dto = new AccountingEntryDto { 
    Id = entity.Id.Value,
    // Map all required properties properly
};
```

### Pattern 6: Property Access Errors (8+ errors) — ⚠️ MISLEADING (2026-06-18)
> **STATUS:** MISLEADING. Pattern claim `BookType` luôn sai và phải thay bằng `BookTypeCode` — nhưng thực tế **cả 2 đều là property hợp lệ trên các type khác nhau**:
> - `BookType` (AccountingBookType enum) — property trên 12 DTOs trong `IHKDTaxReportingService.cs:77`, `IHKDTaxClassificationService.cs:61`, `IHKDComplianceService.cs:69`
> - `BookTypeCode` (string) — property trên `HKDBookService` result, dùng trong `HKDBookServiceTests.cs:60,87,174`
>
> **Nguyên nhân gốc:** Pattern được viết khi `BookType` chưa tồn tại trên DTOs. Giờ DTOs đã có `BookType` (enum) riêng.
> **Áp dụng đúng:** Khi gặp CS1061 trên `.BookType` hoặc `.BookTypeCode`, **INSPECT type thực tế** trước khi fix. Đừng auto-rename.

```
// WRONG (chỉ đúng khi type KHÔNG có property BookType):
result.BookType.Should().Be(AccountingBookType.S1a_HKD);

// CORRECT (chỉ khi type có BookTypeCode, không có BookType):
result.BookTypeCode.Should().Be("S1a_HKD"); // Use correct property name

// CORRECT (khi type CÓ BookType enum — đừng đổi!):
result.BookType.Should().Be(AccountingBookType.S1a_HKD);
```

### Pattern 7: Extension Method Issues (5+ errors)
```
// WRONG:
guid.ToGuid() // Method không exist

// CORRECT:
new TenantId(guid) // Use proper constructor
```

### Pattern 8: Constructor Parameter Mismatches (5+ errors)
```
// WRONG:
new Service(mockA, mockB, logger) // Wrong order

// CORRECT:
new Service(repository, hkdRepository, logger) // Correct parameter order
```

### Pattern 9: UI Platform Component Parameter Mismatch (8+ errors)
```
// WRONG:
<VanAnButton Variant="outline" Size="small" />
<VanAnAlert Variant="info" />

// CORRECT:
<VanAnButton Variant="ButtonVariant.Outline" Size="ButtonSize.Small" />
<VanAnAlert Variant="AlertVariant.Info" />
```

### Pattern 10: Navigation Service Injection Architecture (4+ errors)
```
// WRONG:
@inject NavigationManager NavigationManager // Duplicate injection
// Components don't have access to Navigation

// CORRECT:
@inherits VanAn.UI.Platform.Components.Base.BaseComponent
// Navigation available through inheritance
```

### Pattern 11: Razor TagHelper Syntax Errors (4+ errors)
```
// WRONG:
<VanAnButton OnClick="() => Navigation.NavigateTo('/cart')" />

// CORRECT:
<VanAnButton OnClick="@(() => Navigation.NavigateTo('/cart'))" />
```

### Pattern 12: Navigation Variable Mismatch (3+ errors)
```
// WRONG:
Navigation.NavigateTo('/cart') // Navigation variable not available

// CORRECT:
NavigationManager.NavigateTo('/cart') // Use correct NavigationManager variable
```

### Pattern 13: CSS Media Query in Code Block (2+ errors)
```
// WRONG:
@code {
    @media (max-width: 640px) { ... } // CSS in C# code block
}

// CORRECT:
<style>
@media (max-width: 640px) { ... } // CSS in proper style block
</style>
```

### Pattern 14: Static Method Call Context (2+ errors)
```
// WRONG:
ThemeTypeExtensions.ToCssClass(theme) // Missing this/context

// CORRECT:
ThemeTypeExtensions.ToCssClass(theme) // Call as static method with proper context
```

### Pattern 15: Razor HTML Parsing Errors (1+ errors)
```
// WRONG:
<object> // Unclosed HTML tag causing RZ9980

// CORRECT:
<object>...</object> // Properly closed HTML tags
```

### Pattern 16: Blazor Event Handler Type Mismatch (3+ errors)
```
// WRONG:
@oninput="@(e => HandleFieldInput(e, field))" // Lambda cannot convert to delegate type

// CORRECT:
@bind="field.Value" // Use Blazor @bind pattern for two-way data binding
```

### Pattern 17: Security Vulnerability Dependencies (9+ errors)
```
// WRONG:
<PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" /> // Known vulnerability

// CORRECT:
<PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="8.0.8" /> // Updated secure version
```

### Pattern 18: Duplicate Package Central Management (9+ errors)
```
// WRONG:
Multiple projects with different versions of same package
Microsoft.Extensions.Logging 7.0.0 in project A
Microsoft.Extensions.Logging 8.0.0 in project B

// CORRECT:
Single source of truth in Directory.Packages.props
<PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
```

### Pattern 20: Razor Lambda Type Mismatch (6+ errors)
```
// WRONG:
@oninput="@(e => field.Value = e.Value?.ToString())" // CS1660: Cannot convert lambda to delegate

// CORRECT:
@oninput="@(ChangeEventArgs e => field.Value = e.Value?.ToString())" // Explicit delegate type
```

### Pattern 21: Missing DTO Definitions (2+ errors)
```
// WRONG:
Service references OfflineOrderDto but DTO doesn't exist
CS0246: The type or namespace name 'OfflineOrderDto' could not be found

// CORRECT:
Create missing DTO class with proper properties
public class OfflineOrderDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    // ... other properties
}
```

### Pattern 22: Blazor RenderFragment Method Call (2+ errors)
```
// WRONG:
@column.Template(item) // CS1955: Non-invocable member cannot be used like a method
@column.Template.Invoke(item) // Incorrect delegate invocation

// CORRECT:
@column.Template(item) // Use proper Razor template syntax
// Or better: Generic DataGrid with RenderFragment<TItem>
<VanAnDataGrid TItem="Order" Items="orders">
    <Columns>
        <VanAnColumn TItem="Order" Title="Id" Template="@(o => @<span>@o.Id</span>)" />
    </Columns>
</VanAnDataGrid>
```

### Pattern 23: Dispose Pattern Violations (15+ errors)
```
// WRONG:
public void Dispose()
{
    // Missing GC.SuppressFinalize call
}

// CORRECT:
public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this); // Required for proper dispose pattern
}
```

### Pattern 24: Static Method Opportunities (8+ errors)
```
// WRONG:
public string GetContentType() // CA1822: Member does not access instance data
{
    return "application/json";
}

// CORRECT:
public static string GetContentType() // Mark as static when possible
{
    return "application/json";
}
```

### Pattern 25: Performance Optimizations (10+ errors)
```
// WRONG:
if (items.Any()) // CA1860: Prefer Count to 0 for clarity and performance

// CORRECT:
if (items.Count > 0) // More performant and clearer
```

### Pattern 26: Culture-Dependent Operations (20+ errors)
```
// WRONG:
string.ToLower() // CA1304: Behavior varies based on current culture
DateTime.ToString("dd/MM/yyyy") // CA1305: Culture-dependent formatting

// CORRECT:
string.ToLower(CultureInfo.InvariantCulture) // Explicit culture
DateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) // Culture-safe
```

### Pattern 27: xUnit Test Improvements (5+ errors)
```
// WRONG:
[Theory]
[InlineData("test", true)]
public void TestMethod(string input, bool isCredit) // xUnit1026: Parameter not used

// CORRECT:
[Theory]
[InlineData("test", true)]
public void TestMethod(string input, bool isCredit)
{
    // Use the isCredit parameter
    Assert.Equal(isCredit, input.StartsWith("test"));
}

// WRONG:
Assert.NotNull(null) // xUnit1012: Null should not be used for non-nullable type

// CORRECT:
Assert.Null(actualValue) // Use appropriate assertion
```

### Pattern 28: Default Value Initializations (3+ errors)
```
// WRONG:
public bool IsDark { get; set; } = false; // CA1805: Explicitly initialized to default

// CORRECT:
public bool IsDark { get; set; } // Let compiler use default value
```

### Pattern 29: Null Reference Warnings (4+ errors)
```
// WRONG:
return context?.Tenant?.Id; // CS8603: Possible null reference return

// CORRECT:
return context?.Tenant?.Id ?? TenantId.Empty; // Provide fallback value
```

### Pattern 30: Duplicate Class/Type Definition (CS0101) (NEW)
```
// WRONG:
// NavigationItem.cs exists in UI.Platform/Models/
// NavigationItem also defined in UI.Platform/Models/ComponentModels.cs
// CS0101: The namespace 'VanAn.UI.Platform.Models' already contains a definition for 'NavigationItem'

// CORRECT:
// Delete duplicate definition, enforce "One Class One File" rule
// Keep class definition in dedicated file (NavigationItem.cs)
// Remove class definition from aggregate file (ComponentModels.cs)
```

**Solution Protocol:**
1. Search all files in namespace for duplicate class definitions
2. Identify which file should contain the class (dedicated file vs aggregate)
3. Delete duplicate definition from aggregate file
4. Verify build succeeds after deletion

**Prevention:**
- Enforce "One Class One File" rule in code review
- Use static analysis tools to detect duplicate classes
- Namespace strategy: Each class in separate file

### Pattern 31: Razor Generated File Namespace Resolution (CS0246 in .g.cs) (NEW)
```
// WRONG:
// _Imports.razor has @using VanAn.UI.Platform.Services
// But .g.cs file still cannot find ITenantService
// CS0246: The type or namespace name 'ITenantService' could not be found
// Location: Components/VanADashboard.razor.g.cs (generated file)

// CORRECT:
// Follow systematic resolution protocol
```

**Solution Protocol:**
1. **Verify Using Directives:** Check _Imports.razor has correct namespace
2. **Verify Project Reference:** Check .csproj has <ProjectReference> to UI.Platform
3. **Verify Relative Path:** Ensure path is correct (Pattern 6.2.X)
4. **Clean Build:** Run `dotnet clean && dotnet build --no-incremental`
5. **Check Generated Files:** Inspect .g.cs to see if using directives are generated
6. **Alternative Solution (Last Resort):** Add explicit @using at top of component file

**Prevention:**
- Always add UI Platform reference before implementation
- Verify _Imports.razor structure
- Use centralized namespace management

### Pattern 32: Sync-over-Async Anti-Pattern (24+ instances) (NEW 2026-06-18)
**Detected via codebase scan:** 24 instances of `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` in non-test code.

```
// WRONG — sync-over-async (deadlock risk, thread pool starvation):
var result = _service.GetAsync(id).Result;
_service.ProcessAsync(data).Wait();
var value = _repository.GetValueAsync(key).GetAwaiter().GetResult();

// CORRECT — propagate async:
var result = await _service.GetAsync(id);
await _service.ProcessAsync(data);
var value = await _repository.GetValueAsync(key);
```

**Why it matters:**
- **Deadlock risk:** In Blazor Server / ASP.NET Core sync context, `.Result`/`.Wait()` can deadlock
- **Thread pool starvation:** Blocks a thread while waiting for async work
- **Performance:** Wastes 1 thread per blocking call instead of releasing to pool

**Fix Procedure:**
1. Identify calling method signature
2. If method is `async Task` → change to `await` (preferred)
3. If method is `void` (event handler, constructor) → use one of:
   - `async void` (only for event handlers, with try/catch)
   - Fire-and-forget `_ = DoAsync()` (if caller doesn't need result)
   - `Task.Run(async () => await ...)` (offload, last resort)
4. If method is sync (legacy, cannot change) → wrap with `Task.Run(() => asyncOp).Result` to avoid sync context capture
5. **NEVER** use `.Result` in Blazor Server lifecycle methods (`OnInitialized`, `OnAfterRender`)

**Detection:**
```bash
# Find all sync-over-async instances
grep -rn "\.Result\b\|\.Wait()\|GetAwaiter()\.GetResult()" --include="*.cs" \
  | grep -v "Test" | grep -v "/bin/" | grep -v "/obj/"
```

**Exceptions (allowed sync-over-async):**
- Test setup code (`[Fact]` methods can use `.Result` if test framework requires sync)
- `Main(string[] args)` entry point (before C# 7.1 async Main)
- Property getters that cannot be async (refactor to method instead)

**Prevention:**
- Enable analyzer CA1849 (or CA2007 for ConfigureAwait) in `.editorconfig`
- Code review: reject any `.Result`/`.Wait()` in non-test code
- Prefer `await` over blocking in all new code

## PRIORITY FIXING ORDER
1. **Critical Build Blockers:** Patterns 15-16, 19, 22, 30, 31 (HTML Parsing, Event Handler Mismatch, Domain Entity Compilation, RenderFragment, Duplicate Class, Razor Namespace Resolution)
2. **High Impact:** Patterns 1-2, 20-21 (Direct Entity, Mock Setup, Lambda Type, Missing DTO) — ~~P3~~ OBSOLETE
3. **Security & Package:** Patterns 17-18, 32 (Vulnerabilities, Duplicate Packages, Sync-over-Async deadlock risk)
4. **Medium Impact:** Patterns 4-5, 23-26 (Test Dependencies, Type Mismatch, Dispose, Static Methods, Performance, Culture)
5. **UI/UX Impact:** Patterns 9-14 (UI Platform, Navigation, Razor, CSS, Static Methods)
6. **Low Impact:** Patterns 7-8, 27-29 (Extension Methods, Constructor, xUnit, Default Values, Null References) — ~~P6~~ MISLEADING

**Integration with RULE 6.1:**  
RULE 6.1 focuses on **assessment & planning**, RULE 6.2 focuses on **pattern-based execution**.

**Success Metric:** Each phase must reduce at least 20-30 errors without violating domain integrity

## ADVANCED PATTERN DETECTION

### Error Code Analysis
```
CS0200: Property cannot be assigned to (read-only) -> Pattern 1
CS1729: Constructor does not contain -> Pattern 8
CS1061: Member not found -> Pattern 9, 12 (~~P3, P6~~ OBSOLETE/MISLEADING — inspect interface/type first)
CS0246: Type not found -> Pattern 4, 7, 9, 31
CS1503: Cannot convert from -> Pattern 5
CS1950: Invalid collection initializer -> Pattern 1, 5
CS0101: The namespace already contains a definition -> Pattern 30
CS0103: Name does not exist in current context -> Pattern 9, 10, 11, 12, 14
CS1660: Cannot convert lambda expression to type 'bool' -> Pattern 16
CS8602: Dereference of a possibly null reference -> Pattern 16
CS8603: Possible null reference return -> Pattern 16
RZ9980: Unclosed tag with no matching end tag -> Pattern 15
RZ10012: Found markup element with unexpected name -> Pattern 9, 10
RZ1030: TagHelper attributes must be well-formed -> Pattern 11
NU1903: Package has known vulnerability -> Pattern 17
NU1506: Duplicate PackageVersion items found -> Pattern 18
```

### File Pattern Recognition
```
*Tests.cs files -> Focus on Pattern 4 (Test Dependencies)
*Service*.cs files -> Focus on Pattern 3 (~~Service Mismatch~~ OBSOLETE), Pattern 32 (Sync-over-Async)
*Repository*.cs files -> Focus on Pattern 5 (Type Conversions), Pattern 32 (Sync-over-Async)
*Controller*.cs files -> Focus on Pattern 8 (Constructor), Pattern 32 (Sync-over-Async)
*.razor files -> Focus on Patterns 9-16, 31 (UI Platform, Navigation, Razor, HTML, Event Handlers, Namespace Resolution)
*Layout*.razor files -> Focus on Patterns 10, 12, 13, 31 (Navigation, CSS, Static Methods, Namespace Resolution)
*Pages/*.razor files -> Focus on Patterns 9, 11, 15, 16, 31 (UI Platform, Razor, HTML, Event Handlers, Namespace Resolution)
*Directory.Packages.props -> Focus on Pattern 17, 18 (Security, Duplicate Packages)
*Directory.Build.props -> Focus on Pattern 18 (Duplicate Packages)
*Models/*.cs files -> Focus on Pattern 30 (Duplicate Class Definitions)
```

## EXECUTION GUIDELINES

### Batch Processing Strategy
1. **Group by Pattern:** Fix all occurrences of same pattern together
2. **Use replace_all:** When pattern appears multiple times in same file
3. **Validate Frequently:** Build after each pattern elimination
4. **Track Progress:** Document error count reduction per pattern

### Warning Signs (STOP if you're doing these)
- Fixing individual errors one-by-one without identifying pattern
- Modifying Domain.cs or BaseEntity
- Bypassing protected setters
- Using object initializers for immutable entities
- Creating mock setups instead of business assertions

### Post-Fix Validation
- [ ] Build succeeded with 0 errors
- [ ] Domain integrity maintained
- [ ] No new warnings introduced
- [ ] Architecture compliance verified

## ADAPTATION RULE

**If existing patterns cannot fix the errors, investigate deeper to identify new patterns:**
1. Group similar errors by file type, error code, or component
2. Identify root cause beyond surface-level symptoms
3. Create and document new pattern
4. Apply systematically with batch replacement

## Pattern 6.2.X: Relative Path Integrity (ProjectReference)

**Rule:** Never blame "MSBuild system errors" for CS0234/CS0246 before manually verifying `..` count in ProjectReference paths.

**Anti-pattern:** NEVER copy-paste code between projects to fix build. This violates "Single Source of Truth".

**Protocol:**
1. Check actual folder structure
2. Count required `..` levels from source to target
3. Compare with path in .csproj
4. Fix path if wrong

**Standard structure:**
```
VanAn.sln (Root)
├── 1_Shared/
├── UI.Platform/
├── 2_Gateway/
└── 3_CoreHub/
```

Example: From UI.Platform → 1_Shared = `..\1_Shared\` (1 level up, NOT 2).