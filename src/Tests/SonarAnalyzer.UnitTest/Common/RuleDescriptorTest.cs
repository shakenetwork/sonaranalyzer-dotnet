/*
 * SonarAnalyzer for .NET
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Utilities;
using SonarAnalyzer.Common;

namespace SonarAnalyzer.UnitTest.Common
{
    [TestClass]
    public class RuleDescriptorTest
    {
        [TestMethod]
        public void GetAllRuleDescriptors_Count()
        {
            CheckRuleDescriptorsCount(AnalyzerLanguage.CSharp);
            CheckRuleDescriptorsCount(AnalyzerLanguage.VisualBasic);
        }

        private static void CheckRuleDescriptorsCount(AnalyzerLanguage language)
        {
            Assert.AreEqual(
                RuleDetailBuilder.GetAllRuleDetails(language).Count(),
                new RuleFinder().GetAnalyzerTypes(language).Count());
        }

        [TestMethod]
        public void GetParameterlessRuleDescriptors_Count()
        {
            ParameterlessRuleDescriptorsCount(AnalyzerLanguage.CSharp);
            ParameterlessRuleDescriptorsCount(AnalyzerLanguage.VisualBasic);
        }

        private static void ParameterlessRuleDescriptorsCount(AnalyzerLanguage language)
        {
            Assert.AreEqual(
                RuleDetailBuilder.GetParameterlessRuleDetails(language).Count(),
                new RuleFinder().GetParameterlessAnalyzerTypes(language).Count());
        }

        [TestMethod]
        public void RuleDescriptors_NotEmpty()
        {
            CheckRuleDescriptorsNotEmpty(AnalyzerLanguage.CSharp);
            CheckRuleDescriptorsNotEmpty(AnalyzerLanguage.VisualBasic);
        }

        private static void CheckRuleDescriptorsNotEmpty(AnalyzerLanguage language)
        {
            var ruleDetails = RuleDetailBuilder.GetAllRuleDetails(language).ToList();
            foreach (var ruleDetail in ruleDetails)
            {
                Assert.IsNotNull(ruleDetail);
                Assert.IsNotNull(ruleDetail.Description);
                Assert.IsNotNull(ruleDetail.Key);
                Assert.IsNotNull(ruleDetail.Title);
            }

            Assert.AreEqual(ruleDetails.Count,
                ruleDetails.Select(descriptor => descriptor.Key).Distinct().Count());
        }
    }
}