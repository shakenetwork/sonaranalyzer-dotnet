using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SonarAnalyzer.Rules
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cert)]
    public class UseShortCircuitingOperator : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2178";
        internal const string Title = "Short-circuit logic should be used in boolean contexts";
        internal const string Description =
            "The use of non-short-circuit logic in a boolean context is likely a mistake - one that could cause " +
            "serious program errors as conditions are evaluated under the wrong circumstances.";
        internal const string MessageFormat = "Correct this \"{0}\" to \"{1}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var node = (BinaryExpressionSyntax)c.Node;

                    if (IsBool(node.Left, c.SemanticModel) &&
                        IsBool(node.Right, c.SemanticModel))
                    {
                        var kind = node.Kind();
                        var alternative = ShortCircuitingAlternative[kind];
                        c.ReportDiagnostic(Diagnostic.Create(Rule, node.OperatorToken.GetLocation(),
                            OperatorNames[kind], OperatorNames[alternative]));
                    }
                },
                SyntaxKind.AndExpression,
                SyntaxKind.OrExpression);
        }

        private static bool IsBool(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            if (expression == null)
            {
                return false;
            }

            var type = semanticModel.GetTypeInfo(expression).Type;
            return type.Is(KnownType.System_Boolean);
        }

        internal static readonly IDictionary<SyntaxKind, SyntaxKind> ShortCircuitingAlternative = new Dictionary<SyntaxKind, SyntaxKind>
        {
            { SyntaxKind.AndExpression, SyntaxKind.AndAlsoExpression },
            { SyntaxKind.OrExpression, SyntaxKind.OrElseExpression }
        }.ToImmutableDictionary();

        private static readonly IDictionary<SyntaxKind, string> OperatorNames = new Dictionary<SyntaxKind, string>
        {
            { SyntaxKind.AndExpression, "And" },
            { SyntaxKind.OrExpression, "Or" },
            { SyntaxKind.AndAlsoExpression, "AndAlso" },
            { SyntaxKind.OrElseExpression, "OrElse" },
        }.ToImmutableDictionary();
    }
}
