namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Loại thành viên HTX theo Luật HTX 2023 (SRS §20).
    /// System phải configurable — không hard-code quyền vào frontend.
    /// </summary>
    public enum MembershipType
    {
        OfficialMember = 1,
        LinkedCapitalMember = 2,
        LinkedNonCapitalMember = 3
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Lifecycle của hồ sơ xin gia nhập (SRS §7).
    /// Draft — tạo bởi applicant/CTV (assisted registration), chưa nộp.
    /// Submitted — đang chờ HTX review (NEW/UNDER_REVIEW trên dashboard SRS §15).
    /// NeedInfo — HTX yêu cầu bổ sung thông tin; applicant bổ sung rồi Resubmit → Submitted.
    /// Approved/Rejected — quyết định của HTX (Vạn An KHÔNG tự quyết — SRS §3.1).
    ///
    /// Không xóa hồ sơ vật lý — chỉ chuyển trạng thái (SRS §7, Data Integrity Contract).
    /// </summary>
    public enum ApplicationStatus
    {
        Draft = 0,
        Submitted = 1,
        NeedInfo = 2,
        Approved = 3,
        Rejected = 4
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Trạng thái thành viên sau khi ACTIVE (SRS §7).
    /// ACTIVE → SUSPENDED (tạm đình chỉ) / RESIGNED (xin rút) / TERMINATED (chấm dứt).
    /// Cấm xóa member record vật lý — giữ lịch sử pháp lý/audit.
    /// </summary>
    public enum MemberStatus
    {
        Active = 1,
        Suspended = 2,
        Resigned = 3,
        Terminated = 4
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Mức xác thực danh tính (SRS §11).
    /// Level 1 — OTP. Level 2 — thông tin định danh cá nhân.
    /// Level 3 — KYC phù hợp (Phase 3 — không build ở MVP). Level 4 — manual verification.
    /// Hệ thống KHÔNG lưu ảnh CCCD plaintext — chỉ lưu level + evidence ref (SRS §12, SR-06).
    /// </summary>
    public enum IdentityVerificationLevel
    {
        Level1Otp = 1,
        Level2PersonalInfo = 2,
        Level3Kyc = 3,
        Level4Manual = 4
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Trạng thái vốn/phí thành viên (SRS §16, §23).
    /// MVP chỉ theo dõi status — module Capital/Fee đầy đủ là Phase 2+.
    /// KHÔNG tạo ví nội bộ giữ tiền (SRS §23).
    /// </summary>
    public enum CapitalFeeStatus
    {
        None = 0,
        Pending = 1,
        Satisfied = 2
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Vai trò dự kiến khi đăng ký (SRS §10 Screen 3).
    /// </summary>
    public enum ExpectedRole
    {
        Seller = 1,
        Ctv = 2,
        Shipper = 3,
        Supplier = 4,
        Customer = 5,
        Other = 6
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Loại tài liệu consent (SRS §14).
    /// Mục tiêu: chứng minh người dùng đã xác nhận PHIÊN BẢN nào tại THỜI ĐIỂM nào.
    /// </summary>
    public enum ConsentDocumentType
    {
        Charter = 1,      // Điều lệ HTX
        Terms = 2,        // Điều kiện gia nhập / quyền nghĩa vụ
        DataPolicy = 3    // Chính sách dữ liệu
    }

    /// <summary>
    /// Membership Infrastructure (2026-09-29): Loại tài liệu đính kèm hồ sơ (SRS §17.2 MembershipDocument).
    /// Các cách xác nhận đồng tham gia HTX:
    /// - Signature: chữ ký online vẽ tay (canvas capture — EVIDENCE, KHÔNG phải chữ ký số — SRS §14).
    /// - PaperApplication: form giấy đã in + ký tay + scan/ảnh upload (Assisted Registration — SRS §26-27).
    /// - Charter: bản Điều lệ phiên bản được xác nhận (optional evidence).
    /// - IdProof: giấy tờ định danh (Level 2+ — Phase 2).
    /// Chữ ký số thật (Viettel CA/VNPT CA) = Phase 3 (SRS §40) — không nằm enum này.
    /// </summary>
    public enum MembershipDocumentType
    {
        Charter = 1,
        PaperApplication = 2,
        Signature = 3,
        IdProof = 4,
        Other = 5
    }
}
