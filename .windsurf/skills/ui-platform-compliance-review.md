# Skill: UI Platform Compliance Review

## Purpose
Review UI code for compliance with VanAn UI Platform rules without implementing fixes.

## Use When
- Running `review_ui` mode.
- Checking Razor components for UI Platform bypass.
- Validating frontend migration results.

## Review Checklist
- UI Platform components are used when available.
- No custom button/card/modal/grid/layout duplicates platform components.
- Design tokens are used instead of hardcoded colors/spacing.
- Responsive behavior is mobile-first.
- Accessibility uses semantic HTML, ARIA labels, and keyboard support.
- Custom CSS is justified and minimal.

## Output Format
- Finding.
- Evidence: file and line/symbol.
- Risk.
- Suggested fix.
- Confidence level.

## Stop Conditions
- Finding is speculative.
- Review drifts into unrelated components.
- Suggested fix requires architecture redesign.

## References
- `docs/UI_Platform_Implementation_Guide.md`
- `docs/plan_MVP/khachlink-ui-platform-migration-5432e4.md`
- `.windsurf/workflows/review.md`
