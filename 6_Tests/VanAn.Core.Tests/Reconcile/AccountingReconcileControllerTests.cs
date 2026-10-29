using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using Xunit;

namespace VanAn.Core.Tests.Reconcile;

/// <summary>
/// P3 — AccountingReconcileController (Gateway admin API).
/// 400 (tenantId rỗng/period hỏng) · 404 (tenant không tồn tại) · 200 + summary ·
/// audit ghi (mock) · attribute SystemAdmin.
/// </summary>
public class AccountingReconcileControllerTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-0000000000F3"));

    private sealed class Ctx : IDisposable
    {
        public SqliteConnection Conn { get; } = new("Data Source=:memory:");
        public VanAnDbContext Db { get; }

        public Ctx()
        {
            Conn.Open();
            Db = new VanAnDbContext(new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(Conn).Options);
            _ = Db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Db.Dispose();
            Conn.Dispose();
        }
    }

    private static ReconcileResult SampleResult() => new(
        "2026-10", Tenant.Value, "Test Tenant", DateTime.UtcNow,
        new ReconcileSummary(Pass: 10, Info: 2, Warning: 1, Error: 0, Total: 13),
        [new ReconcileCheck("R1", "Journal cân bằng", ReconcileLevel.Pass, "ok", "")],
        []);

    private static AccountingReconcileController Build(
        VanAnDbContext db,
        Mock<IAccountingReconcileService> svc,
        Mock<IAuditTrailService> audit)
    {
        var ctrl = new AccountingReconcileController(svc.Object, db, audit.Object,
            NullLogger<AccountingReconcileController>.Instance);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        return ctrl;
    }

    private static async Task SeedTenant(Ctx c)
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(Tenant, "Test Tenant");
        c.Db.Tenants.Add(tenant);
        _ = await c.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task EmptyTenantId_Returns400()
    {
        using var c = new Ctx();
        var ctrl = Build(c.Db, new Mock<IAccountingReconcileService>(), new Mock<IAuditTrailService>());

        var result = await ctrl.Run(Guid.Empty, "2026-10", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("2026-13")]
    public async Task BadPeriod_Returns400(string? period)
    {
        using var c = new Ctx();
        var ctrl = Build(c.Db, new Mock<IAccountingReconcileService>(), new Mock<IAuditTrailService>());

        var result = await ctrl.Run(Tenant.Value, period, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TenantNotFound_Returns404()
    {
        using var c = new Ctx(); // không seed tenant
        var ctrl = Build(c.Db, new Mock<IAccountingReconcileService>(), new Mock<IAuditTrailService>());

        var result = await ctrl.Run(Tenant.Value, "2026-10", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Valid_Returns200_AndWritesAudit()
    {
        using var c = new Ctx();
        await SeedTenant(c);
        var svc = new Mock<IAccountingReconcileService>();
        svc.Setup(s => s.RunAsync(
                It.Is<TenantId>(t => t.Value == Tenant.Value),
                It.Is<AccountingPeriod>(p => p.Year == 2026 && p.Month == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleResult());
        var audit = new Mock<IAuditTrailService>();
        var ctrl = Build(c.Db, svc, audit);

        var result = await ctrl.Run(Tenant.Value, "2026-10", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ReconcileResult>().Subject;
        payload.Period.Should().Be("2026-10");
        payload.TenantId.Should().Be(Tenant.Value);
        payload.Summary.Error.Should().Be(0);
        audit.Verify(a => a.LogCreateAsync(
            AuditableEntityType.Tenant, Tenant.Value,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Class_HasSystemAdminAuthorize()
    {
        var attr = typeof(AccountingReconcileController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault(a => a.Policy == "SystemAdmin");
        attr.Should().NotBeNull("G9 — chỉ SystemAdmin được tra soát");
    }
}
