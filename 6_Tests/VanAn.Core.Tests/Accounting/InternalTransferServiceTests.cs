using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using InternalTransferStatus = VanAn.CoreHub.Infrastructure.Entities.InternalTransferStatus;

namespace VanAn.Core.Tests.Accounting;

/// <summary>SRS v1.2 W3/D: chuyển tiền nội bộ — status machine + JE qua account-map + invariant FR-TRF-005.</summary>
public class InternalTransferServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    private readonly Mock<IHKDBookRepository> _repo = new();
    private readonly List<JournalEntry> _journals = [];

    private InternalTransferService BuildService(VanAn.CoreHub.Infrastructure.IAccountingDbContext db, bool withVoucher = true)
    {
        _repo.Setup(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, AccountingBookType, CancellationToken>((j, _, _) => _journals.Add(j))
            .Returns(Task.CompletedTask);
        IVoucherNumberService? voucher = withVoucher
            ? new VanAn.CoreHub.Services.VoucherNumberService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<VanAn.CoreHub.Services.VoucherNumberService>.Instance)
            : null;
        return new InternalTransferService(db, _repo.Object, voucher, accountMapResolver: null);
    }

    private static VanAn.CoreHub.Tests.TestInfrastructure.TestContextScope NewScope()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        return scope;
    }

    [Fact]
    public async Task CreateImmediate_PostsJe_ToDebitFromCredit_AndReceived()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        var t = await svc.CreateAsync(Tenant, "111", "1121", 500_000m, description: "Nộp quỹ vào NH");

        Assert.Equal(InternalTransferStatus.Received, t.Status);
        Assert.Single(_journals);
        Assert.Equal("1121", _journals[0].Lines.ElementAt(0).AccountNumber); // Nợ đích
        Assert.Equal("111", _journals[0].Lines.ElementAt(1).AccountNumber);  // Có nguồn
        Assert.Equal(500_000m, _journals[0].Lines.ElementAt(0).DebitAmount);
        Assert.StartsWith("CT-", t.VoucherNumber);
    }

    [Fact]
    public async Task InTransit_Uses113_ThenReceivePostsInJe()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        var t = await svc.CreateAsync(Tenant, "1121", "111", 200_000m, inTransit: true);
        Assert.Equal(InternalTransferStatus.InTransit, t.Status);
        Assert.Equal("113", _journals[0].Lines.ElementAt(0).AccountNumber);
        Assert.Equal("1121", _journals[0].Lines.ElementAt(1).AccountNumber);

        await svc.ReceiveAsync(t.Id);
        Assert.Equal(InternalTransferStatus.Received, t.Status);
        Assert.Equal(2, _journals.Count);
        Assert.Equal("111", _journals[1].Lines.ElementAt(0).AccountNumber); // Nợ đích
        Assert.Equal("113", _journals[1].Lines.ElementAt(1).AccountNumber); // Có 113
    }

    [Fact]
    public async Task Fee_PostsSeparateJe_642()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        await svc.CreateAsync(Tenant, "1121", "111", 100_000m, feeAmount: 5_000m);

        Assert.Equal(2, _journals.Count);
        var fee = _journals[1];
        Assert.Equal("642", fee.Lines.ElementAt(0).AccountNumber); // Nợ phí
        Assert.Equal("1121", fee.Lines.ElementAt(1).AccountNumber); // Có nguồn
        Assert.Equal(5_000m, fee.Lines.ElementAt(0).DebitAmount);
    }

    [Fact]
    public async Task FrTrf005_TotalCashInvariant_ExceptFee()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);

        await svc.CreateAsync(Tenant, "111", "1121", 100_000m, feeAmount: 3_000m);

        decimal d111 = _journals.SelectMany(j => j.Lines).Where(l => l.AccountNumber == "111").Sum(l => l.CreditAmount - l.DebitAmount);
        decimal d1121 = _journals.SelectMany(j => j.Lines).Where(l => l.AccountNumber == "1121").Sum(l => l.DebitAmount - l.CreditAmount);
        Assert.Equal(103_000m, d111);  // outflow 111 = amount 100k + phí 3k
        Assert.Equal(100_000m, d1121); // tiền chỉ đổi chỗ — tổng tài sản tiền chỉ giảm đúng phí (642 Nợ 3k)
    }

    [Fact]
    public async Task Reverse_Received_CreatesCounterTransfer()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        var t = await svc.CreateAsync(Tenant, "111", "1121", 50_000m);

        var rev = await svc.ReverseAsync(t.Id);

        Assert.Equal(rev.Id, (await svc.GetByIdAsync(t.Id))!.ReversedById);
        Assert.Equal("1121", rev.FromAccountCode);
        Assert.Equal("111", rev.ToAccountCode);
        Assert.Equal(InternalTransferStatus.Received, rev.Status);
    }

    [Fact]
    public async Task CancelInTransit_PostsReversalJe()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        var t = await svc.CreateAsync(Tenant, "1121", "111", 80_000m, inTransit: true);

        await svc.CancelInTransitAsync(t.Id);

        Assert.Equal(InternalTransferStatus.Cancelled, t.Status);
        Assert.Equal(2, _journals.Count);
        Assert.Equal("1121", _journals[1].Lines.ElementAt(0).AccountNumber); // Nợ nguồn về lại
        Assert.Equal("113", _journals[1].Lines.ElementAt(1).AccountNumber);
    }
}
