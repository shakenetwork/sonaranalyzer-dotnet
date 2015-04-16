using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class SequentialSameContition : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2760";
        internal const string Description = @"Sequential tests should not check the same condition";
        internal const string MessageFormat = @"This condition was just checked on line {0}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2760");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    CheckMatchingExpressionsInSucceedingStatements((IfStatementSyntax)c.Node, syntax => syntax.Condition, c);
                },
                SyntaxKind.IfStatement);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    CheckMatchingExpressionsInSucceedingStatements((SwitchStatementSyntax)c.Node, syntax => syntax.Expression, c);
                },
                SyntaxKind.SwitchStatement);
        }

        private static void CheckMatchingExpressionsInSucceedingStatements<T>(T statement, Func<T, ExpressionSyntax> expression, 
            SyntaxNodeAnalysisContext c) where T : StatementSyntax
        {
            var previousStatement = statement.GetPrecedingStatement() as T;

            if (previousStatement == null)
            {
                return;
            }

            CheckMatchingTests(expression(statement), expression(previousStatement), c);
        }

        private static void CheckMatchingTests(ExpressionSyntax current, ExpressionSyntax preceding,
            SyntaxNodeAnalysisContext c)
        {
            if (EquivalenceChecker.AreEquivalent(current, preceding))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, current.GetLocation(),
                    preceding.GetLineNumberToReport()));
            }
        }
    }
}
