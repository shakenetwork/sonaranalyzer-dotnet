/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace SonarLint.UnitTest
{
    public static class Verifier
    {
        #region Verify*

        public static void VerifyAnalyzer(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, "foo", diagnosticAnalyzer);
        }

        public static void VerifyAnalyzerInTest(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, ProjectTypeHelper.TestAssemblyNamePattern, diagnosticAnalyzer);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider)
        {
            VerifyCodeFix(path, pathToExpected, diagnosticAnalyzer, codeFixProvider, null);
        }
        public static void VerifyCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider, string codeFixTitle)
        {
            var fileInput = new FileInfo(path);

            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(fileInput, "foo", workspace);

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
        }

        private static void CalculateDiagnosticsAndCode(DiagnosticAnalyzer diagnosticAnalyzer, Document document,
            out List<Diagnostic> diagnostics,
            out string actualCode)
        {
            diagnostics = GetDiagnostics(document.Project.GetCompilationAsync().Result, diagnosticAnalyzer).ToList();
            actualCode = document.GetSyntaxRootAsync().Result.GetText().ToString();
        }

        #endregion

        #region Generic helper

        private static Document GetDocument(FileInfo file, string assemblyName, AdhocWorkspace workspace)
        {
            var language = file.Extension == ".cs"
                ? LanguageNames.CSharp
                : LanguageNames.VisualBasic;

            return workspace.CurrentSolution.AddProject(assemblyName,
                    string.Format("{0}.dll", assemblyName), language)
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
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
                            new KeyValuePair<string, ReportDiagnostic>(diagnostic.Id, ReportDiagnostic.Warn)));

                var compilationWithOptions = compilation.WithOptions(compilationOptions);
                var compilationWithAnalyzer = compilationWithOptions
                    .WithAnalyzers(ImmutableArray.Create(diagnosticAnalyzer), null, tokenSource.Token);

                return compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync().Result
                    .Where(diag => diag.Id == diagnosticAnalyzer.SupportedDiagnostics.Single().Id);
            }
        }

        private static void Verify(string path, string assemblyName, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(new FileInfo(path), assemblyName, workspace);
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
            return from l in syntaxTree.GetText().Lines
                   where l.ToString().Contains("Noncompliant")
                   select l.LineNumber + 1;
        }

        #endregion

        #region Codefix helper

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
