using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DependencyDirectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1002",
        "Invalid dependency direction",
        "Service layer should not reference API layer",
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
        
        // Only check Service layer
        if (!filePath.Contains("3_CoreHub/Services")) return;
        
        var import = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(import)) return;
        
        // Check for API layer references
        if (import.Contains("VanAn.Gateway") || import.Contains("VanAn.KhachLink") || import.Contains("VanAn.ShopERP"))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.GetLocation()));
        }
    }
}
