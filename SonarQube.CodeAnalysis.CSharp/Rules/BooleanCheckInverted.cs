using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CodeAnalysis.CSharp.Helpers;
using SonarQube.CodeAnalysis.CSharp.SonarQube.Settings;
using SonarQube.CodeAnalysis.CSharp.SonarQube.Settings.Sqale;

namespace SonarQube.CodeAnalysis.CSharp.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("pitfall")]
    public class BooleanCheckInverted : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1940";
        internal const string Description = "Boolean checks should not be inverted";
        internal const string MessageFormat = "Use the opposite operator (\"{0}\") instead.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1940");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var expression = (BinaryExpressionSyntax) c.Node;

                    var parenthesizedParent = expression.Parent;

                    while (parenthesizedParent is ParenthesizedExpressionSyntax)
                    {
                        parenthesizedParent = parenthesizedParent.Parent;
                    }

                    var logicalNot = parenthesizedParent as PrefixUnaryExpressionSyntax;
                    if (logicalNot != null && logicalNot.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, logicalNot.GetLocation(), 
                            OppositeTokens[expression.OperatorToken.Kind()]));
                    }
                },
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }

        private static readonly Dictionary<SyntaxKind, string> OppositeTokens =
            new Dictionary<SyntaxKind, string>
            {
                {SyntaxKind.GreaterThanToken, "<="},
                {SyntaxKind.GreaterThanEqualsToken, "<"},
                {SyntaxKind.LessThanToken, ">="},
                {SyntaxKind.LessThanEqualsToken, ">"},
                {SyntaxKind.EqualsEqualsToken, "!="},
                {SyntaxKind.ExclamationEqualsToken, "=="}
            };
    }
}
