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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Descriptor.RuleDescriptors;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Descriptor
{
    public class RuleFinder
    {
        private readonly List<Type> diagnosticAnalyzers;
        public const string RuleDescriptionPathPattern = "SonarQube.CSharp.CodeAnalysis.Rules.Description.{0}.html";
        public const string RuleAssemblyName = "SonarQube.CSharp.CodeAnalysis";
        public const string RuleAssemblyFileName = RuleAssemblyName + ".dll";
        public const string RuleExtraAssemblyFileName = RuleAssemblyName + ".Extra.dll";

        public static IList<Assembly> GetRuleAssemblies()
        {
            return new[]
            {
                Assembly.LoadFrom(RuleAssemblyFileName),
                Assembly.LoadFrom(RuleExtraAssemblyFileName)
            };
        }

        public RuleFinder()
        {
            diagnosticAnalyzers = GetRuleAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof (DiagnosticAnalyzer)))
                .Where(t => t.GetCustomAttributes<RuleAttribute>().Any())
                .ToList();
        }

        public IEnumerable<Type> GetParameterlessAnalyzerTypes()
        {
            return diagnosticAnalyzers
                .Where(analyzerType =>
                    !analyzerType.GetCustomAttributes<RuleAttribute>().First().Template)
                .Where(analyzerType =>
                    !analyzerType.GetProperties()
                        .Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any()));
        }

        internal IEnumerable<FullRuleDescriptor> GetRuleDescriptors()
        {
            return diagnosticAnalyzers.Select(GetRuleDescriptor);
        }

        private static FullRuleDescriptor GetRuleDescriptor(Type analyzerType)
        {
            var rule = analyzerType.GetCustomAttributes<RuleAttribute>().FirstOrDefault();

            if (rule == null)
            {
                return null;
            }

            var ruleDescriptor = new RuleDescriptor
            {
                Key = rule.Key,
                Title = rule.Description,
                Severity = rule.Severity.ToString().ToUpper(CultureInfo.InvariantCulture),
                IsActivatedByDefault = rule.IsActivatedByDefault,
                Cardinality = rule.Template ? "MULTIPLE" : "SINGLE",
            };

            var resources = analyzerType.Assembly.GetManifestResourceNames();
            var resource = resources.SingleOrDefault(r => r.EndsWith(
                string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, rule.Key),
                StringComparison.OrdinalIgnoreCase));
            if (resource != null)
            {
                using (var stream = analyzerType.Assembly.GetManifestResourceStream(resource))
                using (var reader = new StreamReader(stream))
                {
                    ruleDescriptor.Description = reader.ReadToEnd();
                }
            }
            var parameters = analyzerType.GetProperties()
                .Where(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());

            foreach (var ruleParameter in parameters
                .Select(propertyInfo => propertyInfo.GetCustomAttributes<RuleParameterAttribute>().First()))
            {
                ruleDescriptor.Parameters.Add(
                    new RuleParameter
                    {
                        DefaultValue = ruleParameter.DefaultValue,
                        Description = ruleParameter.Description,
                        Key = ruleParameter.Key,
                        Type = ruleParameter.Type.ToSonarQubeString()
                    });
            }

            var tags = analyzerType.GetCustomAttributes<TagsAttribute>().FirstOrDefault();

            if (tags != null)
            {
                ruleDescriptor.Tags.AddRange(tags.Tags);
            }

            var sqaleRemediation = analyzerType.GetCustomAttributes<SqaleRemediationAttribute>().FirstOrDefault();
            
            if (sqaleRemediation == null || sqaleRemediation is NoSqaleRemediationAttribute)
            {
                return new FullRuleDescriptor
                {
                    RuleDescriptor = ruleDescriptor,
                    SqaleDescriptor = null
                };
            }

            var sqaleSubCharacteristic = analyzerType.GetCustomAttributes<SqaleSubCharacteristicAttribute>().First();
            var sqale = new SqaleDescriptor { SubCharacteristic = sqaleSubCharacteristic.SubCharacteristic.ToSonarQubeString() };
            var constant = sqaleRemediation as SqaleConstantRemediationAttribute;
            if (constant == null)
            {
                return new FullRuleDescriptor
                {
                    RuleDescriptor = ruleDescriptor,
                    SqaleDescriptor = sqale
                };
            }

            sqale.Remediation.Properties.Add(new SqaleRemediationProperty
            {
                Key = "remediationFunction",
                Text = "CONSTANT_ISSUE"
            });

            sqale.Remediation.Properties.Add(new SqaleRemediationProperty
            {
                Key = "offset",
                Value = constant.Value,
                Text = ""
            });

            sqale.Remediation.RuleKey = rule.Key;

            return new FullRuleDescriptor
            {
                RuleDescriptor = ruleDescriptor,
                SqaleDescriptor = sqale
            };
        }
    }
}