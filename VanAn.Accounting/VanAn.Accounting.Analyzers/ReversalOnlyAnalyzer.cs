using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace VanAn.Accounting.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReversalOnlyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VA0002";
        private const string Category = "Architecture";

        private static readonly LocalizableString Title = "AccountingEntry deletion/modification detected";
        private static readonly LocalizableString MessageFormat = "AccountingEntry must only use reversal entries for corrections. Do not delete or modify entries.";
        private static readonly LocalizableString Description = "All corrections to accounting entries must be done through reversal entries to maintain audit trail and data integrity.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeRemoveCall, SyntaxKind.InvocationExpression);
        }

        
        private void AnalyzeRemoveCall(SyntaxNodeAnalysisContext context)
        {
            if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

            var invocation = (InvocationExpressionSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (methodSymbol != null)
            {
                // Check for Remove, Delete, or Update methods on collections containing AccountingEntry
                var methodName = methodSymbol.Name;
                if (methodName == "Remove" || methodName == "Delete" || methodName == "Update")
                {
                    var receiverType = methodSymbol.ReceiverType;
                    if (receiverType != null && IsAccountingEntryCollection(receiverType))
                    {
                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsAccountingEntryCollection(ITypeSymbol type)
        {
            // Check if type is a collection of AccountingEntry
            if (type is INamedTypeSymbol namedType)
            {
                // Check for IEnumerable<AccountingEntry>, List<AccountingEntry>, etc.
                if (namedType.OriginalDefinition != null)
                {
                    var typeName = namedType.OriginalDefinition.Name;
                    if (typeName == "IEnumerable" || typeName == "List" || typeName == "ICollection" || typeName == "DbSet")
                    {
                        var typeArgs = namedType.TypeArguments;
                        if (typeArgs.Length == 1 && typeArgs[0].Name == "AccountingEntry")
                        {
                            return true;
                        }
                    }
                }
                
                // Check for DbSet<AccountingEntry> directly
                if (namedType.Name == "DbSet")
                {
                    var typeArgs = namedType.TypeArguments;
                    if (typeArgs.Length == 1 && typeArgs[0].Name == "AccountingEntry")
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}
