using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace DictionarySelectKeyValue
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DictionarySelectKeyValueAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DictionarySelectKeyValue";
        public const string DictionaryPropKey = nameof(DictionaryPropKey);

        private static readonly LocalizableString Title = "Use built-in Dictionary dimension iteration methods.";
        private static readonly LocalizableString MessageFormat = "Prefer using Dictionary.{0}s instead.";
        private static readonly LocalizableString Description = "Dictionary dimension iteration is manually implemented.";
        private const string Category = "Consistency";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(
              AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private static bool IsEnumerableSelectMethod(ISymbol symbol) =>
            symbol is IMethodSymbol methodSymbol
                && methodSymbol.Name == "Select"
                && methodSymbol.ContainingType.Name == "Enumerable"
                && methodSymbol.ContainingType.ContainingNamespace?.ToDisplayString() == "System.Linq";

        private static SyntaxToken? GetLambdaArgIdentifier(LambdaExpressionSyntax lambdaExpression)
        {
            switch (lambdaExpression)
            {
                case SimpleLambdaExpressionSyntax simpleLambda:
                    {
                        return simpleLambda.Parameter.Identifier;
                    }
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    {
                        var parameters = parenthesizedLambda.ParameterList.Parameters;
                        if (parameters.Count == 1)
                        {
                            return parameters.First().Identifier;
                        }
                        else
                        {
                            return null;
                        }

                    }

                default:
                    throw new InvalidCastException();
            }
        }

        private static ITypeSymbol GetSymbolType(ISymbol symbol)
        {
            switch (symbol)
            {
                case ILocalSymbol local:
                    {
                        return local.Type;
                    }
                case IPropertySymbol property:
                    {
                        return property.Type;
                    }
                case IParameterSymbol parameter:
                    {
                        return parameter.Type;
                    }
                case IMethodSymbol method:
                    {
                        return method.ReturnType;
                    }
                case IFieldSymbol field:
                    {
                        return field.Type;
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private static bool IsDictionaryInterface(INamedTypeSymbol symbol) =>
            (symbol.Name == "IDictionary" || symbol.Name == "IReadOnlyDictionary")
            && symbol.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

        private static bool? SymbolImplementsIDictionary(ISymbol symbol)
        {
            var symbolType = GetSymbolType(symbol);
            if (symbolType is null) return null;

            return (symbolType is INamedTypeSymbol namedSymbol && IsDictionaryInterface(namedSymbol))
                || symbolType.AllInterfaces.Any(IsDictionaryInterface);
        }

        private static ExpressionSyntax GetLambdaReturnExpr(LambdaExpressionSyntax lambda)
        {
            switch (lambda.Body)
            {
                case BlockSyntax block when block.Statements.Count == 1
                    && block.Statements.First() is ReturnStatementSyntax returnStmt:
                    {
                        return returnStmt.Expression;
                    }
                case ExpressionSyntax expr:
                    {
                        return expr;
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            // Find all calls to to the Enumerable.Select extension method
            if (!(invocationExpr.Expression is MemberAccessExpressionSyntax selectMethodAccessExpr
                && context.SemanticModel.GetSymbolInfo(selectMethodAccessExpr).Symbol is IMethodSymbol memberSymbol
                && IsEnumerableSelectMethod(memberSymbol))) return;

            // Get the symbol for the receiver, i.e. the dictionary being iterated
            var receiverSymbol = context.SemanticModel.GetSymbolInfo(selectMethodAccessExpr.Expression).Symbol;

            // Make sure the receiver implements the `IDictionary` interface
            if (!(SymbolImplementsIDictionary(receiverSymbol) is true)) return;

            // Look for calls to Enumerable.Select calls with a lambda as the first arg
            if (!(invocationExpr.ArgumentList.Arguments.First() is ArgumentSyntax selectArg
                && selectArg.Expression is LambdaExpressionSyntax lambdaExpr
                && GetLambdaArgIdentifier(lambdaExpr) is SyntaxToken lambdaArgIdentifier))
            {
                return;
            }

            // Only diagnose on lambdas like "x => x.XXX"
            if (!(GetLambdaReturnExpr(lambdaExpr) is MemberAccessExpressionSyntax kvPropAccessExpr
                && kvPropAccessExpr.Expression.ToString() == lambdaArgIdentifier.ToString()))
            {
                return;
            }

            // The property on the KeyValue structure being accessed ("Key" or "Value")
            var propertyName = kvPropAccessExpr.Name.Identifier.ToString();

            // The property on the Dictionary that we will suggest to use ("Keys" or "Values")
            if (!(new Dictionary<string, string> {
                { "Key", "Keys" },
                { "Value", "Values" }
            }[propertyName] is string dictionaryProp)) return;

            var diagnostic =
                  Diagnostic.Create(
                      Rule,
                      invocationExpr.GetLocation(),
                      properties: new Dictionary<string, string> {
                              // Pass the name of the iter prop to the fix provider.
                              { DictionaryPropKey, dictionaryProp }
                      }.ToImmutableDictionary(),
                      messageArgs: propertyName);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
