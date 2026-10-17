# TASK CARD: P0.4 — RÀ SOÁT CHUẨN DỮ LIỆU HĐĐT THEO NĐ 254/2026 + TT 91/2026

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P0.4 — dep của P0.3)
> Branch: `main`
> Status: 📋 PLANNED

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **2.1/2.2** — cập nhật chuẩn dữ liệu hóa đơn điện tử theo khung pháp lý MỚI: **Luật QLT 2025 (108/2025/QH15) Điều 26** + **NĐ 254/2026/NĐ-CP** + **TT 91/2026/TT-BTC** (hiệu lực 01/07/2026 — **bãi bỏ NĐ 123/2020, NĐ 70/2025, TT 32/2025**).
- **Phát hiện review (2026-10-16):** payload thiết kế theo API Viettel/MISA **thế hệ NĐ 123/2020** (`MisaDTOs.cs`, `ViettelDTOs.cs`, `EInvoiceRequest` — W6-T3 2026-07-05); cần rà soát trường/ký hiệu/mẫu mới + hóa đơn máy tính tiền (không bắt buộc chữ ký số) + chuyển đổi có mã/không mã (Điều 8 TT 91/2026: 11 trường hợp ngừng/tạm ngừng sử dụng HĐĐT).
- **Đã xác minh code:** `IEInvoiceProvider` (Submit/Status/Cancel/File) · `EInvoiceRequest` (SupplierTaxCode, LineItems, CurrencyCode, PaymentType, TransactionUuid) · `ShopFeatureSettings.EInvoice_Auto_Export_Enabled` · UI 7 trang EInvoice.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 2 (P0.4 trước P0.3).
- **Execution Mode:** IMPLEMENT (phần code mapping) + REVIEW_ONLY (phần rà soát pháp lý).
- **Skills (max 3):** `einvoice-integration` + `pattern-based-fixing` + `test-system-upgrade`.

## 3. SCOPE

### 3.1 Rà soát pháp lý ↔ mapping (deliverable: mapping table trong `docs/design/`)
- [ ] Đối chiếu từng trường `EInvoiceRequest`/DTO với yêu cầu định dạng dữ liệu HĐĐT mới (NĐ 254/2026 + TT 91/2026 — Phụ lục định dạng, ký hiệu mẫu/ký hiệu/mẫu hóa đơn tham khảo).
- [ ] Rà các nội dung mới có thể ảnh hưởng: **hóa đơn khởi tạo từ máy tính tiền** (không bắt buộc chữ ký số, kết nối truyền dữ liệu cơ quan thuế — đối tượng bắt buộc: TTM/siêu thị/bán lẻ/ăn uống/khách sạn/vận tải...) · hóa đơn có mã ↔ không mã (chuyển đổi khi rủi ro cao — TT 91/2026 Điều 8) · ủy nhiệm lập hóa đơn · hóa đơn điều chỉnh/thay thế (liên kết P0.3) · thời điểm lập hóa đơn (NĐ 254/2026).
- [ ] Tài liệu: `docs/design/EInvoice multi provider integration.md` cập nhật khung pháp lý mới + bảng mapping trường.

### 3.2 Code mapping
- [ ] Cập nhật `EInvoiceRequest`/DTOs nếu thiếu trường theo chuẩn mới (thêm additive — không phá contract hiện có); tham số hóa cấu hình chuẩn (chuỗi định dạng/ký hiệu) qua config thay vì hardcode.
- [ ] Hỗ trợ đánh dấu loại hóa đơn máy tính tiền (InvoiceType) + ghi chú không bắt buộc chữ ký số (metadata).
- [ ] Kiểm tra `EInvoiceSyncSubscriber` (Gateway NATS) + outbox không đổi hành vi.

### 3.3 Tests
- [ ] Cập nhật `EInvoiceProviderTests`/`MisaEInvoiceProviderTests`/`ViettelEInvoiceProviderTests`/`EInvoiceDISmokeTests` theo mapping mới.
- [ ] Test smoke: request payload khớp mapping table (serialization).

## 4. DECISIONS

