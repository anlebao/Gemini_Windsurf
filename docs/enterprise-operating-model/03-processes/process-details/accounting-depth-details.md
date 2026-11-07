# Accounting Depth Details — D3 deep-dive

> Metadata in `registries/activities.yaml` ACTV-629..640 · coverage anchors ACTV-604/605 + F3 vouchers ACTV-307..312 · captured at `58d3d3d6`.

## Verified mechanics

- **Regime resolution** (`AccountingStandardRules` — pure static 1_Shared): `DefaultStandard` maps TenantType→standard (HTX→TT71, Enterprise_Large→TT99, Enterprise_SuperSmall→TT58, else TT133); `AllowedStandards` enforces default-lock + legal election (SuperSmall may pick TT133; HKD sees TT58 only after electing; `Type=null` → all 4, backward-compat). `ResolveEffective` = override-if-allowed → default-if-allowed → allowed[0] → null. `AccountingStandardResolver` (ShopERP) thin-wraps for UI dropdown labels.
- **Write-path:** `TenantManagementService.SetAccountingStandardAsync` validates `AllowedStandards(tenant.Type, s).Contains(s)` — write-path rejects out-of-lock standard.
- **HKD books:** `HKDBookService` exposes S1a/S2a/S2b/S2c/S2d/S2e/S3a generation (industrySector hint for S2a/S2b per TT 152); `HKDBookGenerationService` runs `HKDBookTemplate`s + `GetAvailableTemplatesAsync`/`ValidateTemplateAsync`/`GetGenerationStatusAsync`; exposed via `GET /api/hkd-books` (`RequireTenantAccess`).
- **Trial balance** (VAS W4): per-account opening (EntryDate < periodStart) + movement (in-period) → signed balance; `IsBalanced` = |ΣNợ−ΣCó| < 0.01. Loads entries `< periodEnd` into memory — Pattern #1/#5 fixed (direct `TenantId` compare + `EntryDate` range).
- **GL reports** (P0.2): `GeneralLedgerService` — Nhật ký chung (full-period journal list), Sổ Cái (opening + cumulative movement + closing per account), balances per account — read-only on `JournalEntry` (double-entry, BCTC regimes).
- **BCTC:** `BalanceSheetService` (B01) + `IncomeStatementService` (B02) + `CashFlowStatementService` (B03) + `FinancialStatementNotesService` (B09 — TT99 Phụ lục IV template text, B09-HTX variant for TT71).
- **Close:** `ValidatePeriodAsync` → errors/warnings (entries-exist, rev/exp ratio warn, pending `Amount=0` counted, reconcile R-checks merged: Error→error, Warning→warning, crash→warning); `GenerateClosingEntriesAsync` → 911 legs per regime, idempotent via JE purpose+period; `ClosePeriodAsync` → persisted `PeriodClosingStatusEntity.MarkClosed(user)` + audit.
- **Reopen:** `PreviewReopenAsync` (count+sum of closing JEs) → `ReopenPeriodAsync` — approver-roles gate, mandatory reason, `MarkReopening` → reverse each closing JE → `MarkReopened` → `LogPeriodReopenAsync`.
- **Reconcile:** `AccountingReconcileService.RunAsync` — R1–R13, `RunSafeAsync` isolates each check; `IMirrorOrderProvider`/`IInventoryValuationProvider` HTTP to ShopERP (`/api/shoperp/accounting/mirror-orders`, `/api/shoperp/inventory/valuation`), fail-open; `BridgeSingleSourceSince = 2026-10-29` cutoff for R8a; SystemAdmin endpoint + audit log per run (no result persistence — G12).
- **Audit:** `AuditTrailService` — `LogPeriodCloseAsync` / `LogPeriodReopenAsync` / reconcile summary.

## Execution split

MANUAL: ACTV-630/637/638/639 (4 — human decisions/approvals) · AUTOMATED: ACTV-629/631/632/633/634/635/636/640 (8) · HYBRID: none.

## INFERRED items

- B01/B02/B03 service internals (account mapping per regime) — services verified present; formulas not deep-traced this pass.
- `GetGenerationStatusAsync` semantics (HKD book progress tracking) — signature verified, impl not traced.
- Cash-flow method (direct vs indirect per regime) — not traced.

## Draft gaps → gap register candidates

| Candidate | Class | Status |
|---|---|---|
| Legacy `Type=null` tenants can emit BCTC under any of 4 regimes | COMPLIANCE | → RISK-014 registered |
| R11/R13 fail-open — cross-store/inventory drift passes unnoticed | OPERATIONAL | → RISK-016 registered |
| TrialBalanceService loads all entries < periodEnd into memory (grows with history) | OPERATIONAL | draft — perf scale, unquantified |
| Reconcile results not persisted (audit-only per G12) — no trend/history | OPERATIONAL | draft — design decision G12, revisit if ops need trend |
