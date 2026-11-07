# Process Catalog — L0 → L2

> Data source: `registries/processes.yaml`. L3–L4 detail arrives with P2a/P2b tracks.
> Variability per SRS §6.6: CORE / CONFIGURABLE / VARIANT (VARIANT must ref CORE parent).

```
PROC-001  L0  Van An Ecosystem (CORE)
├─ PROC-010  L1  Membership & Onboarding (CORE)
│   ├─ PROC-011  L2  Merchant onboarding & tenant provisioning (CORE)        [F4]
│   ├─ PROC-012  L2  HTX formation & member verification (CONFIGURABLE)      [F4]
│   └─ PROC-013  L2  Identity verification KYC L1–L4 (CONFIGURABLE)          [F4]
├─ PROC-020  L1  Commerce & Orders (CORE)
│   ├─ PROC-021  L2  Order lifecycle — Option C (CORE)                       [F1]
│   ├─ PROC-022  L2  Catalog browse & discovery (CONFIGURABLE)               [F1]
│   ├─ PROC-023  L2  Booking & appointments (CORE→variants at L3)            [F7+]
│   └─ PROC-024  L2  Delivery & fulfillment (CONFIGURABLE, INFERRED)         [F7+]
├─ PROC-030  L1  Finance & Accounting (CORE)
│   ├─ PROC-031  L2  Voucher posting — registry+strategy (CORE)              [F3]
│   ├─ PROC-032  L2  Period closing & reporting (CONFIGURABLE)               [F3]
│   └─ PROC-033  L2  Metering quota/wallet/top-up (CORE)                     [F2]
├─ PROC-040  L1  Customer Engagement (CORE)
│   ├─ PROC-041  L2  Loyalty accrual & redemption (CONFIGURABLE)             [F6]
│   └─ PROC-042  L2  Notifications (CORE)                                    [F7+]
├─ PROC-050  L1  AI & Intelligence (CORE)
│   ├─ PROC-051  L2  AI advisory chat — tenant-scoped (CONFIGURABLE)         [F5]
│   └─ PROC-052  L2  AI knowledge lifecycle ingest→approve→retrieve (CORE)   [F5]
└─ PROC-060  L1  Platform Operations & Governance (CORE)
    ├─ PROC-061  L2  Tenant & order data sync (CORE)
    └─ PROC-062  L2  AuthN/AuthZ & tenant isolation (CORE)
```

## L3 workflows added at P2 (F4 + F1)

```
PROC-011 → PROC-101 application & approval · PROC-102 provisioning & ShopInstance ·
PROC-012 → PROC-103 HTX activation · PROC-013 → PROC-104 KYC escalation
PROC-021 → PROC-111 order creation/checkout · PROC-112 routing & delivery · PROC-113 status lifecycle & sync-back
```

## Notes

- **VARIANT examples land at L3–L4** (P2): e.g., F&B kitchen flow vs spa booking flow under PROC-023 — each VARIANT documents only deltas from its CORE parent.
- CONFIGURABLE = behavior varies via `TenantFeatureSettings`/tenant config (real mechanism in code).
- Coverage flags: PROC-024, PROC-062 marked evidence-poor (`none:…`) — deepen at expansion, not guesses.
- F4/F1 tracks decompose their L2 rows into L3 workflows + L4 activities in P2a/P2b.
