# TASK CARD: P0.3 — HÓA ĐƠN ĐIỆN TỬ ĐIỀU CHỈNH / THAY THẾ

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P0.3 — dep: sau P0.4)
> Branch: `main`
> Status: � IMPLEMENTED — commit `34f29d26` (2026-10-17) · guard+build+tests PASS · chờ deploy + RV

> **Implementation notes (W3, 2026-10-17):**
> - Provider contract: thay vì `AdjustInvoiceAsync` riêng, chọn `EInvoiceRequest += EInvoiceAdjustmentInfo` (tail param) — vì CẢ Viettel lẫn MISA xử lý điều chỉnh/thay thế qua CÙNG endpoint tạo hóa đơn → tái dùng 100% pipeline outbox/worker/retry/idempotency, IEInvoiceProvider không đổi (test doubles không vỡ).
> - Viettel: `adjustmentType` 3=thay thế / 5=điều chỉnh + `adjustmentInvoiceType` (1=tiền, 2=thông tin — suy ra từ so sánh totals với HĐ gốc) + `originalInvoiceId` + `originalInvoiceIssueDate` + `invoiceNote`. Default HĐ gốc sửa 0→1 theo spec.
> - MISA: `ReferenceType` 1=thay thế / 2=điều chỉnh + `OrgInvSeries` (snapshot `InvoiceSeries`, fallback config) + `OrgInvNo` + `OrgInvDate` + `OrgInvTemplateNo` + `OrgInvoiceType=1`.
> - Domain (Gate 5 — approved): `InvoiceStatus += Adjusted=7/Replaced=8`, `InvoiceAdjustmentType`, 6 trường linkage nullable + `MarkAsAdjustmentOf/MarkAsAdjusted/MarkAsReplaced`, `AuditableEntityType += ElectronicInvoice=15`, `AuditLog.ForInvoiceCorrection`. Migration `20261017153433_AddElectronicInvoiceAdjustment` (PG).
> - Orchestrator: `AdjustInvoiceAsync` (guard TaxApproved + ProviderInvoiceNumber + reason bắt buộc → HĐ mới linked PendingSend + mark gốc + outbox `InvoiceCreated` + audit `Correction`/ElectronicInvoice) + `ListInvoicesAsync`.
> - Fix pipeline gap phát hiện trong W3: `submitAction` ×2 (CoreHub + ShopERP) trước đây KHÔNG gọi `MarkAsSentToProvider` → HĐ kẹt PendingSend, webhook TaxApproved luôn fail. Đã thêm mark + `SetInvoiceSeries` + save khi provider trả success.
> - UI: `InvoiceManagement.razor` — list thật (thay stub) + Điều Chỉnh/Thay Thế trên TaxApproved + modal lý do bắt buộc + override fields + ghi chú kê khai Điều 10 + badge ĐC/TT + status mới. Fix VanAModal dùng `<Body>/<Footer>` fragment (trước render rỗng).
> - Validation: `dotnet build` 0 lỗi · `guard-check.ps1` ALL PASSED · Core.Tests 82 (e-invoice scope) · ShopERP bUnit 8/8 · E2E spec `einvoice-adjust-replace.spec.ts` (chạy ở RV — Gate 4).

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **2.4** — xử lý hóa đơn điện tử sai sót bằng **hóa đơn điều chỉnh / hóa đơn thay thế** theo Điều 10 TT 91/2026 (kê khai đúng kỳ phát sinh; hóa đơn thay thế khi sai MST/không xác định được nghĩa vụ thuế).
- **Phát hiện review (2026-10-16):** `IEInvoiceProvider` chỉ có `SubmitInvoiceAsync / GetInvoiceStatusAsync / CancelInvoiceAsync / GetInvoiceFileAsync / HealthCheckAsync` — **KHÔNG có Adjust/Replace**; `IEInvoiceOrchestrator` không có method điều chỉnh; UI `InvoiceManagement.razor` chỉ có nút "Hủy" (modal đóng).
- **Đã xác minh code:** `Services/Providers/EInvoice/` (MisaEInvoiceProvider, ViettelEInvoiceProvider — API có khả năng điều chỉnh/thay thế phía provider) · `EInvoiceOrchestrator.cs` (CreateInvoice/Submit/Webhook — outbox async) · `ElectronicInvoice` domain (trạng thái invoice).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 2 (sau P0.4).
- **Execution Mode:** IMPLEMENT.
- **Skills (max 3):** `einvoice-integration` + `outbox-pattern-implementation` + `domain-integrity-validation`.

