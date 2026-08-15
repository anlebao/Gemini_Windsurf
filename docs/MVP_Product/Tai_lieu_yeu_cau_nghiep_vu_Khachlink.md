# Tài liệu Yêu cầu Nghiệp vụ Kỹ thuật (Technical Requirement Specification)
**Dự án:** Hệ thống Khách mua nước (Khachlink - ShopERP)
**Phiên bản:** 1.2 (cập nhật nghiệp vụ: module toggles + polling 3s + voice note redesign + QR table optional)
**Ngày:** 2026-07-11

---

## 1. Tổng quan quy trình
Tài liệu này mô tả luồng nghiệp vụ từ khi khách hàng đặt nước qua QR Code cho đến khi kết thúc đơn hàng và tích lũy điểm thưởng.

Hệ thống được thiết kế theo **kiến trúc module có thể bật/tắt** (feature toggles): admin shop có thể kích hoạt hoặc vô hiệu hóa từng luồng nghiệp vụ (nhà bếp, điểm thưởng, kế toán) tùy theo quy mô và nhu cầu vận hành.

## 2. Các phân hệ chính
1. **Khachlink (Client-side):** Giao diện Web/PWA cho khách hàng đặt hàng.
2. **ShopERP (Admin-side):** Hệ thống quản lý đơn hàng, kho, và tài chính cho chủ shop.
3. **Trang thiết lập Shop (Shop Settings):** Nơi admin shop bật/tắt các module nghiệp vụ và cấu hình tham số vận hành.

---

## 3. Module Toggles (Feature Flags)

> **Nguyên tắc:** Mọi luồng nghiệp vụ phụ đều có thể bật/tắt bởi admin shop trong trang thiết lập. Khi module tắt, luồng chính vẫn chạy nhưng bỏ qua bước đó.

| Toggle | Mặc định | Mô tả hành vi khi BẬT | Mô tả hành vi khi TẮT |
|--------|----------|----------------------|----------------------|
| `QR_TableNumber_Enabled` | OFF | QR Code chứa thêm `Số thứ tự bàn`. Khách scan thấy thông tin bàn. | QR Code chỉ chứa `ProductId, ShopId, Timestamp`. Không có thông tin bàn. |
| `Kitchen_Workflow_Enabled` | ON | Đơn hàng đi qua luồng nhà bếp: Nhận đơn → Đang chế biến → Sẵn sàng giao. Bếp trưởng xác nhận từng bước. | Đơn hàng bỏ qua kitchen flow, đi thẳng từ "Đã xác nhận" → "Hoàn tất" (dành cho shop không cần bếp, ví dụ bán nước đóng chai). |
| `Voice_Note_Enabled` | OFF | Khách có nút "Ghi chú" → ghi âm → STT → lưu text. Nhà bếp có nút đọc ghi chú bằng TTS khi bếp trưởng xác nhận đơn. | Không có nút ghi chú trên KhachLink, không có TTS ở nhà bếp. |
| `Loyalty_Program_Enabled` | ON | Sau khi hoàn tất đơn, KhachLink hiện màn hình điểm thưởng + OTP + PWA prompt. Tích điểm tự động. | Bỏ qua toàn bộ luồng định danh khách hàng, OTP, tích điểm. Khách chỉ nhận "Cảm ơn" sau khi hoàn tất. |
| `Accounting_Sync_Enabled` | ON | Mọi giao dịch thanh toán tự động đồng bộ sang hệ thống kế toán HKD. Doanh thu ghi nhận theo thời điểm thực tế thanh toán. | Không đẩy dữ liệu order sang kế toán. Admin phải nhập liệu thủ công (dành cho shop chưa dùng module kế toán). |
| `EInvoice_Auto_Export_Enabled` | OFF | Tự động kết nối API xuất HĐĐT khởi tạo từ máy tính tiền sau khi đơn hàng ở trạng thái "Hoàn tất". | Không tự động xuất hóa đơn. (Lưu ý: toggle này chỉ có hiệu lực khi đã có credential sandbox Viettel/MISA — hiện đang ở Tech Debt TD-KL-01) |

---

## 4. Mô tả Luồng Nghiệp vụ chi tiết

