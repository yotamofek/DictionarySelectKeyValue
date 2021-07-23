using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DictionarySelectKeyValue
{

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DictionarySelectKeyValueCodeFixProvider)), Shared]
    public class DictionarySelectKeyValueCodeFixProvider : CodeFixProvider
    {
        private readonly string CodeFixTitle = "Use built-in dictionary iterators";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DictionarySelectKeyValueAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var dictionaryProp = diagnostic.Properties[DictionarySelectKeyValueAnalyzer.DictionaryPropKey];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type invocation of `Enumerable.Select` identified by the diagnostic.
            var diagnosticNode = root.FindNode(diagnosticSpan);
            if (!(diagnosticNode is InvocationExpressionSyntax invocationExpr))
            {
                invocationExpr = diagnosticNode
                    .ChildNodes()
                    .Select(node => node as InvocationExpressionSyntax)
                    .Where(node => !(node is null))
                    .First();
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixTitle,
                    createChangedDocument: c =>
                        UseDictionaryIterators(context.Document, invocationExpr, dictionaryProp, c),
                    equivalenceKey: CodeFixTitle),
                diagnostic);
        }

        private async Task<Document> UseDictionaryIterators(Document document, InvocationExpressionSyntax invocationExpr, string dictionaryProp, CancellationToken cancellationToken)
        {
            var receiverExpression = (invocationExpr.Expression as MemberAccessExpressionSyntax).Expression;

            var newPropAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiverExpression,
                SyntaxFactory.IdentifierName(dictionaryProp));

            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode newRoot = oldRoot.ReplaceNode(invocationExpr, newPropAccess);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
