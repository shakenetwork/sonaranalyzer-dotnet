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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Helpers;

namespace SonarLint.UnitTest
{
    public static class Verifier
    {
        public static void Verify(Project project, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            var compilation = project.GetCompilationAsync().Result;

            var compilationWithAnalyzer = GetDiagnostics(compilation, diagnosticAnalyzer);

            var expected = new List<int>(ExpectedIssues(compilation.SyntaxTrees.First()));
            foreach (var diagnostic in compilationWithAnalyzer

                .Where(diag => diag.Id == diagnosticAnalyzer.SupportedDiagnostics.Single().Id))
            {
                var line = diagnostic.GetLineNumberToReport();
                expected.Should().Contain(line);
                expected.Remove(line);
            }

            expected.Should().BeEquivalentTo(Enumerable.Empty<int>());
        }

        internal static IEnumerable<Diagnostic> GetDiagnostics(Compilation compilation,
            DiagnosticAnalyzer diagnosticAnalyzer)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    diagnosticAnalyzer.SupportedDiagnostics
                        .Select(diagnostic =>
                            new KeyValuePair<string, ReportDiagnostic>(diagnostic.Id, ReportDiagnostic.Warn)));

                var compilationWithOptions = compilation.WithOptions(compilationOptions);
                var compilationWithAnalyzer = compilationWithOptions
                    .WithAnalyzers(ImmutableArray.Create(diagnosticAnalyzer), null, tokenSource.Token);

                return compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync().Result;
            }
        }

        public static void Verify(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, "foo", diagnosticAnalyzer);
        }
        public static void VerifyInTest(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            Verify(path, ProjectTypeHelper.TestAssemblyNamePattern, diagnosticAnalyzer);
        }
        private static void Verify(string path, string assemblyName, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            var file = new FileInfo(path);

            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject(assemblyName,
                    string.Format("{0}.dll", assemblyName), LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument(file.Name, File.ReadAllText(file.FullName, Encoding.UTF8));

                Verify(document.Project, diagnosticAnalyzer);
            }
        }

        private static IEnumerable<int> ExpectedIssues(SyntaxTree syntaxTree)
        {
            return from l in syntaxTree.GetText().Lines
                   where l.ToString().Contains("Noncompliant")
                   select l.LineNumber + 1;
        }
    }
}
