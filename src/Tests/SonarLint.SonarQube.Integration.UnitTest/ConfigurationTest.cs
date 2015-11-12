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

using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Rules;
using SonarLint.Runner;

namespace SonarLint.UnitTest
{
    [TestClass]
    public class ConfigurationTest
    {
        [TestMethod]
        public void Configuration()
        {
            var conf = new Configuration(XDocument.Load("TestResources\\ConfigurationTest.xml"), Common.AnalyzerLanguage.CSharp);
            conf.IgnoreHeaderComments.Should().BeTrue();
            conf.Files.Should().BeEquivalentTo("TestResources\\TestInput.cs");

            conf.AnalyzerIds.Should().BeEquivalentTo(
                "S1121",
                "S2306",
                "S1227",

                "S104",
                "S1541",
                "S103",
                "S1479",
                "S1067",
                "S107",
                "S101",
                "S100",
                "S124");

            var analyzers = conf.GetAnalyzers();
            analyzers.OfType<FileLines>().Single().Maximum.ShouldBeEquivalentTo(1000);
            analyzers.OfType<LineLength>().Single().Maximum.ShouldBeEquivalentTo(200);
            analyzers.OfType<TooManyLabelsInSwitch>().Single().Maximum.ShouldBeEquivalentTo(30);
            analyzers.OfType<TooManyParameters>().Single().Maximum.ShouldBeEquivalentTo(7);
            analyzers.OfType<ExpressionComplexity>().Single().Maximum.ShouldBeEquivalentTo(3);
            analyzers.OfType<FunctionComplexity>().Single().Maximum.ShouldBeEquivalentTo(10);
            analyzers.OfType<ClassName>().Single().Convention.ShouldBeEquivalentTo("^(?:[A-HJ-Z][a-zA-Z0-9]+|I[a-z0-9][a-zA-Z0-9]*)$");
            analyzers.OfType<MethodName>().Single().Convention.ShouldBeEquivalentTo("^[A-Z][a-zA-Z0-9]+$");

            var commentAnalyzer = analyzers.OfType<CommentRegularExpression>().Single();
            var ruleInstances = commentAnalyzer.RuleInstances.ToList();
            ruleInstances.Should().HaveCount(2);
            ruleInstances[0].Descriptor.Id.ShouldBeEquivalentTo("TODO");
            ruleInstances[0].Descriptor.MessageFormat.ToString().ShouldBeEquivalentTo("Fix this TODO");
            ruleInstances[0].RegularExpression.ShouldBeEquivalentTo(".*TODO.*");
            ruleInstances[1].Descriptor.Id.ShouldBeEquivalentTo("FIXME");
            ruleInstances[1].Descriptor.MessageFormat.ToString().ShouldBeEquivalentTo("Fix this FIXME");
            ruleInstances[1].RegularExpression.ShouldBeEquivalentTo(".*FIXME.*");
        }
    }
}
