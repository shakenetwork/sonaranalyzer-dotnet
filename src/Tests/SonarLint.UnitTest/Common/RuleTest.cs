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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Utilities;
using SonarLint.Common;
using Microsoft.CodeAnalysis.CodeFixes;

namespace SonarLint.UnitTest.Common
{
    [TestClass]
    public class RuleTest
    {

        private static IEnumerable<Type> GetCodeFixProviderTypes(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(CodeFixProvider)));
        }


        [TestMethod]
        public void DiagnosticAnalyzerHasRuleAttribute()
        {
            var analyzers = new RuleFinder().GetAllAnalyzerTypes();

            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().SingleOrDefault();
                if (ruleDescriptor == null)
                {
                    Assert.Fail("RuleAttribute is missing from DiagnosticAnalyzer '{0}'", analyzer.Name);
                }
            }
        }

        [TestMethod]
        public void AbstractDiagnosticAnalyzer_Should_Have_No_RuleAttribute()
        {
            var analyzers = RuleFinder.GetPackagedRuleAssembly().GetTypes()
                .Where(t =>
                    t.IsSubclassOf(typeof(DiagnosticAnalyzer)) &&
                    t.IsAbstract)
                .ToList();

            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().SingleOrDefault();
                if (ruleDescriptor != null)
                {
                    Assert.Fail("RuleAttribute is added to abstract DiagnosticAnalyzer '{0}'", analyzer.Name);
                }
            }
        }

        [TestMethod]
        public void TemplateRule_With_Direct_Parameters()
        {
            var analyzers = new RuleFinder().GetAllAnalyzerTypes();

            foreach (var analyzer in analyzers.Where(RuleFinder.IsRuleTemplate))
            {
                var hasParameter = analyzer.GetProperties().Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
                if (hasParameter)
                {
                    Assert.Fail(
                        "DiagnosticAnalyzer '{0}' has parameters that are defined outside of IRuleTemplateInstance.",
                        analyzer.Name);
                }
            }
        }

        [TestMethod]
        public void CodeFixProviders_Named_Properly()
        {
            var codeFixProviders = GetCodeFixProviderTypes(new[] { RuleFinder.GetPackagedRuleAssembly() });

            foreach (var codeFixProvider in codeFixProviders)
            {
                var analyzerName = codeFixProvider.FullName.Replace(RuleDetailBuilder.CodeFixProviderSuffix, "");
                if (codeFixProvider.Assembly.GetType(analyzerName) == null)
                {
                    Assert.Fail(
                        "CodeFixProvider '{0}' has no matching DiagnosticAnalyzer.",
                        codeFixProvider.Name);
                }
            }
        }

        [TestMethod]
        public void CodeFixProviders_Have_Title()
        {
            var codeFixProviders = GetCodeFixProviderTypes(new[] { RuleFinder.GetPackagedRuleAssembly() });

            foreach (var codeFixProvider in codeFixProviders)
            {
                var titles = RuleDetailBuilder.GetCodeFixTitles(codeFixProvider);
                if (!titles.Any())
                {
                    Assert.Fail(
                        "CodeFixProvider '{0}' has no title field.",
                        codeFixProvider.Name);
                }
            }
        }
    }
}