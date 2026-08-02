# TASK CARD: EInvoice Provider Rewrite - Wave 1 - Update EInvoiceRequest Contract + Interface

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Update `EInvoiceRequest` record + `IEInvoiceProvider` interface để mang đủ data cho provider implementations thật (line items, supplierTaxCode, transactionUuid, GetInvoiceFileAsync)
- **Nghiệp vụ áp dụng:** HĐĐT cho HKD F&B (TT 152/2025/TT-BTC) — segment không máy tính tiền
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/einvoice-rewrite-wave1-request-contract`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 1 of 4
- **Dependency:** None (first wave); Wave 0 (credential request) runs in parallel

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` (READ)
- `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs` (UPDATE — `EInvoiceRequest`, `EInvoiceResponse`, add `GetInvoiceFileAsync`, add line item type)
- `3_CoreHub/Program.cs` (UPDATE — `EInvoiceRequest` construction line 185 với 4 new fields)
- `1_Shared/Domain.cs` (READ ONLY — confirm `InvoiceItem` line 1752, `ProviderConfiguration` line 2045)
- `6_Tests/VanAn.Core.Tests/Services/ViettelEInvoiceProviderTests.cs` (UPDATE — new signature)
- `6_Tests/VanAn.Core.Tests/Services/MisaEInvoiceProviderTests.cs` (UPDATE — new signature)
- `6_Tests/VanAn.Core.Tests/Services/EInvoiceProviderTests.cs` (UPDATE — new signature, if references `EInvoiceRequest`)
- `3_CoreHub/Services/Orchestration/EInvoiceOrchestrator.cs` (READ — verify no break)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs` — `InvoiceItem` entity đã tồn tại
- KHÔNG sửa `EInvoiceOrchestrator` — design đã đúng
- KHÔNG tạo provider implementations trong wave này (chỉ contract + interface)
- KHÔNG thêm business logic vào `EInvoiceRequest` (chỉ data carrier)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs — dùng `InvoiceItem` entity sẵn có
- [ ] **Provider Purity:** `EInvoiceRequest` là record (immutable data carrier)
- [ ] **Multi-Tenancy:** `SupplierTaxCode` per-tenant (không hardcode)
- [ ] **Idempotency:** `transactionUuid` derive từ `InvoiceId` (UUID format)
- [ ] **Backward Compat:** Update all existing tests + `Program.cs` using old `EInvoiceRequest` signature
- [ ] **Interface Completeness:** `GetInvoiceFileAsync` added to `IEInvoiceProvider` (all providers must implement in Wave 2 & 3)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Line item type defined (prefer reuse `InvoiceItem` from Domain; fallback: `InvoiceLineItemDto` record) — see Open Questions
- [ ] **SC2:** `EInvoiceRequest` updated with: `SupplierTaxCode`, `LineItems`, `CurrencyCode`, `PaymentType`
- [ ] **SC3:** `EInvoiceResponse` updated with: `TransactionUuid`, `ReservationCode` (nullable)
- [ ] **SC4:** `GetInvoiceFileAsync` method added to `IEInvoiceProvider` interface
- [ ] **SC5:** `transactionUuid` mapping documented (derive from `InvoiceId.Value.ToString()`)
- [ ] **SC6:** `SupplierTaxCode` source confirmed (likely `ProviderConfiguration.ConfigurationData` JSON — deserialize helper documented)
- [ ] **SC7:** `Program.cs` line 185 updated với 4 new fields trong `EInvoiceRequest` construction
- [ ] **SC8:** All existing tests updated to new `EInvoiceRequest` signature
- [ ] **SC9:** Build: 0 errors
- [ ] **SC10:** No regression in orchestrator layer
- [ ] **SC11:** All existing tests pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure contract aligns with Domain entities
- `build-error-analysis` — Fix any signature breakage
- `test-system-upgrade` — Update existing tests for new contract

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `InvoiceItem` entity exists in `1_Shared/Domain.cs` line 1752 with ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount
  - Fact 2: `ElectronicInvoice.Items` navigation collection exists line 1642
  - Fact 3: `EInvoiceRequest` current record has 12 fields (TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount, TotalAmount, CustomerName, CustomerTaxCode, CustomerAddress, InvoiceDate, AdditionalData)
  - Fact 4: `EInvoiceResponse` current record has 6 fields (Success, ProviderInvoiceNumber, TaxAuthorityInvoiceNumber, ErrorMessage, ProcessedAt, Metadata)
  - Fact 5: Viettel API requires `transactionUuid` (UUID, 10-36 chars, 3-day validity) for idempotency
  - Fact 6: `EInvoiceRequest` is constructed in `3_CoreHub/Program.cs` line 185 (DI factory for `IRetryPolicyService`), NOT in `EInvoiceOrchestrator`
  - Fact 7: `ProviderConfiguration` entity (line 2045) has `ConfigurationData` JSON string — candidate for `SupplierTaxCode` storage
- **Assumptions:**
  - `transactionUuid` can be derived from `InvoiceId.Value.ToString()` (UUID format, 36 chars)
  - `CurrencyCode` defaults to "VND"
  - `PaymentType` defaults to "TM" (tiền mặt) for F&B HKD
  - `SupplierTaxCode` stored in `ProviderConfiguration.ConfigurationData` as JSON (e.g., `{"supplierTaxCode":"0100109106", ...}`)
- **Open Questions:**
  - Q1: Should line item type be `InvoiceItem` (Domain entity) or new `InvoiceLineItemDto`? (Recommend: `InvoiceItem` — reuse, consistent with existing pattern, avoid duplication)
  - Q2: Should `LineItems` be `IReadOnlyList<InvoiceItem>` or `IReadOnlyList<InvoiceLineItemDto>`? (Depends on Q1)
  - Q3: Should `SupplierTaxCode` be required or optional in `EInvoiceRequest`? (Recommend: required — Viettel needs it in URL path)
  - Q4: How to deserialize `SupplierTaxCode` from `ProviderConfiguration.ConfigurationData`? (Need helper service or extension method)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IEInvoiceProvider.cs` (EInvoiceRequest) | Breaks all provider implementations + tests | Update tests in same wave; providers rewritten in Wave 2 & 3 |
