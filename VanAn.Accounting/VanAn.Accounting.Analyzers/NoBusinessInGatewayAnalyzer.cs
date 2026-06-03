using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace VanAn.Accounting.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NoBusinessInGatewayAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VA0003";
        private const string Category = "Architecture";

        private static readonly LocalizableString Title = "Business logic detected in Gateway/Controller";
        private static readonly LocalizableString MessageFormat = "Business logic should be in CoreHub Services, not in Gateway/Controller. Move this logic to a service class.";
        private static readonly LocalizableString Description = "Controllers should only handle HTTP concerns, routing, and orchestration. All business logic must be implemented in CoreHub services.";

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
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            var typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation).Type;

            if (typeSymbol != null && IsBusinessType(typeSymbol))
            {
                if (IsInController(context, objectCreation))
                {
                    var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
        {
            if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

            var invocation = (InvocationExpressionSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (methodSymbol != null && IsBusinessMethod(methodSymbol))
            {
                if (IsInController(context, invocation))
                {
                    var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        
        private static bool IsBusinessType(ITypeSymbol type)
        {
            // Check if type is in business domain
            var namespaceName = type.ContainingNamespace?.ToString();
            
            if (namespaceName == null) return false;

            // Business namespaces that should not be used directly in controllers
            var businessNamespaces = new[]
            {
                "VanAn.Accounting.Services",
                "VanAn.Accounting.Domain",
                "VanAn.Accounting.Repositories"
            };

            return businessNamespaces.Any(ns => namespaceName.StartsWith(ns, StringComparison.Ordinal));
        }

        private static bool IsBusinessMethod(IMethodSymbol method)
        {
            // Check if method is on a business type
            var containingType = method.ContainingType;
            return containingType != null && IsBusinessType(containingType);
        }

        private static bool IsInController(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check if node is within a Controller class
            var typeDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (typeDeclaration == null) return false;

            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
            if (typeSymbol == null) return false;

            // Check if class inherits from Controller or has Controller suffix
            return typeSymbol.AllInterfaces.Any(i => i.Name == "IController") ||
                   typeSymbol.Name.EndsWith("Controller", StringComparison.Ordinal);
        }
    }
}
