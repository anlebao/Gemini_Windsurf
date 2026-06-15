# Skill: System Refactor Safety

## Purpose
Keep large refactors controlled, reversible, and architecture-safe.

## Use When
- Planning or reviewing system-wide refactors.
- Fixing architecture conflicts.
- Changing shared infrastructure or domain architecture.

## Required Controls
- Define scope before implementation.
- Work phase by phase.
- Validate after each phase.
- Keep rollback path available.
- Avoid unrelated cleanup.

## Procedure
1. Identify affected layers and risk level.
2. Confirm approval and current objective.
3. Create phase plan with validation points.
4. Limit changes to current phase.
5. Run build, guard check, and tests after phase.
6. Report changed files, validation results, and rollback risk.

## Code Quality Patterns (P23-P26)
This skill provides **concrete fix procedures** for orphan patterns in medium-impact category:

### P23: Dispose Pattern Violations
**Detection:** Missing `GC.SuppressFinalize(this)` in Dispose method  
**Fix:**
```csharp
public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this); // Required
}
```

### P24: Static Method Opportunities
**Detection:** CA1822 - Member does not access instance data  
**Fix:**
```csharp
// From:
public string GetContentType() { return "application/json"; }

// To:
public static string GetContentType() { return "application/json"; }
```

### P25: Performance Optimizations
**Detection:** CA1860 - Prefer `Count` property to `Any()`  
**Fix:**
```csharp
// From:
if (items.Any())

// To:
if (items.Count > 0)
```

### P26: Culture-Dependent Operations
**Detection:** CA1304/CA1305 - Culture-dependent string/DateTime operations  
**Fix:**
```csharp
// From:
string.ToLower()
DateTime.ToString("dd/MM/yyyy")

// To:
string.ToLower(CultureInfo.InvariantCulture)
DateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
```

## Batch Fix Strategy for Code Quality
1. Group by analyzer code (CA1822, CA1860, CA1304, etc.)
2. Use `replace_all` within affected file
3. Limit to 5 files per batch
4. Run build + analyzer after each batch
5. Document reduction count per pattern

## Test & Code Quality Patterns (P27-P29)
This skill provides **concrete fix procedures** for test-related low-impact orphan patterns.

### P27: xUnit Test Improvements
**Detection:** xUnit analyzer warnings (xUnit1026, xUnit1012, etc.)

**Fix Patterns:**

**xUnit1026: Unused theory parameter**
```csharp
// WRONG:
[Theory]
[InlineData("test", true)]
public void TestMethod(string input, bool isCredit) // isCredit not used

// CORRECT:
[Theory]
[InlineData("test", true)]
public void TestMethod(string input, bool isCredit)
{
    Assert.Equal(isCredit, input.StartsWith("test")); // Use parameter
}

// Or remove if truly unnecessary:
[Theory]
[InlineData("test")]
public void TestMethod(string input)
```

**xUnit1012: Null value for non-nullable**
```csharp
// WRONG:
Assert.NotNull(null)

// CORRECT:
Assert.Null(actualValue) // Use appropriate assertion
Assert.NotNull(result)
```

### P28: Default Value Initializations
**Detection:** CA1805 - Member explicitly initialized to default value

**Fix:**
```csharp
// WRONG:
public bool IsDark { get; set; } = false; // Redundant
public int Count { get; set; } = 0;      // Redundant
public string Name { get; set; } = null; // Redundant

// CORRECT:
public bool IsDark { get; set; } // Compiler sets false
public int Count { get; set; }   // Compiler sets 0
public string? Name { get; set; } // Nullable, null by default
```

### P29: Null Reference Warnings
**Detection:** CS8602, CS8603 - Possible null reference

**Fix Patterns:**

**CS8602: Dereference of possibly null**
```csharp
// WRONG:
return context.Tenant.Name; // Tenant might be null

// CORRECT:
return context.Tenant?.Name ?? string.Empty; // Safe navigation
```

**CS8603: Possible null return**
```csharp
// WRONG:
return context?.Tenant?.Id; // Id might be null

// CORRECT:
return context?.Tenant?.Id ?? TenantId.Empty; // Provide fallback
```

## Batch Fix Strategy for Test/Code Quality
1. Group by: Same analyzer code
2. Use `replace_all` within test project
3. Limit: 10 files per batch (tests are safer)
4. Run: `dotnet test` after each batch
5. Document: Warning count reduction

## Stop Conditions
- Domain immutability is threatened.
- Multi-tenancy enforcement weakens.
- Public API changes without approval.
- Refactor expands beyond approved scope.
- Validation fails and root cause is unclear.
- Code quality fix causes behavioral change (stop and review).

## References
- `docs/QuyTrinh/Refactor/18_Conflicts_System_Refactor_Plan.md`
- `.windsurf/rules/.windsurfrules`
- `.windsurf/workflows/newfeaturebuild.md`
