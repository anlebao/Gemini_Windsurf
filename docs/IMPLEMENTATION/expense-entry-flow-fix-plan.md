# Coding Plan - Fix Expense Entry Flow & Technical Debt Management

**Date:** 2026-05-31
**Objective:** Đưa test `expense-entry-flow.spec.ts` về PASSED, đóng gói technical debt, lập kế hoạch hardening GĐ 2.

---

## Bước 1: Xanh Test (Verify Format Fix)

**Mục tiêu:** Đưa test `expense-entry-flow.spec.ts` về PASSED, xác nhận format `500.000 ₫` (vi-VN).

### B1.1: Kiểm tra log ROW amount
- **File:** `5_WebApps\ShopERP\bin\Debug\net8.0\Logs\*.txt`
- **Action:** Đọc log mới nhất, tìm dòng `ROW amount raw=... formatted='...'`
- **Verify:** `formatted` phải là `500.000` (dấu chấm phân cách hàng nghìn)
- **Command:**
  ```powershell
  Select-String -Path "bin\Debug\net8.0\Logs\*.txt" -Pattern "ROW amount" | Select-Object -Last 3
  ```

### B1.2: Nếu format sai — điều tra
- **Check 1:** Kiểm tra `VanAn.ShopERP.runtimeconfig.json` có `InvariantGlobalization` không
  ```powershell
  Get-Content "bin\Debug\net8.0\VanAn.ShopERP.runtimeconfig.json" | Select-String -Pattern "Invariant|Globalization"
  ```
  - **Nếu phát hiện InvariantGlobalization: true:** Phải cấu hình sửa lại thành false ngay trong file dự án chính `5_WebApps\ShopERP\VanAn.ShopERP.csproj` (thêm `<InvariantGlobalization>false</InvariantGlobalization>`), không chỉ kiểm tra file runtimeconfig sinh tự động.
- **Check 2:** Verify Razor component đã recompile (`TransactionHistory.razor` change deployed)
  ```powershell
  (Get-Item "bin\Debug\net8.0\VanAn.ShopERP.dll").LastWriteTime
  (Get-Item "Components\Pages\Accounting\TransactionHistory.razor").LastWriteTime
  ```
- **Check 3:** Nếu culture fallback, điều tra tại sao `GetCultureInfo("vi-VN")` không hoạt động

### B1.3: Đưa test về PASSED
- **Command:**
  ```powershell
  npx playwright test e2e-tests/expense-entry-flow.spec.ts -g "should create expense entry with vendor info" --project=e2e-tests --reporter=line
  ```
- **Verify:** Exit code 0, output shows `1 passed`

### B1.4: Cập nhật Playwright spec kiểm tra định dạng tiền tệ (Assertion Gate)
- **File:** `6_Testing\e2e-tests\expense-entry-flow.spec.ts`
- **Action:** Thêm assertion tường minh kiểm tra dấu chấm phân cách theo chuẩn vi-VN trên lưới lịch sử giao dịch
- **Code mẫu:**
  ```typescript
  // Đảm bảo lưới hiển thị đúng dấu chấm phân cách theo chuẩn vi-VN
  const amountCell = page.locator('[data-testid="transaction-row"]').first().locator('.amount-cell');
  await expect(amountCell).toHaveText(/500\.000/);
  ```
- **Lưu ý:** Nếu chưa có `data-testid="transaction-row"` hoặc `.amount-cell`, cần thêm vào `TransactionHistory.razor` trước khi viết assertion

---

## Bước 2: Đóng gói Technical Debt Ledger

**Mục tiêu:** Gỡ log chẩn đoán, đóng dấu workaround, tổng hợp file đã sửa.