### Giai đoạn 1: Đặt hàng (Khachlink)
1. **Quét mã:** Khách quét QR Code chứa thông tin: `Tên sản phẩm`, `Giá`.
    * **[Toggle: `QR_TableNumber_Enabled` = ON]** QR Code thêm `Số thứ tự bàn`. Khách thấy thông tin bàn trên giỏ hàng.
    * **[Toggle: `QR_TableNumber_Enabled` = OFF]** QR Code chỉ chứa `ProductId, ShopId, Timestamp`. Tên/giá fetch từ API sau khi scan.
2. **Giỏ hàng:** Hiển thị danh sách sản phẩm.
3. **Ghi chú bằng giọng nói (tùy chọn):**
    * **[Toggle: `Voice_Note_Enabled` = ON]:**
        * Khách chọn nút "Ghi chú" → ghi âm yêu cầu.
        * Hệ thống tự động chuyển đổi giọng nói thành văn bản (Speech-to-Text).
        * **Lưu trữ:** Chỉ lưu văn bản (text) vào `Order_Line_Items` / `Order.VoiceNoteText`. **KHÔNG lưu tệp âm thanh.**
        * **Đọc ghi chú ở nhà bếp:** Khi bếp trưởng nhấn nút "Nhận đơn", hệ thống tự động chuyển đổi văn bản ghi chú thành giọng nói (Text-to-Speech) để bếp nghe được yêu cầu đặc biệt của khách. Tính năng TTS này có thể bật/tắt độc lập trong trang thiết lập.
    * **[Toggle: `Voice_Note_Enabled` = OFF]:** Không hiển thị nút "Ghi chú". Khách chỉ có thể nhập ghi chú bằng text (nếu có field ghi chú text).

### Giai đoạn 2: Thanh toán & Xử lý đơn (Khachlink & ShopERP)
1. **Xác nhận đặt hàng:** Show tổng tiền, chọn hình thức thanh toán (Tiền mặt/Chuyển khoản).
2. **Tiền mặt:** Hiển thị `Processing Bar` → Gửi request tới `ShopERP` để tiếp nhận đơn.
3. **Chuyển khoản:**
    * Show VietQR (Account Info, Amount, Content: "đơn hàng số ###").
    * Nút "Tải mã QR".
    * Hiển thị 2 thanh trạng thái: `Xử lý đơn hàng` và `Chờ thanh toán`.
4. **Đồng bộ trạng thái real-time:**
    * Cập nhật từ `ShopERP` về `Khachlink` qua **HTTP polling với interval 3 giây** (độ trễ tối đa 3s — chấp nhận được cho nghiệp vụ F&B).
    * **[Toggle: `Kitchen_Workflow_Enabled` = ON]:** Trạng thái đi qua `Nhận đơn` → `Đang chế biến` → `Sẵn sàng giao` → `Hoàn tất`.
    * **[Toggle: `Kitchen_Workflow_Enabled` = OFF]:** Bỏ qua kitchen statuses, đơn hàng đi thẳng `Đã xác nhận` → `Hoàn tất`.

#### 4.1. Quy trình Nhà bếp / Phòng pha chế (chỉ khi `Kitchen_Workflow_Enabled` = ON)

| Bước | Trạng thái | Ai thực hiện | Hành vi |
|------|-----------|-------------|---------|
| 1 | **Nhận đơn** (confirmed) | Bếp trưởng nhấn nút "Nhận đơn" trên ShopERP | Đơn hàng xuất hiện trên Kitchen Display. **[Toggle TTS = ON]** Đọc ghi chú text bằng giọng nói. |
| 2 | **Đang chế biến** (preparing) | Bếp trưởng nhấn nút "Bắt đầu làm" | Trạng thái sync về KhachLink qua polling 3s. |
| 3 | **Sẵn sàng giao** (ready) | Bếp trưởng nhấn nút "Hoàn thành" (hoặc auto khi tất cả items completed) | Trạng thái sync về KhachLink. Khách thấy "Sẵn sàng". |

