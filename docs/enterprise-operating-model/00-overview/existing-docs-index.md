# Existing Documentation Index & Deduplication Decisions — Phase 0

> SRS §14-P0, §16.7: twin **indexes** existing docs, does not duplicate or absorb them.
> Decision per cluster: `INDEX` (twin links to it, keep as-is) · `SUPERSEDE` (twin restates — must record why) · `GAP` (topic has no adequate doc).
> Captured at `64789992` — `docs/` ≈ 766 .md files; below is cluster-level triage, not per-file.

| Cluster | Path | Decision | Note |
|---|---|---|---|
| Requirements & SRS | `docs/requirements/` (~30 files) | **INDEX** | Business intent source — twin references for TO-BE/context; never restates |
| Formal specs | `docs/specs/` (accounting engine, voucher framework, loyalty) | **INDEX** | Authoritative specs — link from registries as evidence |
| AI working docs | `docs/AI/` (project_state, plans, tasks, guides) | **INDEX** | Execution history — twin links master plans for module context |
| Architecture | `docs/Architecture/` (ADRs, monolithic arch, DB docs w/ mermaid) | **INDEX** | Contains DB docs + mermaid — P3 mines for data model |
| Feature guides | `docs/*_Guide*.md`, `*_Documentation.md` (ShopERP, KhachLink, admin, mobile, UI platform) | **INDEX** | User/dev docs — link as narrative evidence |
| Process docs | `docs/QuyTrinh/`, `docs/HopTacXa/`, `docs/IMPLEMENTATION/`, `docs/Implement/` | **INDEX** | Business process descriptions — P2 mines for actor/workflow discovery |
| MVP/design | `docs/MVP_Product/`, `docs/design/` (use-case docs w/ mermaid) | **INDEX** | Historical design intent — ASSUMED-level evidence |
| Decisions | `docs/decisions/` | **INDEX** | ADR-style — authoritative for "why" |
| Diagnostics/archive | `docs/diagnostics/`, `docs/requirements/archive/`, `docs/AI/archive_tasks.md` | **INDEX** | Historical only — exclude from AS-IS evidence |
| Loose root files | `docs/*.md` flat (SQLite fix plans, module designs…) | **INDEX** | Case-by-case linking; some overlap w/ twin scope |
| Digital Twin | `docs/enterprise-operating-model/` | — | This project |

## Dedup rules applied

1. No existing doc is moved/renamed/deleted (§16.8).
2. Twin files reference existing docs by `path@commit`, never copy their content.
3. Where two docs conflict on AS-IS fact → code wins, discrepancy logged in evidence-register §C.
4. SUPERSEDE requires explicit owner authorization — none granted at P0, so **zero SUPERSEDE decisions** made.

## GAP notes (topics with thin docs — seed for gap register)

- No single doc describes end-to-end F4 onboarding flow (pieces in HopTacXa/, membership SRS, OCR onboarding plan).
- No consolidated NATS subject catalog outside code comments.
- No actor/responsibility model exists anywhere — twin fills this.
