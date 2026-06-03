# Technical Debt Ledger - Vạn An ShopERP Accounting Module

**File Location:** `5_WebApps\ShopERP\TECHNICAL_DEBT_LEDGER.md`  
**Created:** 2026-05-31  
**Purpose:** Tổng hợp workaround và technical debt cần refactor trong GĐ 2  
**Owner:** Code Review Team - Kiểm duyệt nội bộ  

---

## Danh sách Workarounds

### Tier 1: Tenant Isolation (Ưu tiên cao)

| # | File | Line | Mô tả | Ghi chú |
|---|------|------|-------|---------|
| 1 | `Components\Pages\Accounting\TransactionHistory.razor` | 187-194 | Fallback tenant hardcode | Đọc claim TenantId, nếu không có thì fallback về `00000000-0000-0000-0000-000000000001` |
| 2 | `Components\Pages\Accounting\ExpenseEntry.razor` | 211-219 | Fallback tenant hardcode | Tương tự TransactionHistory, dùng trong HandleSubmit |

#### Kế hoạch sửa Tier 1:

1. **File:** `Pages\Login.cshtml.cs`
   - **Action:** Thêm `TenantId` claim vào claims list (dòng 55-60)
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

2. **File:** `Components\Pages\Accounting\TransactionHistory.razor` và `ExpenseEntry.razor`
   - **Action:** Xóa block fallback tenant sau khi Login.cshtml.cs được cập nhật
   - **Behavior:** Nếu `TenantId` claim không có → throw hoặc hiển thị lỗi rõ ràng

---

### Tier 2: Component Binding (Ưu tiên sau Tier 1)

| # | File | Line | Mô tả | Ghi chú |
|---|------|------|-------|---------|
| 3 | `Components\Pages\Accounting\ExpenseEntry.razor` | 222-244 | JS Interop workaround | Đọc DOM value qua `vananReadElementValue` do @bind events bị drop qua ranh giới component |
| 4 | `Components\App.razor` | 18-27 | JS helper `vananReadElementValue` | Global JS function để hỗ trợ workaround #3 |

#### Kế hoạch sửa Tier 2:

1. **Điều tra root cause:**
   - Tại sao `@bind` bị drop qua ranh giới component/assembly?
   - Blazor hydration timing issue vs component lifecycle

2. **File:** `Components\Platform\DynamicFormFields.razor`
   - **Action:** Review `@bind:event="oninput"` + `@bind:after` có đủ không
   - **Consider:** Chuyển sang `@oninput` + manual state update

3. **File:** `Components\Pages\Accounting\ExpenseEntry.razor`
   - **Action:** Xóa JS interop block `vananReadElementValue`
   - **Behavior:** Dùng binding chuẩn Blazor

4. **File:** `Components\App.razor`
   - **Action:** Xóa JS helper `window.vananReadElementValue`

5. **Test:** Verify form submit với binding chuẩn, không cần JS fallback

---

## File đã sửa trong GĐ 1 (Fix triệt để)

| File | Thay đổi | Loại |
|------|----------|------|
| `1_Shared\Domain.cs` | Sửa `EndDate` calculation (AddTicks(-1)) | Fix (triệt để) |
| `3_CoreHub\Repositories\AccountingEntryRepository.cs` | Thêm `SaveChangesAsync` vào AddAsync/AddRangeAsync | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Platform\DynamicFormFields.razor` | Chuyển sang `@bind:event="oninput"` + `@bind:after` | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor` | Format vi-VN + log chẩn đoán (đã xóa) | Fix (triệt để) |

## File đã sửa trong Phase 2 (2026-05-31) - Fix 8 Remaining Test Failures

| File | Thay đổi | Loại |
|------|----------|------|
| `5_WebApps\ShopERP\VanAn.ShopERP.csproj` | Thêm `<InvariantGlobalization>false</InvariantGlobalization>` vào PropertyGroup | Fix (triệt để) |
| `3_CoreHub\Repositories\AccountingEntryRepository.cs` | `DateTime.SpecifyKind(..., DateTimeKind.Utc)` cho startDate/endDate trong `GetByTenantAndPeriodAsync` | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor` | Format amount: `InvariantCulture.Replace(",",".")` thay vì `GetCultureInfo("vi-VN")` | Fix (triệt để) |

---

## Chú thích

- **Fix (triệt để):** Đã sửa đúng root cause, không cần refactor sau
- **Workaround (Tier 1):** Tạm thời, ưu tiên sửa trong Hardening GĐ 2 (Tenant)
- **Workaround (Tier 2):** Tạm thời, ưu tiên sửa sau Tier 1 (Binding)

---

## Review Checklist

- [ ] Tier 1: TenantId claim được thêm vào Login.cshtml.cs
- [ ] Tier 1: Fallback tenant blocks đã xóa khỏi TransactionHistory.razor
- [ ] Tier 1: Fallback tenant blocks đã xóa khỏi ExpenseEntry.razor
- [ ] Tier 2: JS Interop workaround đã xóa khỏi ExpenseEntry.razor
- [ ] Tier 2: JS helper đã xóa khỏi App.razor
- [ ] Tier 2: Form binding hoạt động chuẩn không cần JS fallback
- [ ] E2E Tests: Tất cả expense-entry-flow tests PASSED

---

**Người phê duyệt:** _________________  **Ngày:** _________________
