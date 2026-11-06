namespace VanAn.TenantAi.Api.Auth;

/// <summary>SRS [D7] lớp 2 — endpoint declares which caller scope it requires.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}

/// <summary>SRS [D7] lớp 3 — endpoint requires a valid signed tenant context.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireTenantContextAttribute : Attribute;

public static class ScopeNames
{
    public const string PublicChat = "public-chat";
    public const string Ingest = "ingest";
    public const string Admin = "admin";
}

public static class EndpointAuthExtensions
{
    public static TBuilder RequireScope<TBuilder>(this TBuilder builder, string scope)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequireScopeAttribute(scope));

    public static TBuilder RequireTenantContext<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequireTenantContextAttribute());
}
