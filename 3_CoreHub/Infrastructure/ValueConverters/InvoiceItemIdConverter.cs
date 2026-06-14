using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// 2-way ValueConverter for InvoiceItemId Value Object
    /// </summary>
    public class InvoiceItemIdConverter : ValueConverter<InvoiceItemId, Guid>
    {
        public InvoiceItemIdConverter() : base(
            id => id.Value,
            value => new InvoiceItemId(value))
        {
        }
    }
}
