using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>SRS v1.2 W3/D (M9): BankAccountService — seed 111 mặc định + CRUD + paymentMethod→TK tiền.</summary>
public class BankAccountServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

    private static VanAn.CoreHub.Tests.TestInfrastructure.TestContextScope NewScope()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        return scope;
    }

    private static BankAccountService BuildService(VanAn.CoreHub.Infrastructure.IAccountingDbContext db)
        => new(db, NullLogger<BankAccountService>.Instance);

    [Fact]
    public async Task ListAsync_LazySeeds_Default111_Idempotent()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        var accounts = await svc.ListAsync(Tenant);

        Assert.Single(accounts);
        Assert.Equal("111", accounts[0].AccountCode);
        Assert.True(accounts[0].IsActive);

        // Idempotent — gọi lại không tạo thêm.
        var again = await svc.ListAsync(Tenant);
        Assert.Single(again);
    }

    [Fact]
    public async Task CreateAsync_BankAccount_PersistsAndValidatesCode()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        var acc = await svc.CreateAsync(Tenant, "1121", "VCB — TK chính", "Vietcombank", "00112233");

        Assert.Equal("1121", acc.AccountCode);
        Assert.Equal("Vietcombank", acc.BankName);

        // Code ngoài nhóm 11x → reject.
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(Tenant, "331", "Không hợp lệ"));
        // Trùng code cùng tenant → reject.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Tenant, "1121", "Trùng"));
    }

    [Fact]
    public async Task Deactivate_111_Throws_BankAccountDeactivates()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        var accounts = await svc.ListAsync(Tenant);
        var cash = accounts.Single(a => a.AccountCode == "111");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeactivateAsync(cash.Id));

        var bank = await svc.CreateAsync(Tenant, "1121", "VCB");
        await svc.DeactivateAsync(bank.Id);

        var activeOnly = await svc.ListAsync(Tenant);
        Assert.DoesNotContain(activeOnly, a => a.AccountCode == "1121");
        var all = await svc.ListAsync(Tenant, includeInactive: true);
        Assert.Contains(all, a => a.AccountCode == "1121" && !a.IsActive);
    }

    [Theory]
    [InlineData("cash", "111")]
    [InlineData("CASH", "111")]
    [InlineData("COD", "111")]
    [InlineData("other", "111")]
    [InlineData(null, "111")]
    public async Task Resolve_CashLike_Always111(string? method, string expected)
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        await svc.CreateAsync(Tenant, "1121", "VCB");

        Assert.Equal(expected, await svc.ResolveCashAccountCodeAsync(Tenant, method));
    }

    [Theory]
    [InlineData("bank")]
    [InlineData("transfer")]
    [InlineData("VIETQR")]
    [InlineData("qr")]
    public async Task Resolve_BankLike_PicksFirstActive112x_OrGeneric112(string method)
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        // Chưa có TK 112x → fallback "112" generic.
        Assert.Equal("112", await svc.ResolveCashAccountCodeAsync(Tenant, method));

        // Có 112x → chọn TK đầu tiên theo code.
        await svc.CreateAsync(Tenant, "1122", "ACB");
        await svc.CreateAsync(Tenant, "1121", "VCB");
        Assert.Equal("1121", await svc.ResolveCashAccountCodeAsync(Tenant, method));

        // Deactivate TK đầu → fallback TK kế tiếp / generic.
        var acc1121 = (await svc.ListAsync(Tenant)).Single(a => a.AccountCode == "1121");
        await svc.DeactivateAsync(acc1121.Id);
        Assert.Equal("1122", await svc.ResolveCashAccountCodeAsync(Tenant, method));
    }

    [Fact]
    public async Task TenantIsolation_AccountsScopedPerTenant()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        var other = new TenantId(Guid.NewGuid());

        // TK của tenant khác tồn tại trong cùng context → ListAsync(Tenant) KHÔNG được lọt.
        scope.Context.BankAccounts.Add(new VanAn.CoreHub.Infrastructure.Entities.BankAccountEntity(other, "1129", "NH tenant khác"));
        await scope.Context.SaveChangesAsync();
        await svc.CreateAsync(Tenant, "1121", "VCB");

        var accounts = await svc.ListAsync(Tenant);

        Assert.Equal(2, accounts.Count); // 111 seed + 1121
        Assert.DoesNotContain(accounts, a => a.AccountCode == "1129");
    }
}
