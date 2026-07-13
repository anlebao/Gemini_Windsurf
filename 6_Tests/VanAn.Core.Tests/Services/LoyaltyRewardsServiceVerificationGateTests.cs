using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Tiered Auth Phase 2 — TDD tests for verification gate in SubtractPointsAsync.
    /// Gate rule: customer.IdentityLevel >= Verified required to redeem points.
    /// Earn (AddPointsAsync) is NOT gated — Social customers can still earn.
    /// </summary>
    public class LoyaltyRewardsServiceVerificationGateTests
    {
        private static readonly Guid TestCustomerId = Guid.NewGuid();
        private static readonly TenantId TestTenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        private static (Mock<ILoyaltyRewardsRepository> repo, LoyaltyRewardsService svc) BuildSut(Customer? customer, LoyaltyRewards? rewards = null)
        {
            var repo = new Mock<ILoyaltyRewardsRepository>();

            repo.Setup(r => r.GetCustomerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(customer);

            repo.Setup(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(rewards);

            repo.Setup(r => r.AddAsync(It.IsAny<LoyaltyRewards>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LoyaltyRewards r, CancellationToken _) => r);

            repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<IDbContextTransaction>());

            var svc = new LoyaltyRewardsService(repo.Object, NullLogger<LoyaltyRewardsService>.Instance);
            return (repo, svc);
        }

        private static Customer MakeCustomer(IdentityLevel level)
        {
            var c = new Customer(TestTenantId, "Test Customer", "0900000001", "test@example.com");
            if (level > IdentityLevel.Social)
                c.UpgradeIdentityLevel(level);
            return c;
        }

        private static LoyaltyRewards MakeRewards(int balance)
        {
            var r = new LoyaltyRewards(TestTenantId, TestCustomerId);
            // Seed balance via public API (AddPoints doesn't gate earn)
            if (balance > 0) r.AddPoints(balance, "seed");
            return r;
        }

        [Fact(DisplayName = "P2-T1: SubtractPointsAsync throws IdentityLevelNotSufficientException when customer is Social")]
        public async Task SubtractPointsAsync_SocialCustomer_ThrowsGateException()
        {
            // Arrange — Social customer with enough balance, must be blocked by gate
            var customer = MakeCustomer(IdentityLevel.Social);
            var rewards = MakeRewards(500);
            var (_, svc) = BuildSut(customer, rewards);

            // Act
            var act = () => svc.SubtractPointsAsync(TestCustomerId, 100, "redeem reward");

            // Assert
            var ex = await act.Should().ThrowAsync<IdentityLevelNotSufficientException>();
            ex.Which.CurrentLevel.Should().Be(IdentityLevel.Social);
            ex.Which.RequiredLevel.Should().Be(IdentityLevel.Verified);
            ex.Which.CustomerId.Should().Be(TestCustomerId);
        }

        [Fact(DisplayName = "P2-T2: SubtractPointsAsync succeeds when customer is Verified (gate passes)")]
        public async Task SubtractPointsAsync_VerifiedCustomer_SucceedsAndDeductsPoints()
        {
            // Arrange — Verified customer with sufficient balance
            var customer = MakeCustomer(IdentityLevel.Verified);
            var rewards = MakeRewards(500);
            var (repo, svc) = BuildSut(customer, rewards);

            // Act
            var result = await svc.SubtractPointsAsync(TestCustomerId, 100, "redeem reward");

            // Assert
            result.Should().BeTrue();
            repo.Verify(r => r.UpdateAsync(It.IsAny<LoyaltyRewards>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact(DisplayName = "P2-T3: SubtractPointsAsync succeeds when customer is Full (highest level)")]
        public async Task SubtractPointsAsync_FullCustomer_Succeeds()
        {
            // Arrange
            var customer = MakeCustomer(IdentityLevel.Full);
            var rewards = MakeRewards(500);
            var (_, svc) = BuildSut(customer, rewards);

            // Act
            var result = await svc.SubtractPointsAsync(TestCustomerId, 100, "redeem reward");

            // Assert
            result.Should().BeTrue();
        }

        [Fact(DisplayName = "P2-T4: SubtractPointsAsync throws for Guest customer (below Social)")]
        public async Task SubtractPointsAsync_GuestCustomer_ThrowsGateException()
        {
            // Arrange — Guest is below Social; gate must block
            var customer = MakeCustomer(IdentityLevel.Guest);
            var rewards = MakeRewards(500);
            var (_, svc) = BuildSut(customer, rewards);

            // Act
            var act = () => svc.SubtractPointsAsync(TestCustomerId, 100, "redeem reward");

            // Assert
            await act.Should().ThrowAsync<IdentityLevelNotSufficientException>();
        }

        [Fact(DisplayName = "P2-T5: AddPointsAsync is NOT gated — Social customer can still earn")]
        public async Task AddPointsAsync_SocialCustomer_SucceedsEarnNotGated()
        {
            // Arrange — earn must remain ungated per task card boundary
            var customer = MakeCustomer(IdentityLevel.Social);
            var rewards = MakeRewards(0);
            var (repo, svc) = BuildSut(customer, rewards);

            // Act
            var result = await svc.AddPointsAsync(TestCustomerId, 50, "earn from order");

            // Assert
            result.Should().BeTrue();
            repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact(DisplayName = "P2-T6: SubtractPointsAsync still returns false for insufficient balance AFTER gate passes (Verified)")]
        public async Task SubtractPointsAsync_VerifiedButInsufficientBalance_ReturnsFalse()
        {
            // Arrange — Verified passes gate but balance too low
            var customer = MakeCustomer(IdentityLevel.Verified);
            var rewards = MakeRewards(50); // balance < requested
            var (_, svc) = BuildSut(customer, rewards);

            // Act
            var result = await svc.SubtractPointsAsync(TestCustomerId, 100, "redeem reward");

            // Assert — gate passed, but balance check returns false (not throw)
            result.Should().BeFalse();
        }
    }
}
