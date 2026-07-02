# MASTER IMPLEMENTATION PLAN — EInvoice Provider Rewrite (Viettel S-Invoice + MISA meInvoice)

> **Status:** PENDING — Awaiting Approval
> **Created:** 2026-07-02
> **Last Updated:** 2026-07-02 (Review v2 — merged Wave 2+3, fixed 10 issues)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** JIT Planning + Pure Execution
> **Prerequisite:** Gap analysis report (verify sandbox Viettel SInvoicer session, 2026-07-02)

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc và hiểu rõ API spec thật (Viettel v2.0 + MISA meInvoice)
- Identify gaps giữa implementation hiện tại và API spec
- Lập detailed coding plan với specific steps
- Chốt approach trước khi viết bất kỳ dòng code nào

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện viết code theo plan đã chốt
- KHÔNG thay đổi approach khi đang implement
- Mỗi wave xong, chạy `guard-check.ps1` + `dotnet build VanAn.sln`
- Mock trong unit tests phải theo API spec thật, KHÔNG theo implementation

### Session protocol
1. **Mỗi session chỉ làm 1 wave**
2. **Bắt đầu mỗi session:** Đọc `project_state.md` + task card wave đang làm
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end:** Build + test
5. **Sau mỗi wave:** Commit với message format `[WAVE X] Task description`

### Branch protocol
```
main
  └── feature/einvoice-rewrite-wave1-request-contract
      └── feature/einvoice-rewrite-wave2-viettel-provider-tests
          └── feature/einvoice-rewrite-wave3-misa-provider-tests
              └── feature/einvoice-rewrite-wave4-sandbox-verify
```
- Mỗi wave có branch riêng
- Merge wave vào branch trước đó (cherry-pick hoặc rebase)
- Final merge vào `main` khi tất cả waves complete

### Hard rules
- **Domain layer KHÔNG được sửa** — `InvoiceItem` entity đã tồn tại trong `1_Shared/Domain.cs` (line 1752)
- **Sử dụng `InvoiceItem` entity sẵn có** — có đầy đủ ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount
- **Mock theo API spec thật, KHÔNG theo implementation** — unit tests phải phản ánh endpoint/payload thật
- **Provider implementations MUST be stateless** (theo `IEInvoiceProvider` contract hiện có)
- **Orchestrator layer giữ nguyên** — design đã đúng (outbox, state machine, idempotency, retry/fallback)
- **TDD: tests + implementation trong cùng wave** (không tách rời để tránh build-break giữa các wave)
- **Playwright DISABLED cho đến khi build pass + implementation complete**

### Critical regulatory context
- **Viettel S-Invoice v2.0** (`vinvoice.viettel.vn`) — API docs v2.5 (06/2022)
- **MISA meInvoice** (`testapi.meinvoice.vn` / `api.meinvoice.vn`) — integration API
- **TT 152/2025/TT-BTC** — HĐĐT mandatory cho HKD
- **Nghị định 123/2020/NĐ-CP** — HĐĐT-KT-TT cho đơn vị có máy tính tiền (OUT OF SCOPE — segment này là HKD không máy tính tiền)
- **CTS type: HSM only** (USB Token không khả thi cho SaaS multi-tenant)

---

## 0.5. WAVE 0 — PARALLEL: Credential Request (Non-code, start immediately)

> **Bottleneck dài nhất (1-2 tuần) — MUST start before Wave 1, run in parallel with Wave 1-3**

### Tasks
| # | Task | Owner | Status |
|---|---|---|---|
| 1 | Email Viettel Solution (`lienhe@viettelsolution.com.vn` / hotline `1900.8119`) xin sandbox vinvoice 2.0 | User | PENDING |
| 2 | Email MISA partnership request xin `appid` + sandbox `testapi.meinvoice.vn` | User | PENDING |
| 3 | Register IP server vào Viettel whitelist (coordinate với ops) | User/Ops | PENDING |
| 4 | Xin mẫu hóa đơn + ký hiệu trên sandbox (CQT sandbox cấp) | User | PENDING |
| 5 | Contact MISA support confirm status/cancel endpoints (prerequisite cho Wave 3) | User | PENDING |

