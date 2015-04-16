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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("misra", "pitfall")]
    public class ParameterAssignedTo : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1226";
        internal const string Description = "Method parameters and caught exceptions should not be reassigned";
        internal const string MessageFormat = "Introduce a new variable instead of reusing the parameter \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AParameterAssignedTo");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var assignmentNode = (AssignmentExpressionSyntax)c.Node;
                    var symbol = c.SemanticModel.GetSymbolInfo(assignmentNode.Left).Symbol;

                    if (symbol != null && (AssignsToParameter(symbol) || AssignsToCatchVariable(symbol)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, assignmentNode.Left.GetLocation(), assignmentNode.Left.ToString()));
                    }
                },
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.AddAssignmentExpression,
                SyntaxKind.SubtractAssignmentExpression,
                SyntaxKind.MultiplyAssignmentExpression,
                SyntaxKind.DivideAssignmentExpression,
                SyntaxKind.ModuloAssignmentExpression,
                SyntaxKind.AndAssignmentExpression,
                SyntaxKind.ExclusiveOrAssignmentExpression,
                SyntaxKind.OrAssignmentExpression,
                SyntaxKind.LeftShiftAssignmentExpression,
                SyntaxKind.RightShiftAssignmentExpression);
        }

        private static bool AssignsToParameter(ISymbol symbol)
        {
            var parameterSymbol = symbol as IParameterSymbol;
            
            if (parameterSymbol == null)
            {
                return false;
            }
            
            return parameterSymbol.RefKind == RefKind.None;
        }
        private static bool AssignsToCatchVariable(ISymbol symbol)
        {
            var localSymbol = symbol as ILocalSymbol;

            if (localSymbol == null)
            {
                return false;
            }

            return localSymbol.DeclaringSyntaxReferences
                .Select(declaringSyntaxReference => declaringSyntaxReference.GetSyntax())
                .Any(syntaxNode =>
                    syntaxNode.Parent is CatchClauseSyntax &&
                    ((CatchClauseSyntax) syntaxNode.Parent).Declaration == syntaxNode);
        }
    }
}
