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

namespace SonarLint.Common.UnitTest
{
    [TestClass]
    public class RuleTest
    {
        private static IEnumerable<Type> GetDiagnosticAnalyzerTypes(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(DiagnosticAnalyzer)));
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
        public void VisualStudio_NoRuleTemplates()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { RuleFinder.GetPackagedRuleAssembly() });

            foreach (var analyzer in analyzers.Where(RuleFinder.IsRuleTemplate))
            {
                Assert.Fail("Visual Studio rules cannot be templates, remove DiagnosticAnalyzer '{0}'.", analyzer.Name);
            }
        }

        [TestMethod]
        public void VisualStudio_OnlyParameterlessRules()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { RuleFinder.GetPackagedRuleAssembly() });

            foreach (var analyzer in analyzers)
            {
                var hasParameter = analyzer.GetProperties().Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
                if (hasParameter)
                {
                    Assert.Fail("Visual Studio rules cannot have parameters, remove DiagnosticAnalyzer '{0}'.", analyzer.Name);
                }
            }
        }

        [TestMethod]
        public void VisualStudio_AllParameterlessRulesNotRuleTemplate()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { RuleFinder.GetExtraRuleAssembly() });

            foreach (var analyzer in analyzers.Where(type => !RuleFinder.IsRuleTemplate(type)))
            {
                var hasParameter = analyzer.GetProperties().Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
                if (!hasParameter)
                {
                    Assert.Fail(
                        "DiagnosticAnalyzer '{0}' should be moved to the assembly that implements Visual Studio rules.", 
                        analyzer.Name);
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
    }
}