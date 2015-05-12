using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using SonarQube.CSharp.CodeAnalysis.Rules;
using SonarQube.CSharp.CodeAnalysis.Runner;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest
{
    public abstract class IntegrationTestBase
    {
        protected FileInfo[] CodeFiles;
        protected IList<Type> AnalyzerTypes;
        protected DirectoryInfo ItSourcesRootDirectory;
        protected string ItSourcesEnvVarName;
        protected string XmlInputPattern;
        protected DirectoryInfo ExpectedDirectory;

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
            ItSourcesEnvVarName = ConfigurationManager.AppSettings["env.var.it-sources"];
            ItSourcesRootDirectory = new DirectoryInfo(Environment.GetEnvironmentVariable(ItSourcesEnvVarName));
            CodeFiles = new DirectoryInfo(Path.Combine(ItSourcesRootDirectory.FullName,
                ConfigurationManager.AppSettings["path.it-sources.input"]))
                .GetFiles("*.cs", SearchOption.AllDirectories);
            AnalyzerTypes = new RuleFinder().GetAllAnalyzerTypes().ToList();
            XmlInputPattern = GenerateAnalysisInputFilePattern();
            ExpectedDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Expected"));
        }

        private string GenerateAnalysisInputFilePattern()
        {
            var memoryStream = new MemoryStream();
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
            }

            return Encoding.UTF8.GetString(memoryStream.ToArray());
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