using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.ShopERP.Controllers;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Integration.Tests
{
    /// <summary>
    /// AF-P1-T2 (TDD): URL validation tests for POST /api/customer-profile/share (WS-1.3 SC9/SC10).
    ///
    /// Verifies the share URL pattern validation logic:
    ///  - Facebook: accepts /posts/, /permalink, ?story_id=, /share/ patterns
    ///  - TikTok: accepts /video/ pattern
    ///  - Rejects: homepage, profile, empty string, non-FB/TT domains
    ///
    /// 10 tests (5 Facebook + 5 TikTok) covering valid post/video URLs, homepage, profile, and empty string.
    /// Controller is tested directly with mocked dependencies — URL validation is pure controller logic
    /// (no DB or mission service interaction for 400 cases; mission service mocked for 200 cases).
    /// </summary>
    public class CustomerProfileShareUrlValidationTests
    {
        private static readonly Guid TestCustomerId = Guid.NewGuid();
        private static readonly TenantId TestTenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        /// <summary>Build the controller with mocked deps. For 200 cases, customer + mission success are set up.</summary>
        private static (CustomerProfileController controller, Mock<IMissionService> missionMock) BuildController(bool setUpSuccessFlow = false)
        {
            var tokenMock = new Mock<ICustomerTokenService>();
            tokenMock.Setup(t => t.ValidateToken(It.IsAny<string?>())).Returns(TestCustomerId);

            var customerRepo = new Mock<ICustomerRepository>();
            if (setUpSuccessFlow)
            {
                var customer = new Customer(TestTenantId, "Share Test Customer", "0900000001", "share@example.com");
                customerRepo.Setup(r => r.GetByIdAsync(TestCustomerId)).ReturnsAsync(customer);
            }

            var missionMock = new Mock<IMissionService>();
            if (setUpSuccessFlow)
            {
                var completion = new MissionCompletion(TestTenantId, TestCustomerId, Guid.NewGuid(), 50, null);
                missionMock.Setup(m => m.CompleteMissionAsync(It.IsAny<Guid>(), It.IsAny<MissionType>(), It.IsAny<string?>()))
                           .ReturnsAsync(MissionCompletionResult.Ok(completion, 50, 100));
            }

            var controller = new CustomerProfileController(
                tokenMock.Object,
                customerRepo.Object,
                missionMock.Object,
                NullLogger<CustomerProfileController>.Instance);

            return (controller, missionMock);
        }

        private static SubmitShareRequest ShareRequest(string url) => new() { ShareUrl = url };

        // === Facebook valid URLs → 200 ===

        [Fact(DisplayName = "AF-P1-T2-U1: Facebook /user/posts/123 → 200 OK (mission triggered)")]
        public async Task Facebook_PostsUrl_Returns200()
        {
            var (controller, missionMock) = BuildController(setUpSuccessFlow: true);

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://facebook.com/user/posts/123"));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            _ = ok.StatusCode.Should().Be(200);
            missionMock.Verify(m => m.CompleteMissionAsync(It.IsAny<Guid>(), MissionType.FacebookShare, It.IsAny<string?>()), Times.Once);
        }

        [Fact(DisplayName = "AF-P1-T2-U2: Facebook /permalink.php?story_id=123 → 200 OK")]
        public async Task Facebook_PermalinkUrl_Returns200()
        {
            var (controller, missionMock) = BuildController(setUpSuccessFlow: true);

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://facebook.com/permalink.php?story_id=123"));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            _ = ok.StatusCode.Should().Be(200);
            missionMock.Verify(m => m.CompleteMissionAsync(It.IsAny<Guid>(), MissionType.FacebookShare, It.IsAny<string?>()), Times.Once);
        }

        // === Facebook invalid URLs → 400 ===

        [Fact(DisplayName = "AF-P1-T2-U3: Facebook homepage → 400 BadRequest")]
        public async Task Facebook_HomepageUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://facebook.com"));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }

        [Fact(DisplayName = "AF-P1-T2-U4: Facebook /user (profile) → 400 BadRequest")]
        public async Task Facebook_ProfileUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://facebook.com/user"));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }

        [Fact(DisplayName = "AF-P1-T2-U5: Facebook empty string → 400 BadRequest")]
        public async Task Facebook_EmptyUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest(""));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }

        // === TikTok valid URLs → 200 ===

        [Fact(DisplayName = "AF-P1-T2-U6: TikTok /@user/video/123 → 200 OK (mission triggered)")]
        public async Task TikTok_AtUserVideoUrl_Returns200()
        {
            var (controller, missionMock) = BuildController(setUpSuccessFlow: true);

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://tiktok.com/@user/video/123"));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            _ = ok.StatusCode.Should().Be(200);
            missionMock.Verify(m => m.CompleteMissionAsync(It.IsAny<Guid>(), MissionType.TikTokShare, It.IsAny<string?>()), Times.Once);
        }

        [Fact(DisplayName = "AF-P1-T2-U7: TikTok /user/video/123 → 200 OK")]
        public async Task TikTok_UserVideoUrl_Returns200()
        {
            var (controller, missionMock) = BuildController(setUpSuccessFlow: true);

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://tiktok.com/user/video/123"));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            _ = ok.StatusCode.Should().Be(200);
            missionMock.Verify(m => m.CompleteMissionAsync(It.IsAny<Guid>(), MissionType.TikTokShare, It.IsAny<string?>()), Times.Once);
        }

        // === TikTok invalid URLs → 400 ===

        [Fact(DisplayName = "AF-P1-T2-U8: TikTok homepage → 400 BadRequest")]
        public async Task TikTok_HomepageUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://tiktok.com"));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }

        [Fact(DisplayName = "AF-P1-T2-U9: TikTok /@user (profile) → 400 BadRequest")]
        public async Task TikTok_ProfileUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest("https://tiktok.com/@user"));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }

        [Fact(DisplayName = "AF-P1-T2-U10: TikTok empty string → 400 BadRequest")]
        public async Task TikTok_EmptyUrl_Returns400()
        {
            var (controller, _) = BuildController();

            var result = await controller.SubmitShare("valid-token", ShareRequest(""));

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _ = bad.StatusCode.Should().Be(400);
        }
    }
}
