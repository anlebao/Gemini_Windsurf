

Markdown
# Technical Requirements Document: Multi-Tenant Payment Module
**Project:** Vạn An Solution - ShopERP Ecosystem
**Version:** 2.0 (Updated: 2026-07-20)
**Status:** Ready for Implementation

---

## 1. Tổng quan (Overview)
Hệ thống xử lý thanh toán linh hoạt cho mô hình Multi-tenant. Hỗ trợ song song 2 cơ chế vận hành dòng tiền:
1.  **Direct Mode (Decentralized):** Tiền vào trực tiếp tài khoản ngân hàng của Tenant thông qua API Key độc lập.
2.  **Centralized Mode (Global - Thu Hộ):** Dòng tiền tập trung về Vạn An với tư cách là "Đại lý thu hộ", sau đó đối soát và cấn trừ phí để thanh toán lại cho Tenant.

## 2. Kiến trúc Kỹ thuật (Technical Architecture)

### 2.1. Cấu trúc Interface Hợp đồng (Contract)
Bắt buộc bổ sung luồng `Refund` để hoàn thiện vòng đời giao dịch:
```csharp
public interface IPaymentProvider
{
    string ProviderName { get; }
    Task<PaymentLinkResponse> CreatePaymentLinkAsync(OrderInfo order, PaymentConfig config);
    Task<bool> VerifySignatureAsync(HttpRequest request, string secretKey);
    Task<PaymentResult> HandleWebhookAsync(WebhookData data);
    Task<RefundResponse> RefundAsync(OrderInfo order, decimal amount, string reason);
}
2.2. Database Schema (Tối ưu truy vấn & Bảo mật)
Bảng ShopInstances tách biệt ProviderType để dễ Indexing, chỉ mã hóa phần Credentials:

PaymentMode (Enum): Direct | Centralized

ProviderType (String): Cột riêng biệt có Index (VD: "PayOS").

EncryptedCredentials (String): JSON chứa thông tin API Key đã mã hóa.

IsAgentCollection (Boolean): Xác nhận ủy quyền thu hộ (Phục vụ pháp lý).

3. Khắc phục các rủi ro hệ thống (Critical Mitigations)
3.1. Chống trùng lặp Webhook (Idempotency)
Vấn đề: Webhook có thể bị gọi nhiều lần cho 1 giao dịch.

Giải pháp: Bắt buộc áp dụng Idempotency Pattern. Mọi TransactionId từ Webhook phải được tra cứu trong bảng PaymentTransactions. Nếu đã tồn tại và ở trạng thái Success, hệ thống bỏ qua và trả về HTTP 200 ngay lập tức, tuyệt đối không trigger logic cộng tiền lần thứ hai.

3.2. Chống ghi đè số dư (Concurrency Control)
Đối với Centralized Mode, Module Wallet Management phải áp dụng Optimistic Concurrency Control (OCC) sử dụng trường RowVersion trong Entity Framework Core, hoặc sử dụng Redis Distributed Lock (Redlock) khi thực hiện Transaction cập nhật số dư ví của Tenant để tránh mất mát dữ liệu khi request đến dồn dập.

4. Nghiệp vụ Kế toán & Pháp lý (Chuẩn 2026)
4.1. Hạch toán "Thu hộ - Chi hộ" (Centralized Mode)
Vạn An KHÔNG xuất hóa đơn bán lẻ cho khách hàng của Tenant. Quy trình pháp lý chuẩn:

Khách thanh toán: Vạn An nhận tiền (Tài khoản 3388 - Phải trả khác).

Xuất hóa đơn từ máy tính tiền: Hệ thống tự động kích hoạt API Hóa đơn điện tử của Tenant (kết nối cơ quan thuế) để xuất hóa đơn cho khách.

Thu phí dịch vụ: Vạn An xuất hóa đơn điện tử cho Tenant đối với khoản "Phí xử lý giao dịch phần mềm" (Doanh thu Vạn An).

Clearing House: Hệ thống cấn trừ tiền thu hộ (3388) và phí dịch vụ để chốt số dư ròng chuyển trả Tenant.

4.2. Bảo mật Master Key
Tuyệt đối không lưu Master Key (dùng để giải mã EncryptedCredentials) trong source code hoặc file cấu hình thông thường. Bắt buộc phải inject qua Azure Key Vault hoặc AWS Secrets Manager trong môi trường Production.

5. Hướng dẫn & Yêu cầu dành cho Devin
Giai đoạn 1: Base Infrastructure
Khởi tạo IPaymentProvider và PaymentProviderFactory.

Tích hợp logic giải mã (Decryption Service) sử dụng Key Vault.

Cập nhật Entity Framework configurations cho ShopInstances và PaymentTransactions.

Giai đoạn 2: Implementation & Safety
Implement PayOsProvider với cơ chế xử lý Webhook Idempotency.

Cấu hình Polly (Retry, Circuit Breaker) cho hàm CreatePaymentLinkAsync.

Implement bảng WalletBalances với [Timestamp] (RowVersion) để kiểm soát Concurrency.

Giai đoạn 3: Phản biện Kỹ thuật (Devin phải trả lời trước khi code)
Task: Hãy phân tích file Program.cs hiện tại và chỉ ra làm thế nào để đảm bảo tính Atomicity (Toàn vẹn Transaction DB) khi Webhook vừa cập nhật trạng thái đơn hàng (Order) sang "Đã thanh toán", vừa phải cập nhật số dư ví (Wallet) trong Centralized Mode mà không bị lỗi đứt gãy giữa chừng?