### Giai đoạn 3: Hoàn tất & Tích điểm (ShopERP & Khachlink)
1. **Xác nhận thanh toán:** Admin xác nhận thủ công trên `ShopERP`.
2. **Hoàn tất:** Admin bấm "Hoàn tất" trên `ShopERP`.
3. **Khách xác nhận:** `Khachlink` hiện nút "Xác nhận đã nhận hàng".
4. **Định danh khách hàng & Tích điểm (tùy chọn):**
    * **[Toggle: `Loyalty_Program_Enabled` = ON]:**
        * Mở màn hình điểm thưởng & hướng dẫn cài PWA.
        * Xác thực OTP qua SĐT (TTL: 5 phút — quyết định user).
        * Liên kết ID khách hàng với số điện thoại trong Database.
        * Tích điểm tự động theo quy tắc thưởng.
        * Tắt nhắc nhở cài PWA cho các lần truy cập sau (khi đã đăng nhập).
    * **[Toggle: `Loyalty_Program_Enabled` = OFF]:**
        * Bỏ qua toàn bộ: không OTP, không tích điểm, không PWA prompt.
        * Khách chỉ thấy "Cảm ơn quý khách" sau khi xác nhận nhận hàng.

---

## 5. Yêu cầu Phi chức năng & Tuân thủ (Vạn An)

| # | Yêu cầu | Điều kiện áp dụng |
|---|---------|-------------------|
| N1 | **Kế toán:** Mọi giao dịch phải đồng bộ sang hệ thống kế toán HKD. Doanh thu ghi nhận theo thời điểm thực tế thanh toán. | **[Toggle: `Accounting_Sync_Enabled` = ON]** — khi OFF, admin nhập liệu thủ công. |
| N2 | **Hóa đơn:** Tự động kết nối API xuất HĐĐT khởi tạo từ máy tính tiền sau khi đơn hàng ở trạng thái "Hoàn tất". | **[Toggle: `EInvoice_Auto_Export_Enabled` = ON]** — hiện đang OFF, chờ sandbox Viettel/MISA (Tech Debt TD-KL-01). |
| N3 | **Bảo mật:** Dữ liệu cá nhân (SĐT) phải được mã hóa theo quy định bảo vệ dữ liệu cá nhân. OTP có thời hạn hiệu lực (TTL: 5 phút). | Áp dụng khi `Loyalty_Program_Enabled` = ON. |
| N4 | **Real-time sync:** HTTP polling interval 3 giây. | Áp dụng cho OrderTracking. Độ trễ tối đa 3s — chấp nhận cho F&B. |
| N5 | **Ghi chú giọng nói:** Chỉ lưu text (không lưu audio). TTS ở nhà bếp khi bếp trưởng nhận đơn. | Áp dụng khi `Voice_Note_Enabled` = ON. |

---

## 6. Kết quả Verify Codebase (điều tra ngày 2026-07-11)

> **Phương pháp:** 3 subagents điều tra song song — (A) KhachLink client, (B) ShopERP/CoreHub server, (C) E2E Playwright tests. Kết quả dưới đây là tổng hợp từ cả 3 nguồn.

### 6.1. Giai đoạn 1 — Đặt hàng (KhachLink client)

| # | Yêu cầu | Status | Bằng chứng / Ghi chú |
|---|---------|--------|---------------------|
| 1.1 | Quét QR (tên SP, giá) | ✅ **DONE** | `Components/QRScanner.razor`, `Pages/Scan.razor`, `wwwroot/js/qr-scanner.js` — scanner hoạt động. `QRCodePayload` (`1_Shared/DTOs/QRCodePayload.cs` lines 7-36) có `ProductId, ShopId, Timestamp`. Tên/giá fetch từ API sau khi scan. |
| 1.1a | QR chứa số bàn (toggle) | ❌ **MISSING** | `QRCodePayload` chưa có field `TableNumber`. Cần thêm field + toggle `QR_TableNumber_Enabled` trong Shop Settings. |
| 1.2 | Giỏ hàng | ✅ **DONE** | `Pages/Cart.razor`, `Services/CartService.cs`, `Components/CartDrawer.razor` — full cart management với localStorage. |
| 1.3 | Ghi chú giọng nói (STT only, no audio) | ⚠️ **PARTIAL** | `Pages/VoiceNote.razor` (lines 1-410) có STT bằng Web Speech API (Vietnamese). Text lưu qua `api/orders/{id}/note`. **THIẾU**: toggle `Voice_Note_Enabled`, TTS ở nhà bếp (text-to-speech khi bếp trưởng nhận đơn). Domain field `VoiceNoteAudioBlob` sẽ không sử dụng (chỉ lưu text). |