| `IEInvoiceProvider.cs` (EInvoiceResponse) | Breaks provider return mapping | Update tests; providers handle new fields |
| `IEInvoiceProvider.cs` (GetInvoiceFileAsync) | Breaks all provider implementations (new method) | Providers must implement in Wave 2 & 3; stub return `null` or `NotImplementedException` temporarily is NOT acceptable — Wave 2 & 3 must implement |
| `3_CoreHub/Program.cs` (line 185) | `EInvoiceRequest` construction breaks | Update with 4 new fields (SupplierTaxCode from ProviderConfiguration, LineItems from invoice.Items, CurrencyCode="VND", PaymentType="TM") |
| `ViettelEInvoiceProviderTests.cs` | Test compilation breaks | Update `MakeRequest()` helper to new signature |
| `MisaEInvoiceProviderTests.cs` | Test compilation breaks | Update `MakeRequest()` helper to new signature |
| `EInvoiceProviderTests.cs` | Test compilation breaks (if references EInvoiceRequest) | Update to new signature |
| `1_Shared/Domain.cs` | READ ONLY | Không sửa |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Update existing tests to use new `EInvoiceRequest` signature
- **Integration tests:** Không trong wave này
- **E2E tests:** Không trong wave này
- **Verification:** `dotnet build VanAn.sln` + `dotnet test` (all existing tests pass)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt line item type (Q1: `InvoiceItem` vs `InvoiceLineItemDto`)<br>- Chốt `EInvoiceRequest` new fields<br>- Chốt `transactionUuid` mapping strategy<br>- Chốt `SupplierTaxCode` source (Q4: deserialize helper)<br>- Chốt `GetInvoiceFileAsync` signature | - Add line item type<br>- Update `EInvoiceRequest` + `EInvoiceResponse`<br>- Add `GetInvoiceFileAsync` to interface<br>- Update `Program.cs` line 185<br>- Update existing tests<br>- Run build + tests |

---

## 11. DETAILED CONTRACT SPECIFICATION

### 11.1 Line Item Type (Q1 — prefer `InvoiceItem` reuse)
**Option A (Recommend): Reuse `InvoiceItem` from Domain**
```csharp
// No new type needed — use IReadOnlyList<InvoiceItem> in EInvoiceRequest
// InvoiceItem already has: ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount
```

