# Sprint 3B — Provider Integration: Detailed Execution Plan

**Branch:** `align-consumer-phase4`  
**Mục tiêu:** Hoàn tất Sprint 3 E-Invoice — kết nối Viettel + MISA thực tế, wire DI đầy đủ, tất cả tests pass.  
**Ngày tạo:** 2026-06-14 (Restored từ audit codebase sau mất file)

---

## TRẠNG THÁI HIỆN TẠI (Verified từ codebase 2026-06-14, REVIEW_ONLY update 2026-06-18)

### ✅ ĐÃ DONE — Code thực, không phải stub (REVIEW 2026-06-18 confirmed)

| File | Nội dung thực | Ghi chú |
|------|--------------|---------|
| `ViettelEInvoiceProvider.cs` | HTTP auth (JWT cache 55'), submit, status query, cancel, healthcheck | Endpoint: `InvoiceAPI/services/createInvoice` ✅ |
| `MisaEInvoiceProvider.cs` | HTTP auth (JWT cache 55'), submit, status query, cancel, healthcheck | Endpoint: `einvoices` ✅ |
| `EInvoiceOrchestrator.cs` | CreateInvoice (Outbox transaction), GetInvoice, GetInvoiceStatus, SubmitInvoice delegate chain, ProcessWebhook | REAL ✅ |
| `InvoicePolicyService.cs` | ValidateInvoice, CanSubmit, B2B/B2C check, VAT calc (10%/8%), amount range, TT152-2025 | REAL ✅ |
| `WebhookService.cs` | Idempotency L1 (ConcurrentDictionary) + L2 (DB `ProcessedWebhookKeys`), Viettel+MISA typed DTO parsing | REAL ✅ |
| `CircuitBreakerService.cs` | Closed→Open (5 failures)→HalfOpen→Closed, 5' cooldown | REAL ✅ (NHƯNG 0% test coverage) |
| `RetryPolicyService.cs` | 3 lần retry, backoff 1s/2s/4s, logging đầy đủ | REAL ✅ |
| `WebhookDtos.cs` | ViettelWebhookDto + MisaWebhookDto | ✅ CREATED 2026-06-14 |
| `EInvoiceProviderFactory.cs` | Factory pattern lookup theo ProviderId | ✅ ĐÃ register DI (`Program.cs:128`) |
| `EInvoiceProviderRegistry.cs` | Registry đăng ký providers | ✅ ĐÃ register DI (`Program.cs:121-127`) |
| `WebhookController.cs` (Gateway) | Nhận callback POST, delegate tới `IEInvoiceOrchestrator` | REAL ✅ |
| `3_CoreHub/Program.cs` DI | ViettelConfig/MisaConfig Configure + named HttpClients + Registry + Factory + RetryPolicyService wired + Orchestrator + CircuitBreaker + EInvoiceWorker | ✅ DONE (REVIEW 2026-06-18) |
| `3_CoreHub/appsettings.json` | ViettelConfig + MisaConfig sections, `__PLACEHOLDER__` credentials | ✅ DONE (REVIEW 2026-06-18) |
| `Integration.Tests/WebhookServiceTests.cs` | 6 test REAL với DB: Viettel Approved/Rejected, MISA Approved, invalid JSON, duplicate idempotency, non-existent invoice | ✅ REAL |
| `Integration.Tests/InvoicePolicyServiceTests.cs` | 8 test REAL với DB: amount limit, VAT mismatch, TaxApproved, missing name, non-existent | ✅ REAL |
| `EInvoiceDISmokeTests.cs` | DI smoke test | ✅ EXISTS |

### ❌ CÒN LỖI / CHƯA WIRE (REVIEW 2026-06-18 — đã update, xóa items đã done)

| Vấn đề | File | Mô tả | Ưu tiên |
|--------|------|-------|---------|
| ~~TODO(F4) RetryPolicyService `Task.CompletedTask`~~ | `Program.cs` | **ĐÃ FIX** — `Program.cs:132-183` wire submitAction tới real provider + circuit breaker | ~~P0~~ DONE |
| ~~Missing DTO WebhookDtos.cs~~ | `1_Shared/DTOs/` | **ĐÃ TẠO 2026-06-14** | ~~P0~~ DONE |
| ~~Missing DI ViettelConfig/MisaConfig Configure~~ | `Program.cs` | **ĐÃ FIX** — `Program.cs:103,112` | ~~P0~~ DONE |
| ~~Missing DI named HttpClients~~ | `Program.cs` | **ĐÃ FIX** — `Program.cs:104,113` AddHttpClient "viettel"/"misa" | ~~P0~~ DONE |
| ~~Missing DI providers vào Registry~~ | `Program.cs` | **ĐÃ FIX** — `Program.cs:124-125` RegisterProvider | ~~P0~~ DONE |
| ~~Config missing appsettings.json~~ | `appsettings.json` | **ĐÃ FIX** — `appsettings.json:25-39` | ~~P1~~ DONE |
| **MISSING TEST** | `EInvoiceProviderTests.cs` | KHÔNG có HTTP mock tests cho Viettel/MISA. File chỉ chứa Registry/Factory/DTO plumbing. 9 cases S2 plan CHƯA viết. 0 MockHttp/HttpMessageHandler. | **P0** |
| **MISSING TEST** | `CircuitBreakerTests.cs` | FILE KHÔNG TỒN TẠI. `CircuitBreakerService.cs` 0% coverage. | **P0** |
| **MISSING TEST** | `EInvoiceOrchestratorTests.cs` | KHÔNG có test `CreateInvoiceAsync` (DB write + Outbox enqueue) hay `GetInvoiceAsync` flow. Chỉ có delegation tests. | **P0** |
| **STUB TEST** | `Core.Tests/WebhookServiceTests.cs` | STUB-VALIDATING — dùng `new WebhookService()` parameterless → L2 DB OFF. Test chỉ assert `Should not throw`. Comment author ghi "stub implementation". False confidence. | **P1** |
| **PARTIAL TEST** | `Core.Tests/InvoicePolicyServiceTests.cs` | Chỉ test failure path + pure logic. KHÔNG test `ValidateInvoiceAsync` happy/sad path ở unit level (bù đắp Integration.Tests). | **P1** |
| **VERIFY** | Build Release | Claim 0 errors 2026-06-14, CHƯA re-verify sau WebhookDtos.cs | **P2** |

---

## SESSION PLAN

### Session S1 — DI Wiring (P0, ~45 phút) — ✅ DONE (REVIEW 2026-06-18)

**Mục tiêu:** Wire đầy đủ ViettelProvider + MisaProvider vào DI container.

> **STATUS: ĐÃ HOÀN THÀNH** — `Program.cs:103-192` đã wire đầy đủ: Configure<ViettelConfig/MisaConfig>, AddHttpClient named "viettel"/"misa", Registry+Factory, RetryPolicyService submitAction wired tới real provider (TODO F4 đã fix), Orchestrator, CircuitBreaker, EInvoiceWorker. `appsettings.json:25-39` đã có config placeholders. Session S1 KHÔNG cần làm lại.

**Files cần sửa:**
1. `3_CoreHub/Program.cs` — thêm:
   ```csharp
   // ViettelConfig + named HttpClient
   services.Configure<ViettelConfig>(configuration.GetSection("ViettelConfig"));
   services.AddHttpClient("viettel", client => {
       client.BaseAddress = new Uri(configuration["ViettelConfig:BaseUrl"] ?? "https://sinvoice.viettel.vn/");
       client.Timeout = TimeSpan.FromSeconds(30);
   });
   
   // MisaConfig + named HttpClient
   services.Configure<MisaConfig>(configuration.GetSection("MisaConfig"));
   services.AddHttpClient("misa", client => {
       client.BaseAddress = new Uri(configuration["MisaConfig:BaseUrl"] ?? "https://api.meinvoice.vn/");
       client.Timeout = TimeSpan.FromSeconds(45);
   });
   
   // Register providers vào registry
   services.AddScoped<ViettelEInvoiceProvider>();
   services.AddScoped<MisaEInvoiceProvider>();
   
   // Wire RetryPolicyService với provider thực (FIX TODO F4)
   services.AddScoped<IRetryPolicyService>(sp => {
       var registry = sp.GetRequiredService<IEInvoiceProviderRegistry>();
       var circuitBreaker = sp.GetRequiredService<ICircuitBreakerService>();
       var db = sp.GetRequiredService<VanAnDbContext>();
       Func<ElectronicInvoiceId, CancellationToken, Task> submitAction = async (invoiceId, ct) => {
           var invoice = await db.ElectronicInvoices.FindAsync([invoiceId], ct)
               ?? throw new InvalidOperationException($"Invoice {invoiceId.Value} not found");
           var provider = registry.GetProvider(invoice.PreferredProviderId?.Value ?? "viettel");
           var request = EInvoiceRequest.FromDomain(invoice);
           if (circuitBreaker.IsOpen(provider.ProviderId))
               throw new InvalidOperationException($"Circuit breaker OPEN for {provider.ProviderId}");
           var response = await provider.SubmitInvoiceAsync(request, ct);
           if (!response.Success)
               throw new InvalidOperationException(response.ErrorMessage);
           circuitBreaker.RecordSuccess(provider.ProviderId);
       };
       return new RetryPolicyService(submitAction, sp.GetRequiredService<ILogger<RetryPolicyService>>());
   });
   ```

2. `3_CoreHub/appsettings.json` — thêm config placeholders:
   ```json
   "ViettelConfig": {
     "BaseUrl": "https://sinvoice.viettel.vn/",
     "Username": "__PLACEHOLDER__",
     "Password": "__PLACEHOLDER__",
     "TaxCode": "__PLACEHOLDER__",
     "TemplateCode": "01GTKT0/001",
     "SerialNumber": "C25TAA"
   },
   "MisaConfig": {
     "BaseUrl": "https://api.meinvoice.vn/",
     "CompanyCode": "__PLACEHOLDER__",
     "Username": "__PLACEHOLDER__",
     "Password": "__PLACEHOLDER__",
     "InvoiceSeries": "C25T"
   }
   ```

**Test gate S1:**
```powershell
dotnet build VanAn.sln --configuration Release
# Expected: 0 errors
```

---

### Session S2 — Provider Tests (P1, ~60 phút)

**Mục tiêu:** Verify `EInvoiceProviderTests.cs` pass đầy đủ với mock HTTP.

**Kiểm tra các test cases cần có:**
- `ViettelProvider_SubmitInvoice_Success` — mock `POST InvoiceAPI/services/createInvoice` → `ErrorCode="0", InvoiceNo="VT-001"`
- `ViettelProvider_SubmitInvoice_AuthFailure` — mock auth 401 → throws `InvalidOperationException`
- `ViettelProvider_GetStatus_Approved` — mock GET status → `InvoiceStatus.TaxApproved`
- `ViettelProvider_GetStatus_Rejected` — mock GET status → `InvoiceStatus.Rejected`
- `ViettelProvider_CancelInvoice` — mock POST cancel → `Success=true`
- `MisaProvider_SubmitInvoice_Success` — mock `POST einvoices` → `IsSuccess=true, InvNo="MS-001"`
- `MisaProvider_SubmitInvoice_Failure` — mock provider error → `Success=false, ErrorMessage set`
- `MisaProvider_GetStatus_Approved`
- `MisaProvider_CancelInvoice`

**Tool:** `RichardSzalay.MockHttp` (đã có trong test project)

**Test gate S2:**
```powershell
dotnet test 6_Tests\VanAn.Core.Tests --filter "Category=Unit" --configuration Release
# Expected: EInvoiceProviderTests all pass
```

---

### Session S3 — Integration Test + Circuit Breaker (P1-P2, ~45 phút)

**Mục tiêu:** Verify integration tests pass, bổ sung circuit breaker test nếu thiếu.

**Kiểm tra:**
1. `WebhookServiceTests.cs` (Integration) — Viettel Approved/Rejected, MISA Approved, idempotency
2. `EInvoiceOrchestratorTests.cs` — CreateInvoice → Outbox enqueued, GetInvoice, SubmitInvoice validation flow
3. Circuit breaker: tạo `CircuitBreakerServiceTests.cs` nếu chưa có:
   - 5 failures → State = Open
   - Open + 5' cooldown → State = HalfOpen
   - Success khi HalfOpen → State = Closed
   - `Reset()` → State = Closed, failures = 0

**Test gate S3:**
```powershell
dotnet test 6_Tests\VanAn.Integration.Tests --filter "Category=Integration&Service=Webhook" --configuration Release
dotnet test 6_Tests\VanAn.Core.Tests --filter "Service=EInvoice" --configuration Release
```

---

### Session S4 — Final Validation + Commit (P0, ~30 phút)

**Mục tiêu:** Full build + test pass → commit Sprint 3B complete.

```powershell
dotnet build VanAn.sln --configuration Release
# Expected: 0 errors

dotnet test 6_Tests\VanAn.Core.Tests --configuration Release
dotnet test 6_Tests\VanAn.Integration.Tests --configuration Release
# Expected: tất cả relevant tests pass
```

**Commit message:**
```
feat(sprint3b): complete E-Invoice provider integration (Viettel + MISA)

- Wire ViettelEInvoiceProvider + MisaEInvoiceProvider into DI (named HttpClients)
- Configure ViettelConfig + MisaConfig from appsettings
- Fix TODO(F4): RetryPolicyService submitAction wired to real provider via registry
- All EInvoiceProvider unit tests pass (mock HTTP via MockHttp)
- All WebhookService integration tests pass (Viettel/MISA/idempotency)
- CircuitBreaker state transitions verified
- Build: 0 errors Release
```

---

## AUDIT RESULT — Codebase Scan 2026-06-14

### ✅ ĐÃ IMPLEMENT ĐẦY ĐỦ (Verified, code thực)

| Layer | Files | Trạng thái |
|-------|-------|-----------|
| Domain | `ElectronicInvoice`, `InvoiceAggregate`, `InvoiceStatus`, `InvoiceRecipientType`, `ProviderId`, `ElectronicInvoiceId`, `InvoiceIdempotencyKey` | ✅ Real code |
| Orchestration | `EInvoiceOrchestrator`, `InvoicePolicyService`, `WebhookService`, `RetryPolicyService`, `ComplianceService`, `FallbackService` | ✅ Real code |
| Providers | `ViettelEInvoiceProvider`, `MisaEInvoiceProvider`, `EInvoiceProviderFactory`, `EInvoiceProviderRegistry` | ✅ Real HTTP code |
| Resilience | `CircuitBreakerService` (Closed→Open→HalfOpen, 5 failures, 5' cooldown) | ✅ Real code |
| Background | `EInvoiceWorker` (30s cycle, batch 50, dead letter queue) | ✅ Real code |
| Controllers | `HKDElectronicInvoiceController`, `WebhookController` | ✅ Real code |
| Infrastructure | `VanAnDbContext.ElectronicInvoices`, `ProcessedWebhookKey`, `ElectronicInvoiceConfiguration` | ✅ Real code |
| DTOs | `ViettelWebhookDto`, `MisaWebhookDto` | ✅ **CREATED 2026-06-14** |
| Tests (Unit) | `EInvoiceOrchestratorTests`, `InvoicePolicyServiceTests`, `WebhookServiceTests` (Core), `EInvoiceProviderTests` | ✅ 35+ test cases |
| Tests (Integration) | `WebhookServiceTests` (Integration), `EInvoiceDISmokeTests` | ✅ 12+ test cases |

### 🟡 CÒN LẠI CHO SPRINT 3B (4 items)

1. **[P0] RetryPolicyService submitAction** — hiện `Task.CompletedTask`, cần wire tới real provider
2. **[P0] ViettelConfig/MisaConfig DI** — chưa `Configure<T>()` trong `Program.cs`
3. **[P0] Named HttpClients** — `"viettel"` và `"misa"` chưa `AddHttpClient()` với BaseAddress
4. **[P0] Provider registration** — Viettel/MISA chưa register vào `EInvoiceProviderRegistry` qua DI

---

## RISK REGISTER

| Risk | Mitigation |
|------|-----------|
| `EInvoiceRequest.FromDomain()` chưa tồn tại | Tạo factory method trong domain hoặc mapping trong provider |
| `invoice.PreferredProviderId` chưa có trong domain | Dùng fallback `"viettel"` hardcoded trong S1, refactor S3 |
| ViettelConfig/MisaConfig property names mismatch với constructor | Đọc constructor param names trước khi tạo config section |
| Integration tests dùng SQLite in-memory conflict với WebhookService L2 DB check | Verify `IntegrationTestBase` có seeded ElectronicInvoice trước khi test webhook |

---

## DEFINITION OF DONE — Sprint 3B

- [ ] `dotnet build VanAn.sln --configuration Release` → **0 errors**
- [ ] `EInvoiceProviderTests.cs` — tất cả Viettel + MISA tests **pass**
- [ ] `EInvoiceOrchestratorTests.cs` — tất cả **pass**
- [ ] `InvoicePolicyServiceTests.cs` — tất cả **pass**
- [ ] `WebhookServiceTests.cs` (Core + Integration) — tất cả **pass**
- [ ] `CircuitBreakerService` — tested (unit hoặc integration)
- [ ] DI registration đầy đủ: ViettelConfig, MisaConfig, named HttpClients, providers in registry
- [ ] `RetryPolicyService` wired với real provider submit action
- [ ] Config placeholders trong `appsettings.json` (credentials = `__PLACEHOLDER__`, không commit secret)
- [ ] 1 commit clean với message chuẩn format