### 6.2. Giai đoạn 2 — Thanh toán & Xử lý đơn

| # | Yêu cầu | Status | Bằng chứng / Ghi chú |
|---|---------|--------|---------------------|
| 2.1 | Xác nhận đơn + chọn HTTT | ⚠️ **PARTIAL** | `Pages/Checkout.razor` hiển thị tổng tiền + guest form. `Services/CheckoutFlowState.cs` có field `PaymentMethod` nhưng **không có UI chọn Tiền mặt/Chuyển khoản**. |
| 2.2 | Tiền mặt: Processing Bar → gửi ShopERP | ❌ **MISSING** | Không có processing bar, không có cash flow riêng. |
| 2.3 | Chuyển khoản: VietQR + Download + 2 status bars | ⚠️ **PARTIAL** | `Components/QrPaymentModal.razor` (lines 1-245) có VietQR generate (`api/v1/vietqr/generate`), Download QR, "Tôi đã thanh toán". **THIẾU**: 2 thanh trạng thái (Xử lý đơn + Chờ thanh toán). |
| 2.4 | Real-time sync qua polling 3s | ⚠️ **PARTIAL** | **Server**: `OrderHub.cs` (SignalR) + `OrderNotificationService.cs` broadcast ✅. **Client**: KhachLink dùng **HTTP polling** (`Pages/OrderTracking.razor` lines 308-371) — hiện interval 5-10s, **cần đổi thành 3s**. SignalR bị remove khỏi KhachLink `Program.cs` lines 108-110. |

#### 6.2.1. Quy trình Nhà bếp / Phòng pha chế (toggle `Kitchen_Workflow_Enabled`)

> **Yêu cầu:** 3 trạng thái — Nhận đơn (confirmed) → Đang chế biến (preparing) → Sẵn sàng giao (ready). Có thể bật/tắt.

**Domain layer:**
- `KitchenStatus` enum (`1_Shared/Domain/KitchenStatus.cs` lines 6-12): `Pending=0, Preparing=1, Completed=2, Cancelled=3`
- `OrderStatusId` (`1_Shared/Domain.cs` lines 422-433): `Pending, Confirmed, Preparing, Ready, Delivering, Completed, Cancelled, Processing(alias)`
- `OrderStatuses.Default` (`1_Shared/Domain.cs` lines 457-509): pending→confirmed→preparing→ready→completed→cancelled
- State machine (`3_CoreHub/Services/OrderWorkflowService.cs` lines 241-256): `pending→[preparing,cancelled,completed]`, `preparing→[ready,cancelled,completed]`, `ready→[completed,cancelled]`

| Trạng thái yêu cầu | Domain status | Service | SignalR | ShopERP UI | KhachLink UI | Verdict |
|---|---|---|---|---|---|---|
| **Nhận đơn** (confirmed) | ✅ `confirmed` ("Đã xác nhận") | ✅ `OrderWorkflowService.TransitionStatusAsync` | ✅ `OrderNotificationService.NotifyOrderStatusChangedAsync` | ✅ `Orders/Index.razor` line 100: nút "✓ Xác nhận" / `Detail.razor` line 62: "✓ Xác nhận đơn hàng" | ✅ `OrderTracking.razor` line 269: "Đã xác nhận" | ⚠️ **PARTIAL** — label "Xác nhận" nên đổi thành "Nhận đơn" khi kitchen flow ON |
| **Đang chế biến** (preparing) | ✅ `preparing` ("Đang pha chế") + `KitchenStatus.Preparing=1` | ✅ `OrderWorkflowService.TransitionStatusAsync` + `KitchenService.UpdateItemStatusAsync` | ✅ Broadcast qua `KitchenHub` | ⚠️ `Pages/Kitchen/Index.cshtml` lines 149-154: nút "⏳ Bắt đầu làm" (KitchenStatus level) — **NHƯNG** `Orders/Detail.razor` KHÔNG có nút chuyển sang "preparing" ở Order level | ⚠️ `OrderTracking.razor` line 270: dùng `"processing"` (không phải `"preparing"`) — **mismatch status name** | ⚠️ **PARTIAL** — Kitchen item-level OK, Order-level thiếu nút; KhachLink mismatch status name |
| **Sẵn sàng giao** (ready) | ✅ `ready` ("Sẵn sàng") | ✅ `KitchenService.UpdateItemStatusAsync` auto-set Order.Status="ready" khi tất cả items Completed (line 121) | ✅ Broadcast | ⚠️ `Pages/Kitchen/Index.cshtml` lines 149-154: nút "✅ Hoàn thành tất cả" (trigger auto-ready) — **NHƯNG** `Orders/Detail.razor` KHÔNG có nút "Sẵn sàng giao" trực tiếp | ✅ `OrderTracking.razor` line 272: "Sẵn sàng" | ⚠️ **PARTIAL** — auto-ready qua kitchen items OK, nhưng không có nút manual "Sẵn sàng giao" ở Order detail |

