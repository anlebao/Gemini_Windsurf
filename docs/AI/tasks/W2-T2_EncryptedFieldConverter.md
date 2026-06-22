# TASK CARD: SECURITY - Wave 2 - Encrypted Field Converter (Data Protection)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement EF Core `ValueConverter<string, string>` sử dụng ASP.NET Core Data Protection API để encrypt `PhoneNumber` và `Email` của `Customer`, `Lead`, `FacebookLead` trước khi lưu vào database — và decrypt khi đọc ra. Raw SQLite sẽ chứa ciphertext, không phải PII.
- **Nghiệp vụ áp dụng:** Bảo vệ dữ liệu cá nhân (PII) của khách hàng theo yêu cầu bảo mật production. Áp dụng cho tất cả tenant — mọi hộ kinh doanh đều có dữ liệu khách hàng được encrypt.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `3_CoreHub/Infrastructure/ValueConverters/EncryptedStringConverter.cs` (TẠO MỚI)
  - `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` (SỬA — apply converter)
  - `3_CoreHub/Infrastructure/Configurations/LeadConfiguration.cs` (SỬA — apply converter)
  - `3_CoreHub/Infrastructure/Configurations/FacebookLeadConfiguration.cs` (SỬA nếu tồn tại, TẠO NẾU CHƯA)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` (SỬA — đăng ký IDataProtectionProvider)
  - `3_CoreHub/Program.cs` (SỬA — `AddDataProtection()`)
  - `5_WebApps/ShopERP/Program.cs` (SỬA — `AddDataProtection()` với cùng ApplicationName)
  - `3_CoreHub/appsettings.json` (thêm `DataProtection` section)

- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` — Customer, Lead entities không được thay đổi
  - KHÔNG sửa `VanAnDbContext.OnModelCreating` global query filters — chỉ thêm converter configs
  - KHÔNG encrypt `TenantId` hay `Id` fields — chỉ encrypt PII text fields
  - KHÔNG xóa bất kỳ existing configuration nào trong `CustomerConfiguration.cs`

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Same ApplicationName:** `AddDataProtection().SetApplicationName("VanAnEcosystem")` — PHẢI GIỐNG NHAU ở tất cả projects. Nếu khác nhau, ciphertext không decrypt được khi cross-service call
- [ ] **Key persistence:** `PersistKeysToFileSystem(new DirectoryInfo("./keys"))` trong Development. Production path từ config `DataProtection:KeyDirectory`
- [ ] **Column size migration:** Encrypted value dài hơn plain text. `PhoneNumber` từ `HasMaxLength(20)` → `HasMaxLength(500)`. `Email` từ `HasMaxLength(100)` → `HasMaxLength(500)`. Migration bắt buộc
- [ ] **Purpose string:** Mỗi field type dùng `purpose` string riêng: `"Customer.PhoneNumber"`, `"Customer.Email"`, `"Lead.PhoneNumber"` — tránh cross-field decryption
- [ ] **Nullable handling:** Nếu field null/empty → KHÔNG encrypt → return null/empty. Converter phải handle null gracefully
- [ ] **Queryability trade-off:** Sau khi encrypt, `WHERE PhoneNumber = '0901234567'` sẽ KHÔNG WORK. Tìm kiếm phải load-then-filter trong memory hoặc dùng hash index riêng. Ghi chú rõ trong code

