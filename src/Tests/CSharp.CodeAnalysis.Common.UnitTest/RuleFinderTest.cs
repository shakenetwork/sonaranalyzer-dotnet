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

using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.CSharp.CodeAnalysis.Common.UnitTest
{
    [TestClass]
    public class RuleFinderTest
    {
        [TestMethod]
        public void GetPackagedRuleAssembly()
        {
            Assert.IsNotNull(RuleFinder.GetPackagedRuleAssembly());
        }

        [TestMethod]
        public void GetExtraRuleAssembly()
        {
            Assert.IsNotNull(RuleFinder.GetExtraRuleAssembly());
        }

        [TestMethod]
        public void GetParameterlessAnalyzerTypes()
        {
            new RuleFinder().GetParameterlessAnalyzerTypes().Count().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void GetAllAnalyzerTypes()
        {
            var finder = new RuleFinder();
            var countParameterless = finder.GetParameterlessAnalyzerTypes().Count();
            finder.GetAllAnalyzerTypes().Count().Should().BeGreaterThan(countParameterless);
        }
    }
}
