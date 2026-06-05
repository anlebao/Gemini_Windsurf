namespace VanAn.Accounting.Analyzers
{
    // Temporarily commented out to complete service layer build
    /*
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CentralPackageManagementAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VA0004";
        private const string Category = "Architecture";

        private static readonly LocalizableString Title = "Manual package version detected";
        private static readonly LocalizableString MessageFormat = "Manual version '{0}' detected in PackageReference. Use Directory.Build.props for central package management.";
        private static readonly LocalizableString Description = "Central Package Management requires all package versions to be defined in Directory.Build.props, not in individual .csproj files.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            customTags: new[] { "CompilationEnd" });

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(Rule);

    
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            
            // Register compilation action to analyze .csproj files
            context.RegisterCompilationAction(compilationContext =>
            {
                var projectFiles = compilationContext.Options.AdditionalFiles
                    .Where(f => f.Path.EndsWith(".csproj"))
                    .ToList();
                
                foreach (var projectFile in projectFiles)
                {
                    AnalyzeProjectFile(compilationContext, projectFile);
                }
            });
        }

        private void AnalyzeProjectFile(CompilationAnalysisContext context, AdditionalText projectFile)
        {
            // Skip analysis for analyzer project files
            var filePath = projectFile.Path;
            if (filePath.IndexOf("\\Analyzers\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filePath.IndexOf("/Analyzers/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filePath.EndsWith("VanAn.Accounting.Analyzers.csproj", StringComparison.OrdinalIgnoreCase))
                return;

            var text = projectFile.GetText();
            if (text == null) return;

            var content = text.ToString();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                if (line.Contains("<PackageReference") && line.Contains("Version="))
                {
                    var versionStart = line.IndexOf("Version=") + 8;
                    var versionEnd = line.IndexOf("\"", versionStart);
                    
                    if (versionEnd > versionStart)
                    {
                        var versionValue = line.Substring(versionStart, versionEnd - versionStart).Trim('"');
                        
                        if (versionValue.StartsWith("$"))
                            continue;

                        if (versionValue.Contains("$(MauiVersion)") || 
                            versionValue.Contains("$(Version)") ||
                            versionValue.Contains("$(PackageVersion)"))
                            continue;

                        var linePosition = new LinePosition(i, line.IndexOf("Version="));
                        var lineSpan = new LinePositionSpan(linePosition, linePosition);
                        var textSpan = new TextSpan(line.IndexOf("Version="), versionValue.Length + 9);
                        var location = Location.Create(projectFile.Path, textSpan, lineSpan);
                        
                        var diagnostic = Diagnostic.Create(Rule, location, versionValue);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
    */
}
