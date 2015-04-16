using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("pitfall")]
    public class AsyncAwaitIdentifier : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2306";
        internal const string Description = "\"async\" and \"await\" should not be used as identifiers";
        internal const string MessageFormat = "Rename \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AAsyncAwaitIdentifier");

        private static readonly IImmutableSet<string> AsyncOrAwait = ImmutableHashSet.Create("async", "await");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
                c => {
                    foreach (var asyncOrAwaitToken in GetAsyncOrAwaitTokens(c.Tree.GetRoot())
                        .Where(token => !token.Parent.AncestorsAndSelf().OfType<IdentifierNameSyntax>().Any()))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, asyncOrAwaitToken.GetLocation(), asyncOrAwaitToken.ToString()));
                    }
                });
        }

        private static IEnumerable<SyntaxToken> GetAsyncOrAwaitTokens(SyntaxNode node)
        {
            return from token in node.DescendantTokens()
                   where token.IsKind(SyntaxKind.IdentifierToken) &&
                   AsyncOrAwait.Contains(token.ToString())
                   select token;
        }
    }
}
