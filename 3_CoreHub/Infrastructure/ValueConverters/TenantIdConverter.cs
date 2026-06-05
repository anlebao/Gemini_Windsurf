using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// Value converter for TenantId to Guid
    /// </summary>
    public sealed class TenantIdConverter : ValueConverter<TenantId, Guid>
    {
        public TenantIdConverter()
            : base(
                convertToProviderExpression: tenantId => tenantId.Value,
                convertFromProviderExpression: guid => new TenantId(guid))
        {
        }
    }
}
