namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// ID-P1 (2026-11-10, user duyệt): trust tier cho DemoUser — L0-L3 rút gọn phía owner.
    /// None = chưa verify liên hệ · ContactVerified = L2 (SĐT/email đã OTP-verify) ·
    /// BusinessVerified = L3 (tenant đã sysadmin duyệt — chủ thể kinh doanh thật).
    /// </summary>
    public enum UserTrustLevel
    {
        None = 0,
        ContactVerified = 1,
        BusinessVerified = 2
    }
}