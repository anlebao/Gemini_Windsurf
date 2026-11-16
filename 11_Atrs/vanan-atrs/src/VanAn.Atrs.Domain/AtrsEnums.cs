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

// ══════════════════════════════════════════════════════════
// ATRS-9 (Wave 3) — Dispute vertical (FR-012, FR-013, FR-014)
// ══════════════════════════════════════════════════════════

/// <summary>Loại tranh chấp (FR-012) — case type do user/staff chọn khi tạo.</summary>
public enum DisputeCaseType
{
    ACCOUNT_TAKEOVER = 0,
    LOST_DEVICE = 1,
    POINTS_MISSING = 2,
    POINTS_EXCESS = 3,
    POINTS_WRONG = 4,
    WALLET_BENEFIT_WRONG = 5,
    TRANSACTION_DENIED = 6,
    POINTS_ADJUST_REFUND = 7,
    OWNERSHIP_DISPUTE = 8,
    SYSTEM_DISCREPANCY = 9
}

/// <summary>
/// Trạng thái DisputeCase (FR-012). Terminal: RESOLVED / REJECTED / CLOSED.
/// RESOLVED chỉ đạt SAU khi ledger adjustment confirm (FR-013, UI A19 rule).
/// </summary>
public enum DisputeCaseStatus
{
    OPEN = 0,
    TRIAGE = 1,
    WAITING_FOR_USER = 2,
    UNDER_REVIEW = 3,
    WAITING_FOR_EXTERNAL_EVIDENCE = 4,
    DECIDED = 5,
    RESOLVED = 6,
    REJECTED = 7,
    CLOSED = 8
}

/// <summary>Loại quyết định dispute — append-only trên DisputeDecision.</summary>
public enum DisputeDecisionKind
{
    APPROVED = 0,             // chấp nhận khiếu nại (có thể kèm điều chỉnh)
    PARTIALLY_APPROVED = 1,   // chấp nhận một phần
    REJECTED = 2,             // từ chối khiếu nại
    NO_ACTION = 3             // không cần điều chỉnh — chỉ giải thích cho user
}

/// <summary>Vòng đời của một DisputeDecision (maker-checker).</summary>
public enum DisputeDecisionStatus
{
    PENDING_APPROVAL = 0,
    APPROVED = 1,
    REJECTED = 2,             // approver từ chối proposal → quay lại UNDER_REVIEW
    EXECUTED = 3,             // adjustment đã ghi ledger thành công
    FAILED = 4                // ledger adjust fail → case quay lại UNDER_REVIEW
}

/// <summary>Loại bằng chứng đính kèm (FR-012 evidence items).</summary>
public enum DisputeEvidenceType
{
    SCREENSHOT = 0,
    RECEIPT = 1,
    ORDER_REFERENCE = 2,
    CHAT_LOG = 3,
    OTHER = 4
}

/// <summary>Độ tin cậy của evidence — phục vụ investigator, KHÔNG lộ cho user timeline.</summary>
public enum EvidenceReliabilityClass
{
    USER_CLAIM = 0,
    SYSTEM_RECORD = 1,
    THIRD_PARTY = 2
}

/// <summary>Phân loại truy cập evidence (FR-014 — staff scope).</summary>
public enum EvidenceAccessClass
{
    USER_VISIBLE = 0,    // user thấy được trong U13
    STAFF_ONLY = 1,      // chỉ staff có permission dispute.view
    RESTRICTED = 2       // chỉ approver/admin — vd fraud signals
}