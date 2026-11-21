using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Otp;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Customer OTP delivery (2026-11-21): email-first qua IIdentityOtpChannel,
/// phone qua ISmsService. Phải trả false khi kênh fail — KHÔNG fake "đã gửi".
/// </summary>
public class CustomerOtpDeliveryTests
{
    private static CustomerOtpDeliveryService Create(
        Mock<IIdentityOtpChannel>? otp = null,
        Mock<ISmsService>? sms = null)
        => new(otp?.Object ?? new Mock<IIdentityOtpChannel>().Object,
               sms?.Object ?? new Mock<ISmsService>().Object,
               NullLogger<CustomerOtpDeliveryService>.Instance);

    [Fact]
    public async Task DeliverAsync_Email_UsesIdentityOtpChannel()
    {
        var otp = new Mock<IIdentityOtpChannel>();
        otp.Setup(c => c.SendOtpAsync("a@b.com", "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sms = new Mock<ISmsService>();

        var ok = await Create(otp, sms).DeliverAsync("a@b.com", isEmail: true, "123456");

        Assert.True(ok);
        otp.Verify(c => c.SendOtpAsync("a@b.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
        sms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliverAsync_EmailChannelFails_ReturnsFalse()
    {
        var otp = new Mock<IIdentityOtpChannel>();
        otp.Setup(c => c.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ok = await Create(otp).DeliverAsync("a@b.com", isEmail: true, "123456");

        Assert.False(ok);
    }

    [Fact]
    public async Task DeliverAsync_Phone_UsesSmsService()
    {
        var sms = new Mock<ISmsService>();
        sms.Setup(s => s.SendSmsAsync("0901234567", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await Create(sms: sms).DeliverAsync("0901234567", isEmail: false, "654321");

        Assert.True(ok);
        sms.Verify(s => s.SendSmsAsync("0901234567", It.Is<string>(m => m.Contains("654321")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeliverAsync_SmsFails_ReturnsFalse()
    {
        var sms = new Mock<ISmsService>();
        sms.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ok = await Create(sms: sms).DeliverAsync("0901234567", isEmail: false, "654321");

        Assert.False(ok);
    }
}
