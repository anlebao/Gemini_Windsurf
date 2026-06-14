using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// 2-way ValueConverter for ElectronicInvoiceId Value Object
    /// </summary>
    public class ElectronicInvoiceIdConverter : ValueConverter<ElectronicInvoiceId, Guid>
    {
        public ElectronicInvoiceIdConverter() : base(
            id => id.Value,
            value => new ElectronicInvoiceId(value))
        {
        }
    }
}
