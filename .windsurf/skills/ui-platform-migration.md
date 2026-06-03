# Skill: UI Platform Migration

## Purpose
Migrate custom UI code to VanAn UI Platform components while preserving behavior and reducing custom CSS.

## Use When
- A Razor component uses custom HTML/CSS where UI Platform components exist.
- Migrating KhachLink or module UI to platform compliance.
- A feature requires standardized UI components.

## Common Mappings
- Custom button → `VanAButton`.
- Custom card/panel → `VanACard`.
- Custom/Bootstrap modal → `VanAModal`.
- Custom table/grid → `VanADataGrid`.
- Custom layout → `VanALayout`.
- Custom navigation → `VanANavigation`.

## Procedure
1. Identify component role and existing behavior.
2. Map custom elements to UI Platform components.
3. Preserve event handlers and data flow.
4. Remove obsolete custom CSS only when replaced by platform styling.
5. Validate responsive behavior, accessibility, and build.

## Stop Conditions
- Functionality parity is unclear.
- Migration requires changing Domain logic.
- UI Platform component is missing or broken; fix UI.Platform instead of bypassing.

## References
- `docs/UI_Platform_Implementation_Guide.md`
- `docs/plan_MVP/khachlink-ui-platform-migration-5432e4.md`
- `.windsurf/rules/.windsurfrules`
