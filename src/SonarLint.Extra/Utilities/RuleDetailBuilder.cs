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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using SonarLint.Common;
using Microsoft.CodeAnalysis.CodeFixes;
using SonarLint.RuleDescriptors;

namespace SonarLint.Utilities
{
    public static class RuleDetailBuilder
    {
        private const string RuleDescriptionPathPattern = "SonarLint.Rules.Description.{0}.html";
        internal const string CodeFixProviderSuffix = "CodeFixProvider";

        private static readonly Assembly SonarLintExtraAssembly = typeof(RuleDetailBuilder).Assembly;

        public static IEnumerable<RuleDetail> GetAllRuleDetails(AnalyzerLanguage language)
        {
            return new RuleFinder().GetAnalyzerTypes(language).Select(t => GetRuleDetail(t, language));
        }
        public static IEnumerable<RuleDetail> GetParameterlessRuleDetails(AnalyzerLanguage language)
        {
            return new RuleFinder().GetParameterlessAnalyzerTypes(language).Select(t => GetRuleDetail(t, language));
        }

        private static RuleDetail GetRuleDetail(Type analyzerType, AnalyzerLanguage language)
        {
            var rule = analyzerType.GetCustomAttributes<RuleAttribute>().Single();

            var ruleDetail = new RuleDetail
            {
                Key = rule.Key,
                Title = rule.Title,
                Severity = rule.Severity.ToString(),
                IsActivatedByDefault = rule.IsActivatedByDefault,
                Description = GetResourceHtml(rule, language)
            };

            GetParameters(analyzerType, ruleDetail);
            GetTags(analyzerType, ruleDetail);
            GetSqale(analyzerType, ruleDetail);
            GetCodeFixNames(analyzerType, ruleDetail);

            return ruleDetail;
        }

        private static Type GetCodeFixProviderType(Type analyzerType)
        {
            var typeName = analyzerType.FullName + CodeFixProviderSuffix;
            return analyzerType.Assembly.GetType(typeName);
        }

        private static void GetSqale(Type analyzerType, RuleDetail ruleDetail)
        {
            var sqaleRemediation = analyzerType.GetCustomAttributes<SqaleRemediationAttribute>().FirstOrDefault();

            if (sqaleRemediation == null || sqaleRemediation is NoSqaleRemediationAttribute)
            {
                ruleDetail.SqaleDescriptor = null;
                return;
            }

            var sqaleSubCharacteristic = analyzerType.GetCustomAttributes<SqaleSubCharacteristicAttribute>().First();
            var sqaleDescriptor = new SqaleDescriptor
            {
                SubCharacteristic = sqaleSubCharacteristic.SubCharacteristic.ToSonarQubeString()
            };
            var constantRemediation = sqaleRemediation as SqaleConstantRemediationAttribute;
            if (constantRemediation == null)
            {
                ruleDetail.SqaleDescriptor = sqaleDescriptor;
                return;
            }

            sqaleDescriptor.Remediation.Properties.AddRange(new[]
            {
                new SqaleRemediationProperty
                {
                    Key = SqaleRemediationProperty.RemediationFunctionKey,
                    Text = SqaleRemediationProperty.ConstantRemediationFunctionValue
                },
                new SqaleRemediationProperty
                {
                    Key = SqaleRemediationProperty.OffsetKey,
                    Value = constantRemediation.Value,
                    Text = string.Empty
                }
            });

            ruleDetail.SqaleDescriptor = sqaleDescriptor;
        }

        private static void GetTags(Type analyzerType, RuleDetail ruleDetail)
        {
            var tags = analyzerType.GetCustomAttributes<TagsAttribute>().FirstOrDefault();
            if (tags != null)
            {
                ruleDetail.Tags.AddRange(tags.Tags);
            }
        }



        private static void GetCodeFixNames(Type analyzerType, RuleDetail ruleDetail)
        {
            var codeFixProvider = GetCodeFixProviderType(analyzerType);
            if (codeFixProvider == null)
            {
                return;
            }

            var titles = GetCodeFixTitles(codeFixProvider);

            ruleDetail.CodeFixTitles.AddRange(titles);
        }

        public static IEnumerable<string> GetCodeFixTitles(Type codeFixProvider)
        {
            return GetCodeFixProvidersWithBase(codeFixProvider)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(field =>
                    field.Name.StartsWith("Title", StringComparison.Ordinal) &&
                    field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue());
        }

        private static IEnumerable<Type> GetCodeFixProvidersWithBase(Type codeFixProvider)
        {
            yield return codeFixProvider;

            var baseClass = codeFixProvider.BaseType;
            while (baseClass!= null && baseClass != typeof(SonarCodeFixProvider))
            {
                yield return baseClass;
                baseClass = baseClass.BaseType;
            }
        }

        private static void GetParameters(Type analyzerType, RuleDetail ruleDetail)
        {
            var parameters = analyzerType.GetProperties()
                .Select(p => p.GetCustomAttributes<RuleParameterAttribute>().SingleOrDefault());

            foreach (var ruleParameter in parameters
                .Where(attribute => attribute != null))
            {
                ruleDetail.Parameters.Add(
                    new RuleParameter
                    {
                        DefaultValue = ruleParameter.DefaultValue,
                        Description = ruleParameter.Description,
                        Key = ruleParameter.Key,
                        Type = ruleParameter.Type.ToSonarQubeString()
                    });
            }
        }

        private static string GetResourceHtml(RuleAttribute rule, AnalyzerLanguage language)
        {
            var resources = SonarLintExtraAssembly.GetManifestResourceNames();
            var resource = GetResource(resources, rule.Key, language);
            if (resource == null)
            {
                throw new InvalidDataException($"Could not locate resource for rule {rule.Key}");
            }

            using (var stream = SonarLintExtraAssembly.GetManifestResourceStream(resource))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static string GetResource(IEnumerable<string> resources, string key, AnalyzerLanguage language)
        {
            if (language == AnalyzerLanguage.CSharp)
            {
                return resources.FirstOrDefault(r =>
                    r.EndsWith(string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, key), StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith(string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, key + "_cs"), StringComparison.OrdinalIgnoreCase));
            }
            if (language == AnalyzerLanguage.VisualBasic)
            {
                return resources.FirstOrDefault(r =>
                    r.EndsWith(string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, key), StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith(string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, key + "_vb"), StringComparison.OrdinalIgnoreCase));
            }

            throw new ArgumentException("Language needs to be either C# or VB.NET", nameof(language));
        }
    }
}
