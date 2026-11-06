namespace VanAn.TenantAi.Contracts;

/// <summary>Uniform error envelope for all /v1/* responses (pattern metering).</summary>
public sealed record ApiError(string Code, string Message, bool Retryable = false);

public static class ApiErrors
{
    public static ApiError MissingCredentials(string what) =>
        new("missing_credentials", $"{what} headers are required.");

    public static ApiError InvalidCredentials() =>
        new("invalid_credentials", "Unknown caller or invalid key.");

    public static ApiError Forbidden(string scope) =>
        new("forbidden", $"Caller lacks required scope: {scope}.");

    public static ApiError InvalidTenantContext(string reason) =>
        new("invalid_tenant_context", $"Signed tenant context rejected: {reason}.");
}
