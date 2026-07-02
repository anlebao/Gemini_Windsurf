# TASK CARD: EInvoice Provider Rewrite - Wave 2 - Rewrite Viettel Provider + DTOs + Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Rewrite `ViettelEInvoiceProvider` + `ViettelDTOs` + `ViettelEInvoiceProviderTests` theo Viettel S-Invoice API v2.0 spec thật
- **Nghiệp vụ áp dụng:** HĐĐT cho HKD F&B qua Viettel T-VAN
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/einvoice-rewrite-wave2-viettel-provider-tests`
- **Estimated Sessions:** 1-2

> **NOTE:** Wave 2 (cũ) + Wave 3 (cũ) đã MERGE thành 1 wave — tuân thủ TDD (tests + impl cùng wave, build pass khi commit).

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 2 of 4
- **Dependency:** Wave 1 must be merged (`EInvoiceRequest` contract + `GetInvoiceFileAsync` interface updated)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave1_einvoice_request_contract_task_card.md` (READ — contract spec)
- `3_CoreHub/Services/Providers/EInvoice/ViettelDTOs.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs` (READ — contract from Wave 1)
- `3_CoreHub/Program.cs` (UPDATE — register named HttpClient "viettel")
- `6_Tests/VanAn.Core.Tests/Services/ViettelEInvoiceProviderTests.cs` (REWRITE)
- `6_Tests/VanAn.Core.Tests/Helpers/MockHttpMessageHandler.cs` (READ — verify supports new mock patterns)
- `1_Shared/Domain.cs` (READ — InvoiceItem, InvoiceType enum)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa `IEInvoiceProvider` interface (chỉ dùng contract từ Wave 1)
- KHÔNG sửa `EInvoiceOrchestrator` — design đã đúng
- KHÔNG implement MISA provider (Wave 3)
- Mocks MUST reflect real API spec — KHÔNG mock theo implementation

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs
- [ ] **API Spec Compliance:** Endpoint, method, payload, auth phải khớp Viettel docs v2.5
- [ ] **Auth Mechanism:** Cookie header `access_token=...` (NOT Bearer)
- [ ] **Idempotency:** `transactionUuid` in `generalInvoiceInfo` (derive from InvoiceId)
- [ ] **Date Format:** Epoch milliseconds (NOT ISO string)
- [ ] **Timeout:** 90s (Viettel recommended 60-90s)
- [ ] **CTS Type:** HSM only (endpoint `createInvoice`, not `createInvoiceUsbTokenGetHash`)
- [ ] **Stateless Provider:** Token cache per instance, no static state
- [ ] **TDD Compliance:** Tests + impl cùng wave — build MUST pass khi commit

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `ViettelConfig` has `SandboxBaseUrl` default `https://vinvoice.viettel.vn/` + `ProductionBaseUrl`
- [ ] **SC2:** Auth: `POST /auth/login` with `{username, password}` → Cookie header `access_token=...`
- [ ] **SC3:** Create: `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}` with nested payload
- [ ] **SC4:** Payload structure: `generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo[]`, `summarizeInfo`, `taxBreakdowns[]`
- [ ] **SC5:** `transactionUuid` in `generalInvoiceInfo` (UUID from InvoiceId)
- [ ] **SC6:** Date format: epoch milliseconds (`DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`)
- [ ] **SC7:** Status: `POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid` (form-urlencoded)
- [ ] **SC8:** Cancel: `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice` (form-urlencoded, 7 required fields)
- [ ] **SC9:** GetFile: `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile` (JSON) — implement `GetInvoiceFileAsync`
- [ ] **SC10:** Response DTO: `{errorCode, description, result: {supplierTaxCode, invoiceNo, transactionID, reservationCode}}`
- [ ] **SC11:** Capabilities timeout: 90s
- [ ] **SC12:** Named HttpClient "viettel" registered with correct BaseAddress
- [ ] **SC13:** All mocks reflect real Viettel API spec (endpoint, method, payload, response)
- [ ] **SC14:** Tests verify Cookie auth, nested payload, transactionUuid, line items, epoch date
- [ ] **SC15:** Build: 0 errors
- [ ] **SC16:** All tests pass (TDD — impl + tests cùng wave)
- [ ] **SC17:** No regression in orchestrator layer

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure payload mapping aligns with Domain entities
- `einvoice-integration` — Viettel S-Invoice API compliance
- `test-system-upgrade` — Rewrite tests per real API spec

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: Viettel auth endpoint is `POST /auth/login` (not `auth/token`)
  - Fact 2: Token passed via Cookie header `access_token=abc...def` (not Bearer)
  - Fact 3: Create endpoint is `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}` (path param required)
  - Fact 4: Payload is nested: `generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo[]`, `summarizeInfo`, `taxBreakdowns[]`
  - Fact 5: `transactionUuid` REQUIRED in `generalInvoiceInfo` (UUID, 10-36 chars, 3-day validity)
  - Fact 6: Date format is epoch milliseconds (e.g., `1517301625626`)
  - Fact 7: Cancel endpoint is `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice` (form-urlencoded, 7 fields: supplierTaxCode, templateCode, invoiceNo, strIssueDate, additionalReferenceDesc, additionalReferenceDate, reasonDelete)
  - Fact 8: Get file endpoint is `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile` (JSON)
