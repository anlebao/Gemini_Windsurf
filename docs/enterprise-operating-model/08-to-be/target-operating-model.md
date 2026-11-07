# TO-BE Operating Model — **PROPOSED** (awaits G4 owner sign-off)

> SRS §9.2, AC-08: PROPOSED ≠ approved ≠ implemented. Covers F4+F1 + platform-wide items.

## F4 — Onboarding & HTX provisioning (replication spine)

| Aspect | AS-IS | TO-BE (PROPOSED) |
|---|---|---|
| Sales acquisition | Manual, no tooling | Human-led + CRM lead tracking (GAP-001) |
| Application review | Manual ops decision | Keep MANUAL + document checklist + SLA metric (KPI-003) |
| KYC | L1 auto → L4 manual | Keep levels; L4 gets checklist tooling; retention policy (CTRL-010) |
| Provisioning | Automated pipeline | Keep + add routing/dead-letter monitor (CTRL-009) |
| HTX members | Manual registration (GAP-003) | Member portal + auto type classification — **highest-value automation for 3,000-HTX goal** |
| Variants | Config-driven | Formalize: onboarding CORE identical per HTX; activation flags per business type |

## F1 — Order lifecycle

| Aspect | AS-IS | TO-BE (PROPOSED) |
|---|---|---|
| Order spine | Routed NATS, SoT clear | Retain — proven |
| Failure visibility | Silent routing miss | CTRL-009 monitor + replay |
| Sync integrity | Eventual, unmeasured | Drift metric (KPI-005) + alert |
| Cancel path | Exists, audit unclear | Verify reason capture; add audit if missing (GAP-008) |

## Platform-wide (PROPOSED)

| Item | Rationale |
|---|---|
| Metering client consolidation decision | dedup ×4 or accept+document (GAP-007) |
| AI degrade observability | MeterOnly signal → metric/alert (GAP-011) |
| Endpoint naming cleanup | `/api/catalog` vs `/api/products` recommended (GAP-009) |
| `vanan.shoperp.*` subject catalog | close GAP-010 |

## Activities retained as manual (deliberate)

Application review · KYC L4 · order confirm · kitchen prep · sales visits — judgment/relationship work. Automation supports, does not replace.