**Option B (Fallback): New `InvoiceLineItemDto` record**
```csharp
public record InvoiceLineItemDto(
    string ItemCode,
    string ItemName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Amount,
    decimal VatAmount
);
```

### 11.2 EInvoiceRequest (UPDATED)
```csharp
public record EInvoiceRequest(
    TenantId TenantId,
    ElectronicInvoiceId InvoiceId,
    OrderId OrderId,
    InvoiceType InvoiceType,
    decimal Amount,
    decimal VatAmount,
    decimal TotalAmount,
    string CustomerName,
    string CustomerTaxCode,
    string CustomerAddress,
    DateTime InvoiceDate,
    Dictionary<string, string> AdditionalData,
    // NEW FIELDS:
    string SupplierTaxCode,                              // Required — Viettel URL path param
    IReadOnlyList<InvoiceItem> LineItems,                // Required — line items for payload (Option A)
    string CurrencyCode,                                 // Default "VND"
    string PaymentType                                   // Default "TM" (tiền mặt)
);
```

### 11.3 EInvoiceResponse (UPDATED)
```csharp
public record EInvoiceResponse(
    bool Success,
    string? ProviderInvoiceNumber,
    string? TaxAuthorityInvoiceNumber,
    string? ErrorMessage,
    DateTime ProcessedAt,
    Dictionary<string, string> Metadata,
    // NEW FIELDS:
    string? TransactionUuid,        // Viettel: transactionUuid returned
    string? ReservationCode         // Viettel: reservationCode (customer lookup)
);
```

### 11.4 GetInvoiceFileAsync (NEW interface method)
```csharp
Task<byte[]?> GetInvoiceFileAsync(
    TenantId tenantId,
    string supplierTaxCode,
    string invoiceNo,
    string templateCode,
    string fileType = "pdf",
    CancellationToken cancellationToken = default);
```

### 11.5 transactionUuid Mapping
- Source: `EInvoiceRequest.InvoiceId.Value.ToString()` (Guid → string, 36 chars)
- Validity: 3 days (Viettel spec)
- Idempotency: Resending same `transactionUuid` returns same invoice (no duplicate)

### 11.6 SupplierTaxCode Source (Q4)
- **Candidate:** `ProviderConfiguration.ConfigurationData` (JSON string, line 2045)
- **Example JSON:** `{"supplierTaxCode":"0100109106","templateCode":"01GTKT0/001","serialNumber":"C22TAA"}`
- **Helper needed:** Extension method or service to deserialize `ConfigurationData` → extract `supplierTaxCode`
- **Wave 1 task:** Research + confirm this is the source, document deserialize approach

### 11.7 Program.cs Update (line 185)
```csharp
var request = new EInvoiceRequest(
    invoice.TenantId,
    invoice.InvoiceId,
    invoice.OrderId,
    invoice.InvoiceType,
    invoice.Amount,
    invoice.VatAmount,
    invoice.TotalAmount,
    invoice.CustomerName,
    invoice.CustomerTaxCode,
    invoice.CustomerAddress,
    invoice.SubmittedAt ?? DateTime.UtcNow,
    new Dictionary<string, string>(),
    // NEW FIELDS:
    supplierTaxCode,                                    // from ProviderConfiguration
    invoice.Items.ToList().AsReadOnly(),                // from ElectronicInvoice.Items
    "VND",                                              // CurrencyCode
    "TM"                                                // PaymentType
);
```

---

## 12. EXIT CHECKLIST
- [ ] Line item type defined (InvoiceItem reuse or InvoiceLineItemDto)
- [ ] `EInvoiceRequest` updated with 4 new fields
- [ ] `EInvoiceResponse` updated with 2 new fields
- [ ] `GetInvoiceFileAsync` added to `IEInvoiceProvider` interface
- [ ] `SupplierTaxCode` source confirmed + documented
- [ ] `Program.cs` line 185 updated với new fields
- [ ] Existing tests updated to new signature
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] All existing tests pass
- [ ] Commit với message `[WAVE 1] EInvoice request contract + interface update`
- [ ] Ready for Wave 2
