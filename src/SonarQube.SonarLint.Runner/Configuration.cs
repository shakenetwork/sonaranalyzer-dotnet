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
using SonarLint.Rules;
using SonarLint.Utilities;
using SonarLint.Common;
using System.Reflection;

namespace SonarLint.Runner
{
    public class Configuration
    {
        private class RuleParameterValue
        {
            public string ParameterKey { get; set; }
            public string ParameterValue { get; set; }
        }
        private class RuleParameterValues
        {
            public string RuleId { get; set; }
            public List<RuleParameterValue> ParameterValues { get; set; } = new List<RuleParameterValue>();
        }

        private readonly ImmutableArray<DiagnosticAnalyzer> nonTemplateAnalyzers;
        private readonly IImmutableList<RuleParameterValues> parameters;
        private readonly AnalyzerLanguage language;

        public bool IgnoreHeaderComments { get; }
        public IImmutableList<string> Files { get; }
        public IImmutableSet<string> AnalyzerIds { get; }

        public Configuration(XContainer xml, AnalyzerLanguage language)
        {
            this.language = language;
            nonTemplateAnalyzers = ImmutableArray.Create(GetNonTemplateAnalyzers(language).ToArray());

            var settings = ParseSettings(xml);
            IgnoreHeaderComments = "true".Equals(settings["sonar.cs.ignoreHeaderComments"]);

            Files = xml.Descendants("File").Select(e => e.Value).ToImmutableList();

            AnalyzerIds = xml.Descendants("Rule").Select(e => e.Elements("Key").Single().Value).ToImmutableHashSet();

            var builder = ImmutableList.CreateBuilder<RuleParameterValues>();
            foreach (var rule in xml.Descendants("Rule").Where(e => e.Elements("Parameters").Any()))
            {
                var analyzerId = rule.Elements("Key").Single().Value;

                var parameterValues = rule
                    .Elements("Parameters").Single()
                    .Elements("Parameter")
                    .Select(e => new RuleParameterValue
                    {
                        ParameterKey = e.Elements("Key").Single().Value,
                        ParameterValue = e.Elements("Value").Single().Value
                    });

                var pvs = new RuleParameterValues
                {
                    RuleId = analyzerId
                };
                pvs.ParameterValues.AddRange(parameterValues);

                builder.Add(pvs);
            }
            parameters = builder.ToImmutable();
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

        private void SetParameterValues(DiagnosticAnalyzer parameteredAnalyzer)
        {
            var propertyParameterPairs = parameteredAnalyzer.GetType()
                .GetProperties()
                .Select(p => new { Property = p, Descriptor = p.GetCustomAttributes<RuleParameterAttribute>().SingleOrDefault() })
                .Where(p=> p.Descriptor != null);

            foreach (var propertyParameterPair in propertyParameterPairs)
            {
                var value = parameters
                    .Single(p => p.RuleId == parameteredAnalyzer.SupportedDiagnostics.Single().Id).ParameterValues
                    .Single(pv => pv.ParameterKey == propertyParameterPair.Descriptor.Key)
                    .ParameterValue;

                object convertedValue = value;
                switch (propertyParameterPair.Descriptor.Type)
                {
                    case PropertyType.String:
                        if (typeof(IEnumerable<string>).IsAssignableFrom(propertyParameterPair.Property.PropertyType))
                        {
                            //todo: is this a common thing, or it's special for MagicNumbers.
                            //If so, then it would be better to put this parsing logic directly into each class.
                            convertedValue = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        }
                        break;
                    case PropertyType.Integer:
                        convertedValue = int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                propertyParameterPair.Property.SetValue(parameteredAnalyzer, convertedValue);
            }
        }

        public ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        {
            var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

            foreach (var analyzer in nonTemplateAnalyzers
                .Where(analyzer => AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id)))
            {
                if (RuleFinder.IsParametered(analyzer.GetType()))
                {
                    SetParameterValues(analyzer);
                }
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
            if (!AnalyzerIds.Contains(CommentRegularExpression.DiagnosticId))
            {
                return;
            }
            var rules = ImmutableArray.CreateBuilder<CommentRegularExpressionRule>();
            foreach (var parameterValues in parameters.Where(p => p.RuleId == CommentRegularExpression.DiagnosticId).Select(p=>p.ParameterValues))
            {
                rules.Add(
                    new CommentRegularExpressionRule
                    {
                        // TODO: Add rule description
                        Descriptor = CommentRegularExpression.CreateDiagnosticDescriptor(
                            parameterValues.Single(pv =>pv.ParameterKey == "RuleKey").ParameterValue,
                            parameterValues.Single(pv => pv.ParameterKey == "message").ParameterValue),
                        RegularExpression = parameterValues.Single(pv => pv.ParameterKey == "regularExpression").ParameterValue
                    });
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
