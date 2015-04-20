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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class ValuesUselesslyIncremented : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2123";
        internal const string Description = "Values should not be uselessly incremented";
        internal const string MessageFormat = "Remove this {0} or correct the code not to waste it.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2123");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var increment = (PostfixUnaryExpressionSyntax)c.Node;

                    var operatorText = increment.OperatorToken.IsKind(SyntaxKind.PlusPlusToken)
                        ? "increment"
                        : "decrement";

                    if (increment.Parent is ReturnStatementSyntax)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                        return;
                    }

                    var assignment = increment.Parent as AssignmentExpressionSyntax;
                    if (assignment != null &&
                        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                        assignment.Right == increment &&
                        EquivalenceChecker.AreEquivalent(assignment.Left, increment.Operand))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                    }
                },
                SyntaxKind.PostIncrementExpression, SyntaxKind.PostDecrementExpression);
        }
    }
}
