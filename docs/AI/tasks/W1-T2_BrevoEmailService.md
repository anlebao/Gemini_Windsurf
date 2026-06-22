# TASK CARD: NOTIFICATIONS - WAVE 1 - Brevo Email Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `BrevoEmailService` — gọi Brevo (Sendinblue) REST API để gửi email thực thay thế cho `Task.Delay(100)` stub hiện tại. Tạo interface `IBrevoEmailService`, implementation, cấu hình DI, và thêm config keys vào `appsettings.json`.
- **Nghiệp vụ áp dụng:** Hệ thống cần gửi email thực cho các event: xác nhận đơn hàng, thông báo thanh toán, reset password. `BrevoEmailService` là provider cụ thể cho email delivery — tách biệt khỏi `INotificationService` (W1-T4 sẽ wire chúng lại).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `3_CoreHub/Services/BrevoEmailService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/IBrevoEmailService.cs` (TẠO MỚI)
  - `3_CoreHub/appsettings.json` (thêm `Brevo` section)
  - `3_CoreHub/Program.cs` (thêm DI registration)
  - `6_Tests/VanAn.Core.Tests/Services/BrevoEmailServiceTests.cs` (TẠO MỚI)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `INotificationService.cs` — interface này là của W1-T4
  - KHÔNG sửa `NotificationService.cs` — đây là W1-T4's job
  - KHÔNG dùng Brevo SDK/NuGet package — chỉ dùng `HttpClient` thuần
  - KHÔNG commit real API key vào git — dùng placeholder `__REPLACE__`
  - KHÔNG thêm business logic vào Gateway hay Domain layer
  - KHÔNG sửa Domain.cs

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] Dùng `HttpClient` thuần (không dùng Brevo SDK)
- [ ] Brevo API endpoint: `POST https://api.brevo.com/v3/smtp/email`
- [ ] Request header: `api-key: {Brevo:ApiKey}` và `Content-Type: application/json`
- [ ] Request body JSON schema: `{ "sender": { "name": "{Brevo:SenderName}", "email": "{Brevo:SenderEmail}" }, "to": [{ "email": "{toEmail}" }], "subject": "{subject}", "htmlContent": "{htmlMessage}" }`
- [ ] Error handling: catch exception + log error message, return `false` (KHÔNG throw ra ngoài)
- [ ] HTTP timeout: 10 giây (không để indefinite wait)
- [ ] API key KHÔNG commit vào git — `appsettings.json` dùng `"__REPLACE__"`, real values qua environment variables hoặc user secrets
- [ ] `HttpClient` được inject qua `IHttpClientFactory` (không `new HttpClient()`)
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** Mock `IHttpClientFactory` trong unit test → `BrevoEmailService.SendAsync()` với valid params → verify HTTP POST được gọi đến `https://api.brevo.com/v3/smtp/email` với đúng JSON body schema Brevo
- [ ] **SC-2:** Mock trả về HTTP 200 → method returns `true`
- [ ] **SC-3:** Mock trả về HTTP 400 → method returns `false` (không throw), error được logged
- [ ] **SC-4:** Mock throw `TaskCanceledException` (timeout) → method returns `false` (không throw)
- [ ] **SC-5:** Unit test `InvalidEmailFormat` — gửi email với address không có `@` → method returns `false` trước khi gọi API (validate trước)
- [ ] **SC-6:** `dotnet test 6_Tests/VanAn.Core.Tests/` → tất cả new tests PASS
- [ ] **SC-7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-8:** `guard-check.ps1` exits 0
- [ ] **SC-9 (Optional smoke test):** Với real Brevo test API key → email đến inbox trong vòng 2 phút

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave1-notifications`

## 6. ACTIVE SKILLS (MAX 3)
- `outbox-pattern-implementation` — cân nhắc nếu cần retry/delivery guarantee (không bắt buộc trong scope này)
- `build-error-analysis` — xử lý nếu `IHttpClientFactory` registration cần `AddHttpClient<>` extension
- `domain-integrity-validation` — xác nhận `BrevoEmailService` nằm đúng layer (Services, không phải Domain)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `INotificationService` tại `3_CoreHub/Services/INotificationService.cs`: methods `SendEmailAsync(email, subject, message)` và `SendSMSAsync(phoneNumber, message)` — KHÔNG sửa
  - `NotificationService.cs`: cả 2 methods chỉ `Task.Delay(100)` — KHÔNG sửa (W1-T4's job)
  - `3_CoreHub` là pure Class Library (.dll) — KHÔNG có `<OutputType>Exe</OutputType>`
  - Brevo API endpoint xác nhận: `POST https://api.brevo.com/v3/smtp/email`
  - xUnit + FluentAssertions đã có trong test project
  - Architecture constraint: 3_CoreHub MUST remain pure Class Library
