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
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Utilities;
using SonarLint.Common;
using System.Reflection;
using SonarLint.Rules.CSharp;
using SonarLint.Helpers;
using System.IO;

namespace SonarLint.Runner
{
    public class Configuration
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> nonTemplateAnalyzers;
        private readonly AnalyzerLanguage language;
        private readonly XDocument xml;

        public string Path { get; private set; }

        public bool IgnoreHeaderComments { get; }
        public IImmutableList<string> Files { get; }
        public IImmutableSet<string> AnalyzerIds { get; }

        public Configuration(string path, AnalyzerLanguage language)
        {
            if (!ParameterLoader.ConfigurationFilePathMatchesExpected(path))
            {
                throw new ArgumentException(
                    $"Input configuration doesn't match expected file name: '{ParameterLoader.ParameterConfigurationFileName}'",
                    nameof(path));
            }

            Path = path;
            this.language = language;

            nonTemplateAnalyzers = ImmutableArray.Create(GetNonTemplateAnalyzers(language).ToArray());

            this.xml = XDocument.Load(path);
            var settings = ParseSettings(xml);
            IgnoreHeaderComments = "true".Equals(settings["sonar.cs.ignoreHeaderComments"], StringComparison.InvariantCultureIgnoreCase);

            Files = xml.Descendants("File").Select(e => e.Value).ToImmutableList();

            AnalyzerIds = xml.Descendants("Rule").Select(e => e.Elements("Key").Single().Value).ToImmutableHashSet();
        }



        private static ImmutableDictionary<string, string> ParseSettings(XContainer xml)
        {
            return xml
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
        }



        public ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        {
            var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

            foreach (var analyzer in nonTemplateAnalyzers
                .Where(analyzer => AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id)))
            {
                builder.Add(analyzer);
            }

            if (language == AnalyzerLanguage.CSharp)
            {
                AddAnalyzerCommentRegularExpression(builder);
            }

            return builder.ToImmutable();
        }

        #region Add template analyzers

        private void AddAnalyzerCommentRegularExpression(ImmutableArray<DiagnosticAnalyzer>.Builder builder)
        {
            if (!AnalyzerIds.Contains(Rules.CSharp.CommentRegularExpression.TemplateDiagnosticId))
            {
                return;
            }
            var rules = ImmutableArray.CreateBuilder<CommentRegularExpression.CommentRegularExpressionRule>();
            foreach (var parameterValues in ParameterLoader.ParseParameters(xml)
                .Where(p => p.RuleId == CommentRegularExpression.TemplateDiagnosticId)
                .Select(p => p.ParameterValues))
            {
                rules.Add(
                    new CommentRegularExpression.CommentRegularExpressionRule(
                        parameterValues.Single(pv => pv.ParameterKey == "RuleKey").ParameterValue,
                        parameterValues.Single(pv => pv.ParameterKey == "regularExpression").ParameterValue,
                        parameterValues.Single(pv => pv.ParameterKey == "message").ParameterValue));
            }
            var analyzer = new CommentRegularExpression {RuleInstances = rules.ToImmutable()};
            builder.Add(analyzer);
        }

        #endregion

        #region Discover analyzers

        public static IEnumerable<DiagnosticAnalyzer> GetNonTemplateAnalyzers(AnalyzerLanguage language)
        {
            return
                new RuleFinder().GetAnalyzerTypes(language)
                    .Where(type => !RuleFinder.IsRuleTemplate(type))
                    .Select(type => (DiagnosticAnalyzer) Activator.CreateInstance(type));
        }

        #endregion
    }
}
