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

using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SonarLint.Utilities;
using SonarLint.Common;
using System.Collections.Generic;

namespace SonarLint.DocGenerator
{
    public static class Program
    {
        public static void Main()
        {
            var productVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
            WriteRuleJson(productVersion);
        }

        private static void WriteRuleJson(string productVersion)
        {
            var content = GenerateRuleJson(productVersion);

            Directory.CreateDirectory(productVersion);
            File.WriteAllText(Path.Combine(productVersion, "rules.json"), content);
        }

        public static string GenerateRuleJson(string productVersion)
        {
            var csImplementations = RuleDetailBuilder.GetAllRuleDetails(AnalyzerLanguage.CSharp)
                .Select(ruleDetail => RuleImplementationMeta.Convert(ruleDetail, productVersion, AnalyzerLanguage.CSharp))
                .ToList();

            var vbImplementations = RuleDetailBuilder.GetAllRuleDetails(AnalyzerLanguage.VisualBasic)
                .Select(ruleDetail => RuleImplementationMeta.Convert(ruleDetail, productVersion, AnalyzerLanguage.VisualBasic))
                .ToList();

            var sonarLintDescriptor = new SonarLintDescriptor
            {
                Version = productVersion,
                Rules = new List<RuleMeta>()
            };

            foreach (var csImplementation in csImplementations)
            {
                var rule = new RuleMeta
                {
                    Id = csImplementation.Id,
                    Title = csImplementation.Title,
                    Implementations = new List<RuleImplementationMeta>(new[] { csImplementation })
                };
                sonarLintDescriptor.Rules.Add(rule);
            }

            foreach (var vbImplementation in vbImplementations)
            {
                var rule = sonarLintDescriptor.Rules.FirstOrDefault(r => r.Implementations.First().Id == vbImplementation.Id);
                if (rule == null)
                {
                    rule = new RuleMeta
                    {
                        Id = vbImplementation.Id,
                        Title = vbImplementation.Title,
                        Implementations = new List<RuleImplementationMeta>(new[] { vbImplementation })
                    };
                    sonarLintDescriptor.Rules.Add(rule);
                }
                else
                {
                    rule.Implementations.Add(vbImplementation);
                }
            }

            foreach (var rule in sonarLintDescriptor.Rules)
            {
                rule.Tags = rule.Implementations.SelectMany(i => i.Tags).Distinct();
            }

            return JsonConvert.SerializeObject(sonarLintDescriptor,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                });
        }
    }
}
