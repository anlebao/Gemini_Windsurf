using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.TenantAi.Tests.Eval;

/// <summary>
/// task_ai_p3 / SRS NFR-10 — eval harness over `evalset.json` (per-intent vi-VN
/// cases). Runs the real ChatOrchestrator on an InMemory store with deterministic
/// fakes (FakeLlmChatClient grounds every answer to prompt facts; FakeEmbedding
/// is inert on InMemory). Metrics:
///   · Grounded Answer Rate ≥95% — every numeric claim in a non-refused answer
///     matches a retrieved structured fact
///   · Unsupported Claim Rate ≤2%
///   · Refuse-correct rate ≥95% on OutOfScope/PolicyViolation cases
/// </summary>
public sealed class EvalHarnessTests(ITestOutputHelper output)
{
    private static readonly Guid Tenant = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

    private sealed record EvalCase(string Text, string ExpectIntent, bool ExpectRefused);

    private static readonly Regex NumericClaim = new(
        @"(\d[\d.,\s]*\d|\d)\s*(kđ|k|ngàn|nghìn|đ|đồng|vnd|vnđ|kcal|calo|calories)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static TenantAiDbContext SeedDb()
    {
        var db = new TenantAiDbContext(new DbContextOptionsBuilder<TenantAiDbContext>()
            .UseInMemoryDatabase("eval-" + Guid.NewGuid()).Options);
        db.Database.EnsureCreated();
        db.IntentRoutes.AddRange(DefaultPolicySeed.IntentRoutes());
        db.KnowledgeUsagePermissions.AddRange(DefaultPolicySeed.UsagePermissions());

        void Product(string name, decimal price, int? calories = null)
        {
            var p = ProductKnowledge.Create(Tenant, Guid.NewGuid(), name, price, "eval");
            p.UpdateFromSource(name, null, price, calories, "[]", "[]", true, "Món chính", "eval");
            db.ProductKnowledge.Add(p);
        }
        Product("Phở chay", 80_000m, 320);
        Product("Cơm chay", 50_000m, 450);
        Product("Gỏi cuốn", 30_000m, 120);
        Product("Bún chay", 55_000m);

        var doc = KnowledgeDocument.Create(Tenant, "story", "Câu chuyện quán",
            "Quán chay gia đình mở từ năm 2015.", DocumentClassification.Public);
        doc.Approve("eval");
        db.KnowledgeDocuments.Add(doc);
        db.KnowledgeChunks.Add(KnowledgeChunk.Create(Tenant, doc.Id, 0, doc.Content, null, 0));

        var faq = KnowledgeDocument.Create(Tenant, "faq", "Giờ mở cửa",
            "Quán mở cửa 7h-21h hằng ngày.", DocumentClassification.Public);
        faq.Approve("eval");
        db.KnowledgeDocuments.Add(faq);
        db.KnowledgeChunks.Add(KnowledgeChunk.Create(Tenant, faq.Id, 0, faq.Content, null, 0));

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Eval_PipelineMetrics_MeetThresholds()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Eval", "evalset.json");
        File.Exists(path).Should().BeTrue($"eval set must exist at {path}");
        var cases = JsonSerializer.Deserialize<List<EvalCase>>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        cases.Count.Should().BeGreaterThanOrEqualTo(30, "NFR-10 eval set needs ≥30 cases");

        await using var db = SeedDb();
        var llm = new FakeLlmChatClient();
        var config = new ConfigurationBuilder().Build();
        var router = new IntentRouter(llm, NullLogger<IntentRouter>.Instance);
        var retriever = new KnowledgeRetriever(db, new FakeEmbeddingProvider(), config,
            NullLogger<KnowledgeRetriever>.Instance);
        var orch = new ChatOrchestrator(db, router, retriever, new PromptBuilder(),
            new AnswerValidator(config), llm, config, NullLogger<ChatOrchestrator>.Instance);

        var products = await db.ProductKnowledge.AsNoTracking()
            .Where(p => p.TenantId == Tenant).ToListAsync();
        var knownNumbers = products
            .SelectMany(p => new decimal?[] { p.Price, p.Calories })
            .Where(v => v != null).Select(v => v!.Value).ToList();

        int intentOk = 0, refuseExpected = 0, refuseOk = 0, answered = 0, grounded = 0, unsupported = 0;
        var misses = new List<string>();
        var i = 0;
        foreach (var c in cases)
        {
            i++;
            var result = await orch.ChatAsync(Tenant, $"eval-s{i}", $"eval-m{i}", c.Text,
                channel: "public", useDraftConfig: false, CancellationToken.None);

            if (result.Intent == c.ExpectIntent) intentOk++;
            else misses.Add($"intent '{c.Text}': got {result.Intent}, want {c.ExpectIntent}");

            if (c.ExpectRefused)
            {
                refuseExpected++;
                if (result.Refused) refuseOk++;
                else misses.Add($"refuse '{c.Text}': not refused");
                continue;
            }
            if (result.Refused)
            {
                misses.Add($"refuse '{c.Text}': unexpected refusal ({result.RefuseReason})");
                continue;
            }

            answered++;
            var bad = NumericClaim.Matches(result.Answer)
                .Where(m => TryParseNum(m.Groups[1].Value, out var n))
                .Select(m => ClaimValue(m.Groups[1].Value, m.Groups[2].Value))
                .Where(n => !knownNumbers.Any(k => Math.Abs(k - n) < 1m))
                .ToList();
            if (bad.Count == 0) grounded++;
            else { unsupported++; misses.Add($"ungrounded '{c.Text}': claims [{string.Join(",", bad)}] -> {result.Answer}"); }
        }

        var total = (double)cases.Count;
        output.WriteLine($"cases={cases.Count} intent={intentOk}/{cases.Count} "
            + $"refuse={refuseOk}/{refuseExpected} grounded={grounded}/{answered} unsupported={unsupported}");
        foreach (var m in misses) output.WriteLine("  MISS " + m);

        (refuseOk / (double)Math.Max(refuseExpected, 1)).Should().BeGreaterThanOrEqualTo(0.95);
        (grounded / (double)Math.Max(answered, 1)).Should().BeGreaterThanOrEqualTo(0.95);
        (unsupported / total).Should().BeLessThanOrEqualTo(0.02);
        (intentOk / total).Should().BeGreaterThanOrEqualTo(0.90);
    }

    private static bool TryParseNum(string raw, out decimal v)
    {
        var cleaned = raw.Replace(".", "").Replace(",", "").Replace(" ", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out v);
    }

    private static decimal ClaimValue(string number, string unit)
    {
        TryParseNum(number, out var n);
        var mult = unit.ToLowerInvariant() is "k" or "kđ" or "ngàn" or "nghìn" ? 1000m : 1m;
        var isCal = unit.Contains("cal", StringComparison.OrdinalIgnoreCase) || unit == "kcal" || unit == "calo";
        return isCal ? n : n * mult;
    }
}
