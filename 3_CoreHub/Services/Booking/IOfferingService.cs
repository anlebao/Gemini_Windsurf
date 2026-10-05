using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>Dữ liệu tạo/cập nhật 1 AppointmentOffering (SRS §8.2). Duration + price là SNAPSHOT tại booking time.</summary>
public sealed record CreateOfferingCommand(
    string DisplayName,
    OfferingType OfferingType,
    int DurationMinutes,
    decimal Price,
    Guid? CategoryId = null,
    string? RequiredSkillCode = null,
    string? Description = null);

/// <summary>1 dòng package (SRS §8.2 — package duration đã cấu hình trước; child name + giá là snapshot).</summary>
public sealed record PackageItemCommand(Guid? ChildOfferingId, string ChildName, int Quantity, decimal UnitPriceSnapshot);

/// <summary>
/// IOfferingService — CRUD ServiceCategory / AppointmentOffering / Package items / AddOn (SRS §8.2-8.3).
/// Đổi catalog KHÔNG ảnh hưởng booking cũ (snapshot §8.4). MỌI query filter TenantId.
/// </summary>
public interface IOfferingService
{
    // ── ServiceCategory ────────────────────────────────────────────────────
    Task<ServiceCategory> CreateCategoryAsync(TenantId tenantId, string name, int displayOrder = 0, CancellationToken ct = default);
    Task<ServiceCategory> UpdateCategoryAsync(TenantId tenantId, Guid categoryId, string name, int displayOrder, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceCategory>> ListCategoriesAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default);

    // ── AppointmentOffering (SERVICE | PACKAGE) ────────────────────────────
    Task<AppointmentOffering> CreateOfferingAsync(TenantId tenantId, CreateOfferingCommand command, CancellationToken ct = default);
    Task<AppointmentOffering> UpdateOfferingAsync(TenantId tenantId, Guid offeringId, CreateOfferingCommand command, bool isActive = true, CancellationToken ct = default);
    Task<AppointmentOffering?> GetOfferingAsync(TenantId tenantId, Guid offeringId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentOffering>> ListOfferingsAsync(TenantId tenantId, Guid? categoryId = null, bool activeOnly = false, CancellationToken ct = default);

    // ── Package items (chỉ cho OfferingType.Package) ───────────────────────
    Task<IReadOnlyList<AppointmentOfferingItem>> SetPackageItemsAsync(TenantId tenantId, Guid packageOfferingId, IReadOnlyList<PackageItemCommand> items, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentOfferingItem>> ListPackageItemsAsync(TenantId tenantId, Guid packageOfferingId, CancellationToken ct = default);

    // ── AddOn (non-scheduling, SRS §8.3) ───────────────────────────────────
    Task<AddOn> CreateAddOnAsync(TenantId tenantId, string name, decimal price, CancellationToken ct = default);
    Task<AddOn> UpdateAddOnAsync(TenantId tenantId, Guid addOnId, string name, decimal price, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyList<AddOn>> ListAddOnsAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default);
}
