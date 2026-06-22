# Skill: Domain Entity Compilation Fix (P19)

## Purpose
Fix compilation errors related to Domain entities while maintaining immutability, enforcing proper constructor usage, and preventing domain pollution. **CRITICAL BLOCKER PATTERN.**

## Use When
- `CS0200`: Property cannot be assigned (read-only) on domain entity
- `CS1729`: Constructor does not contain expected parameters
- `CS1950`: Invalid collection initializer on domain entity
- Domain entity fails to compile after refactor
- Ghost file errors in Domain layer (CS0246, CS0234)

## Hard Rules (ZERO TOLERANCE)
- **NEVER** use object initializers for immutable entities: `new Entity { Prop = value }`
- **NEVER** bypass protected setters with reflection or direct assignment
- **NEVER** add public setters to immutable properties
- **NEVER** move EF Core attributes into Domain
- **ALWAYS** use proper constructors or factory methods
- **ALWAYS** verify with `domain-integrity-validation` after fix

## Pattern-Based Fixes

### P19.1: Immutable Property Assignment Error (CS0200)
**Error:** `CS0200: Property or indexer cannot be assigned to -- it is read only`

**Detection:** Attempting to use object initializer or direct assignment on immutable entity.

**Root Cause Analysis:**
1. Check if entity is truly immutable (has private/protected setter or init-only)
2. Check if entity has proper constructor parameters
3. Check if factory method exists

**Fix Options (in priority order):**

**Option 1: Use Proper Constructor (BEST)**
```csharp
// WRONG:
var entry = new AccountingEntry 
{ 
    TenantId = tenantId,  // CS0200
    Description = desc     // CS0200
};

// CORRECT:
var entry = new AccountingEntry(
    tenantId,
    entryDate,
    description,
    referenceType,
    referenceId
);
```

**Option 2: Use Factory Method**
```csharp
// WRONG:
var customer = new Customer { FullName = name }; // CS0200

// CORRECT:
var customer = Customer.Create(tenantId, name, phone);
// or
var customer = TestEntityBuilder.CreateCustomer(tenantId, name, phone);
```

**Option 3: Create Factory Method (requires approval)**
If no constructor or factory exists:
1. STOP and report
2. Request approval to add factory method
3. Document in plan

---

### P19.2: Constructor Parameter Mismatch (CS1729)
**Error:** `CS1729: 'Type' does not contain a constructor that takes X arguments`

**Fix Procedure:**
1. Inspect actual constructor signature in Domain.cs
2. Map parameters correctly:
   ```csharp
   // WRONG:
   new Service(mockA, mockB, logger) // Wrong order

   // CORRECT:
   new Service(repository, hkdRepository, logger) // Match actual signature
   ```
3. For test entities, use `TestEntityBuilder` instead of direct construction

---

### P19.3: Collection Initializer Error (CS1950)
**Error:** `CS1950: The best overloaded Add method ... has some invalid arguments`

**Cause:** Trying to use collection initializer on immutable collection property.

**Fix:**
```csharp
// WRONG:
var entity = new MyEntity 
{
    Items = { item1, item2 }  // CS1950 if Items has no setter
};

// CORRECT - Pass in constructor:
var entity = new MyEntity(new List<Item> { item1, item2 });

// Or use factory:
var entity = MyEntity.CreateWithItems(item1, item2);
```

---

### P19.4: Ghost File Domain Errors (CS0246/CS0234 in Domain)
**Error:** Type not found in Domain namespace despite file existing

**Fix Procedure:**
1. Check for **duplicate class definitions** (Pattern 30):
   ```bash
   grep -r "class MyEntity" 1_Shared/
   ```
2. Check namespace consistency:
   ```csharp
   // File location: 1_Shared/Domain/Entities/MyEntity.cs
   namespace VanAn.Shared.Domain.Entities; // Must match folder
   ```
3. Check if class was moved but old file still exists (ghost file)
4. Check `VanAn.sln` includes the project

---

## Fix Workflow

### Phase 1: Classification
Count domain-related errors:
- **Solo (1 error):** Direct fix with validation
- **Multiple (2+):** Require investigation + approval before fixing

### Phase 2: Fix with Safeguards
For each error:
1. Read actual domain entity definition FIRST
2. Identify correct construction pattern
3. Apply fix using constructor/factory
4. NEVER modify domain entity itself
5. Validate: `dotnet build` + `domain-integrity-validation`

### Phase 3: Validation
Mandatory checks after any P19 fix:
- [ ] Build: 0 domain errors
- [ ] `AccountingEntry` immutability maintained
- [ ] No new public setters added
- [ ] No EF Core references in Domain
- [ ] Factory methods preferred over complex constructors

## Stop Conditions
- Fix requires adding public setter to immutable property
- Fix requires changing Domain entity definition
- Entity appears to need redesign (not just construction fix)
- Same P19 error after 3 fix attempts
- Assumption: Any uncertainty about correct construction pattern

## Batch Fix Strategy
- Max 3 files per batch (domain errors are high-risk)
- Group by: Same entity type, same error code
- Always rebuild after each batch
- Never mix P19 fixes with other pattern types

## Tools Integration
- Use `domain-integrity-validation.md` skill after each fix
- Use `pattern-based-fixing.md` for non-domain files
- Escalate to human if domain modeling question arises

## References
- `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md` - P19 definition
- `.devin/skills/domain-integrity-validation.md` - Validation checks
- `.devin/rules/.windsurfrules` - Domain protection rules
