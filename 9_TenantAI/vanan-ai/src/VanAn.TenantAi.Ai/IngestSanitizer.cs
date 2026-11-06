using System.Text.RegularExpressions;

namespace VanAn.TenantAi.Ai;

public sealed record SanitizeResult(string Text, bool Sanitized, int RemovedLines);

/// <summary>
/// task_ai_p2 / SRS T3 — strip instruction-like lines from ingested document content
/// before chunking. Knowledge docs are data, never instructions — a doc containing
/// "IGNORE ALL RULES" must not reach the prompt verbatim. Lines matching the pattern
/// are removed; the doc is marked sanitized (no silent edits).
/// </summary>
public static class IngestSanitizer
{
    private static readonly Regex InstructionPattern = new(
        @"ignore\s+(all|previous|every|the\s+above)|forget\s+(all|previous|your|everything)|"
        + @"system\s*prompt|new\s+instructions?|you\s+are\s+(now|a|an)\b|reveal\s+(your|the)\s+prompt|"
        + @"bỏ\s+qua\s+(mọi|tất\s+cả|luật)|hãy\s+quên|ignore_all_rules|do\s+not\s+follow",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SanitizeResult Sanitize(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new SanitizeResult(string.Empty, false, 0);

        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var kept = lines.Where(l => !InstructionPattern.IsMatch(l)).ToList();
        var removed = lines.Length - kept.Count;
        return new SanitizeResult(string.Join('\n', kept).Trim(), removed > 0, removed);
    }
}
