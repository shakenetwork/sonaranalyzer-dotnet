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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Utilities;

namespace SonarAnalyzer.Runner
{
    public class Configuration
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> analyzers;

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
            analyzers = ImmutableArray.Create(GetAnalyzers(language).ToArray());

            var xml = XDocument.Load(path);
            var settings = ParseSettings(xml);
            IgnoreHeaderComments = "true".Equals(settings[$"sonar.{language}.ignoreHeaderComments"], StringComparison.OrdinalIgnoreCase);

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

            foreach (var analyzer in analyzers
                .Where(analyzer => AnalyzerIds.Contains(analyzer.SupportedDiagnostics.Single().Id)))
            {
                builder.Add(analyzer);
            }

            return builder.ToImmutable();
        }

        #region Discover analyzers

        public static IEnumerable<DiagnosticAnalyzer> GetAnalyzers(AnalyzerLanguage language)
        {
            return
                new RuleFinder().GetAnalyzerTypes(language)
                    .Select(type => (DiagnosticAnalyzer) Activator.CreateInstance(type));
        }

        #endregion
    }
}
