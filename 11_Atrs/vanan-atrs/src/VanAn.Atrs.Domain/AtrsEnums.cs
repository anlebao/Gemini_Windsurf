namespace VanAn.Atrs.Domain;

/// <summary>
/// Trust profile status (SRS FR-002 / §7.1).
/// </summary>
public enum TrustProfileStatus
{
    PENDING_SETUP = 0,
    ACTIVE = 1,
    RESTRICTED = 2,
    CLOSED = 3
}

/// <summary>
/// Assurance state (SRS §6.1 note) — khác Status: mô tả mức hoàn thiện bảo vệ,
/// không phải trạng thái vòng đời. Map UI: BASIC="Chưa thiết lập",
/// PROTECTED="Cơ bản", RECOVERY_READY="Đã tăng cường".
/// </summary>
public enum AssuranceState
{
    BASIC = 0,
    PROTECTED = 1,
    RECOVERY_READY = 2
}

/// <summary>
/// Loại identity hiện hữu được link vào TrustSubject (ADR-001 — canonical identity).
/// Customer = per-tenant (KhachLink), DemoUser = staff/owner (ShopERP),
/// DeviceCluster = cụm device chưa có account (guest stubs).
/// </summary>
public enum TrustIdentityType
{
    Customer = 1,
    DemoUser = 2,
    DeviceCluster = 3
}

/// <summary>
/// Actor của AccountSecurityEvent (FR-009).
/// </summary>
public enum SecurityEventActorType
{
    User = 1,
    System = 2,
    Staff = 3,
    Service = 4
}

/// <summary>
/// Kết quả của AccountSecurityEvent (FR-009 Outcome).
/// </summary>
public enum SecurityEventOutcome
{
    Success = 1,
    Failure = 2,
    Pending = 3,
    Blocked = 4
}