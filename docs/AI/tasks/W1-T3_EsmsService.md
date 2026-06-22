# TASK CARD: NOTIFICATIONS - WAVE 1 - ESMS SMS Notification Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `EsmsNotificationService` — gọi ESMS.vn REST API để gửi SMS thực tại Việt Nam, thay thế `Task.Delay(100)` stub. Tạo interface `IEsmsService`, implementation với retry logic (1 lần sau 2s cho HTTP 5xx), phone number normalization, và DI registration.
- **Nghiệp vụ áp dụng:** Hệ thống cần gửi SMS thông báo cho khách hàng Việt Nam (OTP, xác nhận đơn hàng, cảnh báo bảo mật). ESMS.vn là provider SMS Việt Nam phổ biến hỗ trợ branded SMS (SmsType 2). Phone number từ nhiều format khác nhau cần được normalize về `84xxxxxxxxx` trước khi gửi.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đầu phiên)
  - `3_CoreHub/Services/EsmsNotificationService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/IEsmsService.cs` (TẠO MỚI)
  - `3_CoreHub/appsettings.json` (thêm `Esms` section)
  - `3_CoreHub/Program.cs` hoặc service extension (DI registration)
  - `6_Tests/VanAn.Core.Tests/Services/EsmsNotificationServiceTests.cs` (TẠO MỚI)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `INotificationService.cs` hay `NotificationService.cs` (W1-T4's job)
  - KHÔNG dùng ESMS SDK/NuGet — chỉ `HttpClient` thuần
  - KHÔNG commit real API keys/secret vào git
  - KHÔNG sửa Domain layer
  - KHÔNG thêm business logic vào Gateway
  - KHÔNG sửa `IBrevoEmailService` hay `BrevoEmailService` (W1-T2's scope)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] ESMS API endpoint: `POST https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/`
- [ ] Request body JSON: `{ "ApiKey": "{Esms:ApiKey}", "Content": "{message}", "Phone": "{normalizedPhone}", "SecretKey": "{Esms:SecretKey}", "SmsType": "2", "Brandname": "{Esms:BrandName}" }`
- [ ] SmsType `"2"` = branded SMS (OTP/notification) — không dùng SmsType khác
- [ ] Phone normalization: `0901234567` → `84901234567`, `+84901234567` → `84901234567`, `84901234567` → `84901234567` (giữ nguyên)
- [ ] Retry policy: 1 lần retry sau 2 giây CHỈ khi HTTP 5xx (server error). KHÔNG retry HTTP 4xx (client error)
- [ ] Unicode/UTF-8: ESMS tự xử lý, không cần special encoding
- [ ] Log response code cho mỗi request (success và error)
- [ ] API keys KHÔNG commit vào git — placeholder `"__REPLACE__"` trong `appsettings.json`
- [ ] `IHttpClientFactory` (không `new HttpClient()`)
- [ ] HTTP timeout: 15 giây
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** Unit test `SendAsync_Success_ShouldReturnTrue` — mock HTTP 200 → returns `true`
- [ ] **SC-2:** Unit test `SendAsync_ApiError4xx_ShouldReturnFalse_NoRetry` — mock HTTP 400 → returns `false`, HTTP client được gọi đúng 1 lần (không retry)
- [ ] **SC-3:** Unit test `SendAsync_ApiError5xx_ShouldRetry_ThenReturnFalse` — mock HTTP 500 → HttpClient được gọi 2 lần (original + 1 retry), returns `false`
- [ ] **SC-4:** Unit test `NormalizePhone_VariousFormats_ShouldNormalizeTo84Format` — test 3 input formats: `0901234567`, `+84901234567`, `84901234567` → tất cả → `"84901234567"`
- [ ] **SC-5:** Unit test `SendAsync_ShouldSendCorrectRequestBody` — verify JSON body có đủ `ApiKey`, `Content`, `Phone`, `SecretKey`, `SmsType`, `Brandname`
- [ ] **SC-6:** `dotnet test 6_Tests/VanAn.Core.Tests/` → tất cả new tests PASS
- [ ] **SC-7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-8:** `guard-check.ps1` exits 0
- [ ] **SC-9 (Optional smoke test):** Với real ESMS API key → SMS nhận trên số điện thoại test trong vòng 30 giây

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave1-notifications`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — xử lý compile errors liên quan đến HttpClient/retry logic
- `domain-integrity-validation` — xác nhận service nằm đúng layer, không vi phạm boundaries
- `pattern-based-fixing` — retry pattern phải consistent với standards của project

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `INotificationService` có `SendSMSAsync(phoneNumber, message)` — KHÔNG sửa
  - `NotificationService.cs` `SendSMSAsync` chỉ `Task.Delay(100)` — KHÔNG sửa (W1-T4)
  - ESMS API endpoint: `POST https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/`
  - SmsType `"2"` = branded SMS
  - Phone VN format cần normalize: `0901234567` hoặc `+84901234567` → `84xxxxxxxxx`
  - Retry: 1 lần sau 2s, chỉ cho HTTP 5xx
  - `3_CoreHub` là pure Class Library — không có Exe output
  - xUnit + FluentAssertions trong test project
- **Assumptions:**
  - ESMS response body chứa field `CodeResult` (hoặc tương đương) để check success — cần verify với ESMS docs
  - DI registration pattern giống với `BrevoEmailService` (W1-T2)
  - `ILogger<EsmsNotificationService>` available qua DI
- **Open Questions:**
  - ESMS response JSON schema chính xác là gì? Field nào để check success/failure? → Theo ESMS.vn docs, response có `CodeResult: "100"` = success. Implement với check `CodeResult == "100"`.
- **Recommended Action:** IMPLEMENT

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/IEsmsService.cs` (mới) | Không ảnh hưởng existing code | N/A |
| `3_CoreHub/Services/EsmsNotificationService.cs` (mới) | Không ảnh hưởng existing code | N/A |
| `3_CoreHub/appsettings.json` | Thêm `Esms` section bên cạnh `Brevo` section (W1-T2) | Merge cẩn thận với W1-T2 changes nếu chạy song song. Đảm bảo JSON valid. |
| DI registration | `IEsmsService` mới trong container | Không ảnh hưởng existing services |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests — EsmsNotificationServiceTests (minimum 4 cases):**
  1. `SendAsync_Success_ShouldReturnTrue` — mock `HttpMessageHandler` trả về HTTP 200 với body `{"CodeResult":"100"}` → returns `true`
  2. `SendAsync_ApiError4xx_ShouldReturnFalse_NoRetry` — mock HTTP 400 → returns `false`, mock được gọi đúng 1 lần
  3. `SendAsync_ApiError5xx_ShouldRetry_ThenReturnFalse` — mock HTTP 503 → mock được gọi 2 lần (verify retry), returns `false`. Dùng `Task.Delay(2000)` mock để verify timing (hoặc mock `Task.Delay` nếu cần speed up)
  4. `NormalizePhone_ShouldHandleThreeFormats` — test `"0901234567"` → `"84901234567"`, `"+84901234567"` → `"84901234567"`, `"84901234567"` → `"84901234567"` (normalize method có thể public/internal để test trực tiếp)
  5. (Bonus) `SendAsync_ShouldIncludeAllRequiredFieldsInBody` — capture serialized request body, verify `ApiKey`, `Content`, `Phone`, `SecretKey`, `SmsType="2"`, `Brandname` đều có

- **Note về retry timing:** Trong unit tests, dùng `CancellationToken` với short timeout hoặc inject `IDelayProvider` (testable) để không phải wait 2 giây thực trong test. Nếu không muốn abstract delay, có thể skip timing assertion và chỉ verify call count.

- **Integration Tests:** Manual smoke test với real API key (không auto-run trong CI).
- **E2E Tests:** Không áp dụng trực tiếp trong Wave 1. SMS E2E test phụ thuộc vào business flow triggering SMS.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | (1) Xác nhận ESMS response format (CodeResult field). (2) Xác nhận DI pattern từ W1-T2 để dùng cùng convention. (3) Thiết kế phone normalization logic cho 3 input formats. | (1) Tạo `IEsmsService.cs`: `Task<bool> SendSmsAsync(string phoneNumber, string message)`. (2) Tạo `EsmsNotificationService.cs`: phone normalization + JSON build + HttpClient POST + retry 5xx + log + return bool. (3) Thêm `Esms` section vào `appsettings.json` với placeholders. (4) DI registration. (5) Viết `EsmsNotificationServiceTests.cs` với 4+ cases. (6) `dotnet build VanAn.sln`. (7) `dotnet test 6_Tests/VanAn.Core.Tests/`. (8) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~35 phút)
- **DEPENDENCY:** Không có hard dependency. Có thể làm song song với W1-T2 (BrevoEmailService). Cả 2 nên complete trước W1-T4 (NotificationService wiring).
- **BLOCKS:** W1-T4 (NotificationService sẽ depend on `IEsmsService` để delegate SMS calls)
