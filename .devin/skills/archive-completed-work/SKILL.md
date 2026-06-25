---
name: archive-completed-work
description: Archive completed waves from project_state.md to separate file to reduce file size
allowed-tools:
  - read
  - edit
  - exec
triggers:
  - user
---

Archive completed waves from `docs/AI/project_state.md` to `docs/AI/project_state_archive.md` to reduce file size and keep project_state.md focused on current work.

Steps:

1. Read `docs/AI/project_state.md` to identify completed waves:
   - Look for sections marked as "PREVIOUS OBJECTIVE (archived)" with Status: ✅ COMPLETED
   - Identify which waves to archive (all completed waves, or specific ones if user specifies)

2. Create or read `docs/AI/project_state_archive.md`:
   - If file doesn't exist, create it with header: `# Project State Archive`
   - If file exists, read current content

3. Move completed wave sections:
   - For each completed wave section from project_state.md:
     - Add it to project_state_archive.md (append at the end)
     - Add timestamp: `Archived: [YYYY-MM-DD]`
   - Remove the completed wave section from project_state.md

4. Update project_state.md:
   - Keep Section 2 (Current Objective) - should be empty or active
   - Keep Section 3 (Current Status)
   - Keep Section 4 (Next Actions)
   - Keep Section 11 (Maintenance Log) - update with archive action
   - Remove all archived PREVIOUS OBJECTIVE sections

5. Verify and report:
   - Count how many waves were archived
   - Report new file sizes (project_state.md vs project_state_archive.md)
   - Follow Maintenance Rules from project_state.md Section 0

Note: This skill is MANUAL - user must invoke it explicitly. It does not auto-trigger.