**Tóm tắt kitchen flow:**
- ✅ Domain + state machine: **ĐỦ** (pending→preparing→ready→completed)
- ✅ Service layer: **ĐỦ** (`OrderWorkflowService` + `KitchenService`)
- ✅ SignalR broadcast: **ĐỦ** (`OrderNotificationService` + `KitchenHub`)
- ⚠️ ShopERP UI: **THIẾU** nút chuyển trạng thái ở `Orders/Detail.razor` — chỉ có nút "Xác nhận" (pending→confirmed), không có nút "Bắt đầu làm" (→preparing) hay "Sẵn sàng" (→ready) ở Order level. Kitchen Display (`Pages/Kitchen/Index.cshtml`) có nút item-level nhưng đây là trang MVC legacy, không dùng UI Platform.
- ⚠️ KhachLink UI: **MISMATCH** — dùng `"processing"` thay vì `"preparing"` trong timeline (`OrderTracking.razor` line 270)
- ⚠️ Label semantics: "Xác nhận" ≠ "Nhận đơn", "Đang pha chế" ≠ "Đang chế biến" (có thể chấp nhận được)
- ❌ Toggle `Kitchen_Workflow_Enabled`: **CHƯA CÓ** — cần thêm vào Shop Settings + logic bypass kitchen flow khi OFF

### 6.3. Giai đoạn 3 — Hoàn tất & Tích điểm

| # | Yêu cầu | Status | Bằng chứng / Ghi chú |
|---|---------|--------|---------------------|
| 3.1 | Admin xác nhận thanh toán (ShopERP) | ✅ **DONE** | `Detail.razor` lines 68-76: nút "💰 Xác nhận đã nhận tiền" → `OrderService.ConfirmPaymentAsync` (lines 574-617) với idempotency guard. |
| 3.2 | Admin bấm "Hoàn tất" (ShopERP) | ⚠️ **PARTIAL** | Domain có `MarkAsCompleted()` (`Domain.cs` lines 998-1002). `OrderWorkflowService` xử lý transition → "completed" + trigger `HandleOrderCompletedAsync`. **THIẾU**: không tìm thấy nút "Hoàn tất" rõ ràng trong `Detail.razor` UI. |
| 3.3 | KhachLink: nút "Xác nhận đã nhận hàng" | ❌ **MISSING** | `OrderTracking.razor` không có nút customer confirm receipt. Chỉ show `IdentityUpgradeModal` (lines 11-14, 444-451). |
| 3.4 | OTP + liên kết SĐT + PWA prompt (toggle `Loyalty_Program_Enabled`) | ⚠️ **PARTIAL** | `Pages/Login.razor`: OTP send/verify (`/api/customers/otp/send`, `/api/customers/otp/verify`). `Pages/LoyaltyCard.razor`: hiển thị điểm/tier/history. `Components/PWA/PWAInstallPrompt.razor`: dismiss flag localStorage. **THIẾU**: toggle `Loyalty_Program_Enabled`, client không enforce OTP TTL, không disable PWA cho user đã đăng nhập. Server: `CustomerIdentityController.cs` link customer với phone ✅. |

### 6.4. Yêu cầu Phi chức năng

