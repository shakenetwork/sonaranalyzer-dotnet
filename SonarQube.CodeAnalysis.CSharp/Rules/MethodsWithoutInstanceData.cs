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
    [Tags("performance")]
    public class MethodsWithoutInstanceData : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2325";
        internal const string Description = "Methods that don't access instance data should be static";
        internal const string MessageFormat = "Make \"{0}\" a \"static\" method.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2325");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(
                cbc =>
                {
                    var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;

                    if (methodDeclaration == null)
                    {
                        return;
                    }

                    var reportShouldBeStatic = true;

                    var methodSymbol = cbc.SemanticModel.GetDeclaredSymbol(methodDeclaration);

                    if (methodSymbol == null ||
                        methodSymbol.IsStatic ||
                        methodSymbol.IsVirtual ||
                        methodSymbol.IsOverride)
                    {
                        reportShouldBeStatic = false;
                    }
                    
                    cbc.RegisterSyntaxNodeAction(c =>
                    {
                        var identifier = (IdentifierNameSyntax) c.Node;

                        var identifierSymbol = c.SemanticModel.GetSymbolInfo(identifier).Symbol;

                        if (identifierSymbol == null)
                        {
                            return;
                        }

                        if (PossibleMemberSymbolKinds.Contains(identifierSymbol.Kind) &&
                            !identifierSymbol.IsStatic)
                        {
                            reportShouldBeStatic = false;
                        }
                    },
                        SyntaxKind.IdentifierName);


                    cbc.RegisterCodeBlockEndAction(c =>
                    {
                        if (reportShouldBeStatic)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), 
                                methodDeclaration.Identifier.Text));
                        }
                    });
                });
        }

        private static readonly SymbolKind[] PossibleMemberSymbolKinds =
        {
            SymbolKind.Method,
            SymbolKind.Field,
            SymbolKind.Property,
            SymbolKind.Event
        };
    }
}
