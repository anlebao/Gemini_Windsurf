# TASK CARD: EInvoice Provider Rewrite - Wave 4 - Sandbox Runtime Verification

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify cả 2 provider (Viettel + MISA) hoạt động đúng với sandbox credentials thật
- **Nghiệp vụ áp dụng:** End-to-end validation trước production deployment
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/einvoice-rewrite-wave4-sandbox-verify`
- **Estimated Sessions:** 1-2 (plus 1-2 tuần chờ credentials)

> **NOTE:** Wave 4 (cũ Wave 5) đã được ĐẢO VỊ TRÍ xuống cuối.
> Lý do đảo: Chờ credentials là bottleneck dài nhất — làm song song với code rewrite (Wave 0 + Wave 1-3).
> Verify 1 lần cho cả 2 provider — tiết kiệm thời gian.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 4 of 4 (FINAL)
- **Dependency:** Wave 3 must be merged (both providers rewritten + unit tests pass)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave2_einvoice_viettel_provider_task_card.md` (READ — Viettel API spec)
- `docs/AI/tasks/wave3_einvoice_misa_provider_task_card.md` (READ — MISA API spec)
- `6_Tests/VanAn.Integration.Tests/Services/EInvoiceSandboxTests.cs` (CREATE — sandbox integration tests)
- `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` (READ — verify implementation)
- `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` (READ — verify implementation)
- `docs/AI/project_state.md` (UPDATE — verification results)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa provider implementations (Wave 2 & 3 outputs)
- KHÔNG sửa Domain.cs
- KHÔNG commit sandbox credentials vào repo (use user-secrets or env vars)
- Sandbox tests MUST be marked `[Trait("Category", "Sandbox")]` — skip in CI by default
- KHÔNG chạy sandbox tests nếu chưa có credentials (graceful skip)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Credential Security:** Sandbox credentials in `appsettings.Development.json` (gitignored) or user-secrets — KHÔNG commit
- [ ] **Test Isolation:** Sandbox tests skipped in CI (require explicit `-filter Category=Sandbox`)
- [ ] **IP Whitelist:** Viettel requires registered IP — coordinate with ops team
- [ ] **Rate Limit:** Respect sandbox rate limits (don't spam API)
- [ ] **Cleanup:** Cancel test invoices after verification (don't pollute sandbox)
- [ ] **Idempotency:** Verify same `transactionUuid` returns same invoice (critical legal compliance)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Viettel sandbox account received (vinvoice.viettel.vn)
- [ ] **SC2:** MISA sandbox account + appid received (testapi.meinvoice.vn)
- [ ] **SC3:** IP registered in Viettel whitelist
- [ ] **SC4:** Viettel auth: `POST /auth/login` → receive `access_token` via Cookie
- [ ] **SC5:** Viettel create invoice (HSM): full payload → receive `invoiceNo` + `transactionID` + `reservationCode`
- [ ] **SC6:** Viettel idempotency: resend same `transactionUuid` → same invoice returned (no duplicate)
- [ ] **SC7:** Viettel search by transactionUuid: `POST searchInvoiceByTransactionUuid` → status returned
- [ ] **SC8:** Viettel get invoice file: `POST getInvoiceRepresentationFile` → PDF/XML downloaded
- [ ] **SC9:** Viettel cancel invoice: `POST cancelTransactionInvoice` with 7 fields → success
- [ ] **SC10:** Viettel error handling: test real errorCodes (invalid template, wrong tax code)
- [ ] **SC11:** MISA auth: `POST /api/integration/auth/token` with appid → Bearer token
- [ ] **SC12:** MISA create invoice (SignType=2): full payload → invoice created
- [ ] **SC13:** Sandbox test results documented
- [ ] **SC14:** `project_state.md` updated with verification status
- [ ] **SC15:** Ready for production deployment planning

---

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration` — Sandbox verification per real API
- `test-system-upgrade` — Integration test design
- `domain-integrity-validation` — Verify end-to-end data flow

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: Viettel demo account exists: `0100109106-509` / `123456a@A` (public demo, may be limited)
  - Fact 2: MISA test URL is `testapi.meinvoice.vn` (requires `appid` from MISA partnership)
  - Fact 3: Viettel requires IP whitelist — must register server IP
  - Fact 4: Viettel createInvoice is async — file available 2-5s after publish
  - Fact 5: Viettel `transactionUuid` validity is 3 days
- **Assumptions:**
  - Viettel demo account has HSM signing enabled (not USB Token)
  - MISA will provide `appid` upon partnership request
  - Sandbox environments are stable and available during testing window
- **Open Questions:**
  - Q1: Does Viettel demo account `0100109106-509` support HSM createInvoice? (Need to verify)
  - Q2: How long does MISA take to provide `appid`? (Email response time unknown)
  - Q3: Can we register dynamic IP for sandbox, or must it be static? (Need to ask Viettel)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `EInvoiceSandboxTests.cs` (CREATE) | None — new test file | Mark as `[Trait("Category", "Sandbox")]` — skip in CI |
| `appsettings.Development.json` (gitignored) | None — local config only | Verify in `.gitignore` |
| `docs/AI/project_state.md` (UPDATE) | None — documentation | Follow update-state skill |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Already complete (Wave 2 + Wave 3)
- **Integration tests (Sandbox):** NEW — `EInvoiceSandboxTests.cs`
  - Marked `[Trait("Category", "Sandbox")]`
  - Skip if credentials not available (graceful)
  - Run with explicit filter: `dotnet test --filter Category=Sandbox`
- **E2E tests:** KHÔNG — EInvoice là backend flow

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Verify credentials received<br>- Chốt test case list<br>- Chốt credential storage strategy | - Create `EInvoiceSandboxTests.cs`<br>- Configure sandbox credentials (user-secrets)<br>- Run Viettel auth + create tests |
| **S2** | - Review Viettel results<br>- Chốt MISA test approach | - Run Viettel status + cancel + getfile tests<br>- Run MISA auth + create tests<br>- Document results<br>- Update `project_state.md` |

---

## 11. DETAILED VERIFICATION CHECKLIST

### 11.1 Prerequisites (External — Wave 0, start immediately, parallel with Wave 1-3)
- [ ] Email Viettel Solution: `lienhe@viettelsolution.com.vn` or hotline `1900.8119`
  - Request: Sandbox account vinvoice 2.0 for integration testing
  - Provide: Company info, intended use case (HKD F&B SaaS)
- [ ] Email MISA: partnership request for `appid` + sandbox account
  - Request: `testapi.meinvoice.vn` access + `appid`
  - Provide: Company info, integration purpose
- [ ] Register IP server into Viettel whitelist (coordinate with ops)
- [ ] Obtain invoice template + series on sandbox (CQT sandbox approval)

### 11.2 Viettel Verification (9 steps)
1. [ ] **Auth:** `POST /auth/login` → verify `access_token` received
2. [ ] **Create (HSM):** `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}` → verify `invoiceNo` + `transactionID` + `reservationCode`
3. [ ] **Idempotency:** Resend same `transactionUuid` → verify same invoice returned
4. [ ] **Search:** `POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid` → verify status
5. [ ] **Get File:** `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile` → verify PDF/XML
6. [ ] **Cancel:** `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice` (7 fields) → verify success
7. [ ] **Error - Invalid Template:** Send wrong `templateCode` → verify errorCode
8. [ ] **Error - Wrong Tax Code:** Send wrong `supplierTaxCode` → verify errorCode
9. [ ] **Timeout:** Verify response time (expect 30-60s for createInvoice)

### 11.3 MISA Verification (2 steps minimum)
1. [ ] **Auth:** `POST /api/integration/auth/token` with `{appid, taxcode, username, password}` → verify Bearer token
2. [ ] **Create (SignType=2):** `POST /api/integration/invoice` with full payload → verify invoice created

### 11.4 Credential Storage
```bash
# Use .NET user-secrets (NOT committed to repo)
cd 3_CoreHub
dotnet user-secrets init
dotnet user-secrets set "EInvoiceProviders:Viettel:Username" "<sandbox user>"
dotnet user-secrets set "EInvoiceProviders:Viettel:Password" "<sandbox pass>"
dotnet user-secrets set "EInvoiceProviders:Viettel:TaxCode" "<supplier tax code>"
dotnet user-secrets set "EInvoiceProviders:Viettel:TemplateCode" "<template code>"
dotnet user-secrets set "EInvoiceProviders:Viettel:SerialNumber" "<series>"
dotnet user-secrets set "EInvoiceProviders:Misa:AppId" "<MISA appid>"
dotnet user-secrets set "EInvoiceProviders:Misa:TaxCode" "<tax code>"
dotnet user-secrets set "EInvoiceProviders:Misa:Username" "<MISA user>"
dotnet user-secrets set "EInvoiceProviders:Misa:Password" "<MISA pass>"
```

### 11.5 Sandbox Test Structure
```csharp
[Trait("Category", "Sandbox")]
public class EInvoiceSandboxTests
{
    [Fact]
    public async Task Viettel_Auth_ReturnsAccessToken()
    {
        // Skip if no credentials
        if (!HasViettelCredentials()) return;
        // ... test code
    }

    [Fact]
    public async Task Viettel_CreateInvoice_ReturnsInvoiceNumber()
    {
        // ... full payload test
    }

    [Fact]
    public async Task Viettel_Idempotency_SameTransactionUuid_ReturnsSameInvoice()
    {
        // ... idempotency test
    }
}
```

---

## 12. EXIT CHECKLIST
- [ ] Viettel sandbox credentials received
- [ ] MISA sandbox credentials + appid received
- [ ] IP registered in Viettel whitelist
- [ ] Viettel: auth, create, idempotency, search, get file, cancel, error handling — all verified
- [ ] MISA: auth, create — verified
- [ ] Sandbox test results documented in `project_state.md`
- [ ] No credentials committed to repo (verify `.gitignore`)
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[Wave 4] EInvoice sandbox runtime verification`
- [ ] Master plan status updated to COMPLETE
- [ ] Ready for production deployment planning






