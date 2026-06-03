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

## References
- `.windsurf/workflows/Fix_Errors.md`
- `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md`
