# TASK CARD: P1.2 — REOPEN KỲ: PHÊ DUYỆT CẤP CAO + CẢNH BÁO REVERSAL HÀNG LOẠT

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.2)
> Branch: `main`
> Status: 📋 PLANNED

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
- [ ] `ReopenPeriodAsync` bổ sung guard: đếm **số entries sẽ bị đảo** + trả về `ReopenPreview { EntryCount, TotalAmount }` (method mới `PreviewReopenAsync` — không mutate).
- [ ] Role gate: chỉ `Owner`/`ChiefAccountant` (xác minh claim hiện tại; nếu UI đã gate → service-level guard bổ sung).
- [ ] Bắt buộc `reason` (đã có) + tùy chọn ghi tên người phê duyệt (metadata correlation).

### 3.2 UI (`PeriodClosing.razor`)
- [ ] Nút Mở khóa → **modal cảnh báo đỏ**: "Sẽ đảo {N} bút toán (tổng {X}đ) của kỳ {MM/YYYY} — toàn bộ giao dịch kỳ sẽ được bút toán đảo ngược. Hành động này KHÔNG thể hoàn tác trực tiếp." + ô lý do bắt buộc + xác nhận gõ "MỞ KHÓA".
- [ ] Ẩn/khóa nút cho role không đủ quyền (DebtAccountant đã ẩn đóng kỳ — giữ nguyên).

### 3.3 Tests
- [ ] Core.Tests: preview đếm đúng số entries · role không đủ quyền → throw · reason trống → throw · audit PeriodReopen có reason + user.
- [ ] bUnit: modal cảnh báo hiển thị số lượng + xác nhận chuỗi.

## 4. DECISIONS

- Mặc định: phê duyệt = **role Owner/ChiefAccountant** (không tạo luồng 2 bước mới — gọn, đủ chặt); nếu user muốn dual-approval 2 người → mở rộng sau.

## 5. CONSTRAINTS (governance)

- **AccountingEntry 100% immutable** — reopen vẫn dùng reversal pattern (KHÔNG sửa entries gốc) · KHÔNG đụng Domain.cs (guard service-level) · Multi-tenancy · UI Platform 100% · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Reopen yêu cầu role cấp cao + lý do + preview số lượng entries bị đảo
- [ ] Modal cảnh báo đỏ + xác nhận chuỗi
- [ ] Audit đầy đủ (PeriodReopen + reason + user)
- [ ] guard + build + tests PASS · Deploy + RV PASS → đóng

## 7. DEFERRED

- Dual-approval 2 người (Owner + ChiefAccountant cùng ký) · luồng yêu cầu mở khóa chờ duyệt (workflow request/approve) · khóa năm riêng biệt với khóa tháng