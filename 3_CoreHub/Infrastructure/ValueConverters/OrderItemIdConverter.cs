using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// 2-way ValueConverter for OrderItemId Value Object
    /// </summary>
    public class OrderItemIdConverter : ValueConverter<OrderItemId, Guid>
    {
        public OrderItemIdConverter() : base(
            id => id.Value,
            value => new OrderItemId(value))
        {
        }
    }
}
