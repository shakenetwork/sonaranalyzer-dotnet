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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Omstar;
using SonarQube.CSharp.CodeAnalysis.Runner;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using AnalysisOutput = SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Xml.AnalysisOutput;
using Formatting = Newtonsoft.Json.Formatting;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest
{
    [TestClass]
    public class RulingTest : IntegrationTestBase
    {
        [TestInitialize]
        public override void Setup()
        {
            base.Setup();
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void All_Rules_Have_Integration_Test()
        {
            Assert.AreEqual(AnalyzerTypes.Count, ExpectedDirectory.GetFiles("S*.json").Count());
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All()
        {
            var tempInputFilePath = Path.GetTempFileName();
            try
            {
                File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile());
                var outputPath = Path.Combine(AnalysisOutputDirectory.FullName, "all.xml");

                CallMainAndCheckResult(tempInputFilePath, outputPath);
            }
            finally
            {
                File.Delete(tempInputFilePath);
            }
        }

        private void CallMainAndCheckResult(string tempInputFilePath, string outputPath)
        {
            var retValue = Program.Main(new[]
            {
                tempInputFilePath,
                outputPath
            });

            if (retValue != 0)
            {
                Assert.Fail("Analysis failed with error");
            }

            RemoveExactFilePathNames(outputPath);

            var output = ParseAnalysisXmlOutput(outputPath);
            var omstar = GenerateOmstarOutput(output);
            AddMissingEntries(omstar);
            SplitAndStoreOmstarByIssueType(omstar);
            List<string> problematicRules;
            if (!FilesAreEquivalent(out problematicRules))
            {
                Assert.Fail("Expected and actual files are different, there are differences for rules: {0}",
                    string.Join(", ", problematicRules));
            }
        }

        private void AddMissingEntries(ErrorModels.Omstar.AnalysisOutput output)
        {
            var foundKeys = output.Issues.Select(i => i.RuleId).ToList();

            var notFoundKeys = AnalyzerTypes
                .Select(t => t.GetCustomAttribute<RuleAttribute>())
                .Select(attribute => attribute.Template ? string.Format(TemplateRuleIdPattern, attribute.Key) : attribute.Key)
                .Where(s => !foundKeys.Contains(s));

            foreach (var notFoundKey in notFoundKeys)
            {
                output.Issues.Add(new Issue
                {
                    RuleId = notFoundKey
                });
            }
        }

        private bool FilesAreEquivalent(out List<string> problematicRules)
        {
            problematicRules = new List<string>();
            
            var expectedFiles = ExpectedDirectory.GetFiles("S*.json");
            var actualFiles = AnalysisOutputDirectory.GetFiles("S*.json");

            var problematicFileNames =
                    expectedFiles.Select(file => file.Name)
                        .ToImmutableHashSet()
                        .SymmetricExcept(actualFiles.Select(file => file.Name));

            if (problematicFileNames.Any())
            {
                problematicRules.AddRange(problematicFileNames);
                return false;
            }

            foreach (var expectedFile in expectedFiles)
            {
                var bytesExpected = File.ReadAllBytes(expectedFile.FullName);
                var bytesActual = File.ReadAllBytes(actualFiles.Single(file => file.Name == expectedFile.Name).FullName);

                if (!StructuralComparisons.StructuralEqualityComparer.Equals(bytesExpected, bytesActual))
                {
                    problematicRules.Add(Path.GetFileNameWithoutExtension(expectedFile.Name));
                }
            }

            return !problematicRules.Any();
        }

        private void SplitAndStoreOmstarByIssueType(ErrorModels.Omstar.AnalysisOutput omstar)
        {
            foreach (var issueGroup in omstar.Issues.GroupBy(issue => issue.RuleId))
            {
                var omstarForRule = new ErrorModels.Omstar.AnalysisOutput
                {
                    ToolInfo = omstar.ToolInfo,
                    Version = omstar.Version,
                    Issues = issueGroup
                        .OrderBy(issue =>
                        {
                            var location = issue.Locations.FirstOrDefault();
                            return location == null ? string.Empty : location.AnalysisTarget.Uri;
                        })
                        .ThenBy(issue =>
                        {
                            var location = issue.Locations.FirstOrDefault();
                            return location == null ? 0 : location.AnalysisTarget.Region.StartLine;
                        })
                        .ToList()
                };

                var content = JsonConvert.SerializeObject(omstarForRule,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    });

                File.WriteAllText(Path.Combine(
                    AnalysisOutputDirectory.FullName, string.Format("{0}.json", issueGroup.Key)), content);
            }
        }

        private static ErrorModels.Omstar.AnalysisOutput GenerateOmstarOutput(AnalysisOutput xml)
        {
            var assemblyName = xml.GetType().Assembly.GetName();

            var omstar = new ErrorModels.Omstar.AnalysisOutput
            {
                Version = "0.1",
                ToolInfo = new ToolInfo
                {
                    FileVersion = "1.0.0",
                    ToolName = assemblyName.Name
                }
            };

            omstar.Issues = xml.Files.SelectMany(f => f.Issues.Select(i => new Issue
            {
                FullMessage = i.Message,
                RuleId = i.Id,
                Properties = null,
                Locations = new List<IssueLocation>
                {
                    new IssueLocation
                    {
                        AnalysisTarget = new AnalysisTarget
                        {
                            Region = new Region
                            {
                                StartLine = i.Line,
                                EndLine = i.Line,
                                StartColumn = 0,
                                EndColumn = int.MaxValue
                            },
                            Uri = f.Path
                        }
                    }
                }
            })).ToList();

            return omstar;
        }

        private static AnalysisOutput ParseAnalysisXmlOutput(string path)
        {
            var xml = XDocument.Load(new FileInfo(path).FullName);
            var s = new XmlSerializer(typeof(AnalysisOutput));
            return (AnalysisOutput)s.Deserialize(xml.CreateReader());
        }

        private void RemoveExactFilePathNames(string outputPath)
        {
            var fileContents = File.ReadAllText(outputPath);
            fileContents = fileContents.Replace(ItSourcesRootDirectory.FullName, ItSourcesEnvVarName);
            File.WriteAllText(outputPath, fileContents);
        }
    }
}
