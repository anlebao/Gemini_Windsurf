# TASK CARD: P0.3 — HÓA ĐƠN ĐIỆN TỬ ĐIỀU CHỈNH / THAY THẾ

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P0.3 — dep: sau P0.4)
> Branch: `main`
> Status: ✅ DONE — commits `34f29d26` + `47591f26` (2026-10-17) · deployed + RV L1-L3 PASS (L4 sandbox còn chờ)

> **Implementation notes (W3, 2026-10-17):**
> - Provider contract: thay vì `AdjustInvoiceAsync` riêng, chọn `EInvoiceRequest += EInvoiceAdjustmentInfo` (tail param) — vì CẢ Viettel lẫn MISA xử lý điều chỉnh/thay thế qua CÙNG endpoint tạo hóa đơn → tái dùng 100% pipeline outbox/worker/retry/idempotency, IEInvoiceProvider không đổi (test doubles không vỡ).
> - Viettel: `adjustmentType` 3=thay thế / 5=điều chỉnh + `adjustmentInvoiceType` (1=tiền, 2=thông tin — suy ra từ so sánh totals với HĐ gốc) + `originalInvoiceId` + `originalInvoiceIssueDate` + `invoiceNote`. Default HĐ gốc sửa 0→1 theo spec.
> - MISA: `ReferenceType` 1=thay thế / 2=điều chỉnh + `OrgInvSeries` (snapshot `InvoiceSeries`, fallback config) + `OrgInvNo` + `OrgInvDate` + `OrgInvTemplateNo` + `OrgInvoiceType=1`.
> - Domain (Gate 5 — approved): `InvoiceStatus += Adjusted=7/Replaced=8`, `InvoiceAdjustmentType`, 6 trường linkage nullable + `MarkAsAdjustmentOf/MarkAsAdjusted/MarkAsReplaced`, `AuditableEntityType += ElectronicInvoice=15`, `AuditLog.ForInvoiceCorrection`. Migration `20261017153433_AddElectronicInvoiceAdjustment` (PG).
> - Orchestrator: `AdjustInvoiceAsync` (guard TaxApproved + ProviderInvoiceNumber + reason bắt buộc → HĐ mới linked PendingSend + mark gốc + outbox `InvoiceCreated` + audit `Correction`/ElectronicInvoice) + `ListInvoicesAsync`.
> - Fix pipeline gap phát hiện trong W3: `submitAction` ×2 (CoreHub + ShopERP) trước đây KHÔNG gọi `MarkAsSentToProvider` → HĐ kẹt PendingSend, webhook TaxApproved luôn fail. Đã thêm mark + `SetInvoiceSeries` + save khi provider trả success.
> - UI: `InvoiceManagement.razor` — list thật (thay stub) + Điều Chỉnh/Thay Thế trên TaxApproved + modal lý do bắt buộc + override fields + ghi chú kê khai Điều 10 + badge ĐC/TT + status mới. Fix VanAModal dùng `<Body>/<Footer>` fragment (trước render rỗng).
> - Validation: `dotnet build` 0 lỗi · `guard-check.ps1` ALL PASSED · Core.Tests 82 (e-invoice scope) · ShopERP bUnit 8/8 · E2E spec `einvoice-adjust-replace.spec.ts` (chạy ở RV — Gate 4).
>
> **Deploy + RV (2026-10-17, push `47591f26`):** CI ✅ · Accounting Tests ✅ · CD Multi-VPS ✅ (image tag `47591f26`, smoke test healthy).
> - **L1:** deploy jobs all green; migration `AddElectronicInvoiceAdjustment` auto-apply qua `MigrateAsync` khi startup — ShopERP boot sạch.
> - **L2:** Gateway `/health` Healthy · ShopERP `/health` 200 · `/einvoice/invoices` → 302 Login (auth-gated). `ListInvoicesAsync` query thành công trên prod PG (không lỗi cột) → **migration đã áp dụng**.
> - **L3 Playwright** (`playwright-rv-w3.config.ts`, tenant rv-w1-books): 1 PASS — trang render + status filter có `TaxApproved/Adjusted/Replaced` (code mới live, không JS error, không error alert); 3 SKIP — tenant chưa có hóa đơn TaxApproved để bấm nút.
> - **L4:** chưa chạy — production chỉ có provider thật (Viettel/MISA), tạo HĐ TaxApproved = phát hành hóa đơn thật lên cơ quan thuế → cần sandbox credentials hoặc quyết định nghiệp vụ của user.

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
- [x] Adjust/replace đi qua `EInvoiceRequest.EInvoiceAdjustmentInfo` (tail param trên `SubmitInvoiceAsync`) — thay vì `AdjustInvoiceAsync` riêng, vì cả 2 provider dùng chung endpoint tạo HĐ → tái dùng outbox/idempotency (xem Implementation notes).
- [x] Mapping điều chỉnh/thay thế trong `ViettelDTOs` (`adjustmentType` 3/5 + `adjustmentInvoiceType` + `originalInvoiceId` + `invoiceNote`) + `MisaDTOs` (`ReferenceType` 1/2 + `OrgInv*`) — đồng bộ P0.4.
- [x] Payload validation test đảm bảo field bắt buộc đúng spec; HĐ gốc không đủ dữ liệu (thiếu `ProviderInvoiceNumber`) bị domain reject trước khi tới provider.

