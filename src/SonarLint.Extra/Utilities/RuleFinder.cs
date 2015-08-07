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
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Rules;
using SonarLint.Common;

namespace SonarLint.Utilities
{
    public class RuleFinder
    {
        private readonly List<Type> diagnosticAnalyzers;

        public static Assembly GetPackagedRuleAssembly()
        {
            return Assembly.LoadFrom(typeof(EmptyStatement).Assembly.Location);
        }
        public static Assembly GetExtraRuleAssembly()
        {
            return Assembly.LoadFrom(typeof(MagicNumber).Assembly.Location);
        }

        public RuleFinder()
        {
            diagnosticAnalyzers = new[] {GetPackagedRuleAssembly(), GetExtraRuleAssembly()}
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof (DiagnosticAnalyzer)))
                .Where(t => t.GetCustomAttributes<RuleAttribute>().Any())
                .ToList();
        }

        public IEnumerable<Type> GetParameterlessAnalyzerTypes()
        {
            return diagnosticAnalyzers
                .Where(analyzerType => !IsRuleTemplate(analyzerType))
                .Where(analyzerType =>
                    !analyzerType.GetProperties()
                        .Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any()));
        }

        public static bool IsRuleTemplate(Type analyzerType)
        {
            return analyzerType.GetInterfaces()
                .Any(type => type.IsGenericType &&
                             type.GetGenericTypeDefinition() == typeof(IRuleTemplate<>));
        }

        public IEnumerable<Type> GetAllAnalyzerTypes()
        {
            return diagnosticAnalyzers;
        }
    }
}