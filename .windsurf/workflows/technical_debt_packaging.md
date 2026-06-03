---
description: Package technical debt after bug fix - mark workarounds, create ledger, plan remediation
---

# Technical Debt Packaging Workflow

**Use when:** Sau khi fix bug bằng workaround/temporary solution, cần đóng gói technical debt để refactor sau.

## 1. Identify Workarounds

// turbo
- [ ] Liệt kê tất cả workaround đã thêm trong quá trình fix
- [ ] Phân loại: Tier 1 (Critical - ảnh hưởng data/business logic) vs Tier 2 (Convenience - UX/code quality)
- [ ] Xác định root cause của mỗi workaround

## 2. Mark with TECH DEBT Comments

// turbo
- [ ] Thêm comment `// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]` trước mỗi workaround
- [ ] Format: Mô tả ngắn gọn + Lý do + Tier (1/2)

```csharp
// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
// Fallback tenant hardcode vì login demo không set TenantId claim.
// Sửa triệt để: thêm claim TenantId khi login, xóa fallback.
// Tier: 1 (Tenant - ưu tiên cao)
```

```csharp
// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
// Workaround JS Interop đọc DOM do rớt binding ranh giới Component.
// Sẽ refactor sang cơ chế Event Callback chuẩn sau khi Baseline E2E ổn định.
// Tier: 2 (Binding - ưu tiên sau Tenant)
```

## 3. Clean Diagnostic Code

// turbo
- [ ] Xóa log chẩn đoán đã thêm trong quá trình debug (console.log, Logger.LogDebug)
- [ ] Xóa temporary comments không cần thiết
- [ ] Giữ lại comment giải thích business logic quan trọng

## 4. Create TECHNICAL_DEBT_LEDGER.md

// turbo
- [ ] Tạo file tại thư mục gốc module (ví dụ: `5_WebApps\ShopERP\TECHNICAL_DEBT_LEDGER.md`)
- [ ] Nội dung bao gồm:
  - Danh sách workaround với file/line cụ thể
  - Tier classification (1/2)
  - Kế hoạch remediation chi tiết
  - Review checklist

## 5. Update Test Assertions (nếu cần)

- [ ] Thêm assertion cụ thể kiểm tra behavior đã fix
- [ ] Đảm bảo test sẽ fail nếu workaround bị xóa mà chưa có giải pháp triệt để

## 6. Verify

// turbo
- [ ] Build thành công không có warning mới
- [ ] E2E tests PASSED với workaround hiện tại
- [ ] TECH DEBT comments xuất hiện trong code review diff

---

## Output Artifacts

1. Code changes với TECH DEBT markers
2. TECHNICAL_DEBT_LEDGER.md file
3. Updated test specs (nếu có)

---

## Example: After Fix Flow

```
Bug Report → Root Cause Analysis → Workaround Fix → 
TECH DEBT Marking → Clean Diagnostics → 
Create Ledger → Verify → Done
```