| # | Yêu cầu | Status | Bằng chứng / Ghi chú |
|---|---------|--------|---------------------|
| N1 | Accounting sync → HKD (toggle `Accounting_Sync_Enabled`) | ✅ **DONE** (logic) / ❌ **MISSING** (toggle) | `OrderService.ConfirmPaymentAsync` → `GenerateAccountingEntriesAsync` (line 607). `SimpleAccountingEventHandler.cs` (NATS) → `IHKDBookService.RecordRevenueAsync`. Doanh thu ghi nhận lúc payment confirm. **THIẾU**: toggle `Accounting_Sync_Enabled` trong Shop Settings + logic bypass khi OFF. |
| N2 | EInvoice auto xuất khi "Hoàn tất" (toggle `EInvoice_Auto_Export_Enabled`) | ⚠️ **PARTIAL** | `EInvoiceOrchestrator.cs` + API endpoints (`HKDElectronicInvoiceController.cs`) tồn tại. **THIẾNG**: `OrderWorkflowService.HandleOrderCompletedAsync` không auto-trigger EInvoice. → **Tech Debt TD-KL-01** (chờ sandbox Viettel/MISA). |
| N3 | Mã hóa SĐT | ✅ **DONE** (server) / ❌ **MISSING** (client) | Server: `EncryptedStringConverter` (Data Protection) trong `CustomerConfiguration.cs` lines 24-34. Client: lưu plaintext trong localStorage (`Login.razor` line 156). |
| N4 | OTP TTL 5 phút | ✅ **DONE** (giữ nguyên theo quyết định user) | `OtpService.cs` line 17: `OtpTtlMinutes = 5` (300s). User quyết định giữ 5 phút. |
| N5 | Polling interval 3s | ⚠️ **PARTIAL** | `OrderTracking.razor` lines 377-388: hiện interval 5-10s. **Cần đổi thành 3s**. |

---

## 7. Quyết định User & Tech Debt

### 7.1. Quyết định User (ngày 2026-07-11)

| # | Item | Quyết định | Lý do |
|---|------|-----------|-------|
| D1 | **Real-time sync: Polling vs WebSocket** | **Polling 3s** | User chọn polling interval 3s. Độ trễ tối đa 3s — chấp nhận cho nghiệp vụ F&B. Không cần thêm SignalR client vào KhachLink. |
| D2 | **OTP TTL** | **GIỮ 5 phút** — không fix | User quyết định giữ `OtpTtlMinutes = 5`. |
| D3 | **EInvoice auto-trigger** | **KHÔNG fix bây giờ** — ghi Tech Debt | Đợi đăng ký sandbox Viettel + MISA xong mới làm. |
| D4 | **QR số bàn** | **Tùy chọn (toggle)** | Admin shop bật/tắt `QR_TableNumber_Enabled` trong trang thiết lập. Mặc định OFF. |
| D5 | **Voice note: bỏ audio storage** | **Chỉ STT + TTS** | Bỏ lưu/nén file audio. Chỉ convert giọng nói → text (STT) khi khách nhập ghi chú. Convert text → speech (TTS) ở nhà bếp khi bếp trưởng nhấn "Nhận đơn". Tính năng bật/tắt được (`Voice_Note_Enabled`). |
| D6 | **Kitchen flow toggle** | **Bật/tắt được** | Admin shop có thể bỏ qua kitchen flow (`Kitchen_Workflow_Enabled` = OFF) — đơn đi thẳng confirmed → completed. |
| D7 | **Loyalty flow toggle** | **Bật/tắt được** | Admin shop có thể tắt luồng điểm thưởng/OTP/PWA (`Loyalty_Program_Enabled` = OFF). |
| D8 | **Accounting sync toggle** | **Bật/tắt được** | Admin shop có thể tắt đẩy dữ liệu order sang kế toán (`Accounting_Sync_Enabled` = OFF). |

### 7.2. Tech Debt (ghi nhận, chờ điều kiện)

