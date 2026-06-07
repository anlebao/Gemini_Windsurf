using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EfCoreInDomainAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1003",
        "EF Core found in Domain layer",
        "Domain layer should not reference EF Core (purity violation)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            compilationContext.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.UsingDirective);
        });
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context))
        {
            return;
        }

        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Domain layer
        if (!filePath.Contains("1_Shared")) return;
        
        var import = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(import)) return;
        
        // Check for EF Core references
        if (import.Contains("Microsoft.EntityFrameworkCore") || 
            import.Contains("System.ComponentModel.DataAnnotations"))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.GetLocation()));
        }
    }
}
