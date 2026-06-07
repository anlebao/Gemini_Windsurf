using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DomainEntityLocationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1001",
        "Domain entity defined outside Domain layer",
        "Domain entities should only be defined in 1_Shared/Domain.cs",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context))
        {
            return;
        }

        var node = context.Node;
        var filePath = node.SyntaxTree.FilePath;
        
        // Allow domain entities in 1_Shared
        if (filePath.Contains("1_Shared")) return;
        
        // Allow test files to define test entities
        if (filePath.Contains("Tests")) return;
        
        // Check if this is a domain entity by naming pattern
        var isDomainEntity = IsDomainEntity(node);
        if (!isDomainEntity) return;
        
        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
    }

    private bool IsDomainEntity(SyntaxNode node)
    {
        var name = (node as TypeDeclarationSyntax)?.Identifier.Text;
        if (string.IsNullOrEmpty(name)) return false;
        
        // Domain entity naming patterns
        return name.EndsWith("Entry") ||
               name.EndsWith("Balance") ||
               name.EndsWith("Ledger") ||
               name.EndsWith("Package") ||
               name.EndsWith("Invoice") ||
               name.EndsWith("Aggregate") ||
               name.EndsWith("Event");
    }
}
