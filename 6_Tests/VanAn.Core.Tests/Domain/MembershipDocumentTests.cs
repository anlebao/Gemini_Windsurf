using FluentAssertions;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using Xunit;

namespace VanAn.Core.Tests.Domain;

/// <summary>
/// Membership Infrastructure (2026-09-29): Domain tests cho MembershipDocument (SRS §17.2).
/// Các cách xác nhận đồng tham gia: Signature (chữ ký online — evidence), PaperApplication (form giấy ký tay).
/// Append-only evidence — không có Update/Delete method.
/// </summary>
public class MembershipDocumentTests
{
    private static readonly TenantId HtxId = new(Guid.NewGuid());
    private static readonly Guid ApplicationId = Guid.NewGuid();

    [Fact(DisplayName = "Create_SignatureDocument_SetsEvidence")]
    public void Create_SignatureDocument_SetsEvidence()
    {
        var doc = MembershipDocument.Create(
            HtxId, ApplicationId, MembershipDocumentType.Signature,
            "v2026-09-01", "https://res.cloudinary.com/vanan/signatures/abc.png",
            "sha256-hash");

        doc.DocumentType.Should().Be(MembershipDocumentType.Signature);
        doc.StorageReference.Should().Be("https://res.cloudinary.com/vanan/signatures/abc.png");
        doc.Hash.Should().Be("sha256-hash");
        doc.ApplicationId.Should().Be(ApplicationId);
        doc.TenantId.Should().Be(HtxId);
    }

    [Fact(DisplayName = "Create_PaperApplication_AppendOnly")]
    public void Create_PaperApplication_AppendOnly()
    {
        var doc = MembershipDocument.Create(
            HtxId, ApplicationId, MembershipDocumentType.PaperApplication,
            "v2026-09-01", "https://res.cloudinary.com/vanan/paper-forms/scan.png");

        doc.DocumentType.Should().Be(MembershipDocumentType.PaperApplication);
        doc.Hash.Should().BeNull();
    }

    [Fact(DisplayName = "Create_EmptyApplicationId_Throws")]
    public void Create_EmptyApplicationId_Throws()
    {
        var act = () => MembershipDocument.Create(
            HtxId, Guid.Empty, MembershipDocumentType.Signature, "v1", "https://x/y.png");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Create_EmptyStorageReference_Throws")]
    public void Create_EmptyStorageReference_Throws()
    {
        var act = () => MembershipDocument.Create(
            HtxId, ApplicationId, MembershipDocumentType.Other, "v1", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Document_IsAppendOnly_NoUpdateOrDeleteMethod")]
    public void Document_IsAppendOnly_NoUpdateOrDeleteMethod()
    {
        // Evidence pháp lý: chỉ thêm, không sửa/xóa (SRS §17.2)
        typeof(MembershipDocument).GetMethods()
            .Select(m => m.Name)
            .Should().NotContain(new[] { "Update", "Delete", "SoftDelete" });
    }
}
