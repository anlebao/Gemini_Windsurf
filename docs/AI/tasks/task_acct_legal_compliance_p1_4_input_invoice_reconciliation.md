# TASK CARD: P1.4 — THEO DÕI HÓA ĐƠN ĐẦU VÀO + CẢNH BÁO DN BỎ TRỐN / NGỪNG HOẠT ĐỘNG

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.4)
> Branch: `main`
> Status: 📋 PLANNED

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **2.1** — phát hiện và cảnh báo sớm hóa đơn đầu vào thuộc doanh nghiệp **bỏ trốn, ngưng hoạt động, không hoạt động tại địa chỉ đăng ký** (hóa đơn không được khấu trừ/không hợp lệ chi phí); theo dõi trạng thái hóa đơn đầu vào + báo cáo đối chiếu phục vụ thanh tra.
- **Phát hiện review (2026-10-16):** hệ thống KHÔNG quản lý hóa đơn đầu vào (input invoices) — chỉ xuất hóa đơn đầu ra qua provider MISA/Viettel; không có danh sách rủi ro.
- **Đã xác minh code:** `Vendor` field trên `AccountingEntry`/phiếu chi · `ExpenseEntry.razor` có tra MST (`BusinessInfoApiClient` — Gateway `GET /api/v1/business-info/{mst}`) · `CongNoService` (331 — nhà cung cấp) · feature flags (PG) · UI Platform.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 4.
- **Execution Mode:** IMPLEMENT (module mới — nhập tay + cờ rủi ro; KHÔNG tự động hóa tra cứu cổng TCT ở phase này).
- **Skills (max 3):** `einvoice-integration` + `domain-integrity-validation` + `accounting-ui-implementation`.

## 3. SCOPE

### 3.1 Entity/Data (mới — KHÔNG phải kế toán chính, không đụng AccountingEntry)
- [ ] Bảng theo dõi hóa đơn đầu vào (PG — entity riêng, có tenant): `MST NCC`, `Tên NCC`, `Số hóa đơn`, `Ký hiệu`, `Ngày hóa đơn`, `Số tiền (chưa VAT/VAT/tổng)`, `Hình thức thanh toán` (liên kết ngưỡng 20M — P0.1), `Trạng thái: Hợp lệ | Rủi ro | Hủy/Điều chỉnh`, `Ghi chú`, `Người nhập`, `CreatedAt`.
- [ ] Danh sách rủi ro: nhập tay (từ thông báo cơ quan thuế — danh sách DN bỏ trốn/ngừng hoạt động/không hoạt động tại địa chỉ) — `MST` + trạng thái + nguồn thông báo + ngày hiệu lực.
- [ ] Migration PG (entity mới — qua workflow migration chuẩn).

### 3.2 Services (`3_CoreHub/Services/InputInvoice/`)
- [ ] `IInputInvoiceService`: CRUD hóa đơn đầu vào · `FlagRiskAsync` (tự gắn cờ Rủi ro nếu MST có trong danh sách) · `GetReviewAsync(tenantId, period)` → **Báo cáo đối chiếu**: tổng hóa đơn đầu vào, tổng ≥20M thanh toán tiền mặt (liên kết P0.1), số hóa đơn rủi ro, số bị hủy/điều chỉnh.
- [ ] Cảnh báo UI khi nhập hóa đơn của NCC nằm trong danh sách rủi ro (modal xác nhận).
- [ ] Multi-tenancy filter mọi query.

### 3.3 UI (ShopERP — UI Platform)
- [ ] Trang `/accounting/input-invoices` — danh sách + form nhập (tra MST tái dùng `BusinessInfoApiClient`) + bộ lọc trạng thái + In/Excel.
- [ ] Trang `/accounting/risk-suppliers` (Owner/SystemAdmin) — quản lý danh sách DN rủi ro.
- [ ] NavMenu "Kế toán → Hóa đơn đầu vào" + Sitemap + bUnit.

### 3.4 Tests
- [ ] Core.Tests: CRUD + tự gắn cờ rủi ro theo MST + báo cáo đối chiếu (tổng/≥20M tiền mặt/rủi ro) + isolation tenant.
- [ ] bUnit: form nhập + cảnh báo NCC rủi ro + danh sách.
- [ ] E2E spec (Gate 4 — RV): nhập hóa đơn đầu vào → báo cáo → cờ rủi ro.

## 4. DECISIONS

- Mặc định: **nhập tay + cờ rủi ro thủ công từ thông báo TCT** (MVP — không gọi API cổng TCT tự động ở phase này); nếu user cần tự động tra cứu → tách task riêng (phụ thuộc API TCT).

## 5. CONSTRAINTS (governance)

- Entity mới PHẢI qua workflow Domain/EF chuẩn (Single-Identity pattern — BaseEntity.Id, ignore business key VO) · KHÔNG đụng AccountingEntry/Domain kế toán hiện có · Multi-tenancy · UI Platform 100% · E2E spec (Gate 4) · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Nhập + theo dõi hóa đơn đầu vào + trạng thái rủi ro (DN bỏ trốn/ngừng hoạt động)
- [ ] Báo cáo đối chiếu cuối kỳ (≥20M tiền mặt + rủi ro) + In/Excel
- [ ] Cảnh báo khi nhập hóa đơn NCC rủi ro
- [ ] guard + build + tests PASS · Deploy + RV PASS → đóng

## 7. DEFERRED

- Tự động tra cứu/đối chiếu trạng thái hóa đơn trên cổng TCT (API — phụ thuộc bên ngoài) · nhận dữ liệu hóa đơn đầu vào điện tử (XML) · cảnh báo hóa đơn bị hủy/điều chỉnh sau khi nhập (sync định kỳ)