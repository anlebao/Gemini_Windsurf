# Order Domain

> **Quản lý đơn hàng và luồng xử lý đơn**

## Overview

Order domain quản lý toàn bộ vòng đời đơn hàng từ khi khách đặt đến khi hoàn thành, bao gồm cả Kitchen Display System (KDS) integration.

## Entities

### Order (Aggregate Root)

```csharp
public class Order : BaseEntity
{
    public OrderId OrderId { get; protected set; }
    public Guid? CustomerId { get; protected set; }
    public string OrderType { get; protected set; } // DINEIN, TAKEAWAY, DELIVERY
    public OrderStatusId Status { get; protected set; }
    public KitchenStatus KitchenStatus { get; protected set; }
    
    // Financial
    public decimal SubTotal { get; protected set; }
    public decimal TotalVatAmount { get; protected set; }
    public decimal ShippingFee { get; protected set; }
    public decimal DiscountAmount { get; protected set; }
    public decimal TotalAmount { get; protected set; }
    
    // Payment
    public string? PaymentMethod { get; protected set; }
    public string? PaymentStatus { get; protected set; }
    
    // Navigation
    public virtual ICollection<OrderItem> Items { get; protected set; }
}
```

### OrderItem

```csharp
public class OrderItem : BaseEntity
{
    public OrderItemId OrderItemId { get; protected set; }
    public Guid OrderId { get; protected set; }
    public Guid ProductId { get; protected set; }
    public int Quantity { get; protected set; }
    public decimal UnitPrice { get; protected set; }
    public string ProductName { get; protected set; }
    public KitchenStatus KitchenStatus { get; protected set; }
    
    // Calculated
    public decimal SubTotal => Quantity * UnitPrice;
    public decimal VatAmount => SubTotal * VatRate;
    public decimal TotalAmount => SubTotal + VatAmount;
}
```

### OrderStatus

```csharp
public record OrderStatusId(string Value)
{
    public static readonly OrderStatusId Pending = new("pending");
    public static readonly OrderStatusId Confirmed = new("confirmed");
    public static readonly OrderStatusId Preparing = new("preparing");
    public static readonly OrderStatusId Ready = new("ready");
    public static readonly OrderStatusId Completed = new("completed");
    public static readonly OrderStatusId Cancelled = new("cancelled");
}
```

### KitchenStatus

```csharp
public enum KitchenStatus
{
    Pending = 1,
    Received = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5
}
```

## Order Flow

```
┌─────────┐    ┌───────────┐    ┌───────────┐    ┌─────────┐    ┌───────────┐
│ PENDING │ → │ CONFIRMED │ → │ PREPARING │ → │  READY  │ → │ COMPLETED │
└─────────┘    └───────────┘    └───────────┘    └─────────┘    └───────────┘
                    ↓
               ┌───────────┐
               │ CANCELLED │
               └───────────┘
```

### State Transitions

| From | To | Condition | Side Effects |
|------|----|-----------|--------------|
| Pending | Confirmed | Payment confirmed hoặc COD | Deduct inventory |
| Pending | Cancelled | User cancel hoặc timeout | Release reservation |
| Confirmed | Preparing | Kitchen nhận đơn | Update KDS |
| Confirmed | Cancelled | Refund approved | Restore inventory |
| Preparing | Ready | Món xong | Notify server |
| Ready | Completed | Khách nhận món | Accounting entry |
| Any | Cancelled | Manager override | Restore inventory |

## Business Rules

### BR-001: Inventory Deduction
- Inventory được deduct khi order chuyển sang **Confirmed**
- Không deduct nếu order bị cancel trước Confirmed
- Restore inventory khi Confirmed → Cancelled

### BR-002: Kitchen Status
- Mỗi OrderItem có KitchenStatus riêng
- Order KitchenStatus là aggregation của items:
  - All Pending → Order Pending
  - Any Preparing → Order Preparing
  - All Ready/Served → Order Ready

### BR-003: Payment Flow
```
CASH: Confirmed immediately → No payment gateway
VIETQR: Pending → QR generated → Webhook callback → Confirmed
CREDIT_CARD: Pending → 3DS → Confirmed
```

### BR-004: Cancellation Policy
- **Pending**: Customer có thể cancel
- **Confirmed**: Cần staff/manager approval
- **Preparing/Ready**: Cần manager + Kitchen confirmation
- **Completed**: Không thể cancel, chỉ có thể refund

### BR-005: Financial Calculations
```csharp
SubTotal = SUM(Items.Quantity * Items.UnitPrice)
TotalVatAmount = SUM(Items.SubTotal * Items.VatRate)
ShippingFee = OrderType == DELIVERY ? Calculated : 0
DiscountAmount = AppliedDiscount?.Amount ?? 0
TotalAmount = SubTotal + TotalVatAmount + ShippingFee - DiscountAmount
```

## DTOs

### CreateOrderRequest
```csharp
public class CreateOrderRequest
{
    public string OrderType { get; set; } = "DINEIN";
    public Guid? CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
    public string? PaymentMethod { get; set; }
    public string? CustomerNotes { get; set; }
    public string? DeliveryAddress { get; set; }
}
```

### OrderResponse
```csharp
public class OrderResponse
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "";
    public string KitchenStatus { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
```

## Integration Points

| Domain | Integration | Trigger |
|--------|-------------|---------|
| Inventory | Deduct/Restore stock | Status change to/from Confirmed |
| Accounting | Create revenue entry | Status → Completed |
| Customer | Loyalty points | Status → Completed |
| Kitchen | KDS display | Status → Confirmed |
| Payment | Payment processing | Order creation |

## Events

```csharp
public record OrderCreated(OrderId OrderId, TenantId TenantId, DateTime CreatedAt);
public record OrderStatusChanged(OrderId OrderId, OrderStatusId OldStatus, OrderStatusId NewStatus);
public record OrderCancelled(OrderId OrderId, string Reason, decimal RefundAmount);
public record KitchenOrderReceived(OrderId OrderId, List<OrderItem> Items);
public record KitchenOrderReady(OrderId OrderId);
```

## Validation Rules

- **OrderType**: Must be DINEIN, TAKEAWAY, hoặc DELIVERY
- **Items**: Must have at least 1 item, all items must exist
- **CustomerId**: Optional (walk-in allowed)
- **DeliveryAddress**: Required if OrderType = DELIVERY
- **TotalAmount**: Must be > 0

## References

- `1_Shared/Domain.cs` - Order entities
- `1_Shared/DTOs/` - Order DTOs
- `.windsurf/skills/domain-integrity-validation.md`

---

*Last Updated: June 1, 2026*
