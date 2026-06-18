---
description: "Mandatory session context loading for ShopERP/VanAn project"
trigger: always_on
---

# SESSION CONTEXT LOADING (MANDATORY)

At the START of every session involving code work (implement, fix, review, plan), AI MUST read these two files before doing anything else:

1. `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` — architecture, ADRs, tech stack, code patterns, hard stops
2. `docs/AI/project_state.md` — current objective, status, next actions, history

Then report a short context summary:
```
Context Loaded
- Current Objective: <from project_state.md section 2>
- Active Mode: ANALYZE | IMPLEMENT | FIX_ONLY | REVIEW_ONLY
- Current Branch: <git branch>
- Next Actions: <from project_state.md section 4>
```

## EXCEPTIONS (skip loading)
- Trivial read-only questions (single file read, grep, "what does X do")
- User explicitly says "skip context"
- Pure documentation/CLI lookups unrelated to the codebase

## UPDATING STATE
When the user says "update state" / "save progress", or when a milestone/objective is completed, update `docs/AI/project_state.md`:
- Section 2 (Current Objective): update status; move finished objective into history
- Section 3 (Current Status): add completed items
- Section 4 (Next Actions): remove done, add new
- Section 11 (Maintenance Log): update Last Updated + current branch

Follow the Maintenance Rules in Section 0 of project_state.md (no duplicate sections, no contradictions, verify paths/branches against the real repo).

## FULL GOVERNANCE
The complete VanAn governance (Domain rules, workflow modes, UI Platform, hard stops) is in `.windsurf/rules/governance.md` and is always-on.
