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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Common;
using SonarLint.Runner;
using System.IO;
using System.Linq;

namespace SonarLint.UnitTest
{
    [TestClass]
    public class ProgramTest
    {
        [TestMethod]
        public void End_To_End()
        {
            Program.Main(new [] { "TestResources\\ConfigurationTest.xml", "Output.xml", AnalyzerLanguage.CSharp.ToString()});

            var textActual = new string(File.ReadAllText("Output.xml")
                .ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());

            CheckExpected(textActual);
            CheckNotExpected(textActual);
        }

        private static void CheckExpected(string textActual)
        {
            var expectedContent = new[]
            {
                @"<AnalysisOutput><Files><File><Path>TestResources\TestInput.cs</Path>",
                @"<Metrics><Lines>16</Lines>",
                @"<Issue><Id>FIXME</Id><Line>3</Line>",
                @"<Issue><Id>TODO</Id><Line>5</Line>"
            };

            foreach (var expected in expectedContent)
            {
                if (!textActual.Contains(expected))
                {
                    Assert.Fail("Generated output file doesn't contain expected string '{0}'", expected);
                }
            }
        }
        private static void CheckNotExpected(string textActual)
        {
            var notExpectedContent = new[]
            {
                @"<Id>S1116</Id><Line>14</Line>"
            };

            foreach (var notExpected in notExpectedContent)
            {
                if (textActual.Contains(notExpected))
                {
                    Assert.Fail("Generated output file contains not expected string '{0}'", notExpected);
                }
            }
        }
    }
}