- **Assumptions:**
  - `ILogger<BrevoEmailService>` có thể inject qua DI để log errors
  - `IHttpClientFactory` được registered trong `3_CoreHub/Program.cs` (hoặc host startup)
  - `appsettings.json` trong CoreHub là file config được load khi startup
- **Open Questions:**
  - `3_CoreHub` có `Program.cs` không (nó là Class Library)? DI registration cho CoreHub services được thực hiện ở đâu? → Có thể là extension method `AddCoreHubServices()` được gọi từ `5_WebApps/ShopERP/Program.cs`
- **Recommended Action:** IMPLEMENT (cần xác nhận DI registration pattern của CoreHub trước)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/IBrevoEmailService.cs` (mới) | Không ảnh hưởng existing code | N/A |
| `3_CoreHub/Services/BrevoEmailService.cs` (mới) | Không ảnh hưởng existing code | N/A |
| `3_CoreHub/appsettings.json` | Thêm Brevo section với placeholder — apps khác đọc file này có thể thấy keys mới | Placeholder `__REPLACE__` rõ ràng, không gây runtime error nếu chưa configure (method sẽ log error + return false) |
| DI registration (Program.cs hoặc ServiceExtensions) | `IBrevoEmailService` mới trong container — không ảnh hưởng existing services | Dùng `services.AddScoped<IBrevoEmailService, BrevoEmailService>()` hoặc `AddSingleton` nếu stateless |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests — BrevoEmailServiceTests (minimum 4 cases):**
  1. `SendAsync_Success200_ShouldReturnTrue` — mock `HttpMessageHandler` trả về 200 OK → verify returns `true`
  2. `SendAsync_ApiError4xx_ShouldReturnFalse_NoRetry` — mock trả về 400 Bad Request → returns `false`, không retry (HTTP 4xx không retry)
  3. `SendAsync_NetworkTimeout_ShouldReturnFalse` — mock throw `TaskCanceledException` → returns `false`, không throw ra ngoài
  4. `SendAsync_InvalidEmailFormat_ShouldReturnFalse_BeforeCallingApi` — email `"not-an-email"` → returns `false` without making HTTP call (validate input first)
  5. (Bonus) `SendAsync_ShouldSendCorrectJsonSchema` — capture request content, deserialize, verify `sender.name`, `sender.email`, `to[0].email`, `subject`, `htmlContent` fields

- **Integration Tests:** Smoke test với real API key (manual, không auto-run trong CI).
- **E2E Tests:** Không áp dụng trực tiếp. Email delivery E2E sẽ được test khi feature triggering email (ví dụ: đặt hàng → xác nhận email) được build.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 | (1) Xác nhận DI pattern của CoreHub (extension method hay direct registration). (2) Kiểm tra cách `IHttpClientFactory` được registered. (3) Đọc 1 existing service trong `3_CoreHub/Services/` để nắm coding conventions. | (1) Tạo `IBrevoEmailService.cs` với method `Task<bool> SendAsync(string toEmail, string subject, string htmlContent)`. (2) Tạo `BrevoEmailService.cs` với full implementation: validate email, build request JSON, POST với `HttpClient`, handle errors. (3) Thêm `Brevo` section vào `appsettings.json` với placeholders. |
| Session 2 | Review implementation từ Session 1 — kiểm tra error handling coverage. | (1) Register `IBrevoEmailService` trong DI. (2) Viết `BrevoEmailServiceTests.cs` với 4-5 test cases. (3) `dotnet build VanAn.sln`. (4) `dotnet test 6_Tests/VanAn.Core.Tests/`. (5) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **2 sessions** (~45 phút total)
- **DEPENDENCY:** Không có hard dependency từ Wave 0 (BrevoEmailService là standalone service). Tuy nhiên nên bắt đầu Wave 1 sau khi Wave 0 merge vào `main`.
- **BLOCKS:** W1-T4 (NotificationService wiring sẽ depend on `IBrevoEmailService`)