| ID | Item | Mô tả | Trigger | Severity |
|----|------|-------|---------|----------|
| TD-KL-01 | **EInvoice auto-trigger khi order completed** | `OrderWorkflowService.HandleOrderCompletedAsync` không gọi `IEInvoiceOrchestrator.CreateInvoiceAsync` khi order → "completed". EInvoice API + orchestrator đã sẵn sàng nhưng chỉ tạo thủ công. | Đăng ký sandbox Viettel + MISA xong | Medium — chờ credential sandbox |
| TD-KL-02 | **TTS ở nhà bếp (Text-to-Speech)** | Cần implement TTS để đọc ghi chú text bằng giọng nói khi bếp trưởng nhấn "Nhận đơn". Toggle độc lập trong Shop Settings. | Implement `Voice_Note_Enabled` | Medium |
| TD-KL-03 | **QR payload thêm TableNumber (toggle)** | `QRCodePayload` cần thêm field `TableNumber` + toggle `QR_TableNumber_Enabled` trong Shop Settings. | Implement QR table number feature | Low |
| TD-KL-04 | **Cash payment flow + Processing Bar** | Không có UI chọn Tiền mặt, không có Processing Bar component. | Implement payment method selection | Medium |
| TD-KL-05 | **Dual status bars cho transfer** | `QrPaymentModal.razor` chỉ có single loading spinner, không có 2 thanh (Xử lý đơn + Chờ thanh toán). | UI enhancement | Low |
| TD-KL-06 | **Nút "Xác nhận đã nhận hàng" trên KhachLink** | `OrderTracking.razor` không có nút customer confirm receipt. | Cần thêm customer confirmation flow | Medium |
| TD-KL-07 | **Nút "Hoàn tất" trên ShopERP UI** | `Orders/Detail.razor` không có nút "Hoàn tất" rõ ràng (chỉ có status transition API). | Cần thêm UI button cho admin | Low |
| TD-KL-08 | **ShopERP Order Detail thiếu nút kitchen transition** | `Orders/Detail.razor` chỉ có nút "Xác nhận" (pending→confirmed). Thiếu nút "Bắt đầu làm" (→preparing) và "Sẵn sàng" (→ready) ở Order level. Kitchen Display (`Pages/Kitchen/Index.cshtml`) có nút item-level nhưng là trang MVC legacy. | Cần thêm UI buttons cho full kitchen flow | Medium |
| TD-KL-09 | **KhachLink status name mismatch** | `OrderTracking.razor` line 270 dùng `"processing"` thay vì `"preparing"` — mismatch với domain. | Cần sync status name với domain | Low |
| TD-KL-10 | **PWA disable cho user đã đăng nhập** | `PWAInstallPrompt.razor` có dismiss flag nhưng không disable cho logged-in users. | Cần tie dismiss flag với login state | Low |
| TD-KL-11 | **Phone encryption client-side** | Server mã hóa SĐT (`EncryptedStringConverter`), nhưng client lưu plaintext trong localStorage. | Cần review client storage strategy | Low |
| TD-KL-12 | **Module Toggle infrastructure** | Cần tạo Shop Settings page + toggle storage (DB hoặc config) + logic bypass cho 6 toggles: `QR_TableNumber_Enabled`, `Kitchen_Workflow_Enabled`, `Voice_Note_Enabled`, `Loyalty_Program_Enabled`, `Accounting_Sync_Enabled`, `EInvoice_Auto_Export_Enabled`. | Implement feature flag system | High — nền tảng cho mọi toggle |
| TD-KL-13 | **Polling interval 3s** | `OrderTracking.razor` lines 377-388 hiện 5-10s, cần đổi thành 3s. | Simple config change | Low |
| TD-KL-14 | **Domain field `VoiceNoteAudioBlob` / `ItemNoteAudioBlob` — unused** | Sau khi bỏ audio storage (D5), các field này trong Domain.cs không còn sử dụng. Có thể giữ lại (unused) hoặc mark obsolete. | Cleanup sau khi voice note redesign complete | Low |

---

## 8. E2E Playwright Test Coverage

### 8.1. Test files hiện có (tất cả PARTIAL — KHÔNG có test full luồng)

| Test file | Flow cover | Thiếu |
|-----------|-----------|-------|
| `6_Testing/e2e-tests/order-flow.spec.ts` | Cart + place order + admin view | Payment, sync, completion, OTP |
| `6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts` | Guest checkout → admin accept → kitchen → tracking → QR payment | QR scan, voice note, cash, OTP, loyalty, PWA |
| `6_Testing/e2e-tests/qr-payment-ui.spec.ts` | QR modal UI only | Full flow |
| `6_Testing/e2e-tests/qr-payment.spec.ts` | VietQR API contract | Full flow |
| `6_Testing/e2e-tests/payment-confirm-flow.spec.ts` | Payment confirm (self + admin) | Rest of flow |
| `6_Testing/e2e-tests/order-tracking.spec.ts` | Tracking page UI | Full flow |
| `6_Testing/e2e-tests/realtime-sync-flow.spec.ts` | SignalR broadcast + polling | Full flow |
| `6_Testing/e2e-tests/voice-command.spec.ts` | Voice note (mocked) | **SKIPPED** — SpeechRecognition API không support |

