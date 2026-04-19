using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace VanAn.Accounting.Analyzers;

public static class AnalyzerHelpers
{
    public static bool ShouldSkipAnalysis(SyntaxNodeAnalysisContext context)
    {
        var filePath = context.Node.SyntaxTree?.FilePath ?? "";
        if (string.IsNullOrEmpty(filePath)) return false;

        return filePath.IndexOf("\\Analyzers\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
               filePath.IndexOf("/Analyzers/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               context.SemanticModel?.Compilation?.AssemblyName?.IndexOf("Analyzer", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool ShouldSkipAnalysis(AdditionalFileAnalysisContext context)
    {
        var filePath = context.AdditionalFile?.Path ?? "";
        if (string.IsNullOrEmpty(filePath)) return false;

        return filePath.IndexOf("\\Analyzers\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
               filePath.IndexOf("/Analyzers/", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
