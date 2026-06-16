# Skill: Domain Integrity Validation

## Purpose
Protect domain purity, immutability, and architecture boundaries during implementation, fixing, and review.

## Use When
- Errors touch domain entities or value objects.
- Mapping from DTO/API/UI to domain is unclear.
- `CS1061` or property mismatch appears on domain entities.
- Accounting or multi-tenancy logic is involved.

## Checks
- `AccountingEntry` remains append-only and immutable.
- Domain layer contains no EF Core, DbContext, or DataAnnotations.
- Domain entities are not duplicated outside the source of truth.
- UI/Service fixes do not modify Domain.
- Multi-tenancy is preserved at every layer.

## Procedure
1. Inspect actual domain definition before mapping properties.
2. Verify semantic identity of IDs and references.
3. Report missing domain property as modeling defect.
4. Require approval before any Domain change in IMPLEMENT mode.
5. Refuse Domain changes in FIX_ONLY mode.

## Stop Conditions
- Domain is modified to fix UI/Service issue.
- Protected setters are bypassed.
- AccountingEntry immutability is threatened.
- Business logic is moved into Controller/Gateway/Hub.

## References
- `.windsurf/rules/.windsurfrules`
- `.windsurf/workflows/Fix_Errors.md`
- `docs/QuyTrinh/Refactor/18_Conflicts_System_Refactor_Plan.md`
