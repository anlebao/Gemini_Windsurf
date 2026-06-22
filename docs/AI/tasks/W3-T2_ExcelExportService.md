# TASK CARD: REPORTING - Wave 3 - Excel Export Service (EPPlus)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `IExcelExportService` với EPPlus tạo file Excel thực (`.xlsx`) cho 3 loại báo cáo: Revenue (doanh thu), Inventory (tồn kho), Customer (khách hàng) — download về được từ API endpoint. Thay thế mock bytes hiện tại trong `HKDTaxReportingService`.
- **Nghiệp vụ áp dụng:** Kế toán VN cần xuất Excel cho báo cáo thuế, kiểm kê tồn kho, và phân tích khách hàng. File Excel phải mở được bằng Microsoft Excel / LibreOffice, có VND currency formatting, header hàng đầu đậm.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `3_CoreHub/Services/IExcelExportService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/ExcelExportService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/Reports/RevenueExcelReport.cs` (TẠO MỚI)
  - `3_CoreHub/Services/Reports/InventoryExcelReport.cs` (TẠO MỚI)
  - `3_CoreHub/Services/Reports/CustomerExcelReport.cs` (TẠO MỚI)
  - `3_CoreHub/Program.cs` (thêm DI)
  - `2_Gateway/Controllers/ReportController.cs` (TẠO MỚI — Wave 3 Task 6)
  - `Directory.Packages.props` (thêm EPPlus)
  - `3_CoreHub/VanAn.CoreHub.csproj` (thêm PackageReference)
  - `3_CoreHub/Services/HKDTaxReportingService.cs` (đọc — hiểu data structures hiện có)

- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `HKDTaxReportingService.cs` — chỉ tham khảo data models
  - KHÔNG sửa `1_Shared/Domain.cs`
  - KHÔNG sửa bất kỳ existing Controller nào
  - KHÔNG sửa Database configurations

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **EPPlus License:** EPPlus 7.x dùng LGPL license — **BẮT BUỘC** set `ExcelPackage.LicenseContext = LicenseContext.NonCommercial` trong `Program.cs` nếu không có commercial license, hoặc `LicenseContext.Commercial` nếu có license key. Để `LicenseContext.NonCommercial` cho MVP.
- [ ] **VND Currency Format:** Column tiền tệ phải format `#,##0" ₫"` — đây là chuẩn kế toán VN
- [ ] **Date Range Filter:** Revenue và Customer reports phải accept `DateFrom` / `DateTo` parameters — không export all-time (có thể nhiều triệu records)
- [ ] **Tenant Isolation:** Service phải nhận `tenantId` parameter và filter data theo tenant — KHÔNG query cross-tenant
- [ ] **Max rows:** Cap ở 10,000 rows per export để tránh memory exhaustion. Log warning nếu hit cap
- [ ] **Streaming:** Dùng `MemoryStream` → `byte[]` — không write temporary files lên disk

## 5. SUCCESS CRITERIA
- [ ] **SC-1:** `IExcelExportService.ExportRevenueAsync(tenantId, from, to)` → `byte[]` length > 0
- [ ] **SC-2:** File bytes mở được bằng Excel (valid OOXML format — không corrupt)
- [ ] **SC-3:** Revenue report: Sheet "Tóm tắt" + Sheet "Chi tiết đơn hàng" — đúng 2 sheets
- [ ] **SC-4:** Revenue report: cột "Tổng tiền" có format `#,##0 ₫` (VND)
- [ ] **SC-5:** Inventory report: hàng tồn kho thấp (< `MinStock`) highlight màu đỏ nhạt
- [ ] **SC-6:** Customer report: cột loyalty tier "Bronze/Silver/Gold" có màu sắc tương ứng
- [ ] **SC-7:** Tenant isolation: export với `tenantId = A` chỉ có data của A
- [ ] **SC-8:** Unit tests: `ExcelExportServiceTests` — minimum 6 cases PASS
- [ ] **SC-9:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-10:** `GET /api/reports/export/excel?type=revenue&from=2026-01-01&to=2026-06-30` → HTTP 200, Content-Type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, Content-Disposition `attachment; filename="revenue-report.xlsx"`

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave3-report-export`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — EPPlus có thể cần .NET runtime compatibility check
- `domain-integrity-validation` — verify không vi phạm clean architecture (service không inject DbContext directly)
- `accounting-ui-implementation` — tham khảo số liệu kế toán VN hiện có

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `HKDTaxReportingService.ExportToExcelAsync()` chỉ return mock bytes — stub
  - Fact 2: `EPPlus` không có trong `Directory.Packages.props` — W0-T1 của Wave 3 sẽ add
  - Fact 3: `DashboardService.cs` exists với data aggregation methods — potential data source
  - Fact 4: `IVanAnDbContext` có `DbSet<Order>`, `DbSet<Inventory>`, `DbSet<Customer>`
  - Fact 5: `ReportController` không tồn tại trong `2_Gateway/Controllers/`
  - Fact 6: Wave 0 phải merge trước để `[Authorize(Policy="RequireTenantAccess")]` work trên ReportController
- **Assumptions:**
  - EPPlus 7.x NuGet available cho .NET 8 (standard — high confidence)
  - `DashboardService` có đủ data để populate reports mà không cần query phức tạp
- **Open Questions:**
  - Q1: Revenue report lấy data từ `Orders` hay từ `AccountingEntries`? (Recommend: Orders — đơn giản hơn, kế toán entries có thể chưa đầy đủ)
- **Recommended Action:** IMPLEMENT

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `Directory.Packages.props` | EPPlus package available toàn solution | Chỉ cần thêm vào `.csproj` của CoreHub — không auto-add toàn solution |
| `2_Gateway/Controllers/ReportController.cs` | Thêm new endpoint | Không ảnh hưởng existing controllers |
| `3_CoreHub/Program.cs` | Thêm DI registration | Append-only |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:**
  - `6_Tests/VanAn.Core.Tests/Services/ExcelExportServiceTests.cs`
  - Test với mock `IVanAnDbContext` (InMemory hoặc Moq)
  - Verify: bytes > 0, no exceptions, correct MIME-type header
- **Integration Tests:**
  - Không cần — unit tests với in-memory data đủ cho export service
- **E2E Tests:**
  - Update `6_Testing/e2e-tests/export-excel-flow.spec.ts`
  - Test: click export button → file download → verify filename + Content-Type

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Verify EPPlus 7.x API (WorksheetFill, NumberFormat, etc), plan interface | Thêm EPPlus. Tạo `IExcelExportService`. Implement `RevenueExcelReport`. Viết unit tests. |
| **S2** | Review tests, plan Inventory + Customer reports | Implement `InventoryExcelReport` + `CustomerExcelReport`. Tạo `ReportController`. Update E2E spec. |

### Rules
- EPPlus `LicenseContext` phải được set trong `Program.cs` TRƯỚC bất kỳ EPPlus API call nào
- Không hardcode tenant ID trong tests — dùng fixture tenant từ integration test setup
- File Excel phải valid — test bằng cách parse lại bytes với EPPlus sau khi generate

## 11. ESTIMATED EFFORT
- 2 sessions
- **DEPENDENCY:** Wave 0 (JWT) phải done để `ReportController` có `[Authorize]` working
- **NOTE:** EPPlus LGPL vs Commercial — với doanh nghiệp commercial, cần mua license. MVP dùng `LicenseContext.NonCommercial` được.
- **NOTE Wave 3 package task:** Trước W3-T2 cần add `EPPlus` vào `Directory.Packages.props` và `VanAn.CoreHub.csproj` (tương tự W0-T1 của Wave 0).