- **Assumptions:**
  - `supplierTaxCode` comes from `EInvoiceRequest.SupplierTaxCode` (per-tenant, from Wave 1)
  - `sellerInfo` can be omitted (Viettel falls back to configured seller data)
  - HSM signing is sync (SignType not applicable to Viettel — that's MISA)
- **Open Questions:**
  - Q1: Should `sellerInfo` be populated from config or omitted? (Recommend: omit — Viettel fallback)
  - Q2: How to map `InvoiceType` enum to Viettel codes (`01GTKT`, `02GTTT`)? (Need mapping table)
  - Q3: Does `MockHttpMessageHandler` support path param matching + form-urlencoded + header inspection? (Verify in S1)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ViettelDTOs.cs` (REWRITE) | Breaks `ViettelEInvoiceProviderTests` | Tests rewritten in same wave |
| `ViettelEInvoiceProvider.cs` (REWRITE) | Breaks existing tests | Tests rewritten in same wave |
| `3_CoreHub/Program.cs` (HttpClient registration) | May affect DI | Verify existing registrations intact |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Rewrite + add new tests trong cùng wave (TDD compliance)
- **Test cases:**
  - TC-V01: Submit success — verify Cookie auth, nested payload, transactionUuid, line items
  - TC-V02: Auth fail 401 → Failure
  - TC-V03: ErrorCode non-null → Failure with description
  - TC-V04: Status — searchInvoiceByTransactionUuid returns invoice
  - TC-V05: Status — invoice not found (empty result)
  - TC-V06: Cancel success — 7 required fields sent
  - TC-V07: Cancel fail → Failure
  - TC-V08: GetFile success — returns byte[]
  - TC-V09: HealthCheck success
  - TC-V10: Verify epoch ms date format in payload
  - TC-V11: Verify InvoiceType mapping (Goods → 01GTKT)
  - TC-V12: Verify response maps transactionID + reservationCode
- **Integration tests:** KHÔNG (Wave 4)
- **E2E tests:** KHÔNG

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Verify `MockHttpMessageHandler` capabilities (path params, form-urlencoded, headers)<br>- Chốt payload DTO structure<br>- Chốt InvoiceType → Viettel code mapping<br>- Chốt auth Cookie implementation | - Rewrite `ViettelDTOs.cs`<br>- Rewrite auth + create in `ViettelEInvoiceProvider.cs`<br>- Register HttpClient in Program.cs<br>- Start tests (auth + create mocks) |
| **S2** (if needed) | - Chốt cancel 7 fields mapping<br>- Chốt get file response handling<br>- Chốt remaining test mock shapes | - Rewrite status + cancel + getfile<br>- Complete all tests<br>- Run full test suite |

---

## 11. DETAILED API SPEC MAPPING

### 11.1 Auth
```
POST /auth/login
Content-Type: application/json
Body: {"username":"0100109106-509","password":"123456a@A"}

Response: {access_token: "abc...def"}

Usage: All subsequent requests add header:
  Cookie: access_token=abc...def
```

### 11.2 Create Invoice
```
POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}
Headers:
  Cookie: access_token=...
  Content-Type: application/json
