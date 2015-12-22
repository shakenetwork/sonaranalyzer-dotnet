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

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarLint.Common;
using SonarQube.CSharp.CodeAnalysis.RulingTest.ErrorModels.Omstar;
using SonarLint.Runner;
using SonarLint.Utilities;
using Microsoft.CodeAnalysis;
using SonarLint.Helpers;

namespace SonarQube.CSharp.CodeAnalysis.RulingTest
{
    [TestClass]
    public class RulingTest : IntegrationTestBase
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestCategory("Integration")]
        public void All_Rules_Have_Integration_Test()
        {
            CheckIntegrationTestCount(AnalyzerLanguage.CSharp);
            CheckIntegrationTestCount(AnalyzerLanguage.VisualBasic);
        }

        private void CheckIntegrationTestCount(AnalyzerLanguage language)
        {
            ValidateLanguageInput(language);
            CreateMissingExpectedDirectory(language);

            Assert.AreEqual(
                RuleFinder.GetAnalyzerTypes(language).Count(),
                ExpectedDirectory
                    .GetDirectories(language.GetDirectoryName()).First()
                    .GetFiles("S*.json").Count());
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ITSources_Expected_Number_Of_Files()
        {
            CheckFileCount(AnalyzerLanguage.CSharp, 6569);
            CheckFileCount(AnalyzerLanguage.VisualBasic, 2654);
        }

        private void CheckFileCount(AnalyzerLanguage language, int expectedCount)
        {
            ValidateLanguageInput(language);

            var CSCodeFiles = GetCodeFiles(language);
            Assert.AreEqual(expectedCount, CSCodeFiles.Count());
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All_CSharp()
        {
            RunItSourcesTest(AnalyzerLanguage.CSharp);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All_VisualBasic()
        {
            RunItSourcesTest(AnalyzerLanguage.VisualBasic);
        }
        private void RunItSourcesTest(AnalyzerLanguage language)
        {
            ValidateLanguageInput(language);
            CreateMissingExpectedDirectory(language);
            CreateMissingActualDirectory(language);

            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            try
            {
                File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile(language));
                var outputPath = Path.Combine(AnalysisOutputDirectory.FullName, language.GetDirectoryName(), "all.xml");

                CallMainAndCheckResult(tempInputFilePath, outputPath, language);
            }
            finally
            {
                File.Delete(tempInputFilePath);
            }
        }

        private static void ValidateLanguageInput(AnalyzerLanguage language)
        {
            if (language != AnalyzerLanguage.CSharp &&
                language != AnalyzerLanguage.VisualBasic)
            {
                throw new System.ArgumentException("Supplied language is neither C# nor VB.Net", nameof(language));
            }
        }

        private void CreateMissingExpectedDirectory(AnalyzerLanguage language)
        {
            if (!ExpectedDirectory.GetDirectories(language.GetDirectoryName()).Any())
            {
                Directory.CreateDirectory(Path.Combine(
                    ExpectedDirectory.FullName,
                    language.GetDirectoryName()));
            }
        }

        private void CreateMissingActualDirectory(AnalyzerLanguage language)
        {
            if (!AnalysisOutputDirectory.GetDirectories(language.GetDirectoryName()).Any())
            {
                Directory.CreateDirectory(Path.Combine(
                    AnalysisOutputDirectory.FullName,
                    language.GetDirectoryName()));
            }
        }

        private void CallMainAndCheckResult(string tempInputFilePath, string outputPath,
            AnalyzerLanguage language)
        {
            var retValue = Program.Main(new[]
            {
                tempInputFilePath,
                outputPath,
                language.ToString()
            });

            if (retValue != 0)
            {
                Assert.Fail("Analysis failed with error");
            }

            RemoveExactFilePathNames(outputPath);

            var output = ParseAnalysisXmlOutput(outputPath);
            var omstar = GenerateOmstarOutput(output);
            AddMissingEntries(omstar, language);
            SplitAndStoreOmstarByIssueType(omstar, language);
            List<string> problematicRules;
            if (!FilesAreEquivalent(language, out problematicRules))
            {
                Assert.Fail("Expected and actual files are different, there are differences for rules: {0}",
                    string.Join(", ", problematicRules));
            }
        }

        private void AddMissingEntries(AnalysisOutput output, AnalyzerLanguage language)
        {
            var foundKeys = output.Issues.Select(i => i.RuleId).ToList();

            var notFoundKeys = RuleFinder.GetAnalyzerTypes(language)
                .Select(t =>
                    new
                    {
                        Type = t,
                        RuleAttribute = t.GetCustomAttribute<RuleAttribute>()
                    })
                .Select(rule => rule.RuleAttribute.Key)
                .Where(s => !foundKeys.Contains(s));

            foreach (var notFoundKey in notFoundKeys)
            {
                output.Issues.Add(new Issue
                {
                    RuleId = notFoundKey
                });
            }
        }

        private bool FilesAreEquivalent(AnalyzerLanguage language, out List<string> problematicRules)
        {
            problematicRules = new List<string>();

            var expectedFiles = ExpectedDirectory
                .GetDirectories(language.GetDirectoryName()).First()
                .GetFiles("S*.json");
            var actualFiles = AnalysisOutputDirectory
                .GetDirectories(language.GetDirectoryName()).First()
                .GetFiles("S*.json");

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

        private void SplitAndStoreOmstarByIssueType(AnalysisOutput omstar, AnalyzerLanguage language)
        {
            foreach (var issueGroup in omstar.Issues.GroupBy(issue => issue.RuleId))
            {
                var omstarForRule = new AnalysisOutput
                {
                    ToolInfo = omstar.ToolInfo,
                    Version = omstar.Version,
                    Issues = issueGroup
                        .OrderBy(issue =>
                        {
                            var location = issue.Locations.FirstOrDefault();
                            return location == null ? string.Empty : location.AnalysisTarget.Uri;
                        }, new InvariantCultureStringSortComparer())
                        .ThenBy(issue =>
                        {
                            var location = issue.Locations.FirstOrDefault();
                            return location == null ? 0 : location.AnalysisTarget.Region.StartLine;
                        })
                        .ThenBy(issue =>
                        {
                            var location = issue.Locations.FirstOrDefault();
                            return location == null ? 0 : location.AnalysisTarget.Region.EndLine;
                        })
                        .ThenBy(issue => issue.FullMessage, new InvariantCultureStringSortComparer())
                        .ToList()
                };

                var content = JsonConvert.SerializeObject(omstarForRule,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    });

                File.WriteAllText(Path.Combine(
                    AnalysisOutputDirectory.FullName,
                    language.GetDirectoryName(),
                    $"{issueGroup.Key}.json"), content);
            }
        }

        private static AnalysisOutput GenerateOmstarOutput(ErrorModels.Xml.AnalysisOutput xml)
        {
            var assemblyName = xml.GetType().Assembly.GetName();

            var omstar = new AnalysisOutput
            {
                Version = "0.1",
                ToolInfo = new ToolInfo
                {
                    FileVersion = "1.0.0",
                    ToolName = assemblyName.Name
                },
                Issues = xml.Files.SelectMany(f => f.Issues.Select(i => new Issue
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
                })).ToList()
            };


            return omstar;
        }

        private static ErrorModels.Xml.AnalysisOutput ParseAnalysisXmlOutput(string path)
        {
            var xml = XDocument.Load(new FileInfo(path).FullName);
            var s = new XmlSerializer(typeof(ErrorModels.Xml.AnalysisOutput));
            return (ErrorModels.Xml.AnalysisOutput)s.Deserialize(xml.CreateReader());
        }

        private void RemoveExactFilePathNames(string outputPath)
        {
            var fileContents = File.ReadAllText(outputPath);
            fileContents = fileContents.Replace(ItSourcesRootDirectory.FullName, string.Empty);
            File.WriteAllText(outputPath, fileContents);
        }

        private class InvariantCultureStringSortComparer : IComparer<string>
        {
            private readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;
            public int Compare(string x, string y)
            {
                return _compareInfo.Compare(x, y, CompareOptions.StringSort);
            }
        }
    }
}
