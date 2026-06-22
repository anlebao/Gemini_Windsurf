# Skill: Pattern-Based Fixing

## Purpose
Fix recurring build/code errors by eliminating shared patterns instead of fixing individual symptoms.

## Use When
- Multiple errors share the same code, file type, or root cause.
- `Fix_Errors.md` enters FIX_ONLY mode.
- RULE_6_1 pattern reference is needed.

## Inputs
- Error code groups.
- Affected files.
- Pattern number candidates.
- Build result before fix.

## Procedure
1. Identify the dominant error pattern.
2. Confirm the pattern in the rule reference.
3. Select up to 3 files for the current fix batch.
4. Apply the smallest domain-safe correction.
5. Rebuild after the batch.
6. Report before/after error count and remaining patterns.

## Hard Rules
- Do not fix errors one-by-one when a shared pattern exists.
- Do not bypass protected setters or immutable domain rules.
- Do not introduce new abstractions during FIX_ONLY mode.
- Do not modify unrelated layers.

## Pattern Fix Reference (P6-P8 Low Impact)
This skill provides **concrete fix procedures** for low-impact orphan patterns.

### P6: Property Access Errors
**Error:** `CS1061: 'Type' does not contain a definition for 'PropertyName'`

**Fix Procedure:**
1. Check actual property name in type definition:
   ```csharp
   // WRONG:
   result.BookType.Should().Be(AccountingBookType.S1a_HKD); // Property doesn't exist

   // CORRECT:
   result.BookTypeCode.Should().Be("S1a_HKD"); // Use actual property name
   ```
2. Common renames in codebase:
   - `BookType` → `BookTypeCode`
   - `Amount` → `TotalAmount`
   - `Date` → `EntryDate`
3. Use `replace_all` for same pattern in test files

### P7: Extension Method Issues
**Error:** `CS1061` or `CS0117` - Method not found on type

**Fix Procedure:**
1. Check if extension method exists in namespace:
   ```csharp
   // WRONG:
   guid.ToGuid() // Method doesn't exist

   // CORRECT:
   new TenantId(guid) // Use proper constructor
   ```
2. Common extension replacements:
   - `guid.ToGuid()` → `new TypeId(guid)`
   - `string.ToTenantId()` → `TenantId.Parse(string)`
3. Add missing `using` if extension exists elsewhere:
   ```csharp
   using VanAn.Shared.Extensions;
   ```

### P8: Constructor Parameter Mismatches
**Error:** `CS1729: 'Type' does not contain a constructor that takes X arguments`

**Fix Procedure:**
1. Inspect constructor signature (Go to Definition):
   ```csharp
   // WRONG - Wrong parameter order:
   new Service(mockA, mockB, logger)

   // CORRECT - Match actual signature:
   new Service(repository, hkdRepository, logger)
   ```
2. Check for DI order in constructor:
   ```csharp
   public MyService(
       IRepository repo,      // Position 0
       ILogger logger,        // Position 1
       IOptions options)      // Position 2
   ```
3. For tests, use proper ordering:
   ```csharp
   var service = new MyService(
       mockRepo.Object,       // Position 0
       mockLogger.Object,     // Position 1
       mockOptions.Object     // Position 2
   );
   ```
4. Use `replace_all` within same test class

## Batch Fix Strategy for Low-Impact Patterns
1. Group by: Same error code + same file type
2. Use `replace_all` within each file
3. Limit: 5 files per batch (higher than domain errors)
4. Rebuild after each batch
5. Document: error count before/after per pattern

## References
- `.devin/workflows/Fix_Errors.md`
- `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md`
