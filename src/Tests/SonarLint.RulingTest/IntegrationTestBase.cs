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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using SonarLint.Common;
using SonarLint.Utilities;
using Microsoft.CodeAnalysis;

namespace SonarQube.CSharp.CodeAnalysis.RulingTest
{
    public class IntegrationTestBase
    {
        protected IEnumerable<FileInfo> CodeFiles;
        protected RuleFinder RuleFinder;
        protected DirectoryInfo ItSourcesRootDirectory;
        protected string XmlInputPattern;
        protected DirectoryInfo ExpectedDirectory;
        protected DirectoryInfo AnalysisOutputDirectory;

        public const string TemplateRuleIdPattern = "{0}-1";

        protected IntegrationTestBase()
        {
            Setup();
        }

        private static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings
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

        private const string CSharpFileExtension = ".cs";
        private const string VisualBasicFileExtension = ".vb";
        private static readonly string[] SupportedExtensions = { CSharpFileExtension, VisualBasicFileExtension };
        public void Setup()
        {
            ItSourcesRootDirectory = GetItSourcesFolder();
            CodeFiles = ItSourcesRootDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(file =>
                SupportedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase));
            RuleFinder = new RuleFinder();
            XmlInputPattern = GenerateAnalysisInputFilePattern();
            ExpectedDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Expected"));
            CreateOrClearMissingActualDirectory();
        }

        private void CreateOrClearMissingActualDirectory()
        {
            AnalysisOutputDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Actual"));
            if (AnalysisOutputDirectory.Exists)
            {
                foreach (FileInfo file in AnalysisOutputDirectory.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in AnalysisOutputDirectory.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            AnalysisOutputDirectory.Create();
        }

        private static DirectoryInfo GetItSourcesFolder()
        {
            const string navigationToRoot = "../../../../../";
            const string navigationToItSources = "its/src";

            var testAssembly = new FileInfo(typeof(IntegrationTestBase).Assembly.Location);
            var solutionDirectory = new DirectoryInfo(Path.Combine(testAssembly.DirectoryName, navigationToRoot));
            return solutionDirectory.GetDirectories(navigationToItSources).Single();
        }

        private static string GenerateAnalysisInputFilePattern()
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = XmlWriter.Create(memoryStream, XmlWriterSettings))
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

                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Flush();

                return Encoding.UTF8.GetString(memoryStream.ToArray());
            }
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

                if (rule.Key == SonarLint.Rules.CSharp.CommentRegularExpression.TemplateDiagnosticId)
                {
                    writer.WriteStartElement("Parameters");
                    {
                        writer.WriteStartElement("Parameter");
                        writer.WriteStartElement("Key");
                        writer.WriteString("RuleKey");
                        writer.WriteEndElement();
                        writer.WriteStartElement("Value");
                        writer.WriteString(string.Format(TemplateRuleIdPattern, SonarLint.Rules.CSharp.CommentRegularExpression.TemplateDiagnosticId));
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
                }
                else
                {
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
                }

                writer.WriteEndElement();
            }

            return builder.ToString();
        }
        private string GenerateFilesSegment(AnalyzerLanguage language)
        {
            var files = GetCodeFiles(language);
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, FragmentSettings))
            {
                foreach (var codeFile in files)
                {
                    writer.WriteStartElement("File");
                    writer.WriteString(codeFile.FullName);
                    writer.WriteEndElement();
                }
            }

            return builder.ToString();
        }

        protected IEnumerable<FileInfo> GetCodeFiles(AnalyzerLanguage language)
        {
            var extension = language == AnalyzerLanguage.CSharp
                ? CSharpFileExtension
                : VisualBasicFileExtension;
            return CodeFiles
                .Where(file => file.Extension.ToLowerInvariant() == extension);
        }

        private string GenerateAnalysisInputFile(string rulesContent, string filesContent)
        {
            var xdoc = new XmlDocument();
            xdoc.LoadXml(XmlInputPattern);

            var files = xdoc.CreateElement("Files");
            files.InnerXml = filesContent;
            xdoc.DocumentElement.AppendChild(files);

            var rules = xdoc.CreateElement("Rules");
            rules.InnerXml = rulesContent;
            xdoc.DocumentElement.AppendChild(rules);
            return xdoc.OuterXml;
        }

        protected string GenerateAnalysisInputFile(AnalyzerLanguage language)
        {
            var analyzers = new StringBuilder();
            foreach (var analyzerType in RuleFinder.GetAnalyzerTypes(language))
            {
                analyzers.Append(GenerateAnalysisInputFileSegment(analyzerType));
            }
            return GenerateAnalysisInputFile(analyzers.ToString(), GenerateFilesSegment(language));
        }

        protected string GenerateEmptyAnalysisInputFile(AnalyzerLanguage language)
        {
            return GenerateAnalysisInputFile(string.Empty, GenerateFilesSegment(language));
        }

        protected string GenerateAnalysisInputFile(Type analyzerType, AnalyzerLanguage language)
        {
            if (!RuleFinder.GetTargetLanguages(analyzerType).IsAlso(language))
            {
                throw new ArgumentException("Supplied analyzer doesn't support target language", nameof(language));
            }

            return GenerateAnalysisInputFile(
                GenerateAnalysisInputFileSegment(analyzerType),
                GenerateFilesSegment(language));
        }
    }
}