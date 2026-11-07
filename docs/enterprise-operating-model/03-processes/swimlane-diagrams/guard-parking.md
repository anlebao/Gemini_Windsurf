# Giữ xe — Guard QR Parking (AS-IS, D1 deep-dive addendum)

> Activities → `activities.yaml` ACTV-622..628 · PROC-184 · RULE-026..028 · CTRL-021/022 · RISK-012/013 · `4f3c7ea3`.
> Feature flag: `Guard:QrVerifyEnabled` (all endpoints 403 when off). Issue #126/#130/#147.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph GRD["ACTR-017 Parking guard (Guard role, JWT)"]
        direction TB
        ISS["ACTV-622 Issue session<br/>(photo + 6-digit code + 58mm ticket)"]:::human
        VER["ACTV-625 Verify scan"]:::human
        OUT["ACTV-626 Checkout"]:::human
        FLG["ACTV-627 Flag / Void"]:::human
    end

    subgraph ENG["SYS-002/003 Gateway + CoreHub (PG, tenant-scoped)"]
        direction TB
        R2["ACTV-623 Photo -> R2<br/>(presign / server-side)"]:::auto
        BRD["ACTV-628 Today board +<br/>wallet status sync"]:::auto
    end

    subgraph CUS["ACTR-001 Customer (KhachLink, X-Customer-Token)"]
        CLM["ACTV-624 Claim session<br/>(QR {t,tn} / {sc,sid} / short code)"]:::human
    end

    subgraph DB["SYS-013 PG + R2"]
        VS[("VehicleSession Issued->Claimed->CheckedOut<br/>|Flagged|Voided · DATA-014")]:::auto
        LOG[("GuardScanLog — every verify")]:::auto
    end

    ISS --> R2 --> VS
    VS --> CLM --> VS
    CLM --> VER
    VER --> LOG
    VER -->|match| OUT --> VS
    VER -->|mismatch/override| FLG --> VS
    VER -.->|ManualOverride bypass — RISK-013| ERR["audit only"]:::error
    CLM -.->|short-code collision — RISK-012| ERR
    BRD --> VS
```

**Verification model (RULE-026):** photo + QR token = primary verifiers; plate number optional metadata; customer photo optional (#130). Raw QR token never stored — hash only (`QrTokenHash`, CTRL-021).

**Claim channels (ACTV-624):** A = KhachLink scan QR `{t,tn}` → tenant from payload · B = printed-ticket `{sc,sid}` → tenant via session lookup (#130-fix2) · C→A = 6-digit short code → tenant via first match (#147). Claim idempotent: only `Issued`, already-claimed → 409 (RULE-027).

**State machine (RULE-028):** `Checkout` from Issued|Claimed · `Flag` from Issued|Claimed (reason required) · `Void` never from CheckedOut.

**Audit (CTRL-022):** every verify writes `GuardScanLog` (Match/Mismatch/ManualOverride/Flagged) — ManualOverride is audit-only mitigation for checkout bypass (RISK-013).

**Exceptions:** feature flag off → 403 · unknown QR → 404 · wrong state → 409 · tenant unresolved from payload/sid/shortcode → 400.
