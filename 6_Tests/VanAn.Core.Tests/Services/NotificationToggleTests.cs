using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// AF-P1-T2 (TDD): Notification toggle tests — verify per-tenant ShopFeatureSettings toggles
    /// gate push notifications without blocking core business logic (points awarding, fulfillment, job queries).
    ///
    /// 5 tests covering 4 toggles:
    ///  1. Notify_RedemptionFulfilled ON  → SendRedemptionFulfilledNotificationAsync called
    ///  2. Notify_RedemptionFulfilled OFF → notification skipped, fulfillment still succeeds
    ///  3. Notify_MissionCompleted ON     → SendLoyaltyPointsChangedNotificationAsync called
    ///  4. Notify_BirthdayBonus OFF       → SendBirthdayNotificationAsync skipped, points still awarded
    ///  5. Notify_VoucherExpiringSoon OFF → SendVoucherExpiryReminderAsync skipped, job still queries vouchers
    ///
    /// Production code changes enabling these tests (minimal, non-breaking):
    ///  - PushNotificationService: 4 Send*NotificationAsync methods marked `virtual` (Moq intercept)
    ///  - BirthdayBonusJob.RunBirthdayBonusAsync: private → internal
    ///  - VoucherExpiryReminderJob.RunExpiryRemindersAsync: private → internal
    ///  - ShopERP: InternalsVisibleTo("VanAn.Core.Tests")
    /// </summary>
    public class NotificationToggleTests
    {
        private static readonly Guid TestCustomerId = Guid.NewGuid();
        private static readonly Guid TestCatalogItemId = Guid.NewGuid();
        private static readonly TenantId TestTenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        // === Helper: Mock PushNotificationService (virtual methods, dummy VAPID config) ===

        private static Mock<PushNotificationService> CreatePushMock()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PushNotifications:VapidPrivateKey"] = "test-vapid-private-key",
                    ["PushNotifications:VapidPublicKey"] = "test-vapid-public-key",
                    ["PushNotifications:VapidSubject"] = "mailto:test@vanan.com"
                })
                .Build();

            var subRepo = new Mock<IPushSubscriptionRepository>();
            return new Mock<PushNotificationService>(config, NullLogger<PushNotificationService>.Instance, subRepo.Object, null, null);
        }

        private static ShopFeatureSettingsDto DtoWith(
            bool notifyRedemptionFulfilled = true,
            bool notifyMissionCompleted = true,
            bool notifyBirthdayBonus = true,
            bool notifyVoucherExpiringSoon = true) => new()
        {
            Notify_RedemptionFulfilled = notifyRedemptionFulfilled,
            Notify_MissionCompleted = notifyMissionCompleted,
            Notify_BirthdayBonus = notifyBirthdayBonus,
            Notify_VoucherExpiringSoon = notifyVoucherExpiringSoon
        };

        // === Test 1: Notify_RedemptionFulfilled ON → push sent ===

        [Fact(DisplayName = "AF-P1-T2-T1: Notify_RedemptionFulfilled=ON → SendRedemptionFulfilledNotificationAsync called")]
        public async Task Notify_RedemptionFulfilled_On_SendsPush()
        {
            // Arrange — voucher active + valid, record exists, toggle ON
            var voucher = new Voucher(TestTenantId, Guid.NewGuid(), TestCustomerId, "VC-ON-001", DateTime.UtcNow.AddDays(7));
            var record = new RedemptionRecord(TestTenantId, TestCustomerId, TestCatalogItemId, 100);

            var repo = new Mock<IRedemptionRepository>();
            repo.Setup(r => r.GetVoucherByCodeAsync("VC-ON-001")).ReturnsAsync(voucher);
            repo.Setup(r => r.UpdateVoucherAsync(It.IsAny<Voucher>())).ReturnsAsync(voucher);
            repo.Setup(r => r.GetRecordByIdAsync(voucher.RedemptionRecordId)).ReturnsAsync(record);
            repo.Setup(r => r.UpdateRecordAsync(It.IsAny<RedemptionRecord>())).ReturnsAsync(record);
            repo.Setup(r => r.GetCatalogItemByIdAsync(TestCatalogItemId)).ReturnsAsync((RedemptionCatalogItem?)null);

            var settingsMock = new Mock<IShopFeatureSettingsService>();
            settingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(DtoWith(notifyRedemptionFulfilled: true));

            var pushMock = CreatePushMock();
            pushMock.Setup(p => p.SendRedemptionFulfilledNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
                    .ReturnsAsync(1);

            var sut = new RedemptionService(
                repo.Object,
                new Mock<ILoyaltyRewardsService>().Object,
                new Mock<ITenantProvider>().Object,
                new Mock<IVanAnDbContext>().Object,
                settingsMock.Object,
                pushMock.Object,
                NullLogger<RedemptionService>.Instance);

            // Act
            bool result = await sut.FulfillAsync("VC-ON-001");

            // Assert — fulfillment succeeds + push called
            _ = result.Should().BeTrue();
            pushMock.Verify(p => p.SendRedemptionFulfilledNotificationAsync(TestCustomerId, "VC-ON-001", null), Times.Once);
        }

        // === Test 2: Notify_RedemptionFulfilled OFF → push skipped, fulfillment succeeds ===

        [Fact(DisplayName = "AF-P1-T2-T2: Notify_RedemptionFulfilled=OFF → push skipped, fulfillment still succeeds")]
        public async Task Notify_RedemptionFulfilled_Off_SkipsPush()
        {
            // Arrange — same setup but toggle OFF
            var voucher = new Voucher(TestTenantId, Guid.NewGuid(), TestCustomerId, "VC-OFF-001", DateTime.UtcNow.AddDays(7));
            var record = new RedemptionRecord(TestTenantId, TestCustomerId, TestCatalogItemId, 100);

            var repo = new Mock<IRedemptionRepository>();
            repo.Setup(r => r.GetVoucherByCodeAsync("VC-OFF-001")).ReturnsAsync(voucher);
            repo.Setup(r => r.UpdateVoucherAsync(It.IsAny<Voucher>())).ReturnsAsync(voucher);
            repo.Setup(r => r.GetRecordByIdAsync(voucher.RedemptionRecordId)).ReturnsAsync(record);
            repo.Setup(r => r.UpdateRecordAsync(It.IsAny<RedemptionRecord>())).ReturnsAsync(record);
            repo.Setup(r => r.GetCatalogItemByIdAsync(TestCatalogItemId)).ReturnsAsync((RedemptionCatalogItem?)null);

            var settingsMock = new Mock<IShopFeatureSettingsService>();
            settingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(DtoWith(notifyRedemptionFulfilled: false));

            var pushMock = CreatePushMock();
            pushMock.Setup(p => p.SendRedemptionFulfilledNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
                    .ReturnsAsync(1);

            var sut = new RedemptionService(
                repo.Object,
                new Mock<ILoyaltyRewardsService>().Object,
                new Mock<ITenantProvider>().Object,
                new Mock<IVanAnDbContext>().Object,
                settingsMock.Object,
                pushMock.Object,
                NullLogger<RedemptionService>.Instance);

            // Act
            bool result = await sut.FulfillAsync("VC-OFF-001");

            // Assert — fulfillment succeeds + push NOT called
            _ = result.Should().BeTrue();
            pushMock.Verify(p => p.SendRedemptionFulfilledNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        // === Test 3: Notify_MissionCompleted ON → push sent ===

        [Fact(DisplayName = "AF-P1-T2-T3: Notify_MissionCompleted=ON → SendLoyaltyPointsChangedNotificationAsync called")]
        public async Task Notify_MissionCompleted_On_SendsPush()
        {
            // Arrange — non-one-time mission, no daily cap (simplifies mock setup)
            var mission = new Mission(TestTenantId, MissionType.FacebookShare, "Share on Facebook", 50);
            mission.UpdateDetails("Share on Facebook", null, 50, isOneTime: false, dailyCap: null, isActive: true, sortOrder: 0, config: null);

            var customer = new Customer(TestTenantId, "Test Customer", "0900000001", "test@example.com");
            var rewards = new LoyaltyRewards(TestTenantId, TestCustomerId);
            rewards.AddPoints(100, "seed");

            var missionRepo = new Mock<IMissionRepository>();
            missionRepo.Setup(r => r.GetMissionByTypeAsync(MissionType.FacebookShare)).ReturnsAsync(mission);
            missionRepo.Setup(r => r.AddCompletionAsync(It.IsAny<MissionCompletion>()))
                       .ReturnsAsync((MissionCompletion c) => c); // pass-through
            // Non-one-time + no daily cap → count methods NOT called, but set up just in case
            missionRepo.Setup(r => r.CountCompletionsTodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(0);
            missionRepo.Setup(r => r.CountCompletionsByMissionAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(0);

            var customerRepo = new Mock<ICustomerRepository>();
            customerRepo.Setup(r => r.GetByIdAsync(TestCustomerId)).ReturnsAsync(customer);
            customerRepo.Setup(r => r.UpdateAsync(It.IsAny<Customer>())).ReturnsAsync(customer);

            var loyaltyMock = new Mock<ILoyaltyRewardsService>();
            loyaltyMock.Setup(l => l.AddPointsAsync(TestCustomerId, 50, It.IsAny<string>())).ReturnsAsync(true);
            loyaltyMock.Setup(l => l.GetCustomerRewardsAsync(TestCustomerId)).ReturnsAsync(rewards);

            var tenantProvider = new Mock<ITenantProvider>();
            tenantProvider.SetupGet(t => t.TenantId).Returns(TestTenantId.Value);

            var dbContext = new Mock<IVanAnDbContext>();
            dbContext.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Mock.Of<IDbContextTransaction>());

            var settingsMock = new Mock<IShopFeatureSettingsService>();
            settingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(DtoWith(notifyMissionCompleted: true));

            var pushMock = CreatePushMock();
            pushMock.Setup(p => p.SendLoyaltyPointsChangedNotificationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>()))
                    .ReturnsAsync(1);

            var sut = new MissionService(
                missionRepo.Object,
                customerRepo.Object,
                loyaltyMock.Object,
                tenantProvider.Object,
                dbContext.Object,
                settingsMock.Object,
                pushMock.Object,
                NullLogger<MissionService>.Instance);

            // Act
            var result = await sut.CompleteMissionAsync(TestCustomerId, MissionType.FacebookShare);

            // Assert — mission succeeds + push called
            _ = result.Success.Should().BeTrue();
            pushMock.Verify(p => p.SendLoyaltyPointsChangedNotificationAsync(TestCustomerId, 50, It.IsAny<int>(), It.IsAny<string?>()), Times.Once);
        }

        // === Test 4: Notify_BirthdayBonus OFF → push skipped, points still awarded ===

        [Fact(DisplayName = "AF-P1-T2-T4: Notify_BirthdayBonus=OFF → SendBirthdayNotificationAsync skipped, points still awarded")]
        public async Task Notify_BirthdayBonus_Off_StillAwardsPoints()
        {
            // Arrange — build ServiceProvider with mocked services
            var customer = new Customer(TestTenantId, "Birthday Customer", "0900000099", "bday@example.com");

            var customerRepo = new Mock<ICustomerRepository>();
            customerRepo.Setup(r => r.GetCustomersWithBirthdayTodayAsync())
                        .ReturnsAsync(new List<Customer> { customer }.AsReadOnly());

            var missionService = new Mock<IMissionService>();
            missionService.Setup(m => m.CompleteAnnualMissionAsync(customer.Id, MissionType.Custom, It.IsAny<string?>()))
                          .ReturnsAsync(MissionCompletionResult.Ok(
                              new MissionCompletion(TestTenantId, customer.Id, Guid.NewGuid(), 100, null), 100, 100));

            var settingsMock = new Mock<IShopFeatureSettingsService>();
            settingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(DtoWith(notifyBirthdayBonus: false));

            var pushMock = CreatePushMock();
            pushMock.Setup(p => p.SendBirthdayNotificationAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>()))
                    .ReturnsAsync(1);

            var tenantProvider = new Mock<ITenantProvider>();

            var services = new ServiceCollection();
            services.AddSingleton<ITenantProvider>(tenantProvider.Object);
            services.AddSingleton<ICustomerRepository>(customerRepo.Object);
            services.AddSingleton<IMissionService>(missionService.Object);
            services.AddSingleton<PushNotificationService>(pushMock.Object);
            services.AddSingleton<IShopFeatureSettingsService>(settingsMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:TenantId"] = TestTenantId.Value.ToString()
                })
                .Build();

            var job = new BirthdayBonusJob(serviceProvider, config, NullLogger<BirthdayBonusJob>.Instance, CreateToggleMock());

            // Act — call the internal method directly (bypasses ExecuteAsync's 5-min initial delay)
            await job.RunBirthdayBonusAsync(CancellationToken.None);

            // Assert — points awarded (CompleteAnnualMissionAsync called), push NOT sent
            missionService.Verify(m => m.CompleteAnnualMissionAsync(customer.Id, MissionType.Custom, It.IsAny<string?>()), Times.Once);
            pushMock.Verify(p => p.SendBirthdayNotificationAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
        }

        // === Test 5: Notify_VoucherExpiringSoon OFF → push skipped, job still queries ===

        [Fact(DisplayName = "AF-P1-T2-T5: Notify_VoucherExpiringSoon=OFF → SendVoucherExpiryReminderAsync skipped, job still queries")]
        public async Task Notify_VoucherExpiringSoon_Off_SkipsPush()
        {
            // Arrange — voucher expiring soon, toggle OFF
            var voucher = new Voucher(TestTenantId, Guid.NewGuid(), TestCustomerId, "VC-EXP-001", DateTime.UtcNow.AddDays(2));

            var redemptionRepo = new Mock<IRedemptionRepository>();
            redemptionRepo.Setup(r => r.GetVouchersExpiringWithinAsync(It.IsAny<int>()))
                          .ReturnsAsync(new List<Voucher> { voucher }.AsReadOnly());

            var settingsMock = new Mock<IShopFeatureSettingsService>();
            settingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(DtoWith(notifyVoucherExpiringSoon: false));

            var pushMock = CreatePushMock();
            pushMock.Setup(p => p.SendVoucherExpiryReminderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<int>()))
                    .ReturnsAsync(1);

            var tenantProvider = new Mock<ITenantProvider>();

            var services = new ServiceCollection();
            services.AddSingleton<ITenantProvider>(tenantProvider.Object);
            services.AddSingleton<IRedemptionRepository>(redemptionRepo.Object);
            services.AddSingleton<PushNotificationService>(pushMock.Object);
            services.AddSingleton<IShopFeatureSettingsService>(settingsMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:TenantId"] = TestTenantId.Value.ToString(),
                    ["LoyaltyC:VoucherExpiryReminderDays"] = "3"
                })
                .Build();

            var job = new VoucherExpiryReminderJob(serviceProvider, config, NullLogger<VoucherExpiryReminderJob>.Instance, CreateToggleMock());

            // Act
            await job.RunExpiryRemindersAsync(CancellationToken.None);

            // Assert — job queried vouchers (GetVouchersExpiringWithinAsync called), push NOT sent
            redemptionRepo.Verify(r => r.GetVouchersExpiringWithinAsync(It.IsAny<int>()), Times.Once);
            pushMock.Verify(p => p.SendVoucherExpiryReminderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>REQ-1.2: Creates a toggle mock that returns true (enabled) for all services.</summary>
        private static IBackgroundServiceToggleService CreateToggleMock()
        {
            var mock = new Mock<IBackgroundServiceToggleService>();
            mock.Setup(t => t.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            return mock.Object;
        }
    }
}
