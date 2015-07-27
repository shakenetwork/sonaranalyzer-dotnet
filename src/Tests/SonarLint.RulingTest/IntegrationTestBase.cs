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
using SonarLint.Rules;
using SonarLint.Utilities;

namespace SonarQube.CSharp.CodeAnalysis.RulingTest
{
    public class IntegrationTestBase
    {
        protected FileInfo[] CodeFiles;
        protected IList<Type> AnalyzerTypes;
        protected DirectoryInfo ItSourcesRootDirectory;
        protected string XmlInputPattern;
        protected DirectoryInfo ExpectedDirectory;
        protected DirectoryInfo AnalysisOutputDirectory;

        public const string TemplateRuleIdPattern = "{0}-1";

        protected IntegrationTestBase() { }

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
        
        public virtual void Setup()
        {
            ItSourcesRootDirectory = GetItSourcesFolder();
            CodeFiles = ItSourcesRootDirectory
                .GetFiles("*.cs", SearchOption.AllDirectories);
            AnalyzerTypes = new RuleFinder().GetAllAnalyzerTypes().ToList();
            XmlInputPattern = GenerateAnalysisInputFilePattern();
            ExpectedDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Expected"));

            AnalysisOutputDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Actual"));
            if (AnalysisOutputDirectory.Exists)
            {
                AnalysisOutputDirectory.Delete(true);
            }
            AnalysisOutputDirectory.Create();
        }

        private static DirectoryInfo GetItSourcesFolder()
        {
            const string navigationToRoot = "../../../../../";
            const string navigationToItSources = "its";

            var testAssembly = new FileInfo(typeof(IntegrationTestBase).Assembly.Location);
            var solutionDirectory = new DirectoryInfo(Path.Combine(testAssembly.DirectoryName, navigationToRoot));
            return solutionDirectory.GetDirectories(navigationToItSources).Single();
        }

        private string GenerateAnalysisInputFilePattern()
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

                writer.WriteStartElement("Files");

                foreach (var codeFile in CodeFiles)
                {
                    writer.WriteStartElement("File");
                    writer.WriteString(codeFile.FullName);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Flush();

                return Encoding.UTF8.GetString(memoryStream.ToArray());
            }
        }

        protected static string GenerateAnalysisInputFileSegment(Type analyzerType)
        {
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, FragmentSettings))
            {
                writer.WriteStartElement("Rule");
                writer.WriteStartElement("Key");
                var rule = analyzerType.GetCustomAttribute<RuleAttribute>();
                writer.WriteString(rule.Key);
                writer.WriteEndElement();

                if (rule.Key == CommentRegularExpression.DiagnosticId)
                {
                    writer.WriteStartElement("Parameters");
                    {
                        writer.WriteStartElement("Parameter");
                        writer.WriteStartElement("Key");
                        writer.WriteString("RuleKey");
                        writer.WriteEndElement();
                        writer.WriteStartElement("Value");
                        writer.WriteString(string.Format(TemplateRuleIdPattern, CommentRegularExpression.DiagnosticId));
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

        private string GenerateAnalysisInputFile(string rulesContent)
        {
            var xdoc = new XmlDocument();
            xdoc.LoadXml(XmlInputPattern);

            var rules = xdoc.CreateElement("Rules");
            rules.InnerXml = rulesContent;

            xdoc.DocumentElement.AppendChild(rules);
            return xdoc.OuterXml;
        }

        protected string GenerateAnalysisInputFile()
        {
            var sb = new StringBuilder();
            foreach (var analyzerType in AnalyzerTypes)
            {
                sb.Append(GenerateAnalysisInputFileSegment(analyzerType));
            }
            
            return GenerateAnalysisInputFile(sb.ToString());
        }
        protected string GenerateEmptyAnalysisInputFile()
        {
            return GenerateAnalysisInputFile(string.Empty);
        }

        protected string GenerateAnalysisInputFile(Type analyzerType)
        {
            return GenerateAnalysisInputFile(GenerateAnalysisInputFileSegment(analyzerType));
        }
    }
}