## 3. SCOPE

### 3.1 Provider contract
- [ ] `IEInvoiceProvider` += `Task<EInvoiceResponse> AdjustInvoiceAsync(EInvoiceAdjustRequest request, CancellationToken)` — `EInvoiceAdjustRequest { ProviderInvoiceNumber, AdjustType: Adjust|Replace, Reason, NewInvoiceData (amount/vat/customer...), OriginalInvoiceId }`.
- [ ] Implement `MisaEInvoiceProvider.AdjustInvoiceAsync` + `ViettelEInvoiceProvider.AdjustInvoiceAsync` theo API từng provider (Viettel: điều chỉnh/thay thế; MISA: adjust) — mapping trong `MisaDTOs.cs`/`ViettelDTOs.cs` (đồng bộ P0.4).
- [ ] Provider không hỗ trợ adjust → trả lỗi rõ ràng + fallback hướng dẫn (ghi chú UI).

### 3.2 Orchestrator + domain
- [ ] `IEInvoiceOrchestrator` += `AdjustInvoiceAsync(invoiceId, adjustType, reason, newData)` → tạo bản ghi điều chỉnh + outbox event + audit (`AuditActionType.Correction` — EntityType ElectronicInvoice) + cập nhật trạng thái invoice (Adjusted/Replaced).
- [ ] Ghi chú nghiệp vụ kê khai: hiển thị hướng dẫn "người bán kê khai kỳ phát sinh hóa đơn điều chỉnh/thay thế; người mua kê khai kỳ nhận" (Điều 10 TT 91/2026).

### 3.3 UI (`InvoiceManagement.razor` + list)
- [ ] Action "Điều chỉnh" / "Thay thế" trên từng hóa đơn (chỉ khi trạng thái cho phép): modal nhập lý do (bắt buộc) + dữ liệu mới (amount/vat/customer) → xác nhận → gọi orchestrator → hiển thị trạng thái mới + link hóa đơn điều chỉnh.
- [ ] Danh sách hiển thị badge "Đã điều chỉnh/Thay thế" + lịch sử điều chỉnh.

### 3.4 Tests
- [ ] Core.Tests: adjust (payload đúng + reason bắt buộc) · replace (thay đổi MST → hóa đơn mới) · provider không hỗ trợ → lỗi rõ · audit ghi Correction · outbox enqueue · kê khai kỳ (metadata).
- [ ] bUnit: modal điều chỉnh/thay thế render + badge.
- [ ] E2E spec (RV production — sandbox provider nếu có).

## 4. DECISIONS

- Mặc định: hỗ trợ cả **điều chỉnh** (Adjust) và **thay thế** (Replace) — theo Điều 10 TT 91/2026; giao diện 1 luồng chọn loại. (Chốt chi tiết tại P0.4 khi rà mapping provider.)

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs nếu có thể (dùng factory/extension) — nếu ElectronicInvoice cần state mới (Adjusted/Replaced) → báo cáo Domain Modeling Defect, chờ approval (Gate 5) · Outbox pattern chuẩn (idempotency) · Multi-tenancy · UI Platform 100% · E2E spec (Gate 4) · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Hóa đơn điều chỉnh + thay thế qua provider (MISA/Viettel) + UI + audit
- [ ] Trạng thái hóa đơn cập nhật + badge + lịch sử
- [ ] Hướng dẫn kê khai đúng kỳ theo TT 91/2026
- [ ] guard + build + tests PASS · Deploy + RV production PASS → đóng

## 7. DEFERRED

- Hủy hóa đơn theo quy trình ngoài (đã có Cancel provider) · xử lý hóa đơn chưa gửi cơ quan thuế (điều chỉnh trước khi gửi) · tự động thông báo người mua