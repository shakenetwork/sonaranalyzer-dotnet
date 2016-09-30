/*
 * SonarAnalyzer for .NET
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
using System.Linq;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common;
using System.IO;
using Google.Protobuf;
using SonarAnalyzer.Protobuf;

namespace SonarAnalyzer.Runner
{
    public static class Program
    {
        internal const string TokenTypeFileName = "token-type.pb";
        internal const string SymbolReferenceFileName = "symbol-reference.pb";
        internal const string CopyPasteTokenFileName = "token-cpd.pb";
        internal const string IssuesFileName = "issues.pb";
        internal const string MetricsFileName = "metrics.pb";

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

            Write($"SonarAnalyzer for {language.GetFriendlyName()} version {typeof(Program).Assembly.GetName().Version}");

            var configuration = new Configuration(args[0], language);
            var diagnosticsRunner = new DiagnosticsRunner(configuration);

            var outputDirectory = args[1];
            Directory.CreateDirectory(outputDirectory);

            var currentFileIndex = 0;

            using (var tokentypeStream = File.Create(Path.Combine(outputDirectory, TokenTypeFileName)))
            using (var symRefStream = File.Create(Path.Combine(outputDirectory, SymbolReferenceFileName)))
            using (var cpdStream = File.Create(Path.Combine(outputDirectory, CopyPasteTokenFileName)))
            using (var metricsStream = File.Create(Path.Combine(outputDirectory, MetricsFileName)))
            using (var issuesStream = File.Create(Path.Combine(outputDirectory, IssuesFileName)))
            {
                foreach (var file in configuration.Files)
                {
                    #region Single file processing

                    Write(currentFileIndex + "/" + configuration.Files.Count + " files analyzed, starting to analyze: " + file);
                    currentFileIndex++;

                    try
                    {
                        var solution = CompilationHelper.GetSolutionFromFiles(file, language);

                        var compilation = solution.Projects.First().GetCompilationAsync().Result;
                        var syntaxTree = compilation.SyntaxTrees.First();

                        var tokenCollector = new TokenCollector(file, solution.GetDocument(syntaxTree), solution.Workspace);

                        tokenCollector.TokenTypeInfo.WriteDelimitedTo(tokentypeStream);
                        tokenCollector.SymbolReferenceInfo.WriteDelimitedTo(symRefStream);
                        tokenCollector.CopyPasteTokenInfo.WriteDelimitedTo(cpdStream);

                        var metrics = language == AnalyzerLanguage.CSharp
                            ? (MetricsBase)new Common.CSharp.Metrics(syntaxTree)
                            : new Common.VisualBasic.Metrics(syntaxTree);

                        var complexity = metrics.Complexity;

                        var metricsInfo = new MetricsInfo()
                        {
                            FilePath = file,
                            ClassCount = metrics.ClassCount,
                            StatementCount = metrics.StatementCount,
                            FunctionCount = metrics.FunctionCount,
                            PublicApiCount = metrics.PublicApiCount,
                            PublicUndocumentedApiCount = metrics.PublicUndocumentedApiCount,

                            Complexity = complexity,
                            ComplexityInClasses = metrics.ClassNodes.Sum(metrics.GetComplexity),
                            ComplexityInFunctions = metrics.FunctionNodes.Sum(metrics.GetComplexity),

                            FileComplexityDistribution = new Distribution(Distribution.FileComplexityRange).Add(complexity).ToString(),
                            FunctionComplexityDistribution = metrics.FunctionComplexityDistribution.ToString()
                        };

                        var comments = metrics.GetComments(configuration.IgnoreHeaderComments);
                        metricsInfo.NoSonarComment.AddRange(comments.NoSonar);
                        metricsInfo.NonBlankComment.AddRange(comments.NonBlank);

                        metricsInfo.CodeLine.AddRange(metrics.CodeLines);

                        metricsInfo.WriteDelimitedTo(metricsStream);

                        var issuesInFile = new FileIssues
                        {
                            FilePath = file
                        };

                        foreach (var diagnostic in diagnosticsRunner.GetDiagnostics(compilation))
                        {
                            var issue = new FileIssues.Types.Issue
                            {
                                Id = diagnostic.Id,
                                Message = diagnostic.GetMessage()
                            };

                            if (diagnostic.Location != Location.None)
                            {
                                issue.Location = TokenCollector.GetTextRange(diagnostic.Location.GetLineSpan());
                            }

                            issuesInFile.Issue.Add(issue);
                        }

                        issuesInFile.WriteDelimitedTo(issuesStream);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Failed to analyze the file: " + file);
                        Console.Error.WriteLine(e);
                        return 1;
                    }

                    #endregion
                }

                return 0;
            }
        }

        private static void Write(string text)
        {
            Console.WriteLine(text);
        }
    }
}
