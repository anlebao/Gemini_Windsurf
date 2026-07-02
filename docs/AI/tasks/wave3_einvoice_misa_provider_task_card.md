# TASK CARD: EInvoice Provider Rewrite - Wave 3 - Rewrite MISA Provider + DTOs + Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Rewrite `MisaEInvoiceProvider` + `MisaDTOs` + `MisaEInvoiceProviderTests` theo MISA meInvoice API spec thật
- **Nghiệp vụ áp dụng:** HĐĐT cho HKD F&B qua MISA T-VAN (backup provider)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/einvoice-rewrite-wave3-misa-provider-tests`
- **Estimated Sessions:** 1-2

> **NOTE:** Wave 3 (cũ Wave 4) + tests merged cùng wave (TDD compliance).
> Đã đảo vị trí lên trước sandbox verify — hoàn tất code rewrite trước khi verify.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 3 of 4
- **Dependency:** Wave 2 must be merged (Viettel provider + tests complete)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave2_einvoice_viettel_provider_task_card.md` (READ — pattern reference)
- `3_CoreHub/Services/Providers/EInvoice/MisaDTOs.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` (REWRITE)
- `6_Tests/VanAn.Core.Tests/Services/MisaEInvoiceProviderTests.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs` (READ — contract from Wave 1)
- `3_CoreHub/Program.cs` (UPDATE — register named HttpClient "misa")

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa `IEInvoiceProvider` interface (chỉ dùng contract từ Wave 1)
- KHÔNG sửa `EInvoiceOrchestrator`
- KHÔNG sửa Viettel provider (Wave 2 output)
- KHÔNG tạo integration tests (Wave 4)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs
- [ ] **API Spec Compliance:** Endpoint, method, payload, auth phải khớp MISA meInvoice docs
- [ ] **Auth Mechanism:** Bearer token (MISA uses Bearer, unlike Viettel Cookie)
- [ ] **AppId Required:** MISA auth requires `appid` field (provided by MISA)
- [ ] **SignType Required:** Create payload must include `SignType: 2` (HSM sync)
- [ ] **Token Expiry:** 15 days (NOT 55 minutes — current implementation wrong)
- [ ] **Stateless Provider:** Token cache per instance, no static state

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `MisaConfig` has `AppId` (REQUIRED), `TaxCode`, `SandboxBaseUrl` default `https://testapi.meinvoice.vn/`
- [ ] **SC2:** Auth: `POST /api/integration/auth/token` with `{appid, taxcode, username, password}`
- [ ] **SC3:** Auth response: `{Success, Data (token), ErrorCode}` (not `{AccessToken}`)
- [ ] **SC4:** Token expiry: 15 days (not 55 minutes)
- [ ] **SC5:** Create: `POST /api/integration/invoice` with `{SignType: 2, InvoiceData: [...]}`
- [ ] **SC6:** Payload: `InvoiceData` with `OriginalInvoiceDetail[]` + `TaxRateInfo[]`
- [ ] **SC7:** Bearer token in Authorization header (MISA uses Bearer, not Cookie)
- [ ] **SC8:** Status endpoint: use MISA documented endpoint (research + verify)
- [ ] **SC9:** Cancel endpoint: use MISA documented endpoint (research + verify)
- [ ] **SC10:** Named HttpClient "misa" registered with correct BaseAddress
- [ ] **SC11:** Tests: mocks per real MISA API spec
- [ ] **SC12:** Test: verify `appid` in auth body
- [ ] **SC13:** Test: verify `SignType: 2` in create payload
- [ ] **SC14:** Build: 0 errors
- [ ] **SC15:** All MISA tests pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration` — MISA meInvoice API compliance
- `domain-integrity-validation` — Ensure payload mapping aligns with Domain
- `test-system-upgrade` — Rewrite tests per real API spec

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: MISA auth endpoint is `POST /api/integration/auth/token` (test) or `/api/integration/auth/token` (live)
  - Fact 2: MISA auth body requires `{appid, taxcode, username, password}` (current impl missing `appid`)
  - Fact 3: MISA auth response is `{Success, Data, ErrorCode}` (current impl expects `{AccessToken}`)
  - Fact 4: MISA token validity is 15 days (current impl hardcodes 55 minutes)
  - Fact 5: MISA create endpoint is `POST /api/integration/invoice` (current impl uses `einvoices`)
  - Fact 6: MISA create payload requires `{SignType: 2|3, InvoiceData: [...]}` (current impl flat)
  - Fact 7: MISA uses Bearer token in Authorization header (current impl correct on this point)
- **Assumptions:**
  - `SignType: 2` (HSM sync) is appropriate for SaaS multi-tenant
  - `OriginalInvoiceDetail[]` maps from `EInvoiceRequest.LineItems`
  - `TaxRateInfo[]` groups line items by VAT rate
- **Open Questions:**
  - Q1: What is MISA's status query endpoint? (Docs not clear — may need to contact MISA support)
  - Q2: What is MISA's cancel endpoint? (Docs not clear — may need to contact MISA support)
  - Q3: How to get `appid` from MISA? (Need partnership agreement — Wave 4 prerequisite)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `MisaDTOs.cs` (REWRITE) | Breaks `MisaEInvoiceProviderTests` | Tests rewritten in same wave |
| `MisaEInvoiceProvider.cs` (REWRITE) | Breaks existing tests | Tests rewritten in same wave |
| `3_CoreHub/Program.cs` (HttpClient) | May affect DI | Verify existing registrations intact |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Rewrite + add new tests in same wave (TDD compliance — same pattern as Wave 2)
- **Test cases:**
  - TC-M01: Auth success — verify `appid` in body, Bearer token returned
  - TC-M02: Auth fail — InvalidAppID error
  - TC-M03: Auth fail — UnAuthorize error
  - TC-M04: Create success — verify `SignType: 2`, nested `InvoiceData`
  - TC-M05: Create success — verify `OriginalInvoiceDetail[]` from LineItems
  - TC-M06: Create fail — error response
  - TC-M07: Token expiry — verify 15-day cache (not 55 min)
  - TC-M08: HealthCheck success
- **Integration tests:** KHÔNG (Wave 4)
- **E2E tests:** KHÔNG

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Research MISA status/cancel endpoints (docs + contact support if needed)<br>- Chốt payload DTO structure<br>- Chốt `SignType` decision | - Rewrite `MisaDTOs.cs`<br>- Rewrite auth + create in `MisaEInvoiceProvider.cs`<br>- Register HttpClient<br>- Start tests |
| **S2** (if needed) | - Chốt status/cancel endpoint<br>- Chốt test mock shapes | - Complete status + cancel<br>- Complete all tests<br>- Run full suite |

---

## 11. DETAILED API SPEC MAPPING

### 11.1 Auth
```
POST /api/integration/auth/token
Content-Type: application/json
Body:
{
  "appid": "<from MISA partnership>",
  "taxcode": "0100109106",
  "username": "<MISA account>",
  "password": "<MISA password>"
}

