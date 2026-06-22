---
name: load-context
description: Load ShopERP/VanAn project context (PROJECT_CONTEXT.md + project_state.md)
allowed-tools:
  - read
  - exec
triggers:
  - user
  - model
---

Load the project context so you understand the current state before doing work.

Steps:

1. Read `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` (architecture, ADRs, tech stack, hard stops).
2. Read `docs/AI/project_state.md` (current objective, status, next actions, history).
3. Run `git branch --show-current` to confirm the active branch.
4. Report a short summary:
   ```
   Context Loaded
   - Current Objective: <project_state.md section 2>
   - Active Mode: ANALYZE | IMPLEMENT | FIX_ONLY | REVIEW_ONLY
   - Current Branch: <git>
   - Next Actions: <project_state.md section 4>
   ```

Do not start implementation until context is loaded and (for non-trivial work) the user has confirmed the objective.
