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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Omstar;
using SonarQube.CSharp.CodeAnalysis.Rules;
using SonarQube.CSharp.CodeAnalysis.Runner;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using AnalysisOutput = SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Xml.AnalysisOutput;
using Formatting = Newtonsoft.Json.Formatting;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest
{
    [TestClass]
    public class ItSourcesAnalyzer : IntegrationTestBase
    {
        private DirectoryInfo analysisOutputDirectory;

        [TestInitialize]
        public override void Setup()
        {
            base.Setup();

            analysisOutputDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedOutput"));
            if (!analysisOutputDirectory.Exists)
            {
                analysisOutputDirectory.Create();
            }
        }
        
        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All()
        {
            var tempInputFilePath = Path.GetTempFileName();
            try
            {
                File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile());
                var outputPath = Path.Combine(analysisOutputDirectory.FullName, "all.xml");

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
            SplitAndStoreOmstarByIssueType(omstar);
            List<string> problematicRules;
            if (!FilesAreEquivalent(out problematicRules))
            {
                Assert.Fail("Expected and actual files are different, there are differences for rules: {0}",
                    string.Join(", ", problematicRules));
            }
        }

        private bool FilesAreEquivalent(out List<string> problematicRules)
        {
            problematicRules = new List<string>();
            
            var expectedFiles = ExpectedDirectory.GetFiles("*.json");
            var actualFiles = analysisOutputDirectory.GetFiles("*.json");

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
                if (!FilesAreEqual(expectedFile, actualFiles.Single(file => file.Name == expectedFile.Name)))
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
                        .OrderBy(issue => issue.Locations.First().AnalysisTarget.Uri)
                        .ThenBy(issue => issue.Locations.First().AnalysisTarget.Region.StartLine)
                        .ToList()
                };

                var content = JsonConvert.SerializeObject(omstarForRule,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    });

                File.WriteAllText(Path.Combine(
                    analysisOutputDirectory.FullName, string.Format("{0}.json", issueGroup.Key)), content);
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
                    ProductVersion = assemblyName.Version.ToString(),
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
                    new IssueLocation()
                    {
                        AnalysisTarget = new AnalysisTarget
                        {
                            Region = new Region
                            {
                                StartLine = i.Line,
                                EndLine = i.Line,
                                StartColumn = 0,
                                EndColumn = int.MaxValue,
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

        #region file compare

        const int BYTES_TO_READ = sizeof(Int64);

        static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion
    }
}