### Tracking
- Update `project_state.md` Maintenance Log khi nhận được credentials
- KHÔNG commit credentials vào repo — use user-secrets hoặc env vars

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: ViettelEInvoiceProvider — Sai toàn bộ implementation
**Status:** ❌ STUB — Will fail 100% on real API call
**Priority:** 1 (Critical)

**Current State (20 gaps identified):**
- 🔴 Auth: Sai endpoint (`auth/token` vs `/auth/login`), sai mechanism (Bearer vs Cookie)
- 🔴 Create: Sai endpoint (`InvoiceAPI/services/createInvoice` vs `InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}`)
- 🔴 Payload: Flat structure vs nested (`generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo`, `summarizeInfo`, `taxBreakdowns`)
- 🔴 Missing `transactionUuid` (idempotency — REQUIRED by Viettel)
- 🔴 Missing line items (`itemInfo[]`)
- 🔴 Status: Sai endpoint + method + params
- 🔴 Cancel: Sai endpoint + content-type + thiếu 7 required fields
- 🔴 Missing get invoice file endpoint
- 🔴 Sandbox URL sai (`sinvoice.viettel.vn/` vs `vinvoice.viettel.vn`)

**Files liên quan:**
- `3_CoreHub/Services/Providers/EInvoice/ViettelEInvoiceProvider.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/ViettelDTOs.cs` (REWRITE)
- `6_Tests/VanAn.Core.Tests/Services/ViettelEInvoiceProviderTests.cs` (REWRITE)

### Issue 2: MisaEInvoiceProvider — Sai toàn bộ implementation
**Status:** ❌ STUB — Will fail 100% on real API call
**Priority:** 2 (High)

**Current State (10 gaps identified):**
- 🔴 Auth: Sai endpoint (`auth/login` vs `/api/integration/auth/token`), sai body (thiếu `appid`)
- 🔴 Auth response: Sai structure (`{AccessToken}` vs `{Success, Data, ErrorCode}`)
- 🔴 Token expiry: Sai (55 phút vs 15 ngày)
- 🔴 Create: Sai endpoint (`einvoices` vs `/api/integration/invoice`)
- 🔴 Missing `SignType` (REQUIRED — 2 sync hoặc 3 async)
- 🔴 Missing line items (`OriginalInvoiceDetail[]` + `TaxRateInfo[]`)
- 🔴 Status/Cancel: Fabricated endpoints không tồn tại trong MISA docs

**Files liên quan:**
- `3_CoreHub/Services/Providers/EInvoice/MisaEInvoiceProvider.cs` (REWRITE)
- `3_CoreHub/Services/Providers/EInvoice/MisaDTOs.cs` (REWRITE)
- `6_Tests/VanAn.Core.Tests/Services/MisaEInvoiceProviderTests.cs` (REWRITE)

### Issue 3: EInvoiceRequest contract — Thiếu line items + supplierTaxCode + GetInvoiceFile
**Status:** ❌ INCOMPLETE
**Priority:** 1 (Critical — blocker cho Wave 2 & 3)

**Current State:**
- ✅ Có: TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount, TotalAmount
- ✅ Có: CustomerName, CustomerTaxCode, CustomerAddress, InvoiceDate, AdditionalData
- ❌ Thiếu: Line items (cần cho payload thật)
- ❌ Thiếu: SupplierTaxCode (per-tenant, không hardcode trong config)
- ❌ Thiếu: transactionUuid mapping (có thể map từ InvoiceId)
- ❌ Thiếu: CurrencyCode, PaymentType
- ❌ Thiếu: `GetInvoiceFileAsync` method trong `IEInvoiceProvider` interface
- ❌ `EInvoiceRequest` được construct trong `3_CoreHub/Program.cs` line 185 (KHÔNG phải Orchestrator) — cần update

