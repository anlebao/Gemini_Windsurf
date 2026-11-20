using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// KhachLink Profile Contextual UX v2: EF persistence cho customer-dim context cards
/// trên trang /profile. Mỗi row = 1 lần customer bấm "Ẩn" trên 1 rule card
/// (cardId ổn định, vd "active-order", "voucher-expiring").
///
/// Data-only Infrastructure entity — precedent: <see cref="ShopFeatureSettingsEntity"/>.
/// TTL-based: <see cref="ExpiresAt"/> = DismissedAt + <see cref="DefaultTtlDays"/>;
/// card tự xuất hiện lại sau khi hết hạn (customer vẫn cần thấy nếu tín hiệu còn).
/// </summary>
public class CustomerContextDismissalEntity : BaseEntity
{
    /// <summary>Số ngày card bị ẩn trước khi tự hiện lại.</summary>
    public const int DefaultTtlDays = 7;

    /// <summary>Customer đã dismiss (Customer.Id == CustomerId.Value — single-identity).</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Stable rule card id (khớp ProfileContextCardBuilder ids).</summary>
    public string CardId { get; private set; } = string.Empty;

    public DateTime DismissedAt { get; private set; }

    /// <summary>Thời điểm card được phép hiện lại.</summary>
    public DateTime ExpiresAt { get; private set; }

    private CustomerContextDismissalEntity() { } // EF Core materialization

    public CustomerContextDismissalEntity(TenantId tenantId, Guid customerId, string cardId)
        : base(tenantId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId cannot be empty.");
        if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("CardId cannot be empty.");
        if (cardId.Length > 64) throw new ArgumentException("CardId max 64 chars.");

        CustomerId = customerId;
        CardId = cardId.Trim();
        DismissedAt = DateTime.UtcNow;
        ExpiresAt = DismissedAt.AddDays(DefaultTtlDays);
    }

    /// <summary>Re-dismiss cùng card → refresh TTL (không tạo row mới — caller upsert).</summary>
    public void Refresh()
    {
        DismissedAt = DateTime.UtcNow;
        ExpiresAt = DismissedAt.AddDays(DefaultTtlDays);
        UpdateAudit();
    }

    public bool IsActive(DateTime utcNow) => ExpiresAt > utcNow;
}
