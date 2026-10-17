# TASK CARD: P1.2 — REOPEN KỲ: PHÊ DUYỆT CẤP CAO + CẢNH BÁO REVERSAL HÀNG LOẠT

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.2)
> Branch: `main`
> Status: ✅ **DONE + DEPLOYED + RV L1-L3 PASS (2026-10-18, `06d27d7d`+`a11543ff`) — PreviewReopenAsync + role gate + modal "MỞ KHÓA"**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **1.2** — mọi trường hợp **Mở khóa sổ (Unlock/Reopen)** để điều chỉnh hồi tố phải: (1) yêu cầu **phê duyệt cấp cao** (CEO/CFO = Owner/ChiefAccountant), (2) **cảnh báo mạnh** về việc đảo toàn bộ entries của kỳ, (3) lưu vết (đã có `AuditLog.PeriodReopen`).
- **Phát hiện review (2026-10-16):** `PeriodClosingService.ReopenPeriodAsync` chỉ cần `userId + reason` — **không có phê duyệt/role gate bắt buộc**; reopen tạo **reversal cho TOÀN BỘ entries** của kỳ (`PeriodClosingService.cs:134-142`) — rủi ro dữ liệu nếu thao tác nhầm; cần xác minh UI gate hiện tại `PeriodClosing.razor` (ai truy cập).
- **Đã xác minh code:** `IPeriodClosingService` (Validate/Close/Reopen/GetStatus) · `PeriodClosingStatusEntity` (MarkReopening/MarkReopened) · UI `PeriodClosing.razor` · roles `UserRole.Owner/ChiefAccountant` (`UserAggregate/UserRole.cs`).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 3.
- **Execution Mode:** IMPLEMENT.
- **Skills (max 3):** `period-closing-audit-trail` + `domain-integrity-validation` + `accounting-ui-implementation`.

## 3. SCOPE

### 3.1 Service (`PeriodClosingService`)
- [x] `PreviewReopenAsync` (không mutate) → `ReopenPreview { EntryCount, TotalAmount }`; `ReopenPeriodAsync` giữ reversal pattern nguyên.
- [x] Role gate: chỉ `Owner`/`ChiefAccountant`/`SystemAdmin` — guard service-level.
- [x] Bắt buộc `reason` non-empty → throw nếu trống.

### 3.2 UI (`PeriodClosing.razor`)
- [x] `VanAModal` cảnh báo đỏ: N bút toán + tổng tiền + cảnh báo không hoàn tác + ô lý do bắt buộc + xác nhận gõ "MỞ KHÓA".
- [x] Ẩn nút Mở khóa cho role không đủ quyền.

### 3.3 Tests
- [x] Core.Tests + Integration: preview đếm đúng · role không đủ quyền → throw · reason trống → throw · audit PeriodReopen.
- [x] bUnit: nút render cho Owner / ẩn cho DebtAccountant (W4-PC-13, W4-PC-13b).

## 4. DECISIONS

- Mặc định: phê duyệt = **role Owner/ChiefAccountant** (không tạo luồng 2 bước mới — gọn, đủ chặt); nếu user muốn dual-approval 2 người → mở rộng sau.

## 5. CONSTRAINTS (governance)

- **AccountingEntry 100% immutable** — reopen vẫn dùng reversal pattern (KHÔNG sửa entries gốc) · KHÔNG đụng Domain.cs (guard service-level) · Multi-tenancy · UI Platform 100% · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [x] Reopen yêu cầu role cấp cao + lý do + preview số lượng entries bị đảo
- [x] Modal cảnh báo đỏ + xác nhận chuỗi "MỞ KHÓA"
- [x] Audit đầy đủ (PeriodReopen + reason + user)
- [x] guard + build + tests PASS · [x] Deploy + RV PASS (CD `06d27d7d`/`a11543ff` + L1-L3) → đóng

## 7. DEFERRED

- Dual-approval 2 người (Owner + ChiefAccountant cùng ký) · luồng yêu cầu mở khóa chờ duyệt (workflow request/approve) · khóa năm riêng biệt với khóa tháng