# TASK CARD: INFRASTRUCTURE - WAVE 0 - Package Setup (JWT + BCrypt)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x và `BCrypt.Net-Next` 4.0.x vào Central Package Management (`Directory.Packages.props`), sau đó thêm `PackageReference` (không kèm version) vào 3 project files: `VanAn.Gateway.csproj`, `VanAn.ShopERP.csproj`, `VanAn.CoreHub.csproj`.
- **Nghiệp vụ áp dụng:** Nền tảng xác thực JWT cho toàn bộ Wave 0 (login, token issue, gateway validation). BCrypt phục vụ hash/verify password cho `DemoUser`.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `Directory.Packages.props` (thêm 2 package versions)
  - `2_Gateway/VanAn.Gateway.csproj` (thêm 2 PackageReference)
  - `5_WebApps/ShopERP/VanAn.ShopERP.csproj` (thêm 2 PackageReference)
  - `3_CoreHub/VanAn.CoreHub.csproj` (thêm 2 PackageReference)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file `.cs` nào trong task này
  - KHÔNG thêm version attribute vào `<PackageReference>` trong `.csproj` (vi phạm Central Package Management)
  - KHÔNG thêm packages vào các project khác ngoài 3 projects đã liệt kê
  - KHÔNG sửa `global.json` hay `NuGet.config`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] Central Package Management: version chỉ định tại `Directory.Packages.props`, `.csproj` chỉ dùng `<PackageReference Include="..." />` không có `Version` attribute
- [ ] `Microsoft.AspNetCore.Authentication.JwtBearer` phải là version `8.0.*` (tương thích NET 8, không dùng 2.x hay 6.x)
- [ ] `BCrypt.Net-Next` phải là version `4.0.*`
- [ ] Sau khi thêm, `dotnet build VanAn.sln` phải pass 0 errors
- [ ] `guard-check.ps1` phải PASS
- [ ] Architecture tests `6_Tests/VanAn.Architecture.Tests` vẫn 7/7 PASS (thêm package không phá kiến trúc)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `Directory.Packages.props` chứa `<PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />` và `<PackageVersion Include="BCrypt.Net-Next" Version="4.0.*" />`
- [ ] **SC-2:** 3 file `.csproj` (Gateway, ShopERP, CoreHub) mỗi file chứa cả 2 `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />` và `<PackageReference Include="BCrypt.Net-Next" />` — không có `Version` attribute
- [ ] **SC-3:** `dotnet build VanAn.sln` → Build succeeded, 0 errors
- [ ] **SC-4:** `dotnet list package` trong mỗi project hiển thị cả 2 packages được resolve đúng version
- [ ] **SC-5:** `guard-check.ps1` exits 0

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — phát hiện và fix conflict version nếu NuGet restore thất bại
- `domain-integrity-validation` — đảm bảo thêm package không vi phạm layer boundaries (Gateway/CoreHub không import Domain packages lạ)
- `system-refactor-safety` — rollback plan nếu packages không tương thích

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `Directory.Packages.props` đã có `Microsoft.AspNetCore.DataProtection` 2.3.0 và `Microsoft.AspNetCore.Authentication.Cookies` 2.3.9
  - `Directory.Packages.props` CHƯA CÓ `JwtBearer` và `BCrypt.Net-Next`
  - Project target framework: NET 8 (`net8.0`)
  - 3 projects cần thêm package: `VanAn.Gateway`, `VanAn.ShopERP` (tại `5_WebApps/ShopERP/`), `VanAn.CoreHub` (tại `3_CoreHub/`)
  - Architecture tests: 7/7 phải PASS
  - `guard-check.ps1` phải PASS sau mỗi wave
- **Assumptions:**
  - `Directory.Packages.props` nằm ở root solution (cùng cấp với `VanAn.sln`)
  - Cả 3 `.csproj` đều sử dụng Central Package Management (đã có `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` hoặc file `Directory.Packages.props` được auto-detect)
- **Open Questions:**
  - Version chính xác mới nhất của `BCrypt.Net-Next 4.0.x` trên NuGet là `4.0.3` hay `4.0.2`? → Dùng `4.0.3` (latest stable 4.x)
- **Recommended Action:** IMPLEMENT

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `Directory.Packages.props` | Tất cả projects trong solution đều thấy package mới trong catalog | Chỉ projects có `PackageReference` tường minh mới download — không ảnh hưởng project chưa opt-in |
| `VanAn.Gateway.csproj` | Gateway có thể gọi BCrypt API (không mong muốn về kiến trúc) | Gateway chỉ cần JwtBearer để validate; BCrypt ở Gateway là unnecessary nhưng không vi phạm — nếu muốn strict thì chỉ thêm JwtBearer vào Gateway |
| `VanAn.CoreHub.csproj` | CoreHub có thể hash passwords | Đúng nghiệp vụ — CoreHub chứa business logic kể cả auth services |
| `VanAn.ShopERP.csproj` | ShopERP có thể issue JWT và verify BCrypt | Đúng — ShopERP là main Web API Host |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** Không có unit tests cho task này (chỉ là package management). Validation qua `dotnet build`.
- **Integration Tests:** `dotnet restore VanAn.sln` sau khi thay đổi để xác nhận packages resolve thành công.
- **E2E Tests:** Không áp dụng cho task infrastructure này. E2E tests được kích hoạt ở W0-T7 sau khi feature hoàn chỉnh.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | Đọc `Directory.Packages.props` hiện tại để xác định format. Đọc 1 trong 3 `.csproj` để xác nhận CPM đang dùng. | (1) Thêm 2 `<PackageVersion>` vào `Directory.Packages.props`. (2) Thêm 2 `<PackageReference>` vào `VanAn.Gateway.csproj`. (3) Thêm vào `VanAn.CoreHub.csproj`. (4) Thêm vào `VanAn.ShopERP.csproj`. (5) Chạy `dotnet build VanAn.sln`. (6) Chạy `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~15 phút)
- **DEPENDENCY:** Không có dependency. Đây là task đầu tiên của Wave 0 — phải hoàn thành trước W0-T2 (JwtTokenService), W0-T3 (BCrypt login), W0-T4 (Gateway JWT config), W0-T5 (Seed BCrypt passwords).
- **BLOCKS:** W0-T2, W0-T3, W0-T4, W0-T5, W0-T6 (tất cả đều cần packages đã có)
