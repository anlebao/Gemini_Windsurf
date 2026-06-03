# Payment Domain

> **Xử lý thanh toán và tích hợp phương thức thanh toán**

## Overview

Payment domain quản lý các phương thức thanh toán, xử lý giao dịch, và tích hợp với các payment gateway (VietQR, Stripe, v.v.).

## Payment Methods

```csharp
public enum PaymentMethod
{
    Cash = 1,           // Tiền mặt
    VietQR = 2,         // VietQR Bank transfer
    CreditCard = 3,     // Thẻ tín dụng (Stripe/PayPal)
    EWallet = 4,        // Momo, ZaloPay, VNPay (future)
    BankTransfer = 5    // Chuyển khoản thủ công (future)
}
```

## Entities

### Payment

```csharp
public class Payment : BaseEntity
{
    public Guid PaymentId { get; protected set; }
    public Guid OrderId { get; protected set; }
    public PaymentMethod Method { get; protected set; }
    public PaymentStatus Status { get; protected set; }
    public decimal Amount { get; protected set; }
    public string? TransactionId { get; protected set; }  // Gateway transaction ID
    public string? ReferenceCode { get; protected set; }    // Internal reference
    public DateTime? PaidAt { get; protected set; }
    public string? FailureReason { get; protected set; }
    
    // Gateway-specific data
    public string? VietQR_Payload { get; protected set; }
    public string? VietQR_TransactionId { get; protected set; }
}

public enum PaymentStatus
{
    Pending = 1,
    Processing = 2,
    Paid = 3,
    Failed = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}
```

## Payment Flows

### Cash Payment

```
Order Created
     ↓
Cash Selected
     ↓
Confirmed Immediately
     ↓
Order Status → Confirmed
     ↓
Receipt Printed (optional)
```

### VietQR Payment

```
Order Created
     ↓
VietQR Selected
     ↓
Generate QR Payload (Bank account + Amount + Order ID)
     ↓
Display QR Code to Customer
     ↓
Customer Scans & Pays
     ↓
Webhook from Bank Gateway
     ↓
Verify Payment
     ↓
Order Status → Confirmed
```

### Credit Card Payment

```
Order Created
     ↓
Credit Card Selected
     ↓
Redirect to 3DS/Payment Page
     ↓
Customer Enters Card Info
     ↓
Gateway Processes
     ↓
Webhook Callback
     ↓
Order Status → Confirmed (hoặc Failed)
```

## Business Rules

### BR-001: Payment Amount
- Payment amount phải khớp với Order.TotalAmount
- Cho phép partial payment trong tương lai (nợ khách hàng)
- Overpayment cần confirmation

### BR-002: Idempotency
- Mỗi payment intent có unique idempotency key
- Retry cùng key → cùng kết quả (không charge 2 lần)

### BR-003: Webhook Handling
```csharp
public async Task HandleWebhook(PaymentWebhook webhook)
{
    // 1. Verify signature
    if (!_signatureService.Verify(webhook)) return;
    
    // 2. Idempotency check
    if (_processedWebhooks.Exists(webhook.Id)) return;
    
    // 3. Find payment
    var payment = await _paymentRepo.GetByTransactionId(webhook.TransactionId);
    
    // 4. Update status
    payment.MarkAsPaid(webhook.PaidAt);
    
    // 5. Confirm order
    await _orderService.ConfirmOrder(payment.OrderId);
    
    // 6. Record webhook processed
    _processedWebhooks.Add(webhook.Id);
}
```

### BR-004: Refund Policy
| Order Status | Refund Allowed | Process |
|--------------|----------------|---------|
| Pending | Yes | Cancel order, no charge |
| Confirmed | Yes | Refund to original method |
| Preparing | Yes + Manager approval | Partial refund (excl. prepared items) |
| Ready | No | Exchange/compensation only |
| Completed | No | Separate return process |

### BR-005: Offline Handling
- Cash: Hoạt động 100% offline
- VietQR: Generate QR offline, sync status khi online
- Credit Card: Requires online (queue if offline)

## Integration Points

| Domain | Integration | Trigger |
|--------|-------------|---------|
| Order | Confirm order | Payment → Paid |
| Order | Cancel order | Payment → Failed |
| Accounting | Create payment entry | Payment → Paid |
| Customer | Loyalty points | Payment → Paid |
| Notification | Send receipt | Payment → Paid |

## VietQR Integration

### QR Payload Format (VietQR Standard)
```
000201          # Payload Format Indicator
010212          # Point of Initiation Method (Dynamic)
38540010        # Merchant Account Information
0010A000000727  # GUID
0112            # BNB ID
...             # Bank account details
5303704         # Transaction Currency (VND)
5406100000      # Transaction Amount
5802VN          # Country Code
...
```

### VietQR Configuration per Tenant
```csharp
public class VietQRConfig
{
    public string BankId { get; set; } = "970436"; // Vietcombank
    public string BankAccount { get; set; } = "";  // Account number
    public string AccountName { get; set; } = "";  // Shop name
}
```

## DTOs

### CreatePaymentRequest
```csharp
public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReturnUrl { get; set; }  // For redirect methods
}
```

### PaymentResponse
```csharp
public class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = "";
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? VietQR_Payload { get; set; }
    public string? VietQR_ImageUrl { get; set; }
    public string? CheckoutUrl { get; set; }  // For redirect methods
    public DateTime? PaidAt { get; set; }
}
```

### PaymentWebhook
```csharp
public class PaymentWebhook
{
    public string Id { get; set; } = "";  // Webhook unique ID
    public string TransactionId { get; set; } = "";
    public string Status { get; set; } = "";  // success, failed
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string Signature { get; set; } = "";
}
```

## Events

```csharp
public record PaymentInitiated(PaymentId PaymentId, OrderId OrderId, PaymentMethod Method, decimal Amount);
public record PaymentCompleted(PaymentId PaymentId, OrderId OrderId, DateTime PaidAt);
public record PaymentFailed(PaymentId PaymentId, OrderId OrderId, string Reason);
public record PaymentRefunded(PaymentId PaymentId, decimal RefundAmount, string Reason);
```

## Security Considerations

- **Webhook Signature**: Verify tất cả webhooks từ gateway
- **Idempotency Keys**: Ngăn duplicate payments
- **PCI Compliance**: Không lưu card details (dùng tokenization)
- **HTTPS Only**: Tất cả payment endpoints qua HTTPS
- **Rate Limiting**: Giới hạn payment attempts per IP

## Error Handling

| Error | Action | User Message |
|-------|--------|--------------|
| Gateway timeout | Retry 3x, then queue | "Đang xử lý thanh toán..." |
| Insufficient funds | Mark failed | "Số dư không đủ" |
| Invalid card | Mark failed | "Thẻ không hợp lệ" |
| Network error | Queue for retry | "Mất kết nối, đang thử lại" |

## References

- `1_Shared/Domain.cs` - Payment entities
- `docs/design/` - Integration specs

---

*Last Updated: June 1, 2026*