## 5. SUCCESS CRITERIA
- [ ] **SC-1:** `dotnet build VanAn.sln` → 0 errors sau khi implement
- [ ] **SC-2:** Integration test: Insert `Customer` với `PhoneNumber = "0901234567"` → query raw SQLite → giá trị khác `"0901234567"` (là ciphertext)
- [ ] **SC-3:** Integration test: Read Customer qua EF Core → `PhoneNumber` trả về đúng `"0901234567"` (đã decrypt)
- [ ] **SC-4:** `null` PhoneNumber → không crash, lưu null vào DB, đọc ra null
- [ ] **SC-5:** Keys trong `./keys/` folder exist sau first run
- [ ] **SC-6:** Restart application (keys persist) → decrypt vẫn thành công (không tạo key mới)
- [ ] **SC-7:** Migration apply thành công trên fresh DB (`dotnet ef database update`)
- [ ] **SC-8:** Integration tests hiện tại (customer queries) không bị break

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave2-data-protection`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify không sửa Domain entities
- `build-error-analysis` — Data Protection có thể conflict với existing packages
- `sqlite-concurrency-analysis` — verify migration không break WAL mode

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `CustomerConfiguration.cs` exists tại `3_CoreHub/Infrastructure/Configurations/`
  - Fact 2: `Customer.PhoneNumber` `HasMaxLength(20)`, `Email` `HasMaxLength(100)` — sẽ cần resize lên 500
  - Fact 3: `Microsoft.AspNetCore.DataProtection` version **2.3.0 ĐÃ CÓ** trong `Directory.Packages.props` line 54 — KHÔNG cần thêm package mới
  - Fact 4: `3_CoreHub/Program.cs` dùng `AddMemoryCache()` — compatible với DataProtection
  - Fact 5: SQLite WAL mode enabled trong ShopERP Program.cs
  - Fact 6: `W2-T1` (DataProtection setup) phải complete trước — `AddDataProtection().SetApplicationName("VanAnEcosystem")` phải registered
- **Assumptions:**
  - `Microsoft.AspNetCore.DataProtection` 8.0.x available (standard ASP.NET Core package — high confidence)
  - EF Core `ValueConverter` support tương thích với existing `.ApplyConfigurationsFromAssembly()` pattern
- **Open Questions:**
  - Q1: Có data hiện có trong production DB cần migrate không? (Nếu có → cần data migration script riêng)
- **Recommended Action:** IMPLEMENT

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `CustomerConfiguration.cs` | Phone/Email search bị ảnh hưởng | Document trade-off. Existing tests có search by phone → update test expectations |
| `VanAnDbContext.cs` | Thêm IDataProtectionProvider dependency | Constructor injection — EF Core design-time tool cần handle null provider (đã có pattern này với ITenantProvider) |
| DB Migration | Column size thay đổi | Test migration trên fresh DB trước khi áp dụng existing DB |
| `ShopERP/Program.cs` | Thêm `AddDataProtection()` | Append-only, không ảnh hưởng existing |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:**
  - `EncryptedStringConverterTests.cs`: verify encrypt → không bằng original, decrypt → bằng original, null → null
- **Integration Tests:**
  - `CustomerEncryptionTests.cs`: thêm vào `6_Tests/VanAn.Integration.Tests/`
  - End-to-end EF Core test: insert, query raw, query through EF
- **E2E Tests:** Không cần E2E riêng — existing customer flow E2E đã cover

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Verify DataProtection API compatibility, plan converter design | `AddDataProtection()` trong cả 2 Program.cs. Tạo `EncryptedStringConverter.cs`. Viết unit tests. |
| **S2** | Review converter tests, plan configuration changes | Apply converter vào `CustomerConfiguration.cs`. Tạo EF Core migration. |
| **S3** | Review migration, plan Lead configs | Apply converter vào `LeadConfiguration.cs` + `FacebookLeadConfiguration.cs`. Run integration tests. Fix regressions. |

## 11. ESTIMATED EFFORT
- 3 sessions
- **DEPENDENCY:** W2-T1 (`DataProtectionSetup`) phải complete trước — `AddDataProtection` registered với `SetApplicationName("VanAnEcosystem")` trong cả CoreHub và ShopERP
- **NOTE:** `Microsoft.AspNetCore.DataProtection` 2.3.0 ĐÃ CÓ trong `Directory.Packages.props` — không cần thêm package
- **RISK LEVEL:** MEDIUM — Column resize migration có thể conflict với existing test data
- **MITIGATION:** Luôn test migration trên in-memory SQLite trước khi chạy trên file-based DB