Response:
{
  "Success": true,
  "Data": "<token string>",
  "ErrorCode": "",
  "Errors": [],
  "CustomData": ""
}

Usage: All subsequent requests add header:
  Authorization: Bearer <token>
```

### 11.2 Create Invoice (SignType=2, HSM sync)
```
POST /api/integration/invoice
Headers:
  Authorization: Bearer <token>
  Content-Type: application/json
Body:
{
  "SignType": 2,
  "InvoiceData": [
    {
      "InvoiceSeries": "C22TAA",
      "InvoiceDate": "2026-07-02",
      "CurrencyCode": "VND",
      "ExchangeRate": 1.0,
      "TotalAmountWithoutTax": 100000,
      "TotalVATAmount": 10000,
      "TotalAmount": 110000,
      "BuyerName": "...",
      "BuyerTaxCode": "...",
      "BuyerAddress": "...",
      "OriginalInvoiceDetail": [
        {
          "ItemCode": "P001",
          "ItemName": "Cà phê đen",
          "Unit": "ly",
          "Quantity": 1.0,
          "UnitPrice": 25000,
          "VATRate": 10,
          "Amount": 25000,
          "VATAmount": 2500
        }
      ],
      "TaxRateInfo": [
        {
          "VATRateName": "10%",
          "AmountWithoutTax": 100000,
          "VATAmount": 10000,
          "TotalAmount": 110000
        }
      ]
    }
  ]
}
```

### 11.3 Token Refresh
```
POST /auth/refreshtoken
Body: { "token": "<expired token>" }
Response: { "Success": true, "Data": "<new token>" }
```

---

## 12. EXIT CHECKLIST
- [ ] `MisaDTOs.cs` rewritten with `AppId`, nested payload, correct response structure
- [ ] `MisaEInvoiceProvider.cs` rewritten: auth (Bearer), create (SignType=2), status, cancel
- [ ] Token expiry: 15 days (not 55 minutes)
- [ ] Named HttpClient "misa" registered with `testapi.meinvoice.vn` BaseAddress
- [ ] `MisaEInvoiceProviderTests.cs` rewritten with real API spec mocks
- [ ] All MISA tests pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] No regression in Viettel tests or other suites
- [ ] Commit với message `[Wave 3] MISA provider + tests rewrite per API spec`
- [ ] Ready for Wave 4 (sandbox verification)





