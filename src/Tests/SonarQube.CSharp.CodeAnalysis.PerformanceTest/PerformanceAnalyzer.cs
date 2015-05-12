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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarQube.CSharp.CodeAnalysis.IntegrationTest;
using SonarQube.CSharp.CodeAnalysis.PerformanceTest.Expected;
using SonarQube.CSharp.CodeAnalysis.Runner;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;

namespace SonarQube.CSharp.CodeAnalysis.PerformanceTest
{
    [TestClass]
    public class PerformanceAnalyzer : IntegrationTestBase
    {
        private Performance expectedPerformance;
        private const int NumberOfRoundsToAverage = 5;
        private double actualBaseline;

        [TestInitialize]
        public override void Setup()
        {
            base.Setup();
            ParseExpected();
            CalculateActualBaseline();
        }

        private void ParseExpected()
        {
            var expectedFile = ExpectedDirectory.GetFiles("performance.json").Single();
            expectedPerformance = JsonConvert.DeserializeObject<Performance>(File.ReadAllText(expectedFile.FullName));
        }

        private void CalculateActualBaseline()
        {
            actualBaseline = CalculateAverage(GenerateEmptyAnalysisInputFile());
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Performance_Meets_Expected()
        {
            var errors = new List<string>();
            foreach (var analyzerType in AnalyzerTypes)
            {
                var ruleId = analyzerType.GetCustomAttribute<RuleAttribute>().Key;

                var average = CalculateAverage(GenerateAnalysisInputFile(analyzerType));

                var rulePerformance = expectedPerformance.Rules.Single(rulePerf => rulePerf.RuleId == ruleId).Performance;
                var diff = average / actualBaseline;

                if (expectedPerformance.BaseLine * rulePerformance > diff * expectedPerformance.Threshold.Upper)
                {
                    errors.Add(string.Format("Rule {0} is slower ({1}) than expected {2}", ruleId, diff, rulePerformance));
                }

                if (expectedPerformance.BaseLine * rulePerformance < diff * expectedPerformance.Threshold.Lower)
                {
                    errors.Add(string.Format("Rule {0} is faster ({1}) than expected {2}", ruleId, diff, rulePerformance));
                }
            }

            if (errors.Any())
            {
                Assert.Fail("{0} errors:{1}{2}", errors.Count, Environment.NewLine, string.Join(Environment.NewLine, errors));
            }
        }

        private static double CalculateAverage(string inputFileContent)
        {
            var tempInputFilePath = Path.GetTempFileName();
            var tempOutputFilePath = Path.GetTempFileName();
            try
            {
                File.AppendAllText(tempInputFilePath, inputFileContent);

                return TimeAverage(() =>
                {
                    var retValue = Program.Main(new[]
                    {
                        tempInputFilePath,
                        tempOutputFilePath
                    });

                    if (retValue != 0)
                    {
                        Assert.Fail("Analysis failed with error");
                    }
                });
            }
            finally
            {
                File.Delete(tempInputFilePath);
                File.Delete(tempOutputFilePath);
            }
        }

        private static long Time(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
        private static double TimeAverage(Action action)
        {
            return Enumerable.Range(0, NumberOfRoundsToAverage).Select(i => Time(action)).Average();
        }
        
        [TestMethod]
        [TestCategory("Integration")]
        public void All_Rules_Have_Performance_Test()
        {
            Assert.AreEqual(AnalyzerTypes.Count, expectedPerformance.Rules.Count);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Performance_Generate_Expected()
        {
            var performanceActual = new Performance
            {
                BaseLine = 1.0,
                Threshold = new Threshold {Lower = 0.95, Upper = 1.05},
                Rules = new List<RulePerformance>()
            };

            var baseLineForCalculation = actualBaseline*performanceActual.BaseLine;

            foreach (var analyzerType in AnalyzerTypes)
            {
                var ruleId = analyzerType.GetCustomAttribute<RuleAttribute>().Key;
                var average = CalculateAverage(GenerateAnalysisInputFile(analyzerType));
                performanceActual.Rules.Add(new RulePerformance {Performance = Math.Round(average/baseLineForCalculation,2), RuleId = ruleId});
            }

            var outputDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedOutput"));
            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }

            var content = JsonConvert.SerializeObject(performanceActual, Formatting.Indented);
            File.WriteAllText(Path.Combine(outputDirectory.FullName, "performance.json"), content);
        }
    }
}
