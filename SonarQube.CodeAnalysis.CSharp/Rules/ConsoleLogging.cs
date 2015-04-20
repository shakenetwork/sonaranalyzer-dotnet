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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SecurityFeatures)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("security", "owasp-a6")]
    public class ConsoleLogging : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2228";
        internal const string Description = "Console logging should not be used";
        internal const string MessageFormat = "Remove this logging statement.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2228");

        private static readonly string[] BannedConsoleMembers = { "WriteLine", "Write" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var methodCall = (InvocationExpressionSyntax) c.Node;
                    
                    var methodSymbol = c.SemanticModel.GetSymbolInfo(methodCall.Expression).Symbol;

                    if (methodSymbol != null &&
                        methodSymbol.ContainingType.ToString() == "System.Console" &&
                        BannedConsoleMembers.Contains(methodSymbol.Name))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}
