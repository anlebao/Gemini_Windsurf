using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BusinessLogicInGatewayAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1004",
        "Business logic in Gateway layer",
        "Gateway controllers should delegate to services, not implement business logic",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context))
        {
            return;
        }

        var invocation = (InvocationExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Gateway layer
        if (!filePath.Contains("2_Gateway/Controllers")) return;
        
        var methodName = invocation.Expression?.ToString();
        if (string.IsNullOrEmpty(methodName)) return;
        
        // Check for business logic patterns
        var businessLogicPatterns = new[]
        {
            ".ExecuteAsync",
            ".HandleAsync",
            ".Add(",
            ".Update(",
            ".SaveChangesAsync("
        };
        
        foreach (var pattern in businessLogicPatterns)
        {
            if (methodName.Contains(pattern))
            {
                // Allow if it's a service call
                if (methodName.Contains("_service.") || methodName.Contains("_repository."))
                    continue;
                
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                return;
            }
        }
    }
}
