/*
 * SonarQube C# Code Analysis
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
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarQube.CSharp.CodeAnalysis.Runner
{
    public class DiagnosticsRunner
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> diagnosticAnalyzers;

        public DiagnosticsRunner(ImmutableArray<DiagnosticAnalyzer> diagnosticAnalyzers)
        {
            this.diagnosticAnalyzers = diagnosticAnalyzers;
        }

        public IEnumerable<Diagnostic> GetDiagnostics(Compilation compilation)
        {
            if (diagnosticAnalyzers.IsDefaultOrEmpty)
            {
                return new Diagnostic[0];
            }
            
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                diagnosticAnalyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics)
                    .Select(diagnostic =>
                        new KeyValuePair<string, ReportDiagnostic>(diagnostic.Id, ReportDiagnostic.Warn)));

            var modifiedCompilation = compilation.WithOptions(compilationOptions);

            using (var tokenSource = new CancellationTokenSource())
            {
                var compilationWithAnalyzer = modifiedCompilation.WithAnalyzers(diagnosticAnalyzers, null,
                    tokenSource.Token);

                return compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync().Result;
            }
        }
    }
}
