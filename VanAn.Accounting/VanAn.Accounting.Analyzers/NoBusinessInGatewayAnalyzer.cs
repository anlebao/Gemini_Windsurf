using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

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

        private static readonly DiagnosticDescriptor Rule = new(
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
            if (AnalyzerHelpers.ShouldSkipAnalysis(context))
            {
                return;
            }

            ObjectCreationExpressionSyntax objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            ITypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation).Type;

            if (typeSymbol != null && IsBusinessType(typeSymbol))
            {
                if (IsInController(context, objectCreation))
                {
                    Diagnostic diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
        {
            if (AnalyzerHelpers.ShouldSkipAnalysis(context))
            {
                return;
            }

            InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

            if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol && IsBusinessMethod(methodSymbol))
            {
                if (IsInController(context, invocation))
                {
                    Diagnostic diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }


        private static bool IsBusinessType(ITypeSymbol type)
        {
            // Check if type is in business domain
            string? namespaceName = type.ContainingNamespace?.ToString();

            if (namespaceName == null)
            {
                return false;
            }

            // Business namespaces that should not be used directly in controllers
            string[] businessNamespaces =
            [
                "VanAn.Accounting.Services",
                "VanAn.Accounting.Domain",
                "VanAn.Accounting.Repositories"
            ];

            return businessNamespaces.Any(ns => namespaceName.StartsWith(ns, StringComparison.Ordinal));
        }

        private static bool IsBusinessMethod(IMethodSymbol method)
        {
            // Check if method is on a business type
            INamedTypeSymbol containingType = method.ContainingType;
            return containingType != null && IsBusinessType(containingType);
        }

        private static bool IsInController(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check if node is within a Controller class
            ClassDeclarationSyntax? typeDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (typeDeclaration == null)
            {
                return false;
            }

            INamedTypeSymbol? typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
            if (typeSymbol == null)
            {
                return false;
            }

            // Check if class inherits from Controller or has Controller suffix
            return typeSymbol.AllInterfaces.Any(i => i.Name == "IController") ||
                   typeSymbol.Name.EndsWith("Controller", StringComparison.Ordinal);
        }
    }
}
