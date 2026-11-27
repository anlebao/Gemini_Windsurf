using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Services;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W3b mode A — OcrMenuKnowledgeService: flag OcrMenuKnowledgeAuto gate việc
/// đẩy menu OCR onboarding → TenantAI knowledge (pending-review).
/// Default OFF = mode B (owner tự upload). SourceRef cố định → dedupe.
/// </summary>
public class OcrMenuKnowledgeServiceTests
{
    private sealed class FakeSettings(bool enabled) : IShopFeatureSettingsService
    {
        public HashSet<string> CheckedFlags { get; } = [];
        public Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ShopFeatureSettingsDto());
        public Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
            => Task.FromResult(settings);
        public Task<ShopFeatureSettingsDto> UpsertSyncedSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
            => Task.FromResult(settings);
        public Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default)
        {
            CheckedFlags.Add(toggleName);
            return Task.FromResult(enabled);
        }
        public Task SetToggleAsync(Guid tenantId, string toggleName, bool enabled, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAiClient : ITenantAiClient
    {
        public List<(Guid TenantId, string Type, string Title, string Content, string Classification, string? SourceRef)> Docs { get; } = [];
        public bool IngestResult { get; set; } = true;

        public Task<bool> IngestDocumentAsync(Guid tenantId, string type, string title, string content,
            string classification, string? sourceRef = null, CancellationToken ct = default)
        {
            Docs.Add((tenantId, type, title, content, classification, sourceRef));
            return Task.FromResult(IngestResult);
        }

        public Task<TenantAiBatchResponse?> PushProductsBatchAsync(Guid tenantId, IReadOnlyList<TenantAiProductItem> items,
            bool fullSync, string sourceVersion, string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult<TenantAiBatchResponse?>(null);
        public Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ApproveDocumentAsync(Guid tenantId, Guid docId, string? classification = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RejectDocumentAsync(Guid tenantId, Guid docId, string reason, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<TenantAiDocSummary>> GetPendingDocumentsAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(new List<TenantAiDocSummary>());
        public Task<TenantAiStatus?> GetKnowledgeStatusAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantAiStatus?>(null);
        public Task<TenantAiConversationList?> GetAiConversationsAsync(Guid tenantId, int page, int size, CancellationToken ct = default) => Task.FromResult<TenantAiConversationList?>(null);
        public Task<TenantAiConversationDetail?> GetAiConversationMessagesAsync(Guid tenantId, string sessionId, CancellationToken ct = default) => Task.FromResult<TenantAiConversationDetail?>(null);
        public Task<TenantAiProfileResponse?> GetProfileAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantAiProfileResponse?>(null);
        public Task<TenantAiPutProfileResult?> PutProfileAsync(Guid tenantId, string aiName, string? personaJson, string? businessProfileJson, CancellationToken ct = default) => Task.FromResult<TenantAiPutProfileResult?>(null);
        public Task<TenantAiPublishResult?> PublishConfigAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantAiPublishResult?>(null);
        public Task<TenantAiPublishResult?> RollbackConfigAsync(Guid tenantId, int version, CancellationToken ct = default) => Task.FromResult<TenantAiPublishResult?>(null);
        public Task<TenantAiPolicyResponse?> GetPolicyAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantAiPolicyResponse?>(null);
        public Task<TenantAiPolicyResponse?> PutPolicyAsync(Guid tenantId, string? intentScopesJson, string? externalAllowJson, string? refusedTopicsJson, CancellationToken ct = default) => Task.FromResult<TenantAiPolicyResponse?>(null);
        public Task<TenantAiLearningSetting?> GetLearningSettingAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantAiLearningSetting?>(null);
        public Task<bool> PutLearningSettingAsync(Guid tenantId, bool enabled, CancellationToken ct = default) => Task.FromResult(false);
        public Task<TenantAiChatResult?> PreviewChatAsync(Guid tenantId, string sessionId, string messageId, string text, CancellationToken ct = default) => Task.FromResult<TenantAiChatResult?>(null);
        public Task<TenantAiDocList?> GetDocumentsAsync(Guid tenantId, string? status, int page, int size, CancellationToken ct = default) => Task.FromResult<TenantAiDocList?>(null);
        public Task<bool> DeleteDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<TenantAiUsageResponse?> GetUsageAsync(Guid tenantId, int days, CancellationToken ct = default) => Task.FromResult<TenantAiUsageResponse?>(null);
        public Task<TenantAiExtractResponse?> ExtractKnowledgeAsync(Guid tenantId, string text, string? hint, CancellationToken ct = default) => Task.FromResult<TenantAiExtractResponse?>(null);
        public Task<TenantAiExtractGroupsResponse?> ExtractKnowledgeGroupsAsync(Guid tenantId, string text, string? hint, CancellationToken ct = default) => Task.FromResult<TenantAiExtractGroupsResponse?>(null);
    }

    private static readonly List<MenuOcrLine> SampleLines =
    [
        new("Phở bò", 65000m),
        new("Cơm tấm sườn", 55000m),
        new("Trà đá", null),
    ];

    private static OcrMenuKnowledgeService Build(FakeSettings settings, FakeAiClient ai)
        => new(settings, ai, NullLogger<OcrMenuKnowledgeService>.Instance);

    [Fact]
    public async Task FlagOff_ReturnsNull_NoIngest()
    {
        var settings = new FakeSettings(enabled: false);
        var ai = new FakeAiClient();
        var svc = Build(settings, ai);

        var result = await svc.IngestMenuIfEnabledAsync(Guid.NewGuid(), SampleLines);

        Assert.Null(result);
        Assert.Empty(ai.Docs);
        Assert.Contains("OcrMenuKnowledgeAuto", settings.CheckedFlags);
    }

    [Fact]
    public async Task FlagOn_IngestsMenuNote_WithStableSourceRef()
    {
        var tenantId = Guid.NewGuid();
        var settings = new FakeSettings(enabled: true);
        var ai = new FakeAiClient();
        var svc = Build(settings, ai);

        var result = await svc.IngestMenuIfEnabledAsync(tenantId, SampleLines);

        Assert.NotNull(result);
        var doc = Assert.Single(ai.Docs);
        Assert.Equal(tenantId, doc.TenantId);
        Assert.Equal("menu-note", doc.Type);
        Assert.Equal("public", doc.Classification);
        Assert.Equal($"ocr-menu:{tenantId}", doc.SourceRef);
        Assert.Contains("Phở bò", doc.Content);
        // Price format is culture-dependent (65.000đ / 65,000đ) — normalize separators.
        Assert.Contains("65000", doc.Content.Replace(".", "").Replace(",", ""));
        Assert.Contains("Trà đá", doc.Content);
    }

    [Fact]
    public async Task EmptyLines_ReturnsNull_NoFlagCheck()
    {
        var settings = new FakeSettings(enabled: true);
        var ai = new FakeAiClient();
        var svc = Build(settings, ai);

        var result = await svc.IngestMenuIfEnabledAsync(Guid.NewGuid(), []);

        Assert.Null(result);
        Assert.Empty(ai.Docs);
        Assert.Empty(settings.CheckedFlags);
    }

    [Fact]
    public async Task IngestFails_ReturnsFailureMessage_NoThrow()
    {
        var settings = new FakeSettings(enabled: true);
        var ai = new FakeAiClient { IngestResult = false };
        var svc = Build(settings, ai);

        var result = await svc.IngestMenuIfEnabledAsync(Guid.NewGuid(), SampleLines);

        Assert.NotNull(result);
        Assert.Contains("thất bại", result);
    }
}
