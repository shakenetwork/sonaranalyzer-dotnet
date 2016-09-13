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

using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Helpers;

namespace SonarLint.UnitTest.Helpers
{
    [TestClass]
    public class DiagnosticReportHelperTest
    {
        private const string Source =
@"namespace Test
{
    class TestClass
    {
    }
}";
        [TestMethod]
        public void GetLineNumberToReport()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(Source);
            var method = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            method.GetLineNumberToReport().ShouldBeEquivalentTo(3);
            method.GetLocation().GetLineSpan().StartLinePosition.GetLineNumberToReport().ShouldBeEquivalentTo(3);
        }
    }
}
