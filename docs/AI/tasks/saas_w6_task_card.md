# TASK CARD — SaaS W6: E-Invoice Real Integration Verification

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged
> **Branch:** `feature/saas-w6-einvoice-real-verification`
> **Estimated sessions:** 1-2
> **Sprint:** 2 (Hardening)

## Objective
Verify E-Invoice providers (Viettel + MISA) với real API credentials trong staging environment. Fix Facebook Lead unsafe reflection.

## Prerequisites (verify before code)
- [ ] W0-W2 merged
- [ ] Verify `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` — 149 lines
- [ ] Verify `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` — 100 lines
- [ ] Verify `3_CoreHub/Services/FacebookLeadService.cs:65` — `FormatterServices.GetUninitializedObject`
- [ ] Check if Viettel/MISA test/staging accounts available
- [ ] Verify existing tests: `ViettelEInvoiceProviderTests.cs`, `MisaEInvoiceProviderTests.cs` (mock HTTP)

## Part 1: E-Invoice Provider Verification

### Files to Modify
| File | Changes |
|------|---------|
| `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` | Verify config loading, fix any hardcoded paths |
| `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` | Verify config loading, fix any hardcoded paths |
| `5_WebApps/ShopERP/appsettings.Production.json` | ADD EInvoice config section (env var references) |
| `6_Tests/VanAn.Integration.Tests/` | ADD EInvoiceStagingTests.cs (real API calls, gated by env var) |

### W6-T1: INVESTIGATE provider implementations
- Read `ViettelEInvoiceProvider.cs` — verify HTTP client setup, API endpoints, auth flow
- Read `MisaEInvoiceProvider.cs` — same
- Check config classes: `ViettelConfig`, `MisaConfig` — what fields required?
- Verify error handling: timeout, retry, API error responses

### W6-T2: Configure staging credentials
- Obtain Viettel E-Invoice staging/test account
- Obtain MISA E-Invoice staging/test account
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

### W6-T3: Write staging integration tests
**File:** `6_Tests/VanAn.Integration.Tests/EInvoiceStagingTests.cs`
- Gate tests with env var: `EINVOICE_STAGING_ENABLED=true` (skip if not set)
- Test Viettel:
  1. Authenticate → get token
  2. Create invoice → verify response
  3. Get invoice status → verify
  4. Submit invoice → verify
- Test MISA:
  1. Authenticate → get token
  2. Create invoice → verify
  3. Get invoice → verify
- Tests run locally with staging credentials, NOT in CI (cost)

### W6-T4: Fix provider issues (if found)
- Fix any hardcoded URLs or paths
- Fix any missing error handling
- Fix any config loading issues
- Verify retry logic for transient failures

## Part 2: Facebook Lead Service Fix

### Files to Modify
| File | Changes |
|------|---------|
| `3_CoreHub/Services/FacebookLeadService.cs:65` | Replace `FormatterServices.GetUninitializedObject` with proper factory |
| `1_Shared/Domain.cs` | Verify `Lead` entity has public factory method (if not, add one) |

### W6-T5: Fix Facebook Lead unsafe reflection
**File:** `3_CoreHub/Services/FacebookLeadService.cs:65`
```csharp
// BEFORE (unsafe):
var lead = (Lead)FormatterServices.GetUninitializedObject(typeof(Lead));

// AFTER (proper factory):
var lead = Lead.Create(tenantId, name, phone, email, source: "Facebook");
```
- Verify `Lead` entity has `Create()` factory method
- If not: add factory to Domain (requires Domain Modification approval)
- Update FacebookLeadService to use factory
- Verify existing `FacebookLeadIntegrationTests.cs` still passes

### W6-T6: Build + guard + all tests pass
- Build 0 errors, guard pass, all tests pass
- Staging tests: run locally with `EINVOICE_STAGING_ENABLED=true`

## Verification
- [ ] Viettel E-Invoice: staging test PASS (create + get + submit)
- [ ] MISA E-Invoice: staging test PASS (create + get)
- [ ] `appsettings.Staging.json` has EInvoice config (env var references)
- [ ] `FacebookLeadService.cs` — no `FormatterServices.GetUninitializedObject`
- [ ] `Lead.Create()` factory used instead
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- Git revert (restore old provider code + reflection)
- Staging tests: gated by env var, no impact on CI
- If Lead factory breaks Domain: revert + mark as tech debt

## Open Questions
- Q1: Viettel/MISA staging accounts — available? (Need user to provide credentials)
- Q2: Staging tests — run in CI or local only? (Local only — cost + credentials)
- Q3: Lead.Create() factory — exists in Domain? (INVESTIGATE first)
- Q4: Facebook webhook HMAC validation — TODO in code, fix in this wave or defer?
