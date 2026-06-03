using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers
{
    public static class AnalyzerHelpers
    {
        public static bool ShouldSkipAnalysis(SyntaxNodeAnalysisContext context)
        {
            string filePath = context.Node.SyntaxTree?.FilePath ?? "";
            return !string.IsNullOrEmpty(filePath)
&& (filePath.Contains("\\Analyzers\\", StringComparison.OrdinalIgnoreCase) ||
                   filePath.Contains("/Analyzers/", StringComparison.OrdinalIgnoreCase) ||
                   context.SemanticModel?.Compilation?.AssemblyName?.IndexOf("Analyzer", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool ShouldSkipAnalysis(AdditionalFileAnalysisContext context)
        {
            string filePath = context.AdditionalFile?.Path ?? "";
            return !string.IsNullOrEmpty(filePath)
&& (filePath.Contains("\\Analyzers\\", StringComparison.OrdinalIgnoreCase) ||
                   filePath.Contains("/Analyzers/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// ContainsIgnoreCase - Fix CA2249 using IndexOf (stable on .NET Standard 2.0)
        /// </summary>
        public static bool ContainsIgnoreCase(this string source, string value)
        {
            return source != null && value != null && source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ContainsOrdinal
        /// </summary>
        public static bool ContainsOrdinal(this string source, string value)
        {
            return source != null && value != null && source.Contains(value);
        }

        /// <summary>
        /// Check if string contains any of the values (case-insensitive)
        /// </summary>
        public static bool ContainsAny(this string source, params string[] values)
        {
            if (source == null || values == null || values.Length == 0)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (source.ContainsIgnoreCase(value))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