**Files liên quan:**
- `3_CoreHub/Services/Providers/EInvoice/IEInvoiceProvider.cs` (UPDATE — `EInvoiceRequest`, `EInvoiceResponse`, add `GetInvoiceFileAsync`)
- `3_CoreHub/Program.cs` (UPDATE — `EInvoiceRequest` construction line 185)

### Issue 4: Sandbox runtime verification chưa thực hiện
**Status:** ❌ NOT STARTED
**Priority:** 3 (Medium — sau khi code rewrite xong)
**Estimated Time:** 1-2 tuần (bottleneck: chờ Viettel/MISA cấp sandbox account — Wave 0)

**Prerequisites:**
- Wave 0 complete (credentials received)
- MISA status/cancel endpoints confirmed (Wave 0 task 5)

---

## 2. WAVE 1 — Update EInvoiceRequest Contract + Interface

**Branch:** `feature/einvoice-rewrite-wave1-request-contract`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 1
**Task Card:** `docs/AI/tasks/wave1_einvoice_request_contract_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Add line item type for provider requests (prefer reuse `InvoiceItem` from Domain; fallback: `InvoiceLineItemDto` record) | `IEInvoiceProvider.cs` | PENDING |
| 2 | W1-T2 | Update `EInvoiceRequest` record: add `SupplierTaxCode`, `LineItems`, `CurrencyCode`, `PaymentType` | `IEInvoiceProvider.cs` | PENDING |
| 3 | W1-T3 | Add `transactionUuid` mapping (derive from `InvoiceId`) | `IEInvoiceProvider.cs` | PENDING |
| 4 | W1-T4 | Update `EInvoiceResponse`: add `TransactionUuid`, `ReservationCode` fields | `IEInvoiceProvider.cs` | PENDING |
| 5 | W1-T5 | Add `GetInvoiceFileAsync` method to `IEInvoiceProvider` interface | `IEInvoiceProvider.cs` | PENDING |
| 6 | W1-T6 | Research + confirm `SupplierTaxCode` source (likely `ProviderConfiguration.ConfigurationData` JSON — need deserialize helper) | `IEInvoiceProvider.cs` + research | PENDING |
| 7 | W1-T7 | Update `EInvoiceRequest` construction in `Program.cs` line 185 với 4 new fields | `3_CoreHub/Program.cs` | PENDING |
| 8 | W1-T8 | Update existing tests to use new `EInvoiceRequest` signature | `6_Tests/VanAn.Core.Tests/Services/` | PENDING |
| 9 | W1-T9 | Verify build passes + all tests pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Project builds successfully
- [ ] Git status clean
- [ ] `InvoiceItem` entity confirmed in `1_Shared/Domain.cs` (line 1752)
- [ ] `ProviderConfiguration` entity confirmed (line 2045) — `ConfigurationData` JSON string
- [ ] Gap analysis report reviewed

### Exit criteria
- [ ] `EInvoiceRequest` có `SupplierTaxCode`, `LineItems`, `CurrencyCode`, `PaymentType`
- [ ] `EInvoiceResponse` có `TransactionUuid`, `ReservationCode`
- [ ] `IEInvoiceProvider` có `GetInvoiceFileAsync` method
- [ ] `Program.cs` line 185 updated với new fields
- [ ] `SupplierTaxCode` source confirmed + documented
- [ ] Build: 0 errors
- [ ] All existing tests updated and pass

### Why first
- Là contract cho tất cả provider implementations
- Không sửa Domain.cs (chỉ sửa provider interface + Program.cs)
- Risk thấp vì chỉ update record + DTOs + 1 method

---

## 3. WAVE 2 — Rewrite Viettel Provider + DTOs + Tests

**Branch:** `feature/einvoice-rewrite-wave2-viettel-provider-tests`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM
**Priority:** 2
**Task Card:** `docs/AI/tasks/wave2_einvoice_viettel_provider_task_card.md`

> **NOTE:** Wave 2 (cũ) + Wave 3 (cũ) đã được MERGE thành 1 wave — tuân thủ TDD (tests + impl cùng wave, build pass khi commit).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Rewrite `ViettelConfig`: add `SandboxBaseUrl` default `vinvoice.viettel.vn`, add `ProductionBaseUrl` | `ViettelDTOs.cs` | PENDING |
| 2 | W2-T2 | Rewrite `ViettelAuthRequest`/`ViettelAuthResponse` per real spec | `ViettelDTOs.cs` | PENDING |
| 3 | W2-T3 | Create nested payload DTOs: `ViettelInvoicePayload` with `generalInvoiceInfo`, `buyerInfo`, `sellerInfo`, `itemInfo[]`, `summarizeInfo`, `taxBreakdowns[]` | `ViettelDTOs.cs` | PENDING |
| 4 | W2-T4 | Create `ViettelInvoiceResult` with `result.{supplierTaxCode, invoiceNo, transactionID, reservationCode}` | `ViettelDTOs.cs` | PENDING |
| 5 | W2-T5 | Rewrite `ViettelEInvoiceProvider.SubmitInvoiceAsync`: auth via Cookie, endpoint `InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}`, nested payload, epoch ms date | `ViettelEInvoiceProvider.cs` | PENDING |
| 6 | W2-T6 | Rewrite `GetInvoiceStatusAsync`: use `searchInvoiceByTransactionUuid` (POST, form-urlencoded) | `ViettelEInvoiceProvider.cs` | PENDING |
| 7 | W2-T7 | Rewrite `CancelInvoiceAsync`: use `cancelTransactionInvoice` (POST, form-urlencoded, 7 required fields) | `ViettelEInvoiceProvider.cs` | PENDING |
| 8 | W2-T8 | Implement `GetInvoiceFileAsync`: use `getInvoiceRepresentationFile` (POST, JSON) | `ViettelEInvoiceProvider.cs` | PENDING |
| 9 | W2-T9 | Update `Capabilities`: timeout 90s (Viettel recommended 60-90s) | `ViettelEInvoiceProvider.cs` | PENDING |
| 10 | W2-T10 | Register named HttpClient "viettel" with correct BaseAddress in DI | `3_CoreHub/Program.cs` | PENDING |
| 11 | W2-T11 | Rewrite `ViettelEInvoiceProviderTests`: all mocks per real API spec (auth, create, status, cancel, getfile) | `ViettelEInvoiceProviderTests.cs` | PENDING |
| 12 | W2-T12 | Add tests: verify Cookie auth, nested payload, transactionUuid, line items, epoch date | `ViettelEInvoiceProviderTests.cs` | PENDING |
| 13 | W2-T13 | Run full test suite, verify 0 failures | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 1 merged
- [ ] `EInvoiceRequest` có `LineItems`, `SupplierTaxCode`, `GetInvoiceFileAsync` in interface
- [ ] `Program.cs` line 185 updated
- [ ] Viettel API docs v2.5 reviewed

### Exit criteria
- [ ] Auth: `POST /auth/login` → Cookie header `access_token=...`
- [ ] Create: `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}` with nested payload
- [ ] Status: `POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid`
- [ ] Cancel: `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice` with 7 fields
- [ ] GetFile: `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile`
- [ ] All mocks reflect real Viettel API spec
- [ ] Tests verify Cookie auth, nested payload, transactionUuid, line items, epoch date
- [ ] Build: 0 errors
- [ ] All tests pass (TDD — impl + tests cùng wave)
- [ ] No regression in orchestrator layer

### Why second
- Cần `EInvoiceRequest` contract từ Wave 1
- Viettel là provider ưu tiên (docs chi tiết hơn, sandbox account public)
- Tests + impl cùng wave để build pass khi commit (TDD compliance)

---

## 4. WAVE 3 — Rewrite MISA Provider + DTOs + Tests

**Branch:** `feature/einvoice-rewrite-wave3-misa-provider-tests`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM
**Priority:** 3
**Task Card:** `docs/AI/tasks/wave3_einvoice_misa_provider_task_card.md`

> **NOTE:** Wave 3 (cũ Wave 4) + tests merged cùng wave (TDD compliance).
> Đã đảo vị trí lên trước sandbox verify — hoàn tất code rewrite trước khi verify.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W3-T1 | Rewrite `MisaConfig`: add `AppId` (REQUIRED by MISA), `TaxCode`, `SandboxBaseUrl` default `testapi.meinvoice.vn` | `MisaDTOs.cs` | PENDING |
| 2 | W3-T2 | Rewrite `MisaAuthRequest`: `{appid, taxcode, username, password}` | `MisaDTOs.cs` | PENDING |
| 3 | W3-T3 | Rewrite `MisaAuthResponse`: `{Success, Data (token), ErrorCode}` | `MisaDTOs.cs` | PENDING |
| 4 | W3-T4 | Create `MisaInvoicePayload` with `SignType: 2` (HSM sync), `InvoiceData[]` with `OriginalInvoiceDetail[]` + `TaxRateInfo[]` | `MisaDTOs.cs` | PENDING |
| 5 | W3-T5 | Rewrite `MisaEInvoiceProvider.SubmitInvoiceAsync`: auth via Bearer, endpoint `/api/integration/invoice`, nested payload with SignType | `MisaEInvoiceProvider.cs` | PENDING |
| 6 | W3-T6 | Fix token expiry: 15 ngày (not 55 phút) | `MisaEInvoiceProvider.cs` | PENDING |
| 7 | W3-T7 | Rewrite `GetInvoiceStatusAsync`: use MISA documented endpoint (prerequisite: Wave 0 task 5 — MISA support confirmed) | `MisaEInvoiceProvider.cs` | PENDING |
| 8 | W3-T8 | Rewrite `CancelInvoiceAsync`: use MISA documented endpoint (prerequisite: Wave 0 task 5) | `MisaEInvoiceProvider.cs` | PENDING |
| 9 | W3-T9 | Implement `GetInvoiceFileAsync` per MISA docs | `MisaEInvoiceProvider.cs` | PENDING |
| 10 | W3-T10 | Register named HttpClient "misa" with correct BaseAddress in DI | `3_CoreHub/Program.cs` | PENDING |
| 11 | W3-T11 | Rewrite `MisaEInvoiceProviderTests`: mocks per real MISA API spec | `MisaEInvoiceProviderTests.cs` | PENDING |
| 12 | W3-T12 | Add tests: verify `appid` in auth body, `SignType: 2` in create payload, token expiry 15 days | `MisaEInvoiceProviderTests.cs` | PENDING |

### Entry criteria
- [ ] Wave 2 merged
- [ ] Viettel provider + tests complete
- [ ] MISA meInvoice API docs reviewed (`doc.meinvoice.vn/itg/`)
- [ ] Wave 0 task 5 complete: MISA status/cancel endpoints confirmed

### Exit criteria
- [ ] Auth: `POST /api/integration/auth/token` with `{appid, taxcode, username, password}`
- [ ] Create: `POST /api/integration/invoice` with `{SignType: 2, InvoiceData: [...]}`
- [ ] Token expiry: 15 ngày
- [ ] Build: 0 errors
- [ ] All MISA tests pass with real API spec mocks

### Why third (SWAPPED from original Phase 5)
- Hoàn tất toàn bộ code rewrite trước khi verify sandbox
- Verify sandbox 1 lần cho cả 2 provider — tiết kiệm thời gian chờ credentials
- MISA là backup provider, priority thấp hơn Viettel

---

## 5. WAVE 4 — Sandbox Runtime Verification

**Branch:** `feature/einvoice-rewrite-wave4-sandbox-verify`
**Estimated sessions:** 1-2 (plus 1-2 tuần chờ credentials — Wave 0)
**Conflict risk:** LOW
**Priority:** 4
**Task Card:** `docs/AI/tasks/wave4_einvoice_sandbox_verify_task_card.md`

> **NOTE:** Wave 4 (cũ Wave 5) đã được ĐẢO VỊ TRÍ xuống cuối.
> Lý do đảo: Chờ credentials là bottleneck dài nhất — làm song song với code rewrite (Wave 0 + Wave 1-3).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Create sandbox integration test project/config | `6_Tests/VanAn.Integration.Tests/Services/EInvoiceSandboxTests.cs` | PENDING |
| 2 | W4-T2 | Verify `scripts/ci-full.ps1` excludes `Category=Sandbox` tests | `scripts/ci-full.ps1` | PENDING |
| 3 | W4-T3 | Configure sandbox credentials (user-secrets, KHÔNG commit) | Local config | PENDING |
| 4 | W4-T4 | Verify Viettel auth: `POST /auth/login` → receive `access_token` via Cookie | Sandbox | PENDING |
| 5 | W4-T5 | Verify Viettel create invoice (HSM): full payload → receive `invoiceNo` + `transactionID` + `reservationCode` | Sandbox | PENDING |
| 6 | W4-T6 | Verify Viettel idempotency: resend same `transactionUuid` → same invoice returned | Sandbox | PENDING |
| 7 | W4-T7 | Verify Viettel search by transactionUuid: `POST searchInvoiceByTransactionUuid` | Sandbox | PENDING |
| 8 | W4-T8 | Verify Viettel get invoice file: `POST getInvoiceRepresentationFile` → PDF/XML | Sandbox | PENDING |
| 9 | W4-T9 | Verify Viettel cancel invoice: `POST cancelTransactionInvoice` with 7 fields | Sandbox | PENDING |
| 10 | W4-T10 | Verify Viettel error handling: test real errorCodes (invalid template, wrong tax code) | Sandbox | PENDING |
| 11 | W4-T11 | Verify MISA auth: `POST /api/integration/auth/token` with appid → Bearer token | Sandbox | PENDING |
| 12 | W4-T12 | Verify MISA create invoice (SignType=2): full payload → invoice created | Sandbox | PENDING |
| 13 | W4-T13 | Document sandbox verification results + update `project_state.md` | `docs/AI/project_state.md` | PENDING |

### Entry criteria
- [ ] Wave 3 merged
- [ ] Both providers (Viettel + MISA) rewritten per real API spec
- [ ] All unit tests pass
- [ ] Wave 0 complete: Sandbox credentials received from Viettel AND MISA
- [ ] Wave 0 task 5 complete: MISA status/cancel endpoints confirmed

### Exit criteria
- [ ] Viettel: auth, create, idempotency, search, get file, cancel, error handling — all verified
- [ ] MISA: auth, create — verified
- [ ] CI excludes sandbox tests (verified `ci-full.ps1`)
- [ ] No credentials committed to repo (verify `.gitignore`)
- [ ] Sandbox test results documented
- [ ] `project_state.md` updated with verification status
- [ ] Ready for production deployment planning

### Why fourth (SWAPPED from original Phase 4)
- Bottleneck là chờ credentials (1-2 tuần) — chạy song song với Wave 1-3 code rewrite
- Verify 1 lần cho cả 2 provider — tiết kiệm thời gian
- Cần toàn bộ code rewrite xong trước khi verify có ý nghĩa

---

## 6. CROSS-WAVE CONCERNS

### Domain Protection
- **KHÔNG sửa `1_Shared/Domain.cs`** — `InvoiceItem` entity đã tồn tại (line 1752)
- Sử dụng `InvoiceItem` entity sẵn có: ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount
- `ElectronicInvoice.Items` navigation collection đã có (line 1642)
- Chỉ update `EInvoiceRequest` record (provider interface, không phải Domain)

### Provider Contract Purity
- `IEInvoiceProvider` implementations MUST remain stateless
- Token cache per instance lifetime (existing pattern — giữ nguyên)
- No business logic in providers — chỉ HTTP translation
- `GetInvoiceFileAsync` added to interface (Wave 1) — all providers must implement

### Orchestrator Layer
- `EInvoiceOrchestrator` giữ nguyên — design đã đúng
- Outbox pattern, state machine, idempotency, retry/fallback — không sửa
- **Note:** `EInvoiceRequest` được construct trong `3_CoreHub/Program.cs` line 185 (DI factory cho `IRetryPolicyService`), KHÔNG phải trong Orchestrator — Wave 1 phải update code này

### SupplierTaxCode Source
- KHÔNG có `Tenant` entity với `TaxCode` field trong Domain
- `SupplierTaxCode` likely stored trong `ProviderConfiguration.ConfigurationData` (JSON string, line 2045)
- Wave 1 task W1-T6: research + confirm source + build deserialize helper
- Per-tenant configuration, không hardcode trong `ViettelConfig`/`MisaConfig`

### Multi-Tenancy
- `SupplierTaxCode` per-tenant (không hardcode)
- Mọi API call phải gắn tenant context
- Token cache không cross-tenant (scoped lifetime)

### Testing Strategy
- **Unit tests:** Mock theo API spec thật (endpoint, method, payload, response) — impl + tests cùng wave
- **Integration tests:** Sandbox runtime verification (Wave 4)
- **CI exclusion:** Sandbox tests marked `[Trait("Category", "Sandbox")]` — `ci-full.ps1` must exclude
- **E2E tests:** Out of scope — EInvoice là backend flow, không có UI trực tiếp
- **Playwright:** DISABLED (không liên quan đến EInvoice flow)

### Regulatory Compliance
- **Viettel v2.0** (`vinvoice.viettel.vn`) — current version
- **CTS type: HSM only** — USB Token không khả thi cho SaaS multi-tenant
- **TT 152/2025/TT-BTC** — HĐĐT mandatory cho HKD
- **Segment:** HKD không máy tính tiền (HĐĐT thường qua T-VAN, không phải HĐĐT-KT-TT)

### IP Whitelist (Deployment Blocker)
- Viettel yêu cầu register IP server vào whitelist
- Cần coordinate với ops/infra team trước khi deploy production
- Sandbox có thể dùng IP động qua VPN tạm

---

## 7. APPROVAL CHECKLIST

- [ ] Master plan reviewed (v2 — 4 waves, 10 issues fixed)
- [ ] 4 task cards reviewed (Wave 1-4)
- [ ] Gap analysis report reviewed (20 Viettel gaps + 10 MISA gaps)
- [ ] `InvoiceItem` entity confirmed existing in Domain.cs (no Domain change needed)
- [ ] `ProviderConfiguration.ConfigurationData` confirmed as `SupplierTaxCode` source candidate
- [ ] `EInvoiceRequest` construction location confirmed (`Program.cs` line 185, not Orchestrator)
- [ ] `GetInvoiceFileAsync` added to `IEInvoiceProvider` interface (Wave 1)
- [ ] Wave 2+3 merged (TDD compliance — impl + tests cùng wave)
- [ ] Wave 3+4 swapped (MISA rewrite before sandbox verify)
- [ ] Wave 0 (credential request) tracked as parallel path
- [ ] CI exclusion for sandbox tests noted (Wave 4)
- [ ] Branch strategy confirmed (4 feature branches)
- [ ] Sẵn sàng implement Wave 1

---

## 8. EFFORT SUMMARY

| Wave | Description | Sessions | Bottleneck |
|---|---|---|---|
| Wave 0 | Credential request (parallel, non-code) | 0 sessions | 1-2 tuần chờ Viettel/MISA |
| Wave 1 | Update EInvoiceRequest contract + interface | 1 | None |
| Wave 2 | Rewrite Viettel provider + DTOs + tests | 1-2 | None |
| Wave 3 | Rewrite MISA provider + DTOs + tests | 1-2 | MISA status/cancel endpoints (Wave 0 task 5) |
| Wave 4 | Sandbox runtime verification | 1-2 | Wave 0 credentials |
| **Total** | | **4-7 sessions + 1-2 tuần chờ** | |

**Critical path:** Wave 0 (parallel) + Wave 1 → Wave 2 → Wave 3 → Wave 4
**Parallel path:** Wave 0 (email Viettel + MISA) bắt đầu ngay, song song với Wave 1-3
