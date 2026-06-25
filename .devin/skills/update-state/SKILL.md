---
name: update-state
description: Update docs/AI/project_state.md with current session progress
allowed-tools:
  - read
  - edit
  - exec
triggers:
  - user
  - model
---

Update `docs/AI/project_state.md` to reflect the current state of work in this session.

Steps:

1. Read `docs/AI/project_state.md` (note Section 0 Maintenance Rules — obey them).
2. Gather ground truth:
   - `git branch --show-current`
   - `git status --short`
   - `git log -1 --oneline`
3. Update the relevant sections:
   - **Section 2 (Current Objective):** update status; if the objective is finished, mark it COMPLETED and summarize. If a new objective started, set it.
   - **Section 3 (Current Status):** add newly completed items; record any blockers.
   - **Section 4 (Next Actions):** remove finished actions, add concrete next steps.
   - **Section 11 (Maintenance Log):** update `Last Updated` (date) and `Current Branch`, add a one-line entry summarizing this session.
4. Maintenance Rules to obey:
   - One section appears only once; no duplicates, no contradictions.
   - Verify every path/branch against the real repo before writing it.
   - Keep it concise (DRY) — each fact in exactly one place.
5. Report a short summary of what was changed.

6. Optional: Archive completed waves:
   - After updating, check if there are completed waves in Section 2 (PREVIOUS OBJECTIVE archived sections)
   - If completed waves exist, ask user: "Archive completed waves to project_state_archive.md? (y/n)"
   - If user confirms, invoke `archive-completed-work` skill to move completed waves to archive file
   - This keeps project_state.md focused on current work

Note: if `edit` fails due to file encoding/whitespace, fall back to a small Python script (read with `encoding='utf-8', errors='ignore'`, modify, write back as UTF-8).
