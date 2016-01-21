/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions;
using System.Net;

namespace SonarLint.UnitTest
{
    public static class Verifier
    {
        private static readonly MetadataReference systemAssembly = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference systemLinqAssembly = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference systemNetAssembly = MetadataReference.CreateFromFile(typeof(WebClient).Assembly.Location);
        private static readonly MetadataReference testAssembly = MetadataReference.CreateFromFile(typeof(TestMethodAttribute).Assembly.Location);

        private const string GeneratedAssemblyName = "foo";

        #region Verify*

        public static void VerifyAnalyzer(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, GeneratedAssemblyName, diagnosticAnalyzer);
        }

        public static void VerifyAnalyzerInTest(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, GeneratedAssemblyName, diagnosticAnalyzer, testAssembly);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider)
        {
            VerifyCodeFix(path, pathToExpected, pathToExpected, diagnosticAnalyzer, codeFixProvider, null);
        }
        public static void VerifyCodeFix(string path, string pathToExpected, string pathToBatchExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider)
        {
            VerifyCodeFix(path, pathToExpected, pathToBatchExpected, diagnosticAnalyzer, codeFixProvider, null);
        }
        public static void VerifyCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider, string codeFixTitle)
        {
            VerifyCodeFix(path, pathToExpected, pathToExpected, diagnosticAnalyzer, codeFixProvider, codeFixTitle);
        }
        public static void VerifyCodeFix(string path, string pathToExpected, string pathToBatchExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider, string codeFixTitle)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(path, GeneratedAssemblyName, workspace);

                List<Diagnostic> diagnostics;
                string actualCode;
                CalculateDiagnosticsAndCode(diagnosticAnalyzer, document, out diagnostics, out actualCode);

                Assert.AreNotEqual(0, diagnostics.Count);

                string codeBeforeFix;
                var codeFixExecutedAtLeastOnce = false;

                do
                {
                    codeBeforeFix = actualCode;

                    var codeFixExecuted = false;
                    for (int diagnosticIndexToFix = 0; !codeFixExecuted && diagnosticIndexToFix < diagnostics.Count; diagnosticIndexToFix++)
                    {
                        var codeActionsForDiagnostic = GetCodeActionsForDiagnostic(codeFixProvider, document, diagnostics[diagnosticIndexToFix]);

                        CodeAction codeActionToExecute;
                        if (TryGetCodeActionToApply(codeFixTitle, codeActionsForDiagnostic, out codeActionToExecute))
                        {
                            document = ApplyCodeFix(document, codeActionToExecute);
                            CalculateDiagnosticsAndCode(diagnosticAnalyzer, document, out diagnostics, out actualCode);

                            codeFixExecutedAtLeastOnce = true;
                            codeFixExecuted = true;
                        }
                    }
                } while (codeBeforeFix != actualCode);

                Assert.IsTrue(codeFixExecutedAtLeastOnce);
                Assert.AreEqual(File.ReadAllText(pathToExpected), actualCode);
            }

            VerifyFixAllCodeFix(path, pathToBatchExpected, diagnosticAnalyzer, codeFixProvider, codeFixTitle);
        }

        private static void VerifyFixAllCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider, string codeFixTitle)
        {
            var fixAllProvider = codeFixProvider.GetFixAllProvider();
            if (fixAllProvider == null)
            {
                return;
            }

            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(path, GeneratedAssemblyName, workspace);

                List<Diagnostic> diagnostics;
                string actualCode;
                CalculateDiagnosticsAndCode(diagnosticAnalyzer, document, out diagnostics, out actualCode);

                Assert.AreNotEqual(0, diagnostics.Count);

                var fixAllDiagnosticProvider = new FixAllDiagnosticProvider(
                    codeFixProvider.FixableDiagnosticIds.ToImmutableHashSet(),
                    (doc, ids, ct) => Task.FromResult(
                        GetDiagnostics(document.Project.GetCompilationAsync().Result, diagnosticAnalyzer)),
                    null);
                var fixAllContext = new FixAllContext(document, codeFixProvider, FixAllScope.Document,
                    codeFixTitle,
                    codeFixProvider.FixableDiagnosticIds,
                    fixAllDiagnosticProvider,
                    CancellationToken.None);
                var codeActionToExecute = fixAllProvider.GetFixAsync(fixAllContext).Result;

                Assert.IsNotNull(codeActionToExecute);

                document = ApplyCodeFix(document, codeActionToExecute);

                CalculateDiagnosticsAndCode(diagnosticAnalyzer, document, out diagnostics, out actualCode);
                Assert.AreEqual(File.ReadAllText(pathToExpected), actualCode);
            }
        }

        #endregion

        #region Generic helper

        private static Document GetDocument(string filePath, string assemblyName,
            AdhocWorkspace workspace, params MetadataReference[] additionalReferences)
        {
            var file = new FileInfo(filePath);
            var language = file.Extension == ".cs"
                ? LanguageNames.CSharp
                : LanguageNames.VisualBasic;

            return workspace.CurrentSolution.AddProject(assemblyName,
                    $"{assemblyName}.dll", language)
                .AddMetadataReference(systemAssembly)
                .AddMetadataReference(systemLinqAssembly)
                .AddMetadataReference(systemNetAssembly)
                .AddMetadataReferences(additionalReferences)
                .AddDocument(file.Name, File.ReadAllText(file.FullName, Encoding.UTF8));
        }

        #endregion

        #region Analyzer helpers

        internal static IEnumerable<Diagnostic> GetDiagnostics(Compilation compilation,
            DiagnosticAnalyzer diagnosticAnalyzer)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                var compilationOptions = compilation.Language == LanguageNames.CSharp
                    ? (CompilationOptions)new CS.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    : new VB.VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    diagnosticAnalyzer.SupportedDiagnostics
                        .Select(diagnostic =>
                            new KeyValuePair<string, ReportDiagnostic>(diagnostic.Id, ReportDiagnostic.Warn))
                        .Union(
                            new []
                            {
                                new KeyValuePair<string, ReportDiagnostic>("AD0001", ReportDiagnostic.Error)
                            }));

                var compilationWithOptions = compilation.WithOptions(compilationOptions);
                var compilationWithAnalyzer = compilationWithOptions
                    .WithAnalyzers(
                        ImmutableArray.Create(diagnosticAnalyzer),
                        cancellationToken: tokenSource.Token);

                var diagnostics = compilationWithAnalyzer.GetAllDiagnosticsAsync().Result;
                diagnostics.Where(d => d.Id == "AD0001").Should().BeEmpty();

                return diagnostics.Where(d => d.Id == diagnosticAnalyzer.SupportedDiagnostics.Single().Id);
            }
        }

        private static void Verify(string path, string assemblyName, DiagnosticAnalyzer diagnosticAnalyzer,
            params MetadataReference[] additionalReferences)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(path, assemblyName, workspace, additionalReferences);
                var compilation = document.Project.GetCompilationAsync().Result;
                var diagnostics = GetDiagnostics(compilation, diagnosticAnalyzer);
                var expected = ExpectedIssues(compilation.SyntaxTrees.First()).ToList();

                foreach (var diagnostic in diagnostics)
                {
                    var line = diagnostic.GetLineNumberToReport();
                    expected.Should().Contain(line);
                    expected.Remove(line);
                }

                expected.Should().BeEquivalentTo(Enumerable.Empty<int>());
            }
        }

        private static IEnumerable<int> ExpectedIssues(SyntaxTree syntaxTree)
        {
            return syntaxTree.GetText().Lines
                .Where(l => l.ToString().Contains(NONCOMPLIANT_START))
                .Select(l => GetNoncompliantLineNumber(l));
        }

        private const string NONCOMPLIANT_START = "Noncompliant";
        private const string NONCOMPLIANT_LINE_PATTERN = NONCOMPLIANT_START + @"@([\+|-]?)([0-9]+)";

        private static int GetNoncompliantLineNumber(TextLine line)
        {
            var text = line.ToString();
            var match = Regex.Match(text, NONCOMPLIANT_LINE_PATTERN);
            if (!match.Success)
            {
                return line.LineNumber + 1;
            }

            var sign = match.Groups[1];
            var lineValue = int.Parse(match.Groups[2].Value);
            if (sign.Value == "+")
            {
                return line.LineNumber + 1 + lineValue;
            }

            if (sign.Value == "-")
            {
                return line.LineNumber + 1 - lineValue;
            }

            return lineValue;
        }

        #endregion

        #region Codefix helper

        private static void CalculateDiagnosticsAndCode(DiagnosticAnalyzer diagnosticAnalyzer, Document document,
            out List<Diagnostic> diagnostics,
            out string actualCode)
        {
            diagnostics = GetDiagnostics(document.Project.GetCompilationAsync().Result, diagnosticAnalyzer).ToList();
            actualCode = document.GetSyntaxRootAsync().Result.GetText().ToString();
        }

        private static Document ApplyCodeFix(Document document, CodeAction codeAction)
        {
            var operations = codeAction.GetOperationsAsync(CancellationToken.None).Result;
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            return solution.GetDocument(document.Id);
        }

        private static bool TryGetCodeActionToApply(string codeFixTitle, IEnumerable<CodeAction> codeActions,
            out CodeAction codeAction)
        {
            codeAction = codeFixTitle != null
                ? codeActions.SingleOrDefault(action => action.Title == codeFixTitle)
                : codeActions.FirstOrDefault();

            return codeAction != null;
        }

        private static IEnumerable<CodeAction> GetCodeActionsForDiagnostic(CodeFixProvider codeFixProvider, Document document,
            Diagnostic diagnostic)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, diagnostic, (a, d) => actions.Add(a), CancellationToken.None);

            codeFixProvider.RegisterCodeFixesAsync(context).Wait();
            return actions;
        }

        #endregion
    }
}
