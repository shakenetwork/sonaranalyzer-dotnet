using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace SonarAnalyzer.Rules
{
    [ExportCodeFixProvider(LanguageNames.VisualBasic)]
    public class UseShortCircuitingOperatorFixProvider : SonarCodeFixProvider
    {
        internal const string Title = "Use short-circuiting operators";

        public override ImmutableArray<string> FixableDiagnosticIds =>  ImmutableArray.Create(UseShortCircuitingOperator.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => DocumentBasedFixAllProvider.Instance;

        protected override async Task RegisterCodeFixesAsync(SyntaxNode root, CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var expression = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) as BinaryExpressionSyntax;
            if (expression == null ||
                !UseShortCircuitingOperator.ShortCircuitingAlternative.ContainsKey(expression.Kind()))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => ReplaceExpression(expression, root, context.Document)),
                context.Diagnostics);
        }

        private static Task<Document> ReplaceExpression(BinaryExpressionSyntax expression,
            SyntaxNode root, Document document)
        {
            var replacement = GetShortCircuitingExpressionNode(expression)
                .WithTriviaFrom(expression);
            var newRoot = root.ReplaceNode(expression, replacement);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private static SyntaxNode GetShortCircuitingExpressionNode(BinaryExpressionSyntax expression)
        {
            var kind = expression.Kind();
            if(kind == SyntaxKind.AndExpression)
            {
                return SyntaxFactory.AndAlsoExpression(expression.Left, expression.Right);
            }

            if(kind == SyntaxKind.OrExpression)
            {
                return SyntaxFactory.OrElseExpression(expression.Left, expression.Right);
            }
            return expression;
        }
    }
}
