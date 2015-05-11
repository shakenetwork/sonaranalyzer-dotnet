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
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.CodeAnalysis.Runner
{
    public class Configuration
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> parameterLessAnalyzers = 
            ImmutableArray.Create(GetParameterlessAnalyzers().ToArray());

        public bool IgnoreHeaderComments { private set; get; }
        public IImmutableList<string> Files { private set; get; }
        public IImmutableSet<string> AnalyzerIds { private set; get; }
        public IImmutableDictionary<string, List<IImmutableDictionary<string, string>>> Parameters { private set; get; }

        public Configuration(XContainer xml)
        {
            var settings = xml
                .Descendants("Setting")
                .Select(e =>
                {
                    var keyElement = e.Element("Key");
                    var valueElement = e.Element("Value");
                    if (valueElement != null && keyElement != null)
                    {
                        return new
                        {
                            Key = keyElement.Value,
                            Value = valueElement.Value
                        };
                    }
                    return null;
                })
                .Where(e => e != null)
                .ToImmutableDictionary(e => e.Key, e => e.Value);

            IgnoreHeaderComments = "true".Equals(settings["sonar.cs.ignoreHeaderComments"]);

            Files = xml.Descendants("File").Select(e => e.Value).ToImmutableList();

            AnalyzerIds = xml.Descendants("Rule").Select(e => e.Elements("Key").Single().Value).ToImmutableHashSet();

            var builder = ImmutableDictionary.CreateBuilder<string, List<IImmutableDictionary<string, string>>>();
            foreach (var rule in xml.Descendants("Rule").Where(e => e.Elements("Parameters").Any()))
            {
                var analyzerId = rule.Elements("Key").Single().Value;

                var parameters = rule
                                 .Elements("Parameters").Single()
                                 .Elements("Parameter")
                                 .ToImmutableDictionary(e => e.Elements("Key").Single().Value, e => e.Elements("Value").Single().Value);

                if (!builder.ContainsKey(analyzerId))
                {
                    builder.Add(analyzerId, new List<IImmutableDictionary<string, string>>());
                }
                builder[analyzerId].Add(parameters);
            }
            Parameters = builder.ToImmutable();
        }

        public ImmutableArray<DiagnosticAnalyzer> Analyzers()
        {
            var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

            foreach (var parameterLessAnalyzer in parameterLessAnalyzers
                .Where(parameterLessAnalyzer => 
                    AnalyzerIds.Contains(parameterLessAnalyzer.SupportedDiagnostics.Single().Id)))
            {
                builder.Add(parameterLessAnalyzer);
            }

            AddAnalyzerFileLines(builder);
            AddAnalyzerLineLength(builder);
            AddAnalyzerTooManyLabelsInSwitch(builder);
            AddAnalyzerTooManyParameters(builder);
            AddAnalyzerExpressionCOmplexity(builder);
            AddAnalyzerFunctionalComplexity(builder);
            AddAnalyzerClassName(builder);
            AddAnalyzerMethodName(builder);
            AddAnalyzerMagicNumber(builder);
            AddAnalyzerCommentRegularExpression(builder);

            return builder.ToImmutable();
        }

        #region Add analyzers with parameters
        
        private void AddAnalyzerCommentRegularExpression(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            if (!AnalyzerIds.Contains(CommentRegularExpression.DiagnosticId))
            {
                return;
            }
            var rules = ImmutableArray.CreateBuilder<CommentRegularExpressionRule>();
            foreach (var parameters in Parameters[CommentRegularExpression.DiagnosticId])
            {
                rules.Add(
                    new CommentRegularExpressionRule
                    {
                        // TODO: Add rule description
                        Descriptor = CommentRegularExpression.CreateDiagnosticDescriptor(parameters["RuleKey"], parameters["message"]),
                        RegularExpression = parameters["regularExpression"]
                    });
            }
            var analyzer = new CommentRegularExpression {Rules = rules.ToImmutable()};
            builder.Add(analyzer);
        }

        private void AddAnalyzerMagicNumber(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new MagicNumber();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Exceptions =
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["exceptions"]
                    .Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToImmutableHashSet();
            builder.Add(analyzer);
        }

        private void AddAnalyzerMethodName(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new MethodName();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Convention = Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["format"];
            builder.Add(analyzer);
        }

        private void AddAnalyzerClassName(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new ClassName();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Convention = Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["format"];
            builder.Add(analyzer);
        }

        private void AddAnalyzerFunctionalComplexity(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new FunctionComplexity();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["maximumFunctionComplexityThreshold"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        private void AddAnalyzerExpressionCOmplexity(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new ExpressionComplexity();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["max"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        private void AddAnalyzerTooManyParameters(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new TooManyParameters();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["max"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        private void AddAnalyzerTooManyLabelsInSwitch(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new TooManyLabelsInSwitch();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["maximum"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        private void AddAnalyzerLineLength(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new LineLength();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["maximumLineLength"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        private void AddAnalyzerFileLines(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            var analyzer = new FileLines();
            if (!AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id))
            {
                return;
            }
            analyzer.Maximum = int.Parse(
                Parameters[analyzer.SupportedDiagnostics.Single().Id].Single()["Max"],
                NumberStyles.None, CultureInfo.InvariantCulture);
            builder.Add(analyzer);
        }

        #endregion

        #region Discover analyzers without parameters

        public static IEnumerable<DiagnosticAnalyzer> GetParameterlessAnalyzers()
        {
            return
                new RuleFinder().GetParameterlessAnalyzerTypes()
                    .Select(type => (DiagnosticAnalyzer) Activator.CreateInstance(type));
        }

        #endregion
    }
}
