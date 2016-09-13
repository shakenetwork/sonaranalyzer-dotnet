/*
 * SonarLint for Visual Studio
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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using SonarLint.Helpers;
using SonarLint.Common;
using System.IO;
using Google.Protobuf;

namespace SonarLint.Runner
{
    public static class Program
    {
        internal const string AnalysisOutputFileName = "analysis-output.xml";
        internal const string TokenInfosFileName = "token-infos.dat";
        internal const string TokenReferenceInfosFileName = "token-reference-infos.dat";

        public static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Write("Expected parameters: ");
                Write("[Input configuration path]");
                Write("[Output folder path]");
                Write("[AnalyzerLanguage: 'cs' for C#, 'vbnet' for VB.Net]");

                return -1;
            }

            var language = AnalyzerLanguage.Parse(args[2]);

            Write($"SonarAnalyzer for {language.GetFriendlyName()} version {typeof (Program).Assembly.GetName().Version}");

            var configuration = new Configuration(args[0], language);
            var diagnosticsRunner = new DiagnosticsRunner(configuration);

            var xmlOutSettings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "  "
            };

            var outputDirectory = args[1];
            Directory.CreateDirectory(outputDirectory);

            using (var xmlOut = XmlWriter.Create(Path.Combine(outputDirectory, AnalysisOutputFileName), xmlOutSettings))
            {
                xmlOut.WriteComment("This XML format is not an API");
                xmlOut.WriteStartElement("AnalysisOutput");

                xmlOut.WriteStartElement("Files");
                var currentFileIndex = 0;

                using (var tokenInfoStream = File.Create(Path.Combine(outputDirectory, TokenInfosFileName)))
                using (var tokenReferenceInfoStream = File.Create(Path.Combine(outputDirectory, TokenReferenceInfosFileName)))
                {
                    foreach (var file in configuration.Files)
                    {
                        #region Single file processing

                        xmlOut.Flush();
                        Write(currentFileIndex + "/" + configuration.Files.Count + " files analyzed, starting to analyze: " + file);
                        currentFileIndex++;

                        try
                        {
                            var solution = CompilationHelper.GetSolutionFromFiles(file, language);

                            var compilation = solution.Projects.First().GetCompilationAsync().Result;
                            var syntaxTree = compilation.SyntaxTrees.First();

                            var tokenCollector = new TokenCollector(file, solution.GetDocument(syntaxTree), solution.Workspace);

                            tokenCollector.FileTokenInfo.WriteDelimitedTo(tokenInfoStream);
                            tokenCollector.FileTokenReferenceInfo.WriteDelimitedTo(tokenReferenceInfoStream);

                            var metrics = language == AnalyzerLanguage.CSharp
                                ? (MetricsBase)new Common.CSharp.Metrics(syntaxTree)
                                : new Common.VisualBasic.Metrics(syntaxTree);

                            xmlOut.WriteStartElement("File");
                            xmlOut.WriteElementString("Path", file);

                            xmlOut.WriteStartElement("Metrics");

                            xmlOut.WriteElementString("Lines", metrics.LineCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("Classes", metrics.ClassCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("Accessors", metrics.AccessorCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("Statements", metrics.StatementCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("Functions", metrics.FunctionCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("PublicApi", metrics.PublicApiCount.ToString(CultureInfo.InvariantCulture));
                            xmlOut.WriteElementString("PublicUndocumentedApi", metrics.PublicUndocumentedApiCount.ToString(CultureInfo.InvariantCulture));

                            var complexity = metrics.Complexity;
                            xmlOut.WriteElementString("Complexity", complexity.ToString(CultureInfo.InvariantCulture));

                            // TODO This is a bit ridiculous, but is how SonarQube works
                            var fileComplexityDistribution = new Distribution(0, 5, 10, 20, 30, 60, 90);
                            fileComplexityDistribution.Add(complexity);
                            xmlOut.WriteElementString("FileComplexityDistribution", fileComplexityDistribution.ToString());

                            xmlOut.WriteElementString("FunctionComplexityDistribution", metrics.FunctionComplexityDistribution.ToString());

                            var comments = metrics.GetComments(configuration.IgnoreHeaderComments);
                            xmlOut.WriteStartElement("Comments");
                            xmlOut.WriteStartElement("NoSonar");
                            foreach (var line in comments.NoSonar)
                            {
                                xmlOut.WriteElementString("Line", line.ToString(CultureInfo.InvariantCulture));
                            }
                            xmlOut.WriteEndElement();
                            xmlOut.WriteStartElement("NonBlank");
                            foreach (var line in comments.NonBlank)
                            {
                                xmlOut.WriteElementString("Line", line.ToString(CultureInfo.InvariantCulture));
                            }
                            xmlOut.WriteEndElement();
                            xmlOut.WriteEndElement();

                            xmlOut.WriteStartElement("LinesOfCode");
                            foreach (var line in metrics.LinesOfCode)
                            {
                                xmlOut.WriteElementString("Line", line.ToString(CultureInfo.InvariantCulture));
                            }
                            xmlOut.WriteEndElement();

                            xmlOut.WriteEndElement();

                            xmlOut.WriteStartElement("Issues");

                            foreach (var diagnostic in diagnosticsRunner.GetDiagnostics(compilation))
                            {
                                xmlOut.WriteStartElement("Issue");
                                xmlOut.WriteElementString("Id", diagnostic.Id);
                                if (diagnostic.Location != Location.None)
                                {
                                    xmlOut.WriteElementString("Line", diagnostic.GetLineNumberToReport().ToString(CultureInfo.InvariantCulture));
                                }
                                xmlOut.WriteElementString("Message", diagnostic.GetMessage());
                                xmlOut.WriteEndElement();
                            }

                            xmlOut.WriteEndElement();

                            xmlOut.WriteEndElement();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("Failed to analyze the file: " + file);
                            Console.Error.WriteLine(e);
                            return 1;
                        }

                        #endregion
                    }
                }

                xmlOut.WriteEndElement();

                xmlOut.WriteEndElement();
                xmlOut.WriteEndDocument();

                xmlOut.Flush();
                return 0;
            }
        }

        private static void Write(string text)
        {
            Console.WriteLine(text);
        }
    }
}
