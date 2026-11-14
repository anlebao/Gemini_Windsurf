namespace VanAn.KhachLink.Models
{
    /// <summary>W2-1: HTX card — mirrors Gateway CooperativesController.CooperativeDto (camelCase JSON).</summary>
    public sealed class CooperativeDto
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? LogoUrl { get; set; }
        public int MemberCount { get; set; }
        public string? KhachLinkDomain { get; set; }
    }

    /// <summary>W2-1: Member shop card — mirrors Gateway CooperativeMemberDto.</summary>
    public sealed class CooperativeMemberDto
    {
        public Guid MemberId { get; set; }
        public string MemberNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? LogoUrl { get; set; }
        public string? KhachLinkDomain { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
