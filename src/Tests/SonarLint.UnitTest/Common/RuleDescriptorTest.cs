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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Utilities;

namespace SonarLint.UnitTest.Common
{
    [TestClass]
    public class RuleDescriptorTest
    {
        [TestMethod]
        public void GetAllRuleDescriptors_Count()
        {
            Assert.AreEqual(RuleDetailBuilder.GetAllRuleDetails().Count(),
                (new RuleFinder().GetAllAnalyzerTypes().Count()));
        }
        [TestMethod]
        public void GetParameterlessRuleDescriptors_Count()
        {
            Assert.AreEqual(RuleDetailBuilder.GetParameterlessRuleDetails().Count(),
                (new RuleFinder().GetParameterlessAnalyzerTypes().Count()));
        }
        [TestMethod]
        public void RuleDescriptors_NotEmpty()
        {
            var ruleDetails = RuleDetailBuilder.GetAllRuleDetails().ToList();
            foreach (var ruleDetail in ruleDetails)
            {
                Assert.IsNotNull(ruleDetail);
                Assert.IsNotNull(ruleDetail.Description);
                Assert.IsNotNull(ruleDetail.Key);
                Assert.IsNotNull(ruleDetail.Title);

                if (!ruleDetail.IsTemplate)
                {
                    Assert.IsNotNull(ruleDetail.SqaleDescriptor);
                    Assert.IsNotNull(ruleDetail.SqaleDescriptor.Remediation);
                }
            }

            Assert.AreEqual(ruleDetails.Count(),
                ruleDetails.Select(descriptor => descriptor.Key).Distinct().Count());
        }
    }
}