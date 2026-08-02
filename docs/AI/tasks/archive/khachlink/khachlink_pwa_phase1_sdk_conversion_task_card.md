# TASK CARD: PWA-OFFLINE - Phase 1 - Project SDK Conversion (Blazor Server → WASM)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Convert `VanAn.KhachLink.csproj` từ Blazor Server sang Blazor WebAssembly. Build PASS + online smoke test PASS (không thay đổi behavior online).
- **Nghiệp vụ áp dụng:** Foundation cho Phase 2-6 (offline capability). Không có giá trị user-facing riêng.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (requires Tech Lead approval — architecture change)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/KhachLink/VanAn.KhachLink.csproj` (SDK change)
  - `5_WebApps/KhachLink/Program.cs` (DI + host builder)
  - `5_WebApps/KhachLink/Components/App.razor` (blazor.web.js → blazor.webassembly.js)
  - `5_WebApps/KhachLink/Components/Routes.razor` (router config)
  - `5_WebApps/KhachLink/_Imports.razor`
  - All 13 Pages/*.razor (remove `@rendermode InteractiveServer`)
  - `5_WebApps/KhachLink/Services/*.cs` (audit HttpContext usage)
- **Boundary Rules:**
  - KHÔNG thay đổi Gateway/ShopERP/Domain.
  - KHÔNG thêm offline logic (Phase 2-4).
  - KHÔNG thay đổi public API.
  - **Hard Stop:** Nếu phát hiện `HttpContext` usage không thay thế được → STOP + report.

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **WASM SDK:** `Microsoft.NET.Sdk.BlazorWebAssembly` (thay `Microsoft.NET.Sdk.Web`).
- [ ] **Host builder:** `WebAssemblyHostBuilder.CreateDefault(args)` (thay `WebApplication.CreateBuilder`).
- [ ] **HttpClient:** Register `HttpClient` trực tiếp trong DI (thay `IHttpClientFactory`).
- [ ] **`<HeadOutlet />`:** GIỮ NGUYÊN (WASM vẫn cần cho `PageTitle`).
- [ ] **`blazor.webassembly.js`:** Thay `blazor.web.js`.
- [ ] **`@rendermode`:** Remove từ tất cả 13 Pages.
- [ ] **HttpContext audit:** Không có `HttpContext`/`IHttpContextAccessor` usage trong WASM.
- [ ] **appsettings.json:** WASM tự load config qua `WebAssemblyHostBuilder.CreateDefault`.
- [ ] **guard-check.ps1 + dotnet build PASS.**

## 5. SUCCESS CRITERIA
- [ ] SC1: `VanAn.KhachLink.csproj` SDK = `Microsoft.NET.Sdk.BlazorWebAssembly`.
- [ ] SC2: `Program.cs` dùng `WebAssemblyHostBuilder.CreateDefault` + `AddInteractiveWebAssemblyComponents`.
- [ ] SC3: `App.razor` load `blazor.webassembly.js` + giữ `<HeadOutlet />`.
- [ ] SC4: 0 Pages còn `@rendermode InteractiveServer`.
- [ ] SC5: 0 `HttpContext`/`IHttpContextAccessor` usage trong KhachLink.
- [ ] SC6: `dotnet build VanAn.KhachLink.csproj` PASS (0 errors).
- [ ] SC7: `dotnet build VanAn.sln` PASS.
- [ ] SC8: Online smoke test: all 13 pages render, navigation works.
- [ ] SC9: Cart add + checkout flow works (online).
- [ ] SC10: QR scan works (JS interop `vananPWA.*` + html5-qrcode).
- [ ] SC11: guard-check.ps1 PASS.
- [ ] SC12: KhachLinkStartupTests PASS (hoặc rewrite nếu cần).

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-wasm`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — convert sẽ sinh build errors, cần pattern-based fix
- `domain-integrity-validation` — verify không break Domain layer
- `pattern-based-fixing` — fix build errors theo pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: csproj dùng `Microsoft.NET.Sdk.Web` (server SDK)
  - Fact 2: Program.cs dùng `WebApplication.CreateBuilder` + `AddInteractiveServerComponents`
  - Fact 3: App.razor load `blazor.web.js`
  - Fact 4: 13 Pages dùng `@rendermode InteractiveServer`
  - Fact 5: `IHttpClientFactory` registered với "gateway" client
  - Fact 6: KhachLink dùng `customer_token` localStorage (không cookie auth)
  - Fact 7: Services/ directory toàn HTTP services (no EF Core direct)
  - Fact 8: `IJSRuntime` calls: `vananPWA.*`, `localStorage.getItem`, `vananPWA.getCurrentPosition`
- **Assumptions:**
  - A1: `IJSRuntime` calls work identically trong WASM (cần verify runtime).
  - A2: `appsettings.json` Gateway:BaseUrl được WASM auto-load.
- **Open Questions:**
  - Q1: Có `HttpContext` usage ẩn nào trong Services/ không? → cần audit.
  - Q2: `ForwardedHeaders` middleware (Program.cs line 159-167) — WASM không cần, remove?
  - Q3: `UseStaticFiles` + `UseRouting` + `UseAntiforgery` — WASM không có server pipeline, remove?
- **Recommended Action:** Proceed to ANALYZE (audit HttpContext + JS interop) → IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn.KhachLink.csproj | Build break nếu package references không tương thích WASM | Add `Microsoft.AspNetCore.Components.WebAssembly.Dev` |
| Program.cs | DI registration khác (HttpClient thay IHttpClientFactory) | Audit tất cả `CreateClient("gateway")` calls |
| App.razor | blazor.web.js → blazor.webassembly.js | Verify `<HeadOutlet />` giữ nguyên |
| 13 Pages | Remove @rendermode | Không có impact (WASM interactive by default) |
| Services/*.cs | Có thể break nếu dùng server-only APIs | Audit + replace |

## 9. TDD & E2E TESTING STRATEGY
- **Build verification:** `dotnet build VanAn.KhachLink.csproj` + `dotnet build VanAn.sln`.
- **KhachLinkStartupTests:** Rewrite cho WASM (bUnit TestContext hoặc DI smoke).
- **Manual smoke test:** Run app locally → navigate all 13 pages → cart → checkout → QR scan.
- **Test boundary:** No E2E Playwright yet (Phase 6).

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Audit HttpContext + JS interop + Services/ dependencies | Report findings, chốt conversion plan |
| S2 | Chốt csproj + Program.cs changes | Sửa csproj + Program.cs + App.razor + build |
| S3 | Fix build errors (pattern-based) | Resolve all build errors, build PASS |
| S4 | Remove @rendermode từ 13 Pages | Bulk edit + build |
| S5 | Online smoke test | Run app + test all pages + cart + checkout + QR |

## 12. ESTIMATED EFFORT
- 3-5 sessions. **BLOCKER:** Tech Lead approval (architecture change).
