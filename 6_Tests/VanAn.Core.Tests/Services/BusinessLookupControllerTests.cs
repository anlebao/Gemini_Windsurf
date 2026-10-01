using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Gateway.Controllers;
using VanAn.Gateway.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// 2026-10-01: BusinessLookupController — GET /api/v1/business-info/{mst}.
/// Verifies MST validation + error mapping (400/404/429/502) with a mocked lookup service.
/// </summary>
public class BusinessLookupControllerTests
{
    private static BusinessLookupController Build(Mock<IMstLookupService> svc)
    {
        var controller = new BusinessLookupController(svc.Object, NullLogger<BusinessLookupController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static BusinessLookupResult SampleResult() => new(
        "0313143038", "CÔNG TY TNHH QUÁN CÀ PHÊ 666", "Số 1 Đường Test", "active",
        "Nguyễn Văn A", "Sản xuất sản phẩm điện tử", "TP. Hồ Chí Minh", "doanhnghiep.vn");

    [Fact(DisplayName = "BL-1: MST hợp lệ + tìm thấy → 200 với DTO")]
    public async Task ValidMst_Found_Returns200()
    {
        var svc = new Mock<IMstLookupService>();
        svc.Setup(s => s.LookupByTaxCodeAsync("0313143038", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleResult());
        var controller = Build(svc);

        var result = await controller.GetByMst("0313143038");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<BusinessLookupResult>(ok.Value);
        Assert.Equal("CÔNG TY TNHH QUÁN CÀ PHÊ 666", dto.BusinessName);
        Assert.Equal("active", dto.Status);
    }

    [Fact(DisplayName = "BL-2: MST không hợp lệ → 400")]
    public async Task InvalidMst_Returns400()
    {
        var svc = new Mock<IMstLookupService>();
        var controller = Build(svc);

        var result = await controller.GetByMst("abc123");

        Assert.IsType<BadRequestObjectResult>(result);
        svc.Verify(s => s.LookupByTaxCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "BL-3: Không tìm thấy → 404")]
    public async Task NotFound_Returns404()
    {
        var svc = new Mock<IMstLookupService>();
        svc.Setup(s => s.LookupByTaxCodeAsync("0313143038", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessLookupResult?)null);
        var controller = Build(svc);

        var result = await controller.GetByMst("0313143038");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "BL-4: Hết quota → 429")]
    public async Task RateLimited_Returns429()
    {
        var svc = new Mock<IMstLookupService>();
        svc.Setup(s => s.LookupByTaxCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessLookupRateLimitedException("Đã đạt giới hạn tra cứu hôm nay."));
        var controller = Build(svc);

        var result = await controller.GetByMst("0313143038");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, status.StatusCode);
    }

    [Fact(DisplayName = "BL-5: Upstream lỗi/key sai → 502")]
    public async Task Unavailable_Returns502()
    {
        var svc = new Mock<IMstLookupService>();
        svc.Setup(s => s.LookupByTaxCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessLookupUnavailableException("doanhnghiep.vn trả về lỗi 500."));
        var controller = Build(svc);

        var result = await controller.GetByMst("0313143038");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }
}
