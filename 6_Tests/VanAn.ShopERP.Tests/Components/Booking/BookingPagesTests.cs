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
        CreatedAt: DateTime.UtcNow, CompletedAt: null, OrderId: null);

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
        api.Setup(a => a.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(new BookingTenantConfigDto(true, "None", null, null, null, null)));
        api.Setup(a => a.GetQrChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ApiResponse<List<QrChannelDto>>.Success(channels.ToList()));
        api.Setup(a => a.GetSalesmenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<SalesmanDto>>.Success([]));
        api.Setup(a => a.GetQrChannelAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<QrChannelDetailDto>.Success(
                new QrChannelDetailDto(createdId, "https://khachvip.online/booking/raw-token", "data:image/png;base64,QUJD", true, null)));
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

        // Tạo QR → CreateQrChannelAsync + link + QR image hiển thị (Q1 — render lại từ EncryptedToken)
        cut.FindAll("button").First(b => b.TextContent.Contains("Tạo mã QR")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();
        api.Verify(a => a.CreateQrChannelAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetQrChannelAsync(createdId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("https://khachvip.online/booking/raw-token"));
        cut.WaitForAssertion(() => cut.Find($"[data-testid='qr-image-{createdId}']").Should().NotBeNull());
    }

    [Fact]
    public void QrChannels_ShowsConfigWarning_WhenBookingDisabled()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(new BookingTenantConfigDto(false, "None", null, null, null, null)));
        api.Setup(a => a.GetQrChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<QrChannelDto>>.Success([]));
        api.Setup(a => a.GetSalesmenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<SalesmanDto>>.Success([]));
        Services.AddSingleton(api.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingQrChannels>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='qr-config-warning']").Should().NotBeNull());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("/booking/config"));
    }
}

public class BookingConfigPageTests : ComponentTestBase
{
    private static Mock<IBookingTenantApiClient> MockApi(BookingTenantConfigDto config)
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(config));
        return api;
    }
    [Fact]
    public void Config_Renders_Form_And_Save_CallsClient()
    {
        var api = MockApi(new BookingTenantConfigDto(false, "None", null, null, null, null));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingConfig>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Cấu hình đặt lịch"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='config-toggle']").Should().NotBeNull());
        cut.WaitForAssertion(() => cut.Find("select").Attributes["value"]?.Value.Should().Be("None"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Lưu cấu hình")).Click();
        api.Verify(a => a.UpdateConfigAsync(false, "None", null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Config_Enable_And_Save_ShowsSuccess()
    {
        var api = MockApi(new BookingTenantConfigDto(false, "None", null, null, null, null));
        api.Setup(a => a.UpdateConfigAsync(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(new BookingTenantConfigDto(true, "None", null, null, "Hủy trước 4h.", null)));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingConfig>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='config-toggle']").Should().NotBeNull());
        cut.Find("[data-testid='config-toggle']").Change(true);
        cut.FindAll("button").First(b => b.TextContent.Contains("Lưu cấu hình")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("đang BẬT"));
        api.Verify(a => a.UpdateConfigAsync(true, "None", null, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Config_FixedDeposit_ShowsAmountField()
    {
        var api = MockApi(new BookingTenantConfigDto(true, "Fixed", 100000, null, null, null));
        Services.AddSingleton(api.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingConfig>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='config-toggle']").Should().NotBeNull());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("100000"));
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

public class BookingCreatePageTests : ComponentTestBase
{
    private static Mock<IBookingTenantApiClient> MockCatalogApi(DateTime slotStart)
    {
        var offeringId = Guid.NewGuid();
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(new BookingTenantConfigDto(true, "Fixed", 50000, null, null, null)));
        api.Setup(a => a.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingCategoryDto>>.Success([new BookingCategoryDto(Guid.NewGuid(), "Massage", 1)]));
        api.Setup(a => a.GetOfferingsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingOfferingDto>>.Success([
                new BookingOfferingDto(offeringId, null, "Service", "Massage 60 phút", 60, 300000, null)
            ]));
        api.Setup(a => a.GetAddOnsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingAddOnDto>>.Success([new BookingAddOnDto(Guid.NewGuid(), "Nước uống", 20000)]));
        api.Setup(a => a.GetAvailabilityMatrixAsync(It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<StaffAvailabilityDto>>.Success([
                new StaffAvailabilityDto(Guid.NewGuid(), "Nguyễn Văn A", slotStart, slotStart.AddMinutes(60), true, null)
            ]));
        return api;
    }

    [Fact]
    public void Create_Renders_SelectsOfferingSlot_And_Submit_CallsClient()
    {
        var slotStart = DateTime.UtcNow.AddHours(2);
        var api = MockCatalogApi(slotStart);
        api.Setup(a => a.CreateBookingAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingQueueItemDto>.Success(new BookingQueueItemDto(
                Guid.NewGuid(), "BK-FAST-001", "Confirmed", "None", "Massage 60 phút", slotStart, slotStart.AddMinutes(60),
                300000, null, null, null, null, "KH: Nguyễn Văn Khách", true, 50000, "SecurityDeposit",
                "Pending", "NotRequired", 1, DateTime.UtcNow, null, null)));
        Services.AddSingleton(api.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingCreate>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Đặt lịch nhanh"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Massage 60 phút"));

        // Chọn dịch vụ → slot xuất hiện → chọn slot → điền khách → submit
        cut.FindAll("button").First(b => b.TextContent.Contains("Chọn")).Click();
        cut.WaitForAssertion(() => cut.Find($"[data-testid='slot-{slotStart.ToString("HHmm")}']").Should().NotBeNull());
        cut.Find($"[data-testid='slot-{slotStart.ToString("HHmm")}']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='booking-staff']").Should().NotBeNull());
        cut.Find("[data-testid='booking-customer-name']").Change("Nguyễn Văn Khách");
        cut.Find("[data-testid='booking-customer-phone']").Change("0909123456");
        cut.FindAll("button").First(b => b.TextContent.Contains("Tạo lịch hẹn")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("BK-FAST-001"));
        api.Verify(a => a.CreateBookingAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), slotStart, null,
            "Nguyễn Văn Khách", "0909123456", It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Create_ShowsDisabledWarning_WhenConfigOff()
    {
        var api = new Mock<IBookingTenantApiClient>();
        api.Setup(a => a.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<BookingTenantConfigDto>.Success(new BookingTenantConfigDto(false, "None", null, null, null, null)));
        api.Setup(a => a.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingCategoryDto>>.Success([]));
        api.Setup(a => a.GetOfferingsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingOfferingDto>>.Success([]));
        api.Setup(a => a.GetAddOnsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<BookingAddOnDto>>.Success([]));
        Services.AddSingleton(api.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var cut = RenderComponent<ShopERP.Components.Pages.Booking.BookingCreate>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Tính năng đặt lịch đang TẮT"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("/booking/config"));
    }
}
