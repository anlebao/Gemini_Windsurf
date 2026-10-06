using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Components.Booking;

/// <summary>
/// Booking P5 (2026-10-06, Gate 5): bUnit smoke tests — trang Booking render không crash
/// + action gọi IBookingTenantApiClient (HTTP proxy Gateway — D1).
/// </summary>
public class BookingQueuePageTests : ComponentTestBase
{
    private static BookingQueueItemDto SampleBooking(string status) => new(
        BookingId: Guid.NewGuid(), PublicBookingCode: "BK-TEST-001", Status: status, SubState: "None",
        OfferingNameSnapshot: "Massage thư giãn 60 phút", StartAt: DateTime.UtcNow.AddHours(3),
        EndAt: DateTime.UtcNow.AddHours(4), EstimatedTotal: 300000, ActualTotal: null,
        StaffId: null, CustomerId: null, CustomerDeviceId: "device-abc", CustomerNote: "Phòng riêng",
        DepositRequired: true, DepositAmount: 50000, DepositType: "SecurityDeposit",
        PaymentStatus: "Pending", InvoiceStatus: "NotRequired", Version: 1,
        CreatedAt: DateTime.UtcNow, CompletedAt: null);

    [Fact]
    public void Queue_Renders_BookingList_And_Confirm_CallsClient()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetQueueAsync(It.IsAny<VanAn.Shared.Domain.BookingStatus?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingQueueItemDto>>.Success([SampleBooking("PendingConfirmation")]));
        api.Setup(a => a.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingQueueItemDto>.Success(SampleBooking("Confirmed")));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingQueue>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Hàng đợi đặt lịch"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("BK-TEST-001"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Massage thư giãn 60 phút"));
        // Nút Xác nhận hiển thị cho PendingConfirmation
        cut.WaitForAssertion(() => cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Xác nhận")));

        // Click Xác nhận → ConfirmAsync được gọi
        cut.FindAll("button").First(b => b.TextContent.Contains("Xác nhận")).Click();
        api.Verify(a => a.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Queue_Renders_EmptyState_WhenNoBookings()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetQueueAsync(It.IsAny<VanAn.Shared.Domain.BookingStatus?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingQueueItemDto>>.Success([]));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingQueue>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='queue-empty']").Should().NotBeNull());
    }

    [Fact]
    public void Queue_ShowsError_WhenApiFails()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetQueueAsync(It.IsAny<VanAn.Shared.Domain.BookingStatus?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingQueueItemDto>>.Failure("Khung giờ này vừa có người đặt."));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingQueue>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Khung giờ này vừa có người đặt"));
    }
}

public class BookingStaffPageTests : ComponentTestBase
{
    [Fact]
    public void Staff_Renders_Table_And_Create_CallsClient()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetStaffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<StaffDto>>.Success([new StaffDto(Guid.NewGuid(), "Nguyễn Văn A", "Kỹ thuật viên", null, true)]));
        api.Setup(a => a.GetOfferingsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingOfferingDto>>.Success([]));
        api.Setup(a => a.GetStaffServicesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<StaffServiceDto>>.Success([]));
        api.Setup(a => a.CreateStaffAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<StaffDto>.Success(new StaffDto(Guid.NewGuid(), "Nguyễn Văn B", null, null, true)));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingStaff>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nhân viên phục vụ"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='staff-table']").TextContent.Should().Contain("Nguyễn Văn A"));

        // Mở modal Thêm + nhập tên + save → CreateStaffAsync
        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm nhân viên")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.Find(".modal input[type=text]").Change("Nguyễn Văn B");
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();
        api.Verify(a => a.CreateStaffAsync("Nguyễn Văn B", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class BookingQrChannelsPageTests : ComponentTestBase
{
    [Fact]
    public void QrChannels_Renders_And_Create_ShowsLinkOnce()
    {
        var channels = new List<QrChannelDto>();
        var createdId = Guid.NewGuid();
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetQrChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ApiResponse<List<QrChannelDto>>.Success(channels.ToList()));
        api.Setup(a => a.GetSalesmenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<SalesmanDto>>.Success([]));
        api.Setup(a => a.CreateQrChannelAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                channels.Add(new QrChannelDto(createdId, "hash", null, null, true, null, DateTime.UtcNow));
                return Task.FromResult(ApiResponse<QrChannelCreatedDto>.Success(
                    new QrChannelCreatedDto(createdId, "hash", "raw-token", null, null, true, null, DateTime.UtcNow)));
            });
        Services.AddSingleton(api.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingQrChannels>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Mã QR đặt lịch"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='qr-empty']").Should().NotBeNull());

        // Tạo QR → CreateQrChannelAsync + link hiển thị (raw token 1 lần)
        cut.FindAll("button").First(b => b.TextContent.Contains("Tạo mã QR")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();
        api.Verify(a => a.CreateQrChannelAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("/booking/raw-token"));
    }
}

public class BookingDepositsPageTests : ComponentTestBase
{
    [Fact]
    public void Deposits_Renders_And_MarkReceived_CallsClient()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetDepositsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<DepositItemDto>>.Success([
                new DepositItemDto(Guid.NewGuid(), "BK-001", "Massage 60'", DateTime.UtcNow.AddDays(1),
                    50000, "SecurityDeposit", "Pending", null, "Pending", "device-x", "PendingConfirmation")
            ]));
        api.Setup(a => a.MarkDepositReceivedAsync(It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<FinancialStatusDto>.Success(new FinancialStatusDto(
                Guid.NewGuid(), true, 50000, "SecurityDeposit", "Paid", Guid.NewGuid(), "Paid", "Pending", "OnPayment", 300000, null)));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingDeposits>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Đặt cọc"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='deposits-table']").TextContent.Should().Contain("BK-001"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Đã nhận cọc")).Click();
        api.Verify(a => a.MarkDepositReceivedAsync(It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
