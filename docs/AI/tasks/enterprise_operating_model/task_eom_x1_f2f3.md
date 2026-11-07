# Task EOM-X1 — Expansion: F2 Metering + F3 Voucher Posting

> **Status:** ✅ DONE 2026-11-07 — F2+F3 modeled (12 ACTV, 6 RULE, 3 CTRL, 2 GAP); validator 0 err
> **Gate:** sau khi ≥1 track (P2a hoặc P2b) qua G2 — SRS v2.1 §3.1
> **Pattern:** chạy lại P2→P3→P4 trong phạm vi slice — KHÔNG viết lại backbone

## Objective

Model AS-IS + traceability + gap cho 2 flow kiểm soát tiền:
- **F2 Metering "phí nền tảng":** metered call → `vanan-metering` quota/deny → wallet/top-up (VietQR) → usage ingest → billing. Gồm caller fail-open vs AI stricter degrade.
- **F3 Voucher posting:** Smart Entry intent → `ResolveByIntent` → `VoucherTypeRegistry` → `IVoucherPostingStrategy` → journal entries (AccountingEntry immutable, reversal-only).

## Scope notes

- F2: wallet semantics append-only, rate cards, deny gate paths, metering→caller auth (`X-Caller-Id`/`X-Metering-Key`/`Idempotency-Key`), HTX/platform billing boundary. Read `.devin/rules/metering.md` trước khi model (SRS-conformant lazy-load).
- F3: voucher type registry entries hiện có, strategy pattern, TT133/TT71/TT58 regime differences (CONFIGURABLE/VARIANT candidates), approval/reversal controls.
- Cả hai là critical theo §6.5 tiêu chí 1,4,5,6 → full swimlane + exceptions + governance.

## Deliverables (pattern §12 — update-in-place)

```
03-processes/ += f2-*, f3-* catalogs + swimlanes + details
04-software-architecture/ += metering service, voucher engine components
05-data-architecture/ += wallet/ledger/voucher data flows + SoT notes
06-governance/ += financial authz + reversal controls
09-gap-analysis/ += gaps/risks found
registries/ += PROC/ACTV/RULE/CTRL/RISK/KPI entries
```

## Acceptance

- [ ] Swimlanes + exceptions cho cả F2, F3
- [ ] Traceability tới code (metering middleware, posting strategies)
- [ ] Gap/risk entries mới; validator green