### B2.1: Gỡ log chẩn đoán ROW amount
- **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor`
- **Action:** Xóa block `foreach` log `ROW amount raw=... formatted='...'` (dòng 213-217)
- **Reason:** Log chỉ dùng cho chẩn đoán format, không cần production
- **Code to remove:**
  ```csharp
  foreach (var t in transactions)
  {
      Logger.LogInformation("ROW amount raw={Raw} formatted='{Fmt}'",
          t.Amount, t.Amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")));
  }
  ```

### B2.2: Đóng dấu [TECH DEBT] JS Interop workaround
- **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\ExpenseEntry.razor`
- **Action:** Thêm comment trước block JS interop (dòng ~219-239)
- **Comment mẫu:**
  ```csharp
  // TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
  // Workaround JS Interop đọc DOM do rớt binding ranh giới Component.
  // Sẽ refactor sang cơ chế Event Callback chuẩn sau khi Baseline E2E ổn định.
  ```

### B2.3: Đóng dấu [TECH DEBT] fallback tenant
- **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor`
- **Action:** Thêm comment trước block fallback tenant (dòng 187-191)
- **Comment mẫu:**
  ```csharp
  // TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
  // Fallback tenant hardcode vì login demo không set TenantId claim.
  // Sửa triệt để: thêm claim TenantId khi login, xóa fallback.
  ```

### B2.4: Tổng hợp file đã sửa vào Technical Debt Ledger
- **File mới:** `5_WebApps\ShopERP\TECHNICAL_DEBT_LEDGER.md` (BẮT BUỘC nằm tại thư mục gốc phân hệ Kế toán để Code Review dễ truy vết)
- **Nội dung:**
  - Danh sách file đã sửa (path + mô tả thay đổi)
  - Mỗi workaround được đánh dấu [TECH DEBT]
  - Phân loại: Tier 1 (Tenant - ưu tiên cao), Tier 2 (Binding - ưu tiên sau)
- **Quy định:** File này phải được ghim vào quy trình kiểm duyệt nội bộ, không được lãng quên

---

## Bước 3: Lập kế hoạch Hardening GĐ 2 (Tiered Remediation)

### Tier 1: Sửa triệt để Tenant (Ưu tiên cao)
**Lý do:** Ảnh hưởng trực tiếp Data Isolation — quan trọng cho hệ thống kế toán đa tenant.

**Kế hoạch:**

1. **File:** `5_WebApps\ShopERP\Pages\Login.cshtml.cs`
   - **Action:** Thêm `TenantId` claim vào `claims` list (dòng 55-60)
   - **Source:** Mapping user → tenant (hardcode demo hoặc query DB)
   - **Code:**
     ```csharp
     var claims = new List<Claim>
     {
         new Claim(ClaimTypes.Name, Username),
         new Claim(ClaimTypes.Role, role.ToString()),
         new Claim("DisplayName", GetDisplayName(role)),
         new Claim("TenantId", GetTenantIdForUser(Username)) // TODO: Implement mapping
     };
     ```

2. **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor`
   - **Action:** Xóa block fallback tenant (dòng 187-191)
   - **Behavior:** Nếu `TenantId` claim không có → throw hoặc hiển thị lỗi rõ ràng

3. **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\ExpenseEntry.razor`
   - **Action:** Xóa fallback tenant `...01` (nếu có)
   - **Behavior:** Dùng claim TenantId từ `AuthenticationStateProvider`

4. **Test:** Verify multi-tenant isolation (khác tenant không thấy nhau)

### Tier 2: Sửa triệt để Binding (Ưu tiên sau)
**Lý do:** JS Interop workaround đã ổn định cho user thật, không chặn E2E baseline.

**Kế hoạch:**

1. **Điều tra root cause:**
   - Tại sao `@bind` bị drop qua ranh giới component/assembly?
   - Blazor hydration timing issue vs component lifecycle

2. **File:** `5_WebApps\ShopERP\Components\Platform\DynamicFormFields.razor`
   - **Action:** Review `@bind:event="oninput"` + `@bind:after` có đủ không
   - **Consider:** Chuyển sang `@oninput` + manual state update

3. **File:** `5_WebApps\ShopERP\Components\Pages\Accounting\ExpenseEntry.razor`
   - **Action:** Xóa JS interop block `vananReadElementValue`
   - **Behavior:** Dùng binding chuẩn Blazor

4. **File:** `5_WebApps\ShopERP\Components\App.razor`
   - **Action:** Xóa JS helper `window.vananReadElementValue`

5. **Test:** Verify form submit với binding chuẩn, không cần JS fallback

---

## Thứ tự thực hiện
1. B1 → B2 → B3 (theo chiến lược user)
2. Trong B3: Tier 1 (Tenant) trước, Tier 2 (Binding) sau

---

## File đã sửa (tóm tắt)

| File | Thay đổi | Loại |
|------|----------|------|
| `5_WebApps\ShopERP\Components\App.razor` | Thêm JS helper `vananReadElementValue` | Workaround (Tier 2) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\ExpenseEntry.razor` | JS interop fallback + inject IJSRuntime | Workaround (Tier 2) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor` | Fallback tenant + format vi-VN + log chẩn đoán | Workaround (Tier 1) + Fix |
| `1_Shared\Domain.cs` | Sửa `EndDate` calculation (AddTicks(-1)) | Fix (triệt để) |
| `3_CoreHub\Repositories\AccountingEntryRepository.cs` | Thêm `SaveChangesAsync` vào AddAsync/AddRangeAsync | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Platform\DynamicFormFields.razor` | Chuyển sang `@bind:event="oninput"` + `@bind:after` | Fix (triệt để) |

---

## Chú thích
- **Fix (triệt để):** Đã sửa đúng root cause, không cần refactor sau
- **Workaround (Tier 1):** Tạm thời, ưu tiên sửa trong Hardening GĐ 2 (Tenant)
- **Workaround (Tier 2):** Tạm thời, ưu tiên sửa sau Tier 1 (Binding)

---

## Phase 2 — Fix 8 Remaining Test Failures (2026-05-31)

**Nguồn:** Playwright report `playwright-report.json` lúc 12:51 UTC (trước khi apply P1/P2/P3).

### Phân tích root cause

**Failure Group A (6 tests):** `text=500.000 ₫` không visible tại `line 41`
- Test submit thành công (pass success alert), navigate sang `/accounting/history`, entry không hiện.
- **Nguyên nhân 1:** `InvariantGlobalization` chưa được set → `CultureInfo.GetCultureInfo("vi-VN")` fallback → format dùng dấu phẩy thay dấu chấm → `500.000` không match.
- **Nguyên nhân 2:** Query `GetByTenantAndPeriodAsync` lọc theo `CreatedAt` (UTC) so với `period.StartDate`/`EndDate` (unspecified kind) → lệch timezone có thể miss entry.

**Failure Group B (2 tests — firefox, Mobile Chrome):** `.vanan-alert__message` vendor error không visible tại `line 61`
- `selectOption` không trigger Blazor binding kịp trước submit → `category` = `""` → vendor validation không trigger.
- **Fix P2 đã apply:** `@bind:event="onchange"` explicit cho `<select>` trong `DynamicFormFields.razor`. Cần rebuild để verify.

---

### Step 1 — Fix InvariantGlobalization

**File:** `5_WebApps\ShopERP\VanAn.ShopERP.csproj`
**Action:** Thêm vào `<PropertyGroup>`:
```xml
<InvariantGlobalization>false</InvariantGlobalization>
```
**Verify trước:**
```powershell
Get-Content "5_WebApps\ShopERP\bin\Debug\net8.0\VanAn.ShopERP.runtimeconfig.json" | Select-String "Invariant"
```
- Nếu output trống → Step 1 là fix ưu tiên cao nhất.
- Nếu `InvariantGlobalization: true` → confirm cần fix.

---

### Step 2 — Fix UTC/Local DateTimeKind mismatch trong Repository

**File:** `3_CoreHub\Repositories\AccountingEntryRepository.cs`
**Method:** `GetByTenantAndPeriodAsync` (line ~108-120)
**Action:** Chuẩn hóa `startDate`/`endDate` sang UTC trước khi query:
```csharp
var startDate = DateTime.SpecifyKind(period.StartDate, DateTimeKind.Utc);
var endDate = DateTime.SpecifyKind(period.EndDate, DateTimeKind.Utc);
```
**Lý do:** `AccountingEntry.CreatedAt` lưu `DateTime.UtcNow`, nhưng `AccountingPeriod.StartDate` là `new DateTime(Year, Month, 1)` — kind = `Unspecified`. SQLite/EF Core có thể so sánh sai kind → miss entries.

---

### Step 3 — Fix format vi-VN fallback trong TransactionHistory

**File:** `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor`
**Action:** Thay thế format expression bằng string replace an toàn:
```razor
@* BEFORE *@
@item.Amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) ₫

@* AFTER *@
@item.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") ₫
```
**Lý do:** Không phụ thuộc vào `vi-VN` culture availability — luôn dùng `InvariantCulture` (dấu phẩy) rồi replace sang dấu chấm.

---

### Step 4 — Rebuild & Verify

**Command kiểm tra P2 (vendor validation):**
```powershell
npx playwright test e2e-tests/expense-entry-flow.spec.ts -g "should require vendor" --project=chromium --reporter=line
```

**Command chạy đầy đủ:**
```powershell
npx playwright test e2e-tests/expense-entry-flow.spec.ts --reporter=line
```
**Expected:** 0 failed (10 passed across all browsers).

---

### Thứ tự thực hiện

| Step | File | Change | Rủi ro |
|------|------|--------|--------|
| **1** | `VanAn.ShopERP.csproj` | Thêm `InvariantGlobalization=false` | Thấp |
| **2** | `AccountingEntryRepository.cs` | `SpecifyKind(Utc)` cho startDate/endDate | Thấp |
| **3** | `TransactionHistory.razor` | Replace format expression | Thấp |
| **4** | *(rebuild + retest)* | — | — |

### Fixes đã apply trước đó (session trước)

| Fix | File | Change | Status |
|-----|------|--------|--------|
| **P1** | `ExpenseEntry.razor:31,36` | `Type=` → `Variant=` trên `VanAAlert` | ✅ Applied |
| **P2** | `DynamicFormFields.razor:43` | Thêm `@bind:event="onchange"` cho `<select>` | ✅ Applied |
| **P3** | `TransactionHistory.razor:106` | Thêm `data-testid="datagrid-cell-amount"` | ✅ Applied |
