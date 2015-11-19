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

using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Runner;
using SonarLint.Helpers;
using System.IO;

namespace SonarLint.UnitTest
{
    [TestClass]
    public class DiagnosticRunnerTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DiagnosticRunnerTest_NoAnalyzer()
        {
            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            File.Copy("TestResources\\ConfigurationTest.Empty.xml", tempInputFilePath, true);

            var runner = new DiagnosticsRunner(new Configuration(tempInputFilePath, Common.AnalyzerLanguage.CSharp));

            var solution = CompilationHelper.GetSolutionWithEmptyFile();

            var compilation = solution.Projects.First().GetCompilationAsync().Result;

            var diagnosticsResult = runner.GetDiagnostics(compilation);

            diagnosticsResult.Should().HaveCount(0);
        }
    }
}
