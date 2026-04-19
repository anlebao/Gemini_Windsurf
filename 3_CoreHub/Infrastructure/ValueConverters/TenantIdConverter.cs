using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

public class TenantIdConverter : ValueConverter<TenantId, Guid>
{
    public TenantIdConverter() : base(
        convertToProviderExpression: tenantId => tenantId != null ? tenantId.Value : Guid.Empty,
        convertFromProviderExpression: value => value != Guid.Empty ? new TenantId(value) : null
    ) { }
}
