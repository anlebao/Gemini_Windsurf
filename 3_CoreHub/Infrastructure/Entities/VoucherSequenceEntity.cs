namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W2/C1 (G-H3, M6 — approved 2026-10-31): bộ đếm số chứng từ tuần tự per
/// (TenantId, VoucherType, Year). Unique index 3 cột; increment read-modify-write +
/// retry-on-conflict (VoucherNumberService).
/// VoucherType: PT (phiếu thu) · PC (phiếu chi) · PT-CN (công nợ 131) · PC-CN (331)
/// · CT (chuyển tiền — Phase D).
/// </summary>
public class VoucherSequenceEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string VoucherType { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public long LastNumber { get; private set; }
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private VoucherSequenceEntity() { }

    public VoucherSequenceEntity(Guid tenantId, string voucherType, int year)
    {
        TenantId = tenantId;
        VoucherType = voucherType;
        Year = year;
        LastNumber = 0;
    }

    /// <summary>Tăng bộ đếm, trả số mới (1-based).</summary>
    public long Next()
    {
        LastNumber++;
        UpdatedAt = DateTime.UtcNow;
        return LastNumber;
    }
}
