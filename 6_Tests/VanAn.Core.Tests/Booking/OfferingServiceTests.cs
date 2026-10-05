using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
// Services layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.ValidationException.
using ValidationException = VanAn.CoreHub.Services.ValidationException;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// IOfferingService — CRUD category / offering / package / add-on (SRS §8.2-8.3).
    /// Snapshot fields: đổi catalog không ảnh hưởng booking cũ.
    /// </summary>
    public class OfferingServiceTests
    {
        private static OfferingService BuildService(VanAnDbContext ctx) => new(ctx, NullLogger<OfferingService>.Instance);

        [Fact]
        public async Task Category_Create_Update_List()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            var cat = await svc.CreateCategoryAsync(BookingTestData.TenantId, "Massage", 1);
            Assert.Equal(cat.Id, cat.ServiceCategoryId.Value); // Single-Identity

            var updated = await svc.UpdateCategoryAsync(BookingTestData.TenantId, cat.Id, "Massage & Spa", 2, isActive: true);
            Assert.Equal("Massage & Spa", updated.Name);

            var list = await svc.ListCategoriesAsync(BookingTestData.TenantId, activeOnly: true);
            Assert.Single(list);
            Assert.Equal(cat.Id, list[0].Id);
        }

        [Fact]
        public async Task Offering_Create_Get_List_FilterCategoryAndActive()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            var cat = await svc.CreateCategoryAsync(BookingTestData.TenantId, "Massage");
            var offering = await svc.CreateOfferingAsync(BookingTestData.TenantId,
                new CreateOfferingCommand("Massage 60m", OfferingType.Service, 60, 300_000m, cat.Id, "TECH"));

            Assert.Equal(offering.Id, offering.AppointmentOfferingId.Value); // Single-Identity
            var loaded = await svc.GetOfferingAsync(BookingTestData.TenantId, offering.Id);
            Assert.NotNull(loaded);
            Assert.Equal(60, loaded!.DurationMinutes);

            // Filter theo category + activeOnly.
            var byCat = await svc.ListOfferingsAsync(BookingTestData.TenantId, categoryId: cat.Id, activeOnly: true);
            Assert.Single(byCat);

            var otherCat = await svc.CreateCategoryAsync(BookingTestData.TenantId, "Skin Care");
            var byOtherCat = await svc.ListOfferingsAsync(BookingTestData.TenantId, categoryId: otherCat.Id);
            Assert.Empty(byOtherCat);

            // Deactivate → activeOnly loại.
            await svc.UpdateOfferingAsync(BookingTestData.TenantId, offering.Id,
                new CreateOfferingCommand("Massage 60m", OfferingType.Service, 60, 300_000m, cat.Id), isActive: false);
            var active = await svc.ListOfferingsAsync(BookingTestData.TenantId, activeOnly: true);
            Assert.Empty(active);
        }

        [Fact]
        public async Task Offering_InvalidCategory_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CreateOfferingAsync(BookingTestData.TenantId,
                    new CreateOfferingCommand("X", OfferingType.Service, 60, 100_000m, Guid.NewGuid())));
        }

        [Fact]
        public async Task PackageItems_OnlyForPackageType_And_Replace()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            var service = await svc.CreateOfferingAsync(BookingTestData.TenantId,
                new CreateOfferingCommand("Massage 60m", OfferingType.Service, 60, 300_000m));
            var package = await svc.CreateOfferingAsync(BookingTestData.TenantId,
                new CreateOfferingCommand("Combo Thư Giãn", OfferingType.Package, 120, 550_000m));

            // SERVICE type → chặn.
            await Assert.ThrowsAsync<ValidationException>(() =>
                svc.SetPackageItemsAsync(BookingTestData.TenantId, service.Id,
                    [new PackageItemCommand(null, "Massage 60m", 1, 300_000m)]));

            // PACKAGE type → OK.
            var items = await svc.SetPackageItemsAsync(BookingTestData.TenantId, package.Id,
            [
                new PackageItemCommand(null, "Massage 60m", 1, 300_000m),
                new PackageItemCommand(null, "Đá nóng", 1, 250_000m)
            ]);
            Assert.Equal(2, items.Count);

            // Replace toàn bộ.
            var replaced = await svc.SetPackageItemsAsync(BookingTestData.TenantId, package.Id,
                [new PackageItemCommand(null, "Massage 90m", 1, 450_000m)]);
            Assert.Single(replaced);
            Assert.Equal("Massage 90m", replaced[0].ChildName);
        }

        [Fact]
        public async Task AddOn_Create_Update_List_ActiveFilter()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            var addOn = await svc.CreateAddOnAsync(BookingTestData.TenantId, "Nước ép", 30_000m);
            Assert.Equal(addOn.Id, addOn.AddOnId.Value); // Single-Identity

            await svc.UpdateAddOnAsync(BookingTestData.TenantId, addOn.Id, "Nước ép cam", 35_000m, isActive: true);
            var all = await svc.ListAddOnsAsync(BookingTestData.TenantId, activeOnly: true);
            Assert.Single(all);
            Assert.Equal("Nước ép cam", all[0].Name);

            await svc.UpdateAddOnAsync(BookingTestData.TenantId, addOn.Id, "Nước ép cam", 35_000m, isActive: false);
            var active = await svc.ListAddOnsAsync(BookingTestData.TenantId, activeOnly: true);
            Assert.Empty(active);
        }

        [Fact]
        public async Task TenantIsolation_CatalogOfTenantA_InvisibleFromTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            var cat = await svc.CreateCategoryAsync(BookingTestData.TenantId, "Massage");
            var offering = await svc.CreateOfferingAsync(BookingTestData.TenantId,
                new CreateOfferingCommand("Massage 60m", OfferingType.Service, 60, 300_000m, cat.Id));

            Assert.Null(await svc.GetOfferingAsync(BookingTestData.OtherTenantId, offering.Id));
            Assert.Empty(await svc.ListOfferingsAsync(BookingTestData.OtherTenantId));
            Assert.Empty(await svc.ListCategoriesAsync(BookingTestData.OtherTenantId));
            Assert.Empty(await svc.ListAddOnsAsync(BookingTestData.OtherTenantId));

            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.UpdateOfferingAsync(BookingTestData.OtherTenantId, offering.Id,
                    new CreateOfferingCommand("X", OfferingType.Service, 60, 1m)));
        }
    }
}