### 8.2. Steps KHÔNG có E2E test

- ❌ QR scan entry point
- ❌ QR với số bàn (toggle ON)
- ❌ Cash payment flow
- ❌ Customer "Confirm received" button
- ❌ OTP phone verification
- ❌ PWA install prompt
- ❌ Loyalty points accumulation (Page Object có method nhưng không test nào gọi)
- ❌ Kitchen transition: preparing → ready (Order level)
- ❌ Admin "Hoàn tất" button
- ❌ TTS ở nhà bếp (đọc ghi chú)
- ❌ Module toggle ON/OFF scenarios

### 8.3. Đề xuất E2E test mới

Tạo `6_Testing/e2e-tests/khachlink-full-order-flow.spec.ts` cover end-to-end với **2 scenario chính**:

**Scenario 1: Full flow (tất cả toggle ON)**
1. QR scan simulation (có số bàn) → cart
2. Voice note (mocked Speech API) → STT → lưu text
3. Payment (cash + VietQR transfer)
4. Real-time status sync 3s (all stages: nhận đơn → đang chế biến → sẵn sàng)
5. TTS đọc ghi chú ở nhà bếp khi bếp trưởng nhận đơn
6. Admin payment confirmation + order completion
7. Customer receipt confirmation
8. OTP phone verification
9. Loyalty points + PWA install prompt
10. Accounting sync verified

**Scenario 2: Minimal flow (kitchen + loyalty + accounting OFF)**
1. QR scan (không số bàn) → cart
2. Payment (cash only)
3. Đơn đi thẳng confirmed → completed (bypass kitchen)
4. Customer nhận "Cảm ơn" (không OTP, không loyalty, không PWA)
5. Không có accounting sync

---

## 9. Tóm tắt Execution Readiness

| Giai đoạn | Sẵn sàng chạy? | Blocker chính |
|-----------|---------------|---------------|
| Phase 1: Đặt hàng | ⚠️ PARTIAL — QR scan + cart OK, voice note chỉ có STT (thiếu TTS + toggle) | TTS ở nhà bếp missing, toggle infrastructure missing |
| Phase 2: Thanh toán | ❌ KHÔNG sẵn sàng | Thiếu UI chọn payment method, thiếu cash flow, thiếu dual status bars |
| Phase 2: Kitchen flow | ⚠️ PARTIAL | ShopERP thiếu nút kitchen transition ở Order Detail; KhachLink mismatch status name; thiếu toggle |
| Phase 3: Hoàn tất | ⚠️ PARTIAL | Thiếu nút "Hoàn tất" UI, thiếu nút "Xác nhận đã nhận hàng" |
| Phase 3: Loyalty/OTP | ⚠️ PARTIAL | OTP OK (5 phút), loyalty display OK, PWA prompt OK — thiếu toggle + PWA disable cho logged-in |
| Non-func: Accounting | ✅ DONE (logic) / ❌ MISSING (toggle) | Thiếu toggle `Accounting_Sync_Enabled` |
| Non-func: EInvoice | ⚠️ DEFERRED | Tech debt — chờ sandbox Viettel/MISA |
| Non-func: Security | ⚠️ PARTIAL | Server encryption OK, client plaintext |
| Non-func: Polling 3s | ⚠️ PARTIAL | Hiện 5-10s, cần đổi 3s |
| **Module Toggle Infrastructure** | ❌ **MISSING** | **TD-KL-12 (High)** — nền tảng cho 6 toggles, cần implement trước |
| **E2E test full luồng** | ❌ KHÔNG CÓ | Cần tạo test mới (2 scenarios) |

**Kết luận:** Base code CHƯA sẵn sàng chạy full luồng end-to-end. **Blocker lớn nhất là Module Toggle Infrastructure (TD-KL-12)** — cần implement trước vì 6 toggles ảnh hưởng đến mọi luồng. Sau đó cần implement: cash flow (TD-KL-04), kitchen UI buttons (TD-KL-08), customer confirm (TD-KL-06), polling 3s (TD-KL-13). E2E test full luồng chưa tồn tại.