Body:
{
  "generalInvoiceInfo": {
    "invoiceType": "01GTKT",
    "templateCode": "01GTKT0/001",
    "invoiceSeries": "C22TAA",
    "transactionUuid": "<from InvoiceId>",
    "invoiceIssuedDate": <epoch ms>,
    "currencyCode": "VND",
    "adjustmentType": "1",
    "paymentStatus": true,
    "paymentType": "TM",
    "paymentTypeName": "Tiền mặt",
    "cusGetInvoiceRight": true,
    "userName": "<config username>"
  },
  "buyerInfo": {
    "buyerName": "...",
    "buyerTaxCode": "...",
    "buyerAddressLine": "...",
    "buyerPhoneNumber": "..."
  },
  "sellerInfo": { },  // omit — Viettel fallback
  "itemInfo": [
    {
      "itemCode": "...",
      "itemName": "...",
      "unitName": "...",
      "quantity": 1.0,
      "unitPrice": 25000,
      "vatRate": 10,
      "amount": 25000,
      "vatAmount": 2500
    }
  ],
  "summarizeInfo": {
    "totalAmountWithoutTax": 100000,
    "totalVatAmount": 10000,
    "totalAmount": 110000
  },
  "taxBreakdowns": [
    {"vatRate": 10, "amountWithoutTax": 100000, "vatAmount": 10000}
  ]
}

Response:
{
  "errorCode": null,
  "description": null,
  "result": {
    "supplierTaxCode": "0100109106",
    "invoiceNo": "AA/20E0000001",
    "transactionID": "12523522245",
    "reservationCode": "AXHBNK8I0H"
  }
}
```

### 11.3 Search by transactionUuid (Status)
```
POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid
Headers:
  Cookie: access_token=...
  Content-Type: application/x-www-form-urlencoded
Body: supplierTaxCode=0100109106&transactionUuid=<uuid>

Response:
{
  "transactionUuid": "...",
  "errorCode": null,
  "description": null,
  "result": [
    {
      "supplierTaxCode": "0100109106",
      "invoiceNo": "AB/19E0000522",
      "reservationCode": "OKMYMDX5F4",
      "issueDate": 1587797116843,
      "status": "Hóa đơn gốc"
    }
  ]
}
```

### 11.4 Cancel Invoice
```
POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice
Headers:
  Cookie: access_token=...
  Content-Type: application/x-www-form-urlencoded
Body (7 required fields):
  supplierTaxCode=0100109106
  templateCode=01GTKT0/001
  invoiceNo=AA/20E0000001
  strIssueDate=<epoch ms>
  additionalReferenceDesc=<thỏa thuận hủy>
  additionalReferenceDate=<epoch ms>
  reasonDelete=<lý do>
```

### 11.5 Get Invoice File
```
POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile
Headers:
  Cookie: access_token=...
  Content-Type: application/json
Body:
{
  "supplierTaxCode": "0100109106",
  "invoiceNo": "AA/20E0000001",
  "templateCode": "01GTKT0/001",
  "fileType": "pdf"
}

Response: Binary file content (PDF/XML/ZIP)
```

### 11.6 InvoiceType Mapping
| Domain `InvoiceType` | Viettel Code |
|---|---|
| `Goods` | `01GTKT` |
| `Service` | `02GTTT` |
| Other | TBD — research needed |

---

## 12. EXIT CHECKLIST
- [ ] `ViettelDTOs.cs` rewritten with nested payload structure
- [ ] `ViettelEInvoiceProvider.cs` rewritten: auth (Cookie), create, status, cancel, getfile
- [ ] `GetInvoiceFileAsync` implemented per `IEInvoiceProvider` interface
- [ ] Named HttpClient "viettel" registered with `vinvoice.viettel.vn` BaseAddress
- [ ] `ViettelEInvoiceProviderTests.cs` rewritten with real API spec mocks
- [ ] Tests verify Cookie auth, nested payload, transactionUuid, line items, epoch date
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] All tests pass (TDD compliance)
- [ ] No regression in orchestrator layer
- [ ] Commit với message `[WAVE 2] Viettel provider + tests rewrite per API spec v2.5`
- [ ] Ready for Wave 3 (MISA provider rewrite)
