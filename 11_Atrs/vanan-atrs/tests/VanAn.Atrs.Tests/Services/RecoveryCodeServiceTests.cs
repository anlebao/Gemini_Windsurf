using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class RecoveryCodeServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly MemoryCache _cache;
    private readonly RecoveryCodeService _service;

    public RecoveryCodeServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"rc-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new RecoveryCodeService(
            _db,
            _cache,
            Options.Create(new RecoveryCodeOptions { AttemptLimit = 3, LockoutMinutes = 5 }),
            new StubNotificationWriter(),
            new AtrsMetrics());
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateSet_CodesUniqueNonEmptyAndStoredAsHash()
    {
        var subjectId = Guid.NewGuid();
        var result = await _service.GenerateSetAsync(subjectId);

        Assert.Equal(10, result.Codes.Count);
        Assert.Equal(10, result.Codes.Distinct().Count());
        Assert.All(result.Codes, c => Assert.Matches(@"^[A-Z2-9]{5}-[A-Z2-9]{5}$", c));

        var stored = await _db.RecoveryCodes.Where(c => c.SetId == result.SetId).ToListAsync();
        Assert.Equal(10, stored.Count);
        Assert.All(stored, c => Assert.DoesNotContain(result.Codes, plain => plain == c.CodeHash)); // không plaintext
        Assert.All(stored, c => Assert.False(c.CodeHash.Contains('-', StringComparison.Ordinal))); // hash ≠ format code
    }

    [Fact]
    public async Task Verify_CorrectCode_ConsumesOnce_SecondFails()
    {
        var subjectId = Guid.NewGuid();
        var gen = await _service.GenerateSetAsync(subjectId);
        var code = gen.Codes[0];

        var first = await _service.VerifyAndConsumeAsync(subjectId, code, deviceId: null);
        Assert.True(first.Success);

        var second = await _service.VerifyAndConsumeAsync(subjectId, code, deviceId: null);
        Assert.False(second.Success);
        Assert.Equal("InvalidCode", second.Reason); // generic, không lộ "đã dùng"
    }

    [Fact]
    public async Task Verify_NormalizedInput_AcceptsSpacesAndDashes()
    {
        var subjectId = Guid.NewGuid();
        var gen = await _service.GenerateSetAsync(subjectId);
        var code = gen.Codes[0];
        var messy = code.Replace("-", " ", StringComparison.Ordinal).ToUpperInvariant();

        var result = await _service.VerifyAndConsumeAsync(subjectId, messy, deviceId: null);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Verify_WrongCodes_GenericFailure_ThenLockout()
    {
        var subjectId = Guid.NewGuid();
        await _service.GenerateSetAsync(subjectId);

        var r1 = await _service.VerifyAndConsumeAsync(subjectId, "AAAAA-AAAAA", null);
        Assert.False(r1.Success);
        Assert.Equal("InvalidCode", r1.Reason);

        var r2 = await _service.VerifyAndConsumeAsync(subjectId, "BBBBB-BBBBB", null);
        Assert.False(r2.Success);

        var r3 = await _service.VerifyAndConsumeAsync(subjectId, "CCCCC-CCCCC", null);
        Assert.False(r3.Success);
        Assert.Equal("Locked", r3.Reason); // đạt AttemptLimit=3 → lock

        var r4 = await _service.VerifyAndConsumeAsync(subjectId, "DDDDD-DDDDD", null);
        Assert.False(r4.Success);
        Assert.Equal("Locked", r4.Reason);
    }

    [Fact]
    public async Task Regenerate_RevokesOldSet_NewCodesValid()
    {
        var subjectId = Guid.NewGuid();
        var gen1 = await _service.GenerateSetAsync(subjectId);
        var gen2 = await _service.GenerateSetAsync(subjectId);

        var oldSet = await _db.RecoveryCodeSets.SingleAsync(s => s.Id == gen1.SetId);
        Assert.Equal(RecoveryCodeSetStatus.REVOKED, oldSet.Status);

        var oldCodeResult = await _service.VerifyAndConsumeAsync(subjectId, gen1.Codes[0], null);
        Assert.False(oldCodeResult.Success);

        var newCodeResult = await _service.VerifyAndConsumeAsync(subjectId, gen2.Codes[0], null);
        Assert.True(newCodeResult.Success);
    }

    [Fact]
    public async Task GetStatus_ShowsRemainingCount_WithoutCodes()
    {
        var subjectId = Guid.NewGuid();
        var gen = await _service.GenerateSetAsync(subjectId);
        await _service.VerifyAndConsumeAsync(subjectId, gen.Codes[0], null);

        var status = await _service.GetStatusAsync(subjectId);
        Assert.True(status.HasActiveSet);
        Assert.Equal(9, status.RemainingCount);
        Assert.NotNull(status.GeneratedAt);
    }
}