using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SonarQube.CodeAnalysis.CSharp.Helpers;
using SonarQube.CodeAnalysis.CSharp.SonarQube.Settings;
using SonarQube.CodeAnalysis.CSharp.SonarQube.Settings.Sqale;

namespace SonarQube.CodeAnalysis.CSharp.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class ReversedOperators : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2757";
        internal const string Description = "Reversed operators should not be used";
        internal const string MessageFormat = "Was \"{0}\" meant instead?";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2757");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var unaryExpression = (PrefixUnaryExpressionSyntax) c.Node;

                    var op = unaryExpression.OperatorToken;
                    var prevToken = op.GetPreviousToken();

                    var opLocation = op.GetLocation();
                    var opStartPosition = opLocation.GetLineSpan().StartLinePosition;
                    var prevStartPosition = prevToken.GetLocation().GetLineSpan().StartLinePosition;

                    if (prevToken.IsKind(SyntaxKind.EqualsToken) &&
                        prevStartPosition.Line == opStartPosition.Line &&
                        prevStartPosition.Character == opStartPosition.Character - 1)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, opLocation, string.Format("{0}=", op.Text)));
                    }
                },
                SyntaxKind.UnaryMinusExpression,
                SyntaxKind.UnaryPlusExpression);
        }
    }
}
