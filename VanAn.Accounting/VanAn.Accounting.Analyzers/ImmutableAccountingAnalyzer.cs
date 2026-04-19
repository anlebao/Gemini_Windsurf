using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace VanAn.Accounting.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ImmutableAccountingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VA0001";
        private const string Category = "Architecture";

        private static readonly LocalizableString Title = "AccountingEntry mutation detected";
        private static readonly LocalizableString MessageFormat = "AccountingEntry must be immutable. Do not modify properties after creation. Use reversal entries for corrections.";
        private static readonly LocalizableString Description = "Accounting entries should never be modified after creation. All corrections must be done through reversal entries to maintain audit trail.";

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
            context.RegisterSyntaxNodeAction(AnalyzePropertyAssignment, SyntaxKind.SimpleAssignmentExpression);
        }

        
        private void AnalyzePropertyAssignment(SyntaxNodeAnalysisContext context)
        {
            if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

            var assignment = (AssignmentExpressionSyntax)context.Node;
            
            // Check if assignment is to an AccountingEntry property
            if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = memberAccess.Name.Identifier.ValueText;
                var expressionType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
                
                if (expressionType != null && expressionType.Name == "AccountingEntry")
                {
                    // Allow setting properties in constructors
                    if (!IsInConstructor(context, assignment))
                    {
                        var diagnostic = Diagnostic.Create(Rule, assignment.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsInConstructor(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var constructor = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            return constructor != null;
        }
    }
}
