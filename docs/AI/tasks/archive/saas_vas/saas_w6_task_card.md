# TASK CARD — SaaS W6: E-Invoice Provider Rewrite + Real Integration Verification

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged (W4+W5 merge pending)
> **Branch:** `feature/saas-w6-einvoice-real-verification` (single branch, replaces Stream A's 4-branch strategy)
> **Estimated sessions:** 4-6 + 1-2 tuần chờ credentials (parallel)
> **Sprint:** 2 (Hardening)
> **Last Updated:** 2026-07-05 (Amended — gộp Stream A `einvoice_provider_rewrite_master_plan.md` vào W6)

## Amendment History
- **v1 (2026-07-02):** Original — giả định provider code đúng, chỉ cần "verify + fix hardcoded paths".
- **v2 (2026-07-05):** Amend — INVESTIGATE session chứng minh provider code **100% STUB** (20 Viettel gaps + 10 MISA gaps, verified against actual code). Gộp Stream A (4 waves → 8 tasks, 4 branches → 1 branch). Compress granularity, giữ nguyên scope. Thêm W6-T7 (Facebook Lead) giữ nguyên từ v1.

## Objective
Rewrite E-Invoice providers (Viettel + MISA) per real API spec, verify với staging credentials, fix Facebook Lead unsafe reflection. Provider code hiện tại là STUB — sẽ fail 100% trên real API call (verified 2026-07-05).

## Prerequisites (verify before code)
- [ ] W0-W5 merged (W4+W5 pending merge)
- [x] **W6-T1 INVESTIGATE COMPLETE (2026-07-05)** — verified 20 Viettel + 10 MISA gaps against actual code:
  - `ViettelEInvoiceProvider.cs` (193 LOC): auth sai endpoint (`auth/token` vs `/auth/login`), Bearer vs Cookie, create sai endpoint (`services/createInvoice` vs `InvoiceWS/createInvoice/{taxCode}`), payload flat vs nested, missing `transactionUuid`, missing `itemInfo[]`, status sai method (GET vs POST `searchInvoiceByTransactionUuid`), cancel thiếu 5/7 fields, missing `GetInvoiceFileAsync`, sandbox URL sai (`sinvoice` vs `vinvoice`)
  - `MisaEInvoiceProvider.cs` (202 LOC): auth sai endpoint (`auth/login` vs `/api/integration/auth/token`), thiếu `appid`, auth response sai structure, create sai endpoint (`einvoices` vs `/api/integration/invoice`), missing `SignType`, missing `OriginalInvoiceDetail[]`/`TaxRateInfo[]`, status/cancel fabricated
  - `EInvoiceRequest` (IEInvoiceProvider.cs line 20-33): thiếu LineItems, SupplierTaxCode, CurrencyCode, PaymentType, transactionUuid
  - `IEInvoiceProvider` interface: thiếu `GetInvoiceFileAsync` method
  - `EInvoiceRequest` construct tại `3_CoreHub/Program.cs` line 206 (trong RetryPolicyService factory)
  - `InvoiceItem` entity confirmed tại `1_Shared/Domain.cs` line 1802-1872 — đủ field, **KHÔNG cần sửa Domain**
  - `EInvoiceOrchestrator.cs` (113 LOC) — clean coordination only, **giữ nguyên**
  - Tests hiện tại (Viettel 201 LOC, MISA 205 LOC) mock theo endpoint SAI — pass white, cần rewrite
- [ ] Viettel/MISA staging accounts (W6-T2, parallel bottleneck)

## Hard Rules (from Stream A, preserved)
- **Domain layer KHÔNG được sửa** — `InvoiceItem` entity đã tồn tại trong `1_Shared/Domain.cs` (line 1802)
- **Sử dụng `InvoiceItem` entity sẵn có** — có đầy đủ ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount
- **Mock theo API spec thật, KHÔNG theo implementation** — unit tests phải phản ánh endpoint/payload thật
- **Provider implementations MUST be stateless** (theo `IEInvoiceProvider` contract hiện có)
- **Orchestrator layer giữ nguyên** — design đã đúng (outbox, state machine, idempotency, retry/fallback)
- **TDD: tests + implementation trong cùng task** (không tách rời để tránh build-break)
- **Playwright DISABLED cho đến khi build pass + implementation complete**

## Regulatory Context
- **Viettel S-Invoice v2.0** (`vinvoice.viettel.vn`) — API docs v2.5 (06/2022)
- **MISA meInvoice** (`testapi.meinvoice.vn` / `api.meinvoice.vn`) — integration API
- **TT 152/2025/TT-BTC** — HĐĐT mandatory cho HKD
- **Nghị định 123/2020/NĐ-CP** — OUT OF SCOPE (HKD không máy tính tiền)
- **CTS type: HSM only** (USB Token không khả thi cho SaaS multi-tenant)

## Files to Modify (consolidated from Stream A Wave 1-4)
| File | Changes | Task |
|------|---------|------|
| `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs` | +LineItems, +SupplierTaxCode, +CurrencyCode, +PaymentType, +transactionUuid in `EInvoiceRequest`; +TransactionUuid, +ReservationCode in `EInvoiceResponse`; +`GetInvoiceFileAsync` method | W6-T3 |
| `3_CoreHub/Program.cs` (line 206) | Update `EInvoiceRequest` construction với 4 new fields | W6-T3 |
| `3_CoreHub/Services/Providers/EInvoice/ViettelDTOs.cs` | REWRITE — `ViettelConfig` (+ProductionBaseUrl), `ViettelAuthRequest`/`Response`, nested payload DTOs (`generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo[]`, `summarizeInfo`, `taxBreakdowns[]`), `ViettelInvoiceResult` (result.{supplierTaxCode, invoiceNo, transactionID, reservationCode}) | W6-T4 |
| `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` | REWRITE — auth via Cookie, `InvoiceWS/createInvoice/{supplierTaxCode}`, nested payload, epoch ms date, `searchInvoiceByTransactionUuid`, `cancelTransactionInvoice` (7 fields), `getInvoiceRepresentationFile`, timeout 90s | W6-T4 |
| `3_CoreHub/Services/Providers/EInvoice/MisaDTOs.cs` | REWRITE — `MisaConfig`, `MisaAuthRequest` (+appid), `MisaAuthResponse` ({Success, Data, ErrorCode}), `MisaInvoicePayload` (+SignType, OriginalInvoiceDetail[], TaxRateInfo[]), token expiry 15 ngày | W6-T5 |
| `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` | REWRITE — `/api/integration/auth/token`, `/api/integration/invoice`, SignType, line items, real status/cancel endpoints | W6-T5 |
| `5_WebApps/ShopERP/appsettings.Production.json` | ADD EInvoice config section (env var references) | W6-T2 |
| `5_WebApps/ShopERP/appsettings.Staging.json` | ADD EInvoice staging config (env var references) | W6-T2 |
| `6_Tests/VanAn.Core.Tests/Services/ViettelEInvoiceProviderTests.cs` | REWRITE — mocks per real API spec (auth, create, status, cancel, getfile) | W6-T4 |
| `6_Tests/VanAn.Core.Tests/Services/MisaEInvoiceProviderTests.cs` | REWRITE — mocks per real API spec | W6-T5 |
| `6_Tests/VanAn.Integration.Tests/EInvoiceStagingTests.cs` | NEW — staging tests gated by `EINVOICE_STAGING_ENABLED` env var | W6-T6 |
| `3_CoreHub/Services/FacebookLeadService.cs:65` | Replace `FormatterServices.GetUninitializedObject` with `Lead.Create()` factory | W6-T7 |

## Tasks (8 tasks — gộp Stream A 4 waves → 8 tasks, 4 branches → 1 branch)

### W6-T1: INVESTIGATE providers — verify gaps ✅ COMPLETE (2026-07-05)
- Read `ViettelEInvoiceProvider.cs` (193 LOC) — verify 20 gaps
- Read `MisaEInvoiceProvider.cs` (202 LOC) — verify 10 gaps
- Read `IEInvoiceProvider.cs` — verify missing fields/methods
- Read `EInvoiceOrchestrator.cs` (113 LOC) — confirm clean, giữ nguyên
- Read `InvoiceItem` entity (Domain.cs line 1802-1872) — confirm đủ field, không sửa Domain
- Verify `EInvoiceRequest` construction site (Program.cs line 206)
- **Result:** All Stream A claims verified. Providers 100% STUB. Rewrite required.

### W6-T2: Configure staging credentials (parallel, non-code, 1-2 tuần bottleneck)
**Owner:** User (email Viettel/MISA) + Dev (config files)
- Email Viettel Solution (`lienhe@viettelsolution.com.vn` / hotline `1900.8119`) xin sandbox vinvoice 2.0
- Email MISA partnership request xin `appid` + sandbox `testapi.meinvoice.vn`
- Register IP server vào Viettel whitelist (coordinate với ops)
- Xin mẫu hóa đơn + ký hiệu trên sandbox (CQT sandbox cấp)
- Contact MISA support confirm status/cancel endpoints (prerequisite cho W6-T5)
- Add config to `appsettings.Staging.json` (NOT Production):
```json
{
  "EInvoice": {
    "Viettel": {
      "BaseUrl": "${VIETTEL_EINVOICE_STAGING_URL}",
      "Username": "${VIETTEL_EINVOICE_STAGING_USER}",
      "Password": "${VIETTEL_EINVOICE_STAGING_PASS}",
      "TaxCode": "${VIETTEL_EINVOICE_TAX_CODE}"
    },
    "Misa": {
      "BaseUrl": "${MISA_EINVOICE_STAGING_URL}",
      "ApiKey": "${MISA_EINVOICE_STAGING_KEY}",
      "TaxCode": "${MISA_EINVOICE_TAX_CODE}"
    }
  }
}
```
- Add config to `appsettings.Production.json` (env var references, same structure)
- **KHÔNG commit credentials vào repo** — use user-secrets hoặc env vars
- Update `project_state.md` Maintenance Log khi nhận được credentials
- **Parallel:** Run alongside W6-T3/T4/T5. Blocker cho W6-T6 only.

### W6-T3: Update EInvoiceRequest contract + interface (Stream A Wave 1 compressed)
**Estimated:** 1 session
- Add line item type for provider requests — **reuse `InvoiceItem` from Domain** (line 1802); fallback: `InvoiceLineItemDto` record if mapping needed
- Update `EInvoiceRequest` record: +`SupplierTaxCode`, +`LineItems` (IReadOnlyList<InvoiceItem>), +`CurrencyCode`, +`PaymentType`
- Add `transactionUuid` mapping (derive from `InvoiceId` — `InvoiceId.Value.ToString("N")`)
- Update `EInvoiceResponse`: +`TransactionUuid`, +`ReservationCode` fields
- Add `GetInvoiceFileAsync` method to `IEInvoiceProvider` interface (returns byte[] PDF/XML)
- Research + confirm `SupplierTaxCode` source (likely `ProviderConfiguration.ConfigurationData` JSON — need deserialize helper)
- Update `EInvoiceRequest` construction in `Program.cs` line 206 với 4 new fields (LineItems từ `invoice.Items`, SupplierTaxCode từ provider config, CurrencyCode="VND", PaymentType từ AdditionalData)
- Update existing tests (`ViettelEInvoiceProviderTests`, `MisaEInvoiceProviderTests`, `EInvoiceProviderTests`) to use new `EInvoiceRequest` signature
- Verify build passes + all tests pass

**Exit criteria:**
- [ ] `EInvoiceRequest` có `SupplierTaxCode`, `LineItems`, `CurrencyCode`, `PaymentType`
- [ ] `EInvoiceResponse` có `TransactionUuid`, `ReservationCode`
- [ ] `IEInvoiceProvider` có `GetInvoiceFileAsync` method
- [ ] `Program.cs` line 206 updated với new fields
- [ ] `SupplierTaxCode` source confirmed + documented
- [ ] Build: 0 errors
- [ ] All existing tests updated and pass

### W6-T4: Rewrite Viettel provider + DTOs + tests (Stream A Wave 2 compressed)
**Estimated:** 1-2 sessions
- Rewrite `ViettelConfig`: add `SandboxBaseUrl` default `vinvoice.viettel.vn`, add `ProductionBaseUrl`
- Rewrite `ViettelAuthRequest`/`ViettelAuthResponse` per real spec
- Create nested payload DTOs: `ViettelInvoicePayload` with `generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo[]`, `summarizeInfo`, `taxBreakdowns[]`
- Create `ViettelInvoiceResult` with `result.{supplierTaxCode, invoiceNo, transactionID, reservationCode}`
- Rewrite `ViettelEInvoiceProvider.SubmitInvoiceAsync`: auth via Cookie (NOT Bearer), endpoint `InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}`, nested payload, epoch ms date
- Rewrite `GetInvoiceStatusAsync`: use `searchInvoiceByTransactionUuid` (POST, form-urlencoded)
- Rewrite `CancelInvoiceAsync`: use `cancelTransactionInvoice` (POST, form-urlencoded, 7 required fields)
- Implement `GetInvoiceFileAsync`: use `getInvoiceRepresentationFile` (POST, JSON)
- Update `Capabilities`: timeout 90s (Viettel recommended 60-90s)
- Register named HttpClient "viettel" with correct BaseAddress (`vinvoice.viettel.vn`) in DI (Program.cs line 156)
- Rewrite `ViettelEInvoiceProviderTests`: all mocks per real API spec (auth, create, status, cancel, getfile)
- Add tests: verify Cookie auth, nested payload, transactionUuid, line items, epoch date
- Run full test suite, verify 0 failures

**Exit criteria:**
- [ ] Auth: `POST /auth/login` → Cookie header `access_token=...`
- [ ] Create: `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}` with nested payload
- [ ] Status: `POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid`
- [ ] Cancel: `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice` with 7 fields
- [ ] GetFile: `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile`
- [ ] All mocks reflect real Viettel API spec
- [ ] Tests verify Cookie auth, nested payload, transactionUuid, line items, epoch date
- [ ] Build: 0 errors
- [ ] All tests pass (TDD — impl + tests cùng task)
- [ ] No regression in orchestrator layer

### W6-T5: Rewrite MISA provider + DTOs + tests (Stream A Wave 3 compressed)
**Estimated:** 1-2 sessions
**Prerequisite:** W6-T2 task 5 (MISA status/cancel endpoints confirmed)
- Rewrite `MisaConfig`: add `AppId`, `ProductionBaseUrl`, `SandboxBaseUrl` (`testapi.meinvoice.vn`)
- Rewrite `MisaAuthRequest`: +`appid` field
- Rewrite `MisaAuthResponse`: `{Success, Data, ErrorCode}` structure (NOT flat `access_token`)
- Rewrite `MisaInvoicePayload`: +`SignType` (2 sync hoặc 3 async), +`OriginalInvoiceDetail[]`, +`TaxRateInfo[]`
- Token expiry: 15 ngày (NOT 55 phút)
- Rewrite `MisaEInvoiceProvider.SubmitInvoiceAsync`: endpoint `/api/integration/invoice`, SignType, line items
- Rewrite `GetInvoiceStatusAsync`: use real MISA status endpoint (confirm via W6-T2 task 5)
- Rewrite `CancelInvoiceAsync`: use real MISA cancel endpoint (confirm via W6-T2 task 5)
- Implement `GetInvoiceFileAsync` if MISA supports it
- Register named HttpClient "misa" with correct BaseAddress in DI (Program.cs line 165)
- Rewrite `MisaEInvoiceProviderTests`: all mocks per real MISA API spec
- Run full test suite, verify 0 failures

**Exit criteria:**
- [ ] Auth: `POST /api/integration/auth/token` with `appid`
- [ ] Create: `POST /api/integration/invoice` with SignType + line items
- [ ] Status/Cancel: real MISA endpoints (confirmed via W6-T2 task 5)
- [ ] Token expiry: 15 ngày
- [ ] All mocks reflect real MISA API spec
- [ ] Build: 0 errors
- [ ] All tests pass

### W6-T6: Staging integration tests (Stream A Wave 4 + W6 v1 T3 merged)
**Estimated:** 0.5-1 session
**Prerequisite:** W6-T2 complete (credentials received) + W6-T4/T5 complete (providers rewritten)
**File:** `6_Tests/VanAn.Integration.Tests/EInvoiceStagingTests.cs`
- Gate tests with env var: `EINVOICE_STAGING_ENABLED=true` (skip if not set)
- Test Viettel:
  1. Authenticate → get Cookie token
  2. Create invoice → verify response (invoiceNo, transactionID, reservationCode)
  3. Get invoice status via transactionUuid → verify
  4. Cancel invoice → verify
  5. Get invoice file (PDF/XML) → verify non-empty
- Test MISA:
  1. Authenticate with appid → get token
  2. Create invoice with SignType + line items → verify
  3. Get invoice status → verify
  4. Cancel invoice → verify (if endpoint confirmed)
- Tests run locally with staging credentials, NOT in CI (cost + credentials)
- Fix any provider issues found during staging tests (hardcoded URLs, error handling, retry logic)

**Exit criteria:**
- [ ] Viettel E-Invoice: staging test PASS (create + status + cancel + getfile)
- [ ] MISA E-Invoice: staging test PASS (create + status + cancel)
- [ ] `appsettings.Staging.json` has EInvoice config (env var references)
- [ ] Tests gated by `EINVOICE_STAGING_ENABLED` — no CI impact
- [ ] CI exclusion documented

### W6-T7: Fix Facebook Lead unsafe reflection (W6 v1 Part 2 preserved)
**Estimated:** 0.5 session
**File:** `3_CoreHub/Services/FacebookLeadService.cs:65`
```csharp
// BEFORE (unsafe):
var lead = (Lead)FormatterServices.GetUninitializedObject(typeof(Lead));

// AFTER (proper factory):
var lead = Lead.Create(tenantId, name, phone, email, source: "Facebook");
```
- INVESTIGATE: Verify `Lead` entity has `Create()` factory method (read `1_Shared/Domain.cs`)
- If `Lead.Create()` exists → use it directly
- If NOT exists → **Domain Modification approval required** (governance Hard Stop). Add factory to Domain with Tech Lead approval.
- Update `FacebookLeadService` to use factory
- Verify existing `FacebookLeadIntegrationTests.cs` still passes

**Exit criteria:**
- [ ] `FacebookLeadService.cs` — no `FormatterServices.GetUninitializedObject`
- [ ] `Lead.Create()` factory used instead (or Domain mod approved + added)
- [ ] Existing `FacebookLeadIntegrationTests.cs` passes

### W6-T8: Build + guard + all tests pass
- Build 0 errors, guard pass, all tests pass
- Staging tests: run locally with `EINVOICE_STAGING_ENABLED=true` (W6-T6)
- Commit với message format `[SAAS W6] E-Invoice Provider Rewrite + Real Integration Verification`

## Verification (consolidated)
- [ ] `EInvoiceRequest` có `SupplierTaxCode`, `LineItems`, `CurrencyCode`, `PaymentType`, `transactionUuid` (W6-T3)
- [ ] `EInvoiceResponse` có `TransactionUuid`, `ReservationCode` (W6-T3)
- [ ] `IEInvoiceProvider` có `GetInvoiceFileAsync` method (W6-T3)
- [ ] Viettel provider: Cookie auth, nested payload, `InvoiceWS/createInvoice/{taxCode}`, `searchInvoiceByTransactionUuid`, `cancelTransactionInvoice`, `getInvoiceRepresentationFile` (W6-T4)
- [ ] MISA provider: `/api/integration/auth/token` + appid, `/api/integration/invoice` + SignType, line items (W6-T5)
- [ ] Sandbox URL: `vinvoice.viettel.vn` (NOT `sinvoice`) (W6-T4)
- [ ] Token expiry MISA: 15 ngày (NOT 55 phút) (W6-T5)
- [ ] All mocks reflect real API spec (W6-T4/T5)
- [ ] Staging tests gated by env var, no CI impact (W6-T6)
- [ ] `appsettings.Staging.json` + `appsettings.Production.json` have EInvoice config (W6-T2)
- [ ] `FacebookLeadService.cs` — no `FormatterServices.GetUninitializedObject` (W6-T7)
- [ ] `Lead.Create()` factory used (W6-T7)
- [ ] Build 0 errors, guard pass, all tests pass (W6-T8)
- [ ] Domain KHÔNG sửa (hard rule — `InvoiceItem` reused, `Lead.Create()` only if approved)
- [ ] Orchestrator KHÔNG sửa (hard rule — clean coordination only)

## Rollback
- Git revert (restore old provider code + reflection)
- Staging tests: gated by env var, no impact on CI
- If `Lead.Create()` factory breaks Domain: revert + mark as tech debt
- If W6-T4/T5 rewrite introduces regression: revert to W6-T3 state (contract updated, providers old) — but old providers will fail staging, so this is debt-only

## Open Questions
- Q1: Viettel/MISA staging accounts — available? (W6-T2, user-side, 1-2 tuần)
- Q2: Staging tests — run in CI or local only? (Local only — cost + credentials)
- Q3: `Lead.Create()` factory — exists in Domain? (INVESTIGATE in W6-T7)
- Q4: Facebook webhook HMAC validation — TODO in code, fix in this wave or defer?
- Q5: MISA status/cancel endpoints — confirm via MISA support (W6-T2 task 5, blocker cho W6-T5)
- Q6: `SupplierTaxCode` source — `ProviderConfiguration.ConfigurationData` JSON or per-tenant config? (W6-T3 research)

## Effort Summary
| Task | Description | Sessions | Bottleneck |
|---|---|---|---|
| W6-T1 | INVESTIGATE providers | 0.5 ✅ | None |
| W6-T2 | Configure staging credentials (parallel) | 0 (user-side) | 1-2 tuần chờ Viettel/MISA |
| W6-T3 | Update EInvoiceRequest contract + interface | 1 | None |
| W6-T4 | Rewrite Viettel provider + DTOs + tests | 1-2 | None |
| W6-T5 | Rewrite MISA provider + DTOs + tests | 1-2 | MISA status/cancel endpoints (W6-T2 task 5) |
| W6-T6 | Staging integration tests | 0.5-1 | W6-T2 credentials |
| W6-T7 | Fix Facebook Lead unsafe reflection | 0.5 | None |
| W6-T8 | Build + guard + all tests pass | 0.5 | None |
| **Total** | | **4-6 + 1-2 tuần chờ** | |

**Critical path:** W6-T1 ✅ → W6-T3 → W6-T4 → W6-T5 → W6-T6 (needs W6-T2) → W6-T8
**Parallel path:** W6-T2 (email Viettel + MISA) bắt đầu ngay, song song với W6-T3/T4/T5
**Independent:** W6-T7 (Facebook Lead) — chạy song song với bất kỳ task nào

## Source Plans (superseded by this card)
- `einvoice_provider_rewrite_master_plan.md` (Stream A, 4 waves) → **superseded** — gộp vào W6-T3/T4/T5/T6
- `wave1_einvoice_request_contract_task_card.md` → **superseded** — gộp vào W6-T3
- `wave2_einvoice_viettel_provider_task_card.md` → **superseded** — gộp vào W6-T4
- `wave3_einvoice_misa_provider_task_card.md` → **superseded** — gộp vào W6-T5
- `wave4_einvoice_sandbox_verify_task_card.md` → **superseded** — gộp vào W6-T6
- W6 v1 (2026-07-02) Part 1 → **superseded** — replace với W6-T3/T4/T5/T6
- W6 v1 Part 2 (Facebook Lead) → **preserved** — W6-T7
