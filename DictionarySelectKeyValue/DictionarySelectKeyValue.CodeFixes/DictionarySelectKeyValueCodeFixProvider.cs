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

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var dictionaryProp = diagnostic.Properties["dictionaryProp"];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindNode(diagnosticSpan) as InvocationExpressionSyntax;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixTitle,
                    createChangedDocument: c => UseDictionaryIterators(context.Document, declaration, dictionaryProp, c),
                    equivalenceKey: CodeFixTitle),
                diagnostic);
        }

        private async Task<Document> UseDictionaryIterators(Document document, InvocationExpressionSyntax invocationExpr, string dictionaryProp, CancellationToken cancellationToken)
        {
            var newPropAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                (invocationExpr.Expression as MemberAccessExpressionSyntax).Expression,
                SyntaxFactory.IdentifierName(dictionaryProp));

            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode newRoot = oldRoot.ReplaceNode(invocationExpr, newPropAccess);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
