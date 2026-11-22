using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// UNIVERSAL AI ASSISTANT W1 (2026-11-22): AssistantLink entity tests.
    /// Link = slug high-entropy → tenant cho public /a/{slug}; revoke = IsActive=false;
    /// regenerate = revoke + tạo mới (không đổi slug tại chỗ).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Entity", "AssistantLink")]
    public class AssistantLinkTests
    {
        private static AssistantLink CreateLink(string slug = "aB3xK9mN2p", string? label = null)
            => new(new TenantId(Guid.NewGuid()), slug, label);

        [Fact(DisplayName = "Constructor sets slug, label, IsActive=true, TenantId")]
        public void Constructor_SetsFields()
        {
            var tenantId = Guid.NewGuid();
            var link = new AssistantLink(new TenantId(tenantId), "slug123", "Zalo");

            link.Slug.Should().Be("slug123");
            link.Label.Should().Be("Zalo");
            link.IsActive.Should().BeTrue();
            link.TenantId.Value.Should().Be(tenantId);
            link.RevokedAtUtc.Should().BeNull();
            link.RevokedBy.Should().BeNull();
        }

        [Fact(DisplayName = "Revoke sets IsActive=false + audit fields, idempotent")]
        public void Revoke_SetsInactiveAndAudit()
        {
            var link = CreateLink();
            link.Revoke("owner@test");

            link.IsActive.Should().BeFalse();
            link.RevokedAtUtc.Should().NotBeNull();
            link.RevokedBy.Should().Be("owner@test");

            var firstRevokedAt = link.RevokedAtUtc;
            link.Revoke("other@test"); // idempotent — không ghi đè
            link.RevokedAtUtc.Should().Be(firstRevokedAt);
            link.RevokedBy.Should().Be("owner@test");
        }

        [Fact(DisplayName = "Relabel updates label only, keeps active state")]
        public void Relabel_UpdatesLabel()
        {
            var link = CreateLink(label: "old");
            link.Relabel("QR quầy");

            link.Label.Should().Be("QR quầy");
            link.IsActive.Should().BeTrue();
        }

        [Fact(DisplayName = "Slug is immutable after construction (no setter path)")]
        public void Slug_HasNoPublicSetter()
        {
            typeof(AssistantLink).GetProperty(nameof(AssistantLink.Slug))!
                .GetSetMethod(false).Should().BeNull("slug không được đổi tại chỗ — regenerate tạo link mới");
        }
    }
}
