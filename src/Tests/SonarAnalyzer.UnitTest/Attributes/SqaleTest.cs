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

using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Utilities;

namespace SonarAnalyzer.UnitTest.Attributes
{
    [TestClass]
    public class SqaleTest
    {
        [TestMethod]
        public void SingleSqaleRemediationAttribute()
        {
            var analyzers = new RuleFinder().GetAllAnalyzerTypes();

            foreach (var analyzer in analyzers)
            {
                var count = analyzer.GetCustomAttributes<SqaleRemediationAttribute>().Count();
                if (count != 1)
                {
                    Assert.Fail("Only one SqaleRemediationAttribute can be assigned to DiagnosticAnalyzers, '{0}' has {1}",
                        analyzer.Name, count);
                }
            }
        }

        [TestMethod]
        public void SqaleSubCharacteristicAttribute()
        {
            var analyzers = new RuleFinder().GetAllAnalyzerTypes();

            foreach (var analyzer in analyzers)
            {
                var noSqaleCount = analyzer.GetCustomAttributes<NoSqaleRemediationAttribute>().Count();

                var subCharacteristicCount = analyzer.GetCustomAttributes<SqaleSubCharacteristicAttribute>().Count();

                if (noSqaleCount > 0)
                {
                    if (subCharacteristicCount > 0)
                    {
                        Assert.Fail(
                            "SqaleSubCharacteristicAttribute can only be assigned to DiagnosticAnalyzers that have a SQALE " +
                            "remediation function, '{0}' has NoSqaleRemediationAttribute",
                            analyzer.Name);
                    }
                }
                else
                {
                    if (subCharacteristicCount != 1)
                    {
                        Assert.Fail(
                            "Only one SqaleSubCharacteristicAttribute can be assigned to DiagnosticAnalyzers, '{0}' has {1}",
                            analyzer.Name, subCharacteristicCount);
                    }
                }
            }
        }

        [TestMethod]
        public void SqaleSubCharacteristic()
        {
            var stringsFromJava = new[]
            {
                "MODULARITY",
                "TRANSPORTABILITY",
                "COMPILER_RELATED_PORTABILITY",
                "HARDWARE_RELATED_PORTABILITY",
                "LANGUAGE_RELATED_PORTABILITY",
                "OS_RELATED_PORTABILITY",
                "SOFTWARE_RELATED_PORTABILITY",
                "TIME_ZONE_RELATED_PORTABILITY",
                "READABILITY",
                "UNDERSTANDABILITY",
                "API_ABUSE",
                "ERRORS",
                "INPUT_VALIDATION_AND_REPRESENTATION",
                "SECURITY_FEATURES",
                "CPU_EFFICIENCY",
                "MEMORY_EFFICIENCY",
                "NETWORK_USE",
                "ARCHITECTURE_CHANGEABILITY",
                "DATA_CHANGEABILITY",
                "LOGIC_CHANGEABILITY",
                "ARCHITECTURE_RELIABILITY",
                "DATA_RELIABILITY",
                "EXCEPTION_HANDLING",
                "FAULT_TOLERANCE",
                "INSTRUCTION_RELIABILITY",
                "LOGIC_RELIABILITY",
                "RESOURCE_RELIABILITY",
                "SYNCHRONIZATION_RELIABILITY",
                "UNIT_TESTS",
                "INTEGRATION_TESTABILITY",
                "UNIT_TESTABILITY",
                "USABILITY_ACCESSIBILITY",
                "USABILITY_COMPLIANCE",
                "USABILITY_EASE_OF_USE",
                "REUSABILITY_COMPLIANCE",
                "PORTABILITY_COMPLIANCE",
                "MAINTAINABILITY_COMPLIANCE",
                "SECURITY_COMPLIANCE",
                "EFFICIENCY_COMPLIANCE",
                "CHANGEABILITY_COMPLIANCE",
                "RELIABILITY_COMPLIANCE",
                "TESTABILITY_COMPLIANCE"
            };

            var enumValues = Enum.GetValues(typeof(SqaleSubCharacteristic)).Cast<SqaleSubCharacteristic>();
            var enumStrings = enumValues.Select(v => v.ToSonarQubeString()).ToList();

            var matchingEnumStrings = enumStrings.Intersect(stringsFromJava);

            enumStrings.Should().HaveCount(matchingEnumStrings.Count());
        }
    }
}