### 3.2 Orchestrator + domain
- [x] `IEInvoiceOrchestrator` += `AdjustInvoiceAsync(invoiceId, adjustType, reason, newData)` → bản ghi điều chỉnh mới + outbox `InvoiceCreated` + audit `Correction`/ElectronicInvoice + trạng thái gốc Adjusted/Replaced (+ `ListInvoicesAsync`).
- [x] Ghi chú kê khai Điều 10 TT 91/2026 hiển thị trong modal (`adjust-legal-note`).

### 3.3 UI (`InvoiceManagement.razor` + list)
- [x] Action "Điều chỉnh" / "Thay thế" chỉ trên HĐ `TaxApproved`: modal lý do bắt buộc + override amount/vat/customer → orchestrator → trạng thái mới + link HĐ gốc qua `OriginalInvoiceId`.
- [x] Badge ĐC/TT + trạng thái `Adjusted`/`Replaced` + status filter.

### 3.4 Tests
- [x] Core.Tests: domain transitions + reason bắt buộc + guard provider-number · orchestrator guards/flow/outbox/audit · Viettel + MISA payload mapping.
- [x] bUnit 8/8: list + actions + modal + badge + legal note.
- [x] E2E spec `einvoice-adjust-replace.spec.ts` — RV production L3 (PASS phần render; full flow SKIP chờ HĐ TaxApproved/sandbox).

## 4. DECISIONS

- Mặc định: hỗ trợ cả **điều chỉnh** (Adjust) và **thay thế** (Replace) — theo Điều 10 TT 91/2026; giao diện 1 luồng chọn loại. (Chốt chi tiết tại P0.4 khi rà mapping provider.)

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs nếu có thể (dùng factory/extension) — nếu ElectronicInvoice cần state mới (Adjusted/Replaced) → báo cáo Domain Modeling Defect, chờ approval (Gate 5) · Outbox pattern chuẩn (idempotency) · Multi-tenancy · UI Platform 100% · E2E spec (Gate 4) · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [x] Hóa đơn điều chỉnh + thay thế qua provider (MISA/Viettel) + UI + audit — code path hoàn chỉnh, payload unit-verified; end-to-end qua provider thật chờ L4 sandbox.
- [x] Trạng thái hóa đơn cập nhật + badge + lịch sử (linkage `OriginalInvoiceId`)
- [x] Hướng dẫn kê khai đúng kỳ theo TT 91/2026 (legal note trong modal)
- [x] guard + build + tests PASS · Deploy + RV L1-L3 PASS → đóng (L4 deferred — xem §7)

## 7. DEFERRED

- Hủy hóa đơn theo quy trình ngoài (đã có Cancel provider) · xử lý hóa đơn chưa gửi cơ quan thuế (điều chỉnh trước khi gửi) · tự động thông báo người mua
- RV L4 (provider sandbox): flow thật tạo HĐ → TaxApproved → điều chỉnh trên sandbox Viettel/MISA — cần credentials sandbox; verify `adjustmentType`/`ReferenceType` payload được provider chấp nhận + `InvoiceSeries` fallback khi merchant dùng nhiều ký hiệu.