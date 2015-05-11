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
using System.Configuration;
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
    public class ItSources
    {
        private FileInfo[] codeFiles;
        private IList<Type> analyzerTypes;
        private string xmlInputPattern;
        private string pathToRoot;
        private DirectoryInfo outputDirectory;

        private static readonly XmlWriterSettings Settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  "
        };
        private static readonly XmlWriterSettings FragmentSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            ConformanceLevel = ConformanceLevel.Fragment,
            CloseOutput = false,
            OmitXmlDeclaration = true
        };

        private string environmentVarName;

        [TestInitialize]
        public void Setup()
        {
            environmentVarName = ConfigurationManager.AppSettings["env.var.it-sources"];

            pathToRoot = new DirectoryInfo(Environment.GetEnvironmentVariable(environmentVarName)).FullName;

            var rootItSources = new DirectoryInfo(
                Path.Combine(pathToRoot, ConfigurationManager.AppSettings["path.it-sources.input"]));

            codeFiles = rootItSources.GetFiles("*.cs", SearchOption.AllDirectories);

            analyzerTypes = new RuleFinder().GetAllAnalyzerTypes().ToList();

            xmlInputPattern = GenerateAnalysisInputFilePattern();

            outputDirectory = new DirectoryInfo("GeneratedOutput");
            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }
        }

        #region Analysis input file generation

        private string GenerateAnalysisInputFilePattern()
        {
            var memoryStream = new MemoryStream();
            using (var writer = XmlWriter.Create(memoryStream, Settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("AnalysisInput");

                //some mandatory settings
                writer.WriteStartElement("Settings");
                writer.WriteStartElement("Setting");
                writer.WriteStartElement("Key");
                writer.WriteString("sonar.cs.ignoreHeaderComments");
                writer.WriteEndElement();
                writer.WriteStartElement("Value");
                writer.WriteString("true");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                
                writer.WriteStartElement("Files");

                foreach (var codeFile in codeFiles)
                {
                    writer.WriteStartElement("File");
                    writer.WriteString(codeFile.FullName);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
        private static string GenerateAnalysisInputFileSegment(Type analyzerType)
        {
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, FragmentSettings))
            {
                writer.WriteStartElement("Rule");
                writer.WriteStartElement("Key");
                var rule = analyzerType.GetCustomAttribute<RuleAttribute>();
                writer.WriteString(rule.Key);
                writer.WriteEndElement();
                
                switch (rule.Key)
                {
                    case CommentRegularExpression.DiagnosticId:
                        writer.WriteStartElement("Parameters");
                        {
                            writer.WriteStartElement("Parameter");
                            writer.WriteStartElement("Key");
                            writer.WriteString("RuleKey");
                            writer.WriteEndElement();
                            writer.WriteStartElement("Value");
                            writer.WriteString("S124-1");
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                        {
                            writer.WriteStartElement("Parameter");
                            writer.WriteStartElement("Key");
                            writer.WriteString("message");
                            writer.WriteEndElement();
                            writer.WriteStartElement("Value");
                            writer.WriteString("Some message");
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                        {
                            writer.WriteStartElement("Parameter");
                            writer.WriteStartElement("Key");
                            writer.WriteString("regularExpression");
                            writer.WriteEndElement();
                            writer.WriteStartElement("Value");
                            writer.WriteString("(?i)TODO");
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        break;
                    default:
                        var parameters = analyzerType.GetProperties()
                    .Where(p => p.GetCustomAttributes<RuleParameterAttribute>().Any())
                    .ToList();

                        if (parameters.Any())
                        {
                            writer.WriteStartElement("Parameters");

                            foreach (var propertyInfo in parameters)
                            {
                                var ruleParameter = propertyInfo.GetCustomAttribute<RuleParameterAttribute>();

                                writer.WriteStartElement("Parameter");
                                writer.WriteStartElement("Key");
                                writer.WriteString(ruleParameter.Key);
                                writer.WriteEndElement();
                                writer.WriteStartElement("Value");
                                writer.WriteString(ruleParameter.DefaultValue);
                                writer.WriteEndElement();
                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                        }
                        break;
                }

                writer.WriteEndElement();
            }

            return builder.ToString();
        }
        private string GenerateAnalysisInputFile()
        {
            var xdoc = new XmlDocument();
            xdoc.LoadXml(xmlInputPattern);

            var rules = xdoc.CreateElement("Rules");
            
            var sb = new StringBuilder();
            foreach (var analyzerType in analyzerTypes)
            {
                sb.Append(GenerateAnalysisInputFileSegment(analyzerType));
            }
            rules.InnerXml = sb.ToString();

            xdoc.DocumentElement.AppendChild(rules);

            return xdoc.OuterXml;
        }

        #endregion
        
        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All()
        {
            var tempInputFilePath = Path.GetTempFileName();
            try
            {
                File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile());
                var outputPath = Path.Combine(outputDirectory.FullName, "all.xml");

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

            var expectedDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Expected"));
            var expectedFiles = expectedDirectory.GetFiles("*.json");
            var actualFiles = outputDirectory.GetFiles("*.json");

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
                    outputDirectory.FullName, string.Format("{0}.json", issueGroup.Key)), content);
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
            fileContents = fileContents.Replace(pathToRoot, environmentVarName);
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
