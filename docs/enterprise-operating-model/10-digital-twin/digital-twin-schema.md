# Digital Twin Schema — Overview

> Contracts: `registries/schema/*.schema.json` (machine-checked by `scripts/validate-twin.py`).

## Layer model

| Layer | Files | Role |
|---|---|---|
| Data | `registries/*.yaml` (13) | Single source for entities/relationships — entries carry id, evidence `path@commit`, confidence, status |
| Narrative | `**/ *.md` + Mermaid | Human explanation — references IDs, never restates registry facts (§16.15) |
| Contract | `registries/schema/*.schema.json` | required fields, enums, id patterns, references, evidence pattern |
| Check | `scripts/validate-twin.py` | 6 checks: IDs, required, referential integrity, enums, evidence, coverage warnings |

## Status lifecycle

`ACTIVE` (current/verified) · `PROPOSED` (TO-BE awaiting owner) · `APPROVED` (owner-signed at gate) · `RETIRED` (never reuse ID)

## Confidence

`VERIFIED` (code/schema/API/test) · `INFERRED` (strong indication, unverified) · `ASSUMED` (needs owner)

## Reference fields (integrity-checked)

`*Ref` / `*Refs` fields point to IDs in other registries (e.g., `processRef→PROC`, `orgRef→ORG`, `ctrlRefs→CTRL`). Dangling refs = validator ERROR. `VARIANT` processes require `parentRef→CORE` (§6.6).
