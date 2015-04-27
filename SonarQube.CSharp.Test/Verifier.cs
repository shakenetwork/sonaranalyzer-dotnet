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
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.Runner;

namespace SonarQube.CSharp.Test
{
    public class Verifier
    {
        public static void Verify(Solution solution, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            var runner = new DiagnosticsRunner(ImmutableArray.Create(diagnosticAnalyzer));
            
            var compilation = solution.Projects.First().GetCompilationAsync().Result;
            var syntaxTree = compilation.SyntaxTrees.First();
            
            var expected = new List<int>(ExpectedIssues(syntaxTree));
            foreach (var diagnostic in runner.GetDiagnostics(compilation))
            {
                if (diagnostic.Id != diagnosticAnalyzer.SupportedDiagnostics.Single().Id)
                {
                    continue;
                }

                var line = diagnostic.GetLineNumberToReport();
                expected.Should().Contain(line);
                expected.Remove(line);
            }

            expected.Should().BeEquivalentTo(Enumerable.Empty<int>());
        }

        public static void Verify(string path, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            var solution = CompilationHelper.GetSolutionFromFiles(path);
            Verify(solution, diagnosticAnalyzer);
        }

        private static IEnumerable<int> ExpectedIssues(SyntaxTree syntaxTree)
        {
            return from l in syntaxTree.GetText().Lines
                   where l.ToString().Contains("Noncompliant")
                   select l.LineNumber + 1;
        }
    }
}
