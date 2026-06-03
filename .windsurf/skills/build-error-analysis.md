# Skill: Build Error Analysis

## Purpose
Analyze build errors, classify severity, and identify root-cause patterns before any fix.

## Use When
- `dotnet build` fails.
- Compile/runtime errors block progress.
- Error count must be classified before fixing.

## Inputs
- Build output.
- Total error count.
- Top error codes.
- Affected files and projects.

## Procedure
1. Run clean build when required by workflow.
2. Count total errors.
3. Group errors by code, file type, and affected layer.
4. Classify severity: small `<2`, medium `2-5`, high `>5`.
5. Map errors to known patterns before proposing fixes.
6. Report root cause summary and recommended next action.

## Stop Conditions
- Error count increases by more than 10%.
- Architecture boundary is crossed.
- Domain immutability is threatened.
- More than 3 files are needed in one fix batch.

## References
- `.windsurf/workflows/Fix_Errors.md`
- `.windsurf/rules/.windsurfrules`
- `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md`
