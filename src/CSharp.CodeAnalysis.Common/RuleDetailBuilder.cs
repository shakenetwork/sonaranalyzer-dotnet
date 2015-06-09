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
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Common
{
    public class RuleDetailBuilder
    {
        private const string RuleDescriptionPathPattern = "SonarQube.CSharp.CodeAnalysis.Rules.Description.{0}.html";

        public static IEnumerable<RuleDetail> GetAllRuleDetails()
        {
            return new RuleFinder().GetAllAnalyzerTypes().Select(GetRuleDetail);
        }
        public static IEnumerable<RuleDetail> GetParameterlessRuleDetails()
        {
            return new RuleFinder().GetParameterlessAnalyzerTypes().Select(GetRuleDetail);
        }

        private static RuleDetail GetRuleDetail(Type analyzerType)
        {
            var rule = analyzerType.GetCustomAttributes<RuleAttribute>().FirstOrDefault();

            if (rule == null)
            {
                return null;
            }

            var ruleDetail = new RuleDetail
            {
                Key = rule.Key,
                Title = rule.Title,
                Severity = rule.Severity.ToString().ToUpper(CultureInfo.InvariantCulture),
                IsActivatedByDefault = rule.IsActivatedByDefault,
                Description = GetResourceHtml(analyzerType, rule),
                IsTemplate = rule.Template
            };

            var parameters = analyzerType.GetProperties()
                .Where(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());

            foreach (var ruleParameter in parameters
                .Select(propertyInfo => propertyInfo.GetCustomAttributes<RuleParameterAttribute>().First()))
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

            var tags = analyzerType.GetCustomAttributes<TagsAttribute>().FirstOrDefault();

            if (tags != null)
            {
                ruleDetail.Tags.AddRange(tags.Tags);
            }

            var sqaleRemediation = analyzerType.GetCustomAttributes<SqaleRemediationAttribute>().FirstOrDefault();

            if (sqaleRemediation == null || sqaleRemediation is NoSqaleRemediationAttribute)
            {
                ruleDetail.SqaleDescriptor = null;
                return ruleDetail;
            }

            var sqaleSubCharacteristic = analyzerType.GetCustomAttributes<SqaleSubCharacteristicAttribute>().First();
            var sqaleDescriptor = new SqaleDescriptor { SubCharacteristic = sqaleSubCharacteristic.SubCharacteristic.ToSonarQubeString() };
            var constantRemediation = sqaleRemediation as SqaleConstantRemediationAttribute;
            if (constantRemediation == null)
            {
                ruleDetail.SqaleDescriptor = sqaleDescriptor;
                return ruleDetail;
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
            return ruleDetail;
        }

        private static string GetResourceHtml(Type analyzerType, RuleAttribute rule)
        {
            var resources = analyzerType.Assembly.GetManifestResourceNames();
            var resource = resources.SingleOrDefault(r => r.EndsWith(
                string.Format(CultureInfo.InvariantCulture, RuleDescriptionPathPattern, rule.Key),
                StringComparison.OrdinalIgnoreCase));

            if (resource == null)
            {
                throw new Exception(string.Format("Could not locate resource for rule {0}", rule.Key));
            }

            using (var stream = analyzerType.Assembly.GetManifestResourceStream(resource))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