- Mặc định: **không phá contract hiện có** — chỉ thêm trường/config additive; nếu provider yêu cầu chuẩn khác → tài liệu + task riêng sau khi provider nâng cấp.

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs (record DTO ở Services layer OK) · KHÔNG phá outbox/idempotency hiện có · Multi-tenancy · Secrets: KHÔNG commit token provider · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Mapping table pháp lý ↔ code hoàn tất (docs)
- [ ] DTO/config cập nhật theo chuẩn NĐ 254/2026 + TT 91/2026 (additive)
- [ ] Tests mapping PASS · smoke PASS
- [ ] Deploy + RV production PASS → đóng

## 7. DEFERRED

- Đăng ký sử dụng HĐĐT với cơ quan thuế (flow đăng ký nội bộ — cần tích hợp cổng TCT, tách task riêng) · đối chiếu trạng thái hóa đơn đầu vào (P1.4) · ký hiệu/mẫu hóa đơn tùy biến theo ngành
## 8. ✅ DONE — W2 (2026-10-17, commit `a3688428`)

**Đã implement (additive, không phá contract):**
- `EInvoiceRequest` += `InvoiceForm` (`string?`, default null) + `IsCashierGeneratedInvoice` (`bool`, default false) — positional tail, back-compat.
- `EInvoiceDataKeys` (IEInvoiceProvider.cs): `invoiceForm`/`isCashierInvoice` keys + `coMa`/`khongMa`/`mayTinhTien` values (Điều 26 Luật QLT 2025 — 3 loại HĐĐT).
- `Program.cs` ×2 (CoreHub + ShopERP): populate từ config `EInvoiceConfig:InvoiceForm` (default `khongMa`) + `EInvoiceConfig:IsCashierGeneratedInvoice` (default false) — chuyển đổi có mã khi rủi ro = đổi config, không đổi code.
- `ViettelEInvoiceProvider.BuildPayload` += `metadata[]` `invoiceForm`/`isCashierInvoice` (khi có). MISA: chưa map payload (chờ spec mới) — ghi trong mapping table.
- **Điều 8 TT 91/2026 — ngừng/tạm ngừng HĐĐT:** `SystemSetting` key `EInvoice_Usage_Suspended` (+ `_Reason`) per-tenant (key-value, không migration) · guard trong `ComplianceService.ValidateComplianceAsync`/`IsCompliantAsync` (IgnoreQueryFilters + filter TenantId tường minh) · `IEInvoiceOrchestrator.SetEInvoiceUsageSuspendedAsync` upsert · endpoint `POST /api/einvoice/usage-suspension` (Roles Admin/Owner/SystemAdmin) · DTO `SetUsageSuspensionRequest`.
- Mapping table NĐ 254/2026 + TT 91/2026 thêm vào `docs/design/EInvoice multi provider integration.md` (§P0.4 — 3 loại HĐĐT, cashier-invoice không bắt buộc ký số, suspension, chuyển đổi có mã 10 ngày, W3 dependency điều chỉnh/thay thế).
- `InvoiceType` enum KHÔNG đổi (constraint Domain.cs) — cashier-invoice qua metadata Services-layer.
- Outbox/idempotency/EInvoiceSyncSubscriber không đổi hành vi.

**Tests:** `EInvoiceRequestTests` +3 (defaults/set/constants khớp mapping doc) · `ViettelEInvoiceProviderTests` +2 (metadata payload có/không) · `MisaEInvoiceProviderTests` +1 (contract unchanged) · `ComplianceServiceTests` +4 (suspension throw/false/other-tenant) · `EInvoiceOrchestratorTests` +2 (upsert/toggle) · `EInvoiceDISmokeTests` +1.
**Validation:** build 0 errors · guard ALL PASSED · Core.Tests 2228 · ShopERP.Tests 209 · Integration 51.
**Deploy + RV:** `a3688428` → CD Multi-VPS SUCCESS → RV L1 (`SetEInvoiceUsageSuspendedAsync`×2 + `usage-suspension`×1 production DLL) · L2 (health 200) PASS.

**Còn lại / DEFERRED giữ nguyên:** đăng ký HĐĐT với CQT · đối chiếu HĐ đầu vào (P1.4) · điều chỉnh/thay thế HĐĐT (W3/P0.3) · map `InvoiceForm` vào MISA payload khi provider cập nhật spec.
