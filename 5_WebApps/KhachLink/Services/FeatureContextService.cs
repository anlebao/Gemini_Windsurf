namespace VanAn.KhachLink.Services;

/// <summary>
/// W2-5 (2026-11-14, FR-V6/V7): chuẩn hoá tenant-context resolution cho các feature
/// cần tenant (AI chat, voice call, voice note) + render rule tập trung.
///
/// Chain ưu tiên <see cref="GetEffectiveTenantIdAsync"/>:
///   1. explicit — tenantId page/card truyền vào (vd Store.razor _store.Id, order TenantId)
///   2. instance OwnerTenantId — tenant scope của KhachLinkInstance hiện tại
///   3. _lastTenantId — LastInteractionService (localStorage, tương tác gần nhất)
///   → null = không có tenant context → flagship feature KHÔNG render (không nút chết).
/// </summary>
public sealed class FeatureContextService
{
    private readonly Func<Task<Guid?>> _getOwnerTenantIdAsync;
    private readonly Func<Task<Guid>> _getLastTenantIdAsync;

    public FeatureContextService(InstanceContextService instanceContext, LastInteractionService lastInteraction)
        : this(
            async () => (await instanceContext.GetConfigAsync())?.OwnerTenantId,
            () => lastInteraction.GetLastTenantIdAsync())
    {
    }

    /// <summary>Test seam — inject providers trực tiếp (Core.Tests chain-order tests).</summary>
    public FeatureContextService(Func<Task<Guid?>> getOwnerTenantIdAsync, Func<Task<Guid>> getLastTenantIdAsync)
    {
        _getOwnerTenantIdAsync = getOwnerTenantIdAsync;
        _getLastTenantIdAsync = getLastTenantIdAsync;
    }

    /// <summary>
    /// Resolve tenant hiệu lực theo chain explicit → instance owner → last interaction.
    /// Trả null khi không có context nào (Guid.Empty coi như không có).
    /// </summary>
    public async Task<Guid?> GetEffectiveTenantIdAsync(Guid? explicitTenantId = null)
    {
        if (explicitTenantId.HasValue && explicitTenantId.Value != Guid.Empty)
        {
            return explicitTenantId.Value;
        }

        Guid? ownerTenantId = await _getOwnerTenantIdAsync();
        if (ownerTenantId.HasValue && ownerTenantId.Value != Guid.Empty)
        {
            return ownerTenantId.Value;
        }

        Guid lastTenantId = await _getLastTenantIdAsync();
        return lastTenantId != Guid.Empty ? lastTenantId : null;
    }

    /// <summary>True khi resolve được tenant context (dùng cho ShouldRenderFeature).</summary>
    public async Task<bool> HasTenantContextAsync(Guid? explicitTenantId = null)
        => await GetEffectiveTenantIdAsync(explicitTenantId) != null;

    /// <summary>
    /// FR-V7 render rule: feature chỉ render khi toggle ON **và** có tenant context.
    /// Cấm render nút disabled/dead trên trang không-tenant.
    /// </summary>
    public static bool ShouldRenderFeature(bool toggleEnabled, bool hasTenantContext)
        => toggleEnabled && hasTenantContext;
}
