using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;
using ValidationException = VanAn.CoreHub.Services.ValidationException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// IQRAttributionService — resolve QR (SRS §7.3: token active ∧ tenant active ∧ salesman ∈ tenant),
    /// AttributionSession first-qualified-wins (§7.5), rapid-scan guard (§26.3), revoke (§26.1).
    /// </summary>
    public class QRAttributionServiceTests
    {
        private static QRAttributionService BuildService(VanAnDbContext ctx)
            => new(ctx, NullLogger<QRAttributionService>.Instance);

        private static async Task<(VanAnDbContext Ctx, QRAttributionService Svc, Guid QrId, Guid SalesmanId)> SeedAsync(
            VanAnDbContext ctx, TenantId tenantId, string token = "tok-abc-123")
        {
            await BookingTestData.SeedActiveTenantAsync(ctx, tenantId);
            Guid salesmanId = await BookingTestData.SeedSalesmanAsync(ctx, tenantId);
            var svc = BuildService(ctx);
            var qr = await svc.CreateQrChannelAsync(tenantId, token, salesmanId);
            return (ctx, svc, qr.Id, salesmanId);
        }

        [Fact]
        public async Task Resolve_ValidQr_CreatesQualifiedSession()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, svc, qrId, salesmanId) = await SeedAsync(scope.Context, BookingTestData.TenantId);

            var result = await svc.ResolveQrAsync("tok-abc-123", "anon-1");

            Assert.Equal(qrId, result.QrId);
            Assert.Equal(BookingTestData.TenantId, result.TenantId);
            Assert.Equal(salesmanId, result.SalesmanId);
            Assert.True(result.IsQualified);
            Assert.True(result.IsNewSession);

            var session = await ctx.AttributionSessions.IgnoreQueryFilters()
                .FirstAsync(s => s.Id == result.AttributionSessionId);
            Assert.Equal("anon-1", session.AnonymousSessionId);
            Assert.True(session.IsQualified);
            Assert.Equal(salesmanId, session.SalesmanId);
        }

        [Fact]
        public async Task Resolve_SameTokenAndSession_RefreshNoNewSession()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, svc, _, _) = await SeedAsync(scope.Context, BookingTestData.TenantId);

            var first = await svc.ResolveQrAsync("tok-abc-123", "anon-1");
            var second = await svc.ResolveQrAsync("tok-abc-123", "anon-1");

            Assert.False(second.IsNewSession);
            Assert.Equal(first.AttributionSessionId, second.AttributionSessionId);
            Assert.Equal(1, await ctx.AttributionSessions.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Resolve_UnknownToken_ThrowsNotFound()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (_, svc, _, _) = await SeedAsync(scope.Context, BookingTestData.TenantId);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.ResolveQrAsync("tok-khac", "anon-1"));
        }

        [Fact]
        public async Task Resolve_RevokedQr_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, svc, qrId, _) = await SeedAsync(scope.Context, BookingTestData.TenantId);
            await svc.RevokeQrAsync(BookingTestData.TenantId, qrId);

            var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.ResolveQrAsync("tok-abc-123", "anon-1"));
            Assert.Contains("vô hiệu hóa", ex.Message);
        }

        [Fact]
        public async Task Resolve_TenantNotActive_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            // KHÔNG seed tenant → tenant không tồn tại (không active).
            Guid salesmanId = await BookingTestData.SeedSalesmanAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);
            await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-abc-123", salesmanId);

            await Assert.ThrowsAsync<ValidationException>(() => svc.ResolveQrAsync("tok-abc-123", "anon-1"));
        }

        [Fact]
        public async Task Resolve_SalesmanRoleDeactivated_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.TenantId);
            Guid salesmanId = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId);
            var svc = BuildService(ctx);
            await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-abc-123", salesmanId);

            // Deactivate role SAU khi tạo QR → resolve phải từ chối (§7.3 defense-in-depth).
            var role = await ctx.CommunityRoles.IgnoreQueryFilters().FirstAsync(r => r.CustomerId == salesmanId);
            role.Deactivate();
            await ctx.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.ResolveQrAsync("tok-abc-123", "anon-1"));
            Assert.Contains("kênh bán hàng", ex.Message);
        }

        [Fact]
        public async Task Resolve_FirstQualifiedWins_AnotherQrDoesNotSteal()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, svc, _, salesmanA) = await SeedAsync(scope.Context, BookingTestData.TenantId, "tok-A");
            Guid salesmanB = await BookingTestData.SeedSalesmanAsync(scope.Context, BookingTestData.TenantId, "Salesman B");
            var qrB = await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-B", salesmanB);

            // Scan QR A trước → qualified.
            var first = await svc.ResolveQrAsync("tok-A", "anon-1");

            // Scan QR B cùng anonymous session → attribution KHÔNG đổi (first-qualified-wins §7.5).
            var second = await svc.ResolveQrAsync("tok-B", "anon-1");

            Assert.Equal(first.AttributionSessionId, second.AttributionSessionId);
            Assert.Equal(salesmanA, second.SalesmanId);
            Assert.True(second.IsQualified);
            Assert.Equal(qrB.Id, second.QrId);   // QR hiện tại vẫn trả về cho UI
        }

        [Fact]
        public async Task Resolve_CrossTenant_NoContamination()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            // Tenant A: QR A + session qualified.
            var (ctx, _, _, salesmanA) = await SeedAsync(scope.Context, BookingTestData.TenantId, "tok-A");
            var svc = BuildService(ctx);
            _ = await svc.ResolveQrAsync("tok-A", "anon-shared");

            // Tenant B: QR B + cùng anonymous session — phải tạo session RIÊNG cho tenant B.
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.OtherTenantId);
            await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.OtherTenantId, "Salesman B-tenant");
            var qrB = await svc.CreateQrChannelAsync(BookingTestData.OtherTenantId, "tok-B", salesmanId: null);

            var resultB = await svc.ResolveQrAsync("tok-B", "anon-shared");

            Assert.Equal(BookingTestData.OtherTenantId, resultB.TenantId);
            Assert.True(resultB.IsNewSession);   // session riêng — không dùng session tenant A
            Assert.Null(resultB.SalesmanId);

            // Vẫn còn đúng 1 session tenant A.
            Assert.Equal(1, await ctx.AttributionSessions.IgnoreQueryFilters()
                .CountAsync(s => s.TenantId == BookingTestData.TenantId));
        }

        [Fact]
        public async Task TokenStoredHashed_NotPlaintext()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, _, _, _) = await SeedAsync(scope.Context, BookingTestData.TenantId);

            var qr = await ctx.QRChannels.IgnoreQueryFilters().FirstAsync();
            Assert.NotEqual("tok-abc-123", qr.QrTokenHash);
            Assert.Equal(QRAttributionService.HashToken("tok-abc-123"), qr.QrTokenHash);
        }

        [Fact]
        public async Task CreateQr_DuplicateToken_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);
            await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-dup");

            await Assert.ThrowsAsync<ValidationException>(() => svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-dup"));
        }

        [Fact]
        public async Task Resolve_ExpiredSession_StartsNewPeriod()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, svc, _, _) = await SeedAsync(scope.Context, BookingTestData.TenantId);

            var first = await svc.ResolveQrAsync("tok-abc-123", "anon-1");
            var session = await ctx.AttributionSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == first.AttributionSessionId);
            // Ép hết hạn.
            typeof(AttributionSession).GetProperty("AttributionExpiryAt")!
                .SetValue(session, DateTime.UtcNow.AddMinutes(-1));
            await ctx.SaveChangesAsync();

            var second = await svc.ResolveQrAsync("tok-abc-123", "anon-1");

            Assert.True(second.IsNewSession);
            Assert.Equal(first.AttributionSessionId, second.AttributionSessionId);   // 1 row / key — reset period
            Assert.True(second.IsQualified);
        }

        // ── Q1 (2026-10-08 — issue #188 bug 2): EncryptedToken + xem lại QR/link ──

        [Fact]
        public async Task CreateQr_StoresEncryptedToken_NotPlaintext()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);

            await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-secret-1");

            var qr = await scope.Context.QRChannels.IgnoreQueryFilters().FirstAsync();
            Assert.False(string.IsNullOrWhiteSpace(qr.EncryptedToken));
            Assert.NotEqual("tok-secret-1", qr.EncryptedToken);          // mã hóa, không plaintext
            Assert.DoesNotContain("tok-secret-1", qr.EncryptedToken);
        }

        [Fact]
        public async Task Detail_ActiveQr_ReturnsLinkAndQrPng()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);
            var qr = await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-secret-2");

            var detail = await svc.GetQrChannelDetailAsync(BookingTestData.TenantId, qr.Id, "https://khachvip.online");

            Assert.True(detail.IsActive);
            Assert.Equal("https://khachvip.online/booking/tok-secret-2", detail.BookingLink);
            Assert.NotNull(detail.QrCodePngBase64);
            Assert.StartsWith("data:image/png;base64,", detail.QrCodePngBase64);
        }

        [Fact]
        public async Task Detail_RevokedQr_ReturnsNullLink()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);
            var qr = await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-secret-3");
            await svc.RevokeQrAsync(BookingTestData.TenantId, qr.Id);

            var detail = await svc.GetQrChannelDetailAsync(BookingTestData.TenantId, qr.Id, "https://khachvip.online");

            Assert.False(detail.IsActive);
            Assert.Null(detail.BookingLink);
            Assert.Null(detail.QrCodePngBase64);
            Assert.NotNull(detail.RevokedAt);
        }

        [Fact]
        public async Task Detail_CrossTenant_ThrowsNotFound()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);
            var qr = await svc.CreateQrChannelAsync(BookingTestData.TenantId, "tok-secret-4");

            // Tenant khác đọc QR tenant A → NotFound (không leak cross-tenant).
            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.GetQrChannelDetailAsync(BookingTestData.OtherTenantId, qr.Id, "https://khachvip.online"));
        }

        [Fact]
        public async Task Detail_UnknownQr_ThrowsNotFound()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            await BookingTestData.SeedActiveTenantAsync(scope.Context, BookingTestData.TenantId);
            var svc = BuildService(scope.Context);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.GetQrChannelDetailAsync(BookingTestData.TenantId, Guid.NewGuid(), "https://khachvip.online"));
        }
    }
}
