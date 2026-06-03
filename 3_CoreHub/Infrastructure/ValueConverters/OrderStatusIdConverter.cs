using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// 2-way ValueConverter for OrderStatusId Value Object
    /// </summary>
    public class OrderStatusIdConverter : ValueConverter<OrderStatusId, string>
    {
        public OrderStatusIdConverter() : base(
            id => id.Value,
            value => new OrderStatusId(value))
        {
        }
    }
}
