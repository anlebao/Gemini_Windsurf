using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class PersonalEvidenceServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly PersonalEvidenceService _service;

    public PersonalEvidenceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"pes-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new PersonalEvidenceService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Upsert_ValidField_CreatesUnverifiedDeclared()
    {
        var subjectId = Guid.NewGuid();
        var dto = await _service.UpsertAsync(subjectId, new UpsertPersonalEvidenceRequest(
            "FULL_NAME", "Nguyen Van A", "v1.0"));

        Assert.NotNull(dto);
        Assert.Equal("FULL_NAME", dto!.FieldKey);
        Assert.Equal("USER_DECLARED", dto.Source);
        Assert.Equal("UNVERIFIED", dto.VerificationStatus);
        Assert.True(dto.HasValue);
        Assert.NotEqual("Nguyen Van A", dto.MaskedValue); // masked — không trả plaintext
    }

    [Fact]
    public async Task Upsert_InvalidFieldKey_ReturnsNull()
    {
        var dto = await _service.UpsertAsync(Guid.NewGuid(), new UpsertPersonalEvidenceRequest(
            "PASSPORT_NUMBER", "X123", "v1.0"));
        Assert.Null(dto);
    }

    [Fact]
    public async Task Upsert_MissingNoticeVersion_ReturnsNull()
    {
        var dto = await _service.UpsertAsync(Guid.NewGuid(), new UpsertPersonalEvidenceRequest(
            "FULL_NAME", "A", " "));
        Assert.Null(dto);
    }

    [Fact]
    public async Task Upsert_SameField_UpdatesNotDuplicates()
    {
        var subjectId = Guid.NewGuid();
        await _service.UpsertAsync(subjectId, new UpsertPersonalEvidenceRequest("CONTACT_PHONE", "0901234567", "v1.0"));
        await _service.UpsertAsync(subjectId, new UpsertPersonalEvidenceRequest("CONTACT_PHONE", "0912345678", "v1.0"));

        var items = await _service.GetMineAsync(subjectId);
        Assert.Single(items);
        Assert.Equal("CONTACT_PHONE", items[0].FieldKey);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnItems()
    {
        var subjectId = Guid.NewGuid();
        await _service.UpsertAsync(subjectId, new UpsertPersonalEvidenceRequest("FULL_NAME", "A", "v1.0"));
        await _service.UpsertAsync(Guid.NewGuid(), new UpsertPersonalEvidenceRequest("FULL_NAME", "B", "v1.0"));

        var mine = await _service.GetMineAsync(subjectId);
        Assert.Single(mine);
    }

    [Fact]
    public async Task Consent_Grant_IdempotentSamePurpose()
    {
        var subjectId = Guid.NewGuid();
        var first = await _service.GrantConsentAsync(subjectId, new GrantConsentRequest("evidence_processing", "v1.0"));
        var second = await _service.GrantConsentAsync(subjectId, new GrantConsentRequest("evidence_processing", "v1.0"));
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Consent_Withdraw_ThenRelist_ShowsWithdrawn()
    {
        var subjectId = Guid.NewGuid();
        var consent = await _service.GrantConsentAsync(subjectId, new GrantConsentRequest("security_notifications", "v1.0"));
        var ok = await _service.WithdrawConsentAsync(subjectId, consent.Id);
        Assert.True(ok);

        var list = await _service.ListConsentsAsync(subjectId);
        Assert.Single(list);
        Assert.NotNull(list[0].WithdrawnAt);
    }

    [Fact]
    public async Task Consent_Withdraw_WrongSubject_ReturnsFalse()
    {
        var subjectId = Guid.NewGuid();
        var consent = await _service.GrantConsentAsync(subjectId, new GrantConsentRequest("security_notifications", "v1.0"));
        Assert.False(await _service.WithdrawConsentAsync(Guid.NewGuid(), consent.Id));
    }

    [Fact]
    public async Task Consent_InvalidPurpose_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GrantConsentAsync(Guid.NewGuid(), new GrantConsentRequest("marketing_spam", "v1.0")));
    }

    [Fact]
    public async Task Upsert_WritesSecurityEvent()
    {
        var subjectId = Guid.NewGuid();
        await _service.UpsertAsync(subjectId, new UpsertPersonalEvidenceRequest("FULL_NAME", "A", "v1.0"));
        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e => e.EventType == "evidence.updated" && e.TrustSubjectId == subjectId));
    }
}
