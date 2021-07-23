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

        private static readonly LocalizableString Title = "Use built-in Dictionary dimension iteration methods.";
        private static readonly LocalizableString MessageFormat = "Use Dictionary.{0} instead.";
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

        private static bool IsEnumerableSelectMethod(IMethodSymbol methodSymbol)
        {
            var methodContainingType = methodSymbol.ContainingType;

            return methodContainingType.Name == "Enumerable"
                && methodContainingType.ContainingNamespace?.ToDisplayString() == "System.Linq";
        }

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
                default:
                    {
                        throw new InvalidCastException();
                    }
            }
        }

        private static bool SymbolImplementsIDictionary(ISymbol symbol)
        {
            var symbolType = GetSymbolType(symbol);

            return symbolType.AllInterfaces.Any(
                interface_ =>
                    interface_.Name == "IDictionary"
                        && interface_.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            var memberAccessExpr =
              invocationExpr.Expression as MemberAccessExpressionSyntax;

            if (memberAccessExpr?.Name.ToString() != "Select") return;

            var memberSymbol = context.SemanticModel.
              GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;

            // TODO: support more than `ILocalSymbol`
            var receiverSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr.Expression).Symbol;

            // make sure the receiver implement the `IDictionary` interface
            if (!SymbolImplementsIDictionary(receiverSymbol)) return;

            if (!IsEnumerableSelectMethod(memberSymbol)) return;

            var selectArg = invocationExpr.ArgumentList.Arguments.First();
            if (selectArg is null) return;

            if (!(selectArg.Expression is LambdaExpressionSyntax argExpression)) return;
            if (!(GetLambdaArgIdentifier(argExpression) is SyntaxToken kvIdentifier)) return;

            string propertyName;

            switch (argExpression.Body)
            {
                case MemberAccessExpressionSyntax expression:
                    {
                        if (expression.Expression.ToString() != kvIdentifier.ToString()) return;
                        propertyName = expression.Name.Identifier.ToString();

                        break;
                    }
                default:
                    {
                        return;
                    }
            }

            var dictionaryProp = new Dictionary<string, string> {
                { "Key", "Keys" },
                { "Value", "Values" }
            }[propertyName];

            if (dictionaryProp != null)
            {
                var diagnostic =
                      Diagnostic.Create(
                          Rule,
                          invocationExpr.GetLocation(),
                          properties: new Dictionary<string, string> {
                              { "dictionaryProp", dictionaryProp }
                          }.ToImmutableDictionary(),
                          messageArgs: propertyName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
