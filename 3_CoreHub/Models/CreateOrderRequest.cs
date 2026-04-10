using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Models;

public record CreateOrderRequest(
    ProductId ProductId,
    int Quantity,
    decimal TotalPrice
);
