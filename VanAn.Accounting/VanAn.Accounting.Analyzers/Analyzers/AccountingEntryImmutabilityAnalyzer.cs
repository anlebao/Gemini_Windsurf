using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AccountingEntryImmutabilityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1005",
        "AccountingEntry mutability violation",
        "AccountingEntry must be immutable - use Reversal Entry pattern",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context))
        {
            return;
        }

        var property = (PropertyDeclarationSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Domain layer
        if (!filePath.Contains("1_Shared")) return;
        
        // Check if this is in AccountingEntry
        var classNode = property.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classNode == null) return;
        
        var className = classNode.Identifier.Text;
        if (className != "AccountingEntry" && className != "GeneralLedgerEntry") return;
        
        // Check if property has setter
        if (property.AccessorList != null)
        {
            var hasSetter = false;
            foreach (var accessor in property.AccessorList.Accessors)
            {
                if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
                {
                    // Check if setter is not private
                    var isPrivate = false;
                    foreach (var modifier in accessor.Modifiers)
                    {
                        if (modifier.IsKind(SyntaxKind.PrivateKeyword))
                        {
                            isPrivate = true;
                            break;
                        }
                    }
                    if (!isPrivate)
                    {
                        hasSetter = true;
                        break;
                    }
                }
            }
            
            if (hasSetter)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, property.GetLocation()));
            }
        }
    }
}
