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
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Descriptor;
using SonarQube.CSharp.CodeAnalysis.Rules;
using SonarQube.CSharp.CodeAnalysis.Runner;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest
{
    [TestClass]
    public class ItSources
    {
        private FileInfo[] codeFiles;
        private IList<Type> analyzerTypes;
        private string xmlInputPattern;
        private string pathToRoot;
        private DirectoryInfo output;

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
        
        [TestInitialize]
        public void Setup()
        {
            pathToRoot = new DirectoryInfo(
                Environment.GetEnvironmentVariable(
                    ConfigurationManager.AppSettings["env.var.it-sources"])).FullName;

            var rootItSources = new DirectoryInfo(
                Path.Combine(pathToRoot, ConfigurationManager.AppSettings["path.it-sources.input"]));

            codeFiles = rootItSources.GetFiles("*.cs", SearchOption.AllDirectories);

            analyzerTypes = new RuleFinder().GetAllAnalyzerTypes().ToList();

            xmlInputPattern = GenerateAnalysisInputFilePattern();

            output = new DirectoryInfo("GeneratedOutput");
            if (!output.Exists)
            {
                output.Create();
            }
        }

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

        private string GenerateAnalysisInputFile(Type analyzerType)
        {
            var xdoc = new XmlDocument();
            xdoc.LoadXml(xmlInputPattern);

            var rules = xdoc.CreateElement("Rules");
            rules.InnerXml = GenerateAnalysisInputFileSegment(analyzerType);
            xdoc.DocumentElement.AppendChild(rules);

            return xdoc.OuterXml;
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

        [TestMethod]
        [TestCategory("Integration")]
        [Ignore]
        public void ItSources_Match_Expected_Single()
        {
            foreach (var analyzerType in analyzerTypes)
            {
                var tempInputFilePath = Path.GetTempFileName();
                try
                {
                    System.IO.File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile(analyzerType));
                    var outputFileName = string.Format("{0}.xml", analyzerType.GetCustomAttribute<RuleAttribute>().Key);
                    var outputPath = Path.Combine(output.FullName, outputFileName);

                    CallMainAndCheckResult(tempInputFilePath, outputPath, outputFileName);
                }
                finally
                {
                    System.IO.File.Delete(tempInputFilePath);
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ItSources_Match_Expected_All()
        {
            var tempInputFilePath = Path.GetTempFileName();
            try
            {
                System.IO.File.AppendAllText(tempInputFilePath, GenerateAnalysisInputFile());
                var outputPath = Path.Combine(output.FullName, "all.xml");
                const string outputFileName = "all.xml";

                CallMainAndCheckResult(tempInputFilePath, outputPath, outputFileName);
            }
            finally
            {
                System.IO.File.Delete(tempInputFilePath);
            }
        }

        private void CallMainAndCheckResult(string tempInputFilePath, string outputPath, string outputFileName)
        {
            var retValue = Program.Main(new[]
            {
                tempInputFilePath,
                outputPath
            });

            var fileContents = System.IO.File.ReadAllText(outputPath);
            fileContents = fileContents.Replace(pathToRoot, ConfigurationManager.AppSettings["env.var.it-sources"]);
            System.IO.File.WriteAllText(outputPath, fileContents);

            if (retValue != 0)
            {
                Assert.Fail("Analysis failed with error");
            }

            if (!XmlsAreEqual(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Expected", outputFileName), outputPath))
            {
                Assert.Fail("Expected and actual files are different");
            }
        }
        
        private static bool XmlsAreEqual(string pathExpected, string pathActual)
        {
            var first = XDocument.Load(new FileInfo(pathExpected).FullName);
            var second = XDocument.Load(new FileInfo(pathActual).FullName);

            var s = new XmlSerializer(typeof(AnalysisOutput));
            var output1 = (AnalysisOutput)s.Deserialize(first.CreateReader());
            var output2 = (AnalysisOutput)s.Deserialize(second.CreateReader());

            if (output1.Files.Count != output2.Files.Count)
            {
                Console.WriteLine("The expected number of 'File' entries doesn't match in file: '{0}'", pathActual);
                return false;
            }

            var files1 = output1.Files.OrderBy(file => file.Path).ToList();
            var files2 = output2.Files.OrderBy(file => file.Path).ToList();

            for (int i = 0; i < files1.Count; i++)
            {
                var file1 = files1[i];
                var file2 = files2[i];

                if (file1.Path != file2.Path ||
                    file1.Issues.Count != file2.Issues.Count)
                {
                    Console.WriteLine("The expected file paths or the issue count doesn't match in file: '{0}'", file2.Path);
                    return false;
                }

                var issues1 = file1.Issues.OrderBy(issue => issue.Id).ThenBy(issue => issue.Line).ToList();
                var issues2 = file2.Issues.OrderBy(issue => issue.Id).ThenBy(issue => issue.Line).ToList();

                for (int j = 0; j < issues1.Count; j++)
                {
                    var issue1 = issues1[j];
                    var issue2 = issues2[j];

                    if (issue1.Id != issue2.Id || issue1.Line != issue2.Line || issue1.Message != issue2.Message)
                    {
                        Console.WriteLine("The expected issues don't match in file: '{0}'", file1);
                        return false;
                    }
                }
            }

            return true;
        }

        #region XML model

        public class AnalysisOutput
        {
            public AnalysisOutput()
            {
                Files = new List<File>();
            }

            public List<File> Files { get; set; }
        }

        public class File
        {
            public File()
            {
                Issues = new List<Issue>();
            }

            public string Path { get; set; }
            public List<Issue> Issues { get; set; }
        }

        public class Issue
        {
            public string Id { get; set; }
            public int Line { get; set; }
            public string Message { get; set; }
        }

        #endregion
    }
}
