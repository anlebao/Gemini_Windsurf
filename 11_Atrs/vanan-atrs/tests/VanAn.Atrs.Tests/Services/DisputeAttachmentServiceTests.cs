using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class DisputeAttachmentServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly FakeObjectStorage _storage = new();
    private readonly DisputeAttachmentService _service;

    public DisputeAttachmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"das-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new DisputeAttachmentService(_db, _storage);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(Guid caseId, Guid subjectId)> SeedCaseAsync(bool terminal = false)
    {
        var subjectId = Guid.NewGuid();
        var c = new DisputeCase(Guid.NewGuid(), subjectId, Guid.NewGuid(), DisputeCaseType.POINTS_MISSING, "test issue", "[]", "[]");
        if (terminal)
        {
            // CLOSED qua Withdraw (OPEN → CLOSED)
            c.Withdraw();
        }
        _db.DisputeCases.Add(c);
        await _db.SaveChangesAsync();
        return (c.Id, subjectId);
    }

    private static UploadAttachmentRequest PngRequest() => new(
        "receipt.png", "image/png", Convert.ToBase64String([1, 2, 3, 4]));

    [Fact]
    public async Task Upload_ValidCase_StoresMetadataAndEvidenceItem()
    {
        var (caseId, subjectId) = await SeedCaseAsync();
        var result = await _service.UploadAsync(caseId, subjectId, PngRequest());

        Assert.NotNull(result);
        Assert.Equal("receipt.png", result!.Attachment.FileName);
        Assert.Equal(4, result.Attachment.SizeBytes);
        Assert.NotNull(result.Attachment.DownloadUrl);
        Assert.Equal(1, await _db.EvidenceAttachments.CountAsync());
        Assert.Equal(1, await _db.DisputeEvidenceItems.CountAsync(e => e.DisputeCaseId == caseId));
        Assert.Single(_storage.Uploads);
    }

    [Fact]
    public async Task Upload_WrongSubject_ReturnsNull()
    {
        var (caseId, _) = await SeedCaseAsync();
        var result = await _service.UploadAsync(caseId, Guid.NewGuid(), PngRequest());
        Assert.Null(result);
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task Upload_TerminalCase_Throws()
    {
        var (caseId, subjectId) = await SeedCaseAsync(terminal: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadAsync(caseId, subjectId, PngRequest()));
    }

    [Fact]
    public async Task Upload_DisallowedContentType_Throws()
    {
        var (caseId, subjectId) = await SeedCaseAsync();
        var req = new UploadAttachmentRequest("evil.exe", "application/x-msdownload", Convert.ToBase64String([1]));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadAsync(caseId, subjectId, req));
    }

    [Fact]
    public async Task Upload_Oversized_Throws()
    {
        var (caseId, subjectId) = await SeedCaseAsync();
        var big = new byte[EvidenceAttachment.MaxSizeBytes + 1];
        var req = new UploadAttachmentRequest("big.png", "image/png", Convert.ToBase64String(big));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadAsync(caseId, subjectId, req));
    }

    [Fact]
    public async Task List_WrongSubject_ReturnsNull_OwnerGetsSignedUrls()
    {
        var (caseId, subjectId) = await SeedCaseAsync();
        await _service.UploadAsync(caseId, subjectId, PngRequest());

        Assert.Null(await _service.ListAsync(caseId, Guid.NewGuid()));
        var list = await _service.ListAsync(caseId, subjectId);
        Assert.Single(list!);
        Assert.NotNull(list![0].DownloadUrl);
    }

    [Fact]
    public async Task Upload_Cap10_Throws()
    {
        var (caseId, subjectId) = await SeedCaseAsync();
        for (var i = 0; i < 10; i++)
            await _service.UploadAsync(caseId, subjectId, PngRequest());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadAsync(caseId, subjectId, PngRequest()));
    }

    private sealed class FakeObjectStorage : IAtrsObjectStorage
    {
        public List<(string Key, byte[] Data, string ContentType)> Uploads { get; } = [];
        public bool IsConfigured => true;
        public string GetPresignedPutUrl(string key, string contentType, int ttlMinutes = 15) => $"put://{key}";
        public string GetPresignedGetUrl(string key, int ttlMinutes = 15) => $"get://{key}";
        public Task UploadAsync(string key, byte[] data, string contentType, CancellationToken ct = default)
        {
            Uploads.Add((key, data, contentType));
            return Task.CompletedTask;
        }
    }
}
