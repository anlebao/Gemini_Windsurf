# Unresolved Questions — Phase 0

> SRS §16.9: record questions rather than inventing answers. Captured at `64789992`.

| # | Question | Context | Needed from |
|---|---|---|---|
| Q1 | Is `3_CoreHub` `<OutputType>Exe</OutputType>` intentional (generic host for NatsSyncWorker/migrations, added 2026-06-17) — and should governance text "pure Class Library" be updated? | Drift D1: code deviates from written rule | Owner / Tech Lead |
| Q2 | Is `1_Shared/Domain/` folder layout the sanctioned SSOT (superseding "all entities in Domain.cs")? | Drift D2: 57 files vs single-file wording | Tech Lead |
| Q3 | F4 onboarding: what is the CURRENT real-world process to sign a shop + form an HTX (sales motion, contracts, verification)? Code shows tenant provisioning mechanics; the human process is doc-thin. | F4 is least-implemented slice — need business narrative | Business owner |
| Q4 | Who legally operates HTX onboarding vs who technically executes it (Vạn An Ecommerce vs Technology vs Hạt Nhân)? | §4.3: legal responsibility ≠ workflow participation | Business owner |
| Q5 | Are `7_Tooling/VanAn.Crawler` outputs business-visible (directory/catalog seeding) or internal-only? | Found crawler worker; scope unknown | Tech Lead |
| Q6 | Which capabilities in SRS §2.2 (e.g., marketplace, procurement, subscription billing beyond metering) are planned vs implemented? | P0 found partial evidence only; defer detailed check to expansion cards | Owner (priority) + P3 |
| Q7 | Metering caller-key + wallet semantics — re-verify against `8_Metering` code (P0 read rules doc only). | Anchor A-6 PARTIAL | P3 (deferred) |
