/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarQube.CSharp.CodeAnalysis.Runner
{
    public class DiagnosticsRunner
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> diagnosticAnalyzers;

        public DiagnosticsRunner(ImmutableArray<DiagnosticAnalyzer> diagnosticAnalyzers)
        {
            foreach (var analyzer in diagnosticAnalyzers)
            {
                foreach (var diagnostic in analyzer.SupportedDiagnostics)
                {
                    diagnostic.GetType().GetProperty("IsEnabledByDefault").SetValue(diagnostic, true);
                }
            }

            this.diagnosticAnalyzers = diagnosticAnalyzers;
        }

        public IEnumerable<Diagnostic> GetDiagnostics(Compilation compilation)
        {
            if (diagnosticAnalyzers.IsDefaultOrEmpty)
            {
                return new Diagnostic[0];
            }

            using (var tokenSource = new CancellationTokenSource())
            {
                var compilationWithAnalyzer = new CompilationWithAnalyzers(compilation, diagnosticAnalyzers, null,
                    tokenSource.Token);

                return compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync().Result;
            }
        }
    }
}
