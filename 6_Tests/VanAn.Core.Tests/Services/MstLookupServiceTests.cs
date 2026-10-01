using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// 2026-10-01: MST lookup service — local-first (PG tenants) → doanhnghiep.vn fallback.
/// Verifies local hit avoids HTTP, remote mapping, cache hit, daily quota, missing key.
/// </summary>
public class MstLookupServiceTests
{
    public MstLookupServiceTests()
    {
        // Daily quota counter is static (shared across requests by design) — isolate tests.
        MstLookupService.ResetDailyQuotaForTesting();
    }

    private const string ActiveJson = """
        {"mst":"0313143038","name_vi":"CÔNG TY TNHH QUÁN CÀ PHÊ 666","legal_form":"Công ty TNHH",
         "status":"active","address_full":"Số 1 Đường Test, Tp. HCM","legal_rep_name":"Nguyễn Văn A",
         "industry_main_code":"C26","province":{"code":"VN-HCM","name_vi":"TP. Hồ Chí Minh","slug":"tp-ho-chi-minh"},
         "industry":{"code":"C26","name_vi":"Sản xuất sản phẩm điện tử"}}
        """;

    private static (VanAnDbContext db, ServiceProvider sp) CreateDb()
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();
        var services = new ServiceCollection();
        var ef = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(o => o.UseInternalServiceProvider(ef).UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<VanAnDbContext>();
        db.Database.EnsureCreated();
        return (db, sp);
    }

    private static (MstLookupService service, Mock<HttpMessageHandler> handler) BuildService(
        VanAnDbContext db, int dailyLimit = 50, string apiKey = "test-key")
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                callCount++;
                if (req.RequestUri!.AbsolutePath.Contains("not-found-mst"))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ActiveJson) };
            });
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://doanhnghiep.vn") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("doanhnghiep")).Returns(client);

        var options = Options.Create(new BusinessLookupOptions { ApiKey = apiKey, DailyLimit = dailyLimit });
        var service = new MstLookupService(factory.Object, new MemoryCache(new MemoryCacheOptions()), options, db, NullLogger<MstLookupService>.Instance);
        return (service, handler);
    }

    private static VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant SeedTenant(
        VanAnDbContext db, string taxCode, string name = "Công Ty Test Địa Phương")
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(
            new TenantId(Guid.NewGuid()), name, TenantSettings.Empty().WithTaxCode(taxCode));
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    [Fact(DisplayName = "ML-1: MST có trong PG (tenant crawl/onboard) → trả local, KHÔNG gọi doanhnghiep.vn")]
    public async Task LocalTenant_ReturnsLocal_WithoutHttpCall()
    {
        var (db, sp) = CreateDb();
        try
        {
            var tenant = SeedTenant(db, "0313143038", "Công Ty Test Địa Phương");
            var (service, handler) = BuildService(db);

            var result = await service.LookupByTaxCodeAsync("0313143038");

            Assert.NotNull(result);
            Assert.Equal("Công Ty Test Địa Phương", result!.BusinessName);
            Assert.Equal("vanan", result.Source);
            Assert.Equal("active", result.Status);
            handler.VerifyNoOtherCalls();
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-2: MST local có dấu gạch — lookup không dấu vẫn khớp (normalize dash)")]
    public async Task LocalTenant_DashNormalization_Matches()
    {
        var (db, sp) = CreateDb();
        try
        {
            SeedTenant(db, "0302839105-001", "Quán Cà Phê Cửa Sổ 2");
            var (service, handler) = BuildService(db);

            var result = await service.LookupByTaxCodeAsync("0302839105001");

            Assert.NotNull(result);
            Assert.Equal("Quán Cà Phê Cửa Sổ 2", result!.BusinessName);
            Assert.Equal("vanan", result.Source);
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-3: Không có local → gọi doanhnghiep.vn, map đúng schema")]
    public async Task Remote_ActiveCompany_MapsFields()
    {
        var (db, sp) = CreateDb();
        try
        {
            var (service, handler) = BuildService(db);

            var result = await service.LookupByTaxCodeAsync("0313143038");

            Assert.NotNull(result);
            Assert.Equal("CÔNG TY TNHH QUÁN CÀ PHÊ 666", result!.BusinessName);
            Assert.Equal("Số 1 Đường Test, Tp. HCM", result.Address);
            Assert.Equal("active", result.Status);
            Assert.Equal("Nguyễn Văn A", result.LegalRepName);
            Assert.Equal("Sản xuất sản phẩm điện tử", result.IndustryName);
            Assert.Equal("TP. Hồ Chí Minh", result.ProvinceName);
            Assert.Equal("doanhnghiep.vn", result.Source);
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-4: doanhnghiep.vn 404 → trả null")]
    public async Task Remote_NotFound_ReturnsNull()
    {
        var (db, sp) = CreateDb();
        try
        {
            var (service, handler) = BuildService(db);
            var result = await service.LookupByTaxCodeAsync("not-found-mst");
            Assert.Null(result);
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-4b: doanhnghiep.vn trả sparse data (industry/province null) → không crash, trả result")]
    public async Task Remote_SparseNulls_NoCrash()
    {
        const string sparseJson = """
            {"mst":"9999999999","name_vi":"Công Ty Cổ Phần Nội Thất Sơn Hà","status":"active",
             "address_full":null,"legal_rep_name":null,"province":null,"industry":null}
            """;
        var (db, sp) = CreateDb();
        try
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sparseJson) });
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://doanhnghiep.vn") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("doanhnghiep")).Returns(client);
            var service = new MstLookupService(factory.Object, new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new BusinessLookupOptions { ApiKey = "test-key" }), db, NullLogger<MstLookupService>.Instance);

            var result = await service.LookupByTaxCodeAsync("9999999999");

            Assert.NotNull(result);
            Assert.Equal("Công Ty Cổ Phần Nội Thất Sơn Hà", result!.BusinessName);
            Assert.Null(result.IndustryName);
            Assert.Null(result.ProvinceName);
            Assert.Equal("active", result.Status);
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-5: Cache hit — lookup cùng MST 2 lần chỉ gọi API 1 lần")]
    public async Task Remote_CacheHit_SecondCallNoHttp()
    {
        var (db, sp) = CreateDb();
        try
        {
            var (service, handler) = BuildService(db);

            var first = await service.LookupByTaxCodeAsync("0313143038");
            var second = await service.LookupByTaxCodeAsync("0313143038");

            Assert.NotNull(first);
            Assert.Equal(first, second);
            handler.Protected().Verify(
                "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-6: Quá daily limit → BusinessLookupRateLimitedException")]
    public async Task Remote_DailyLimit_ThrowsRateLimited()
    {
        var (db, sp) = CreateDb();
        try
        {
            var (service, handler) = BuildService(db, dailyLimit: 1);

            var first = await service.LookupByTaxCodeAsync("0313143038");
            Assert.NotNull(first);

            await Assert.ThrowsAsync<BusinessLookupRateLimitedException>(
                () => service.LookupByTaxCodeAsync("0317777282"));
        }
        finally { sp.Dispose(); db.Dispose(); }
    }

    [Fact(DisplayName = "ML-7: Thiếu API key → BusinessLookupUnavailableException")]
    public async Task Remote_ApiKeyMissing_ThrowsUnavailable()
    {
        var (db, sp) = CreateDb();
        try
        {
            var (service, handler) = BuildService(db, apiKey: "");
            await Assert.ThrowsAsync<BusinessLookupUnavailableException>(
                () => service.LookupByTaxCodeAsync("0313143038"));
        }
        finally { sp.Dispose(); db.Dispose(); }
    }
}
