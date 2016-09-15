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
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Common;

namespace SonarAnalyzer.Helpers
{
    public static class EnumHelper
    {
        public static string[] SplitCamelCase(this string source)
        {
            return Regex.Split(source, @"(?<!^)(?=[A-Z])");
        }

        public static string ToSonarQubeString(this SqaleSubCharacteristic subCharacteristic)
        {
            var parts = subCharacteristic.ToString().SplitCamelCase();
            return string.Join("_", parts).ToUpper(CultureInfo.InvariantCulture);
        }

        public static DiagnosticSeverity ToDiagnosticSeverity(this Severity severity)
        {
            return severity.ToDiagnosticSeverity(IdeVisibility.Visible);
        }

        public static DiagnosticSeverity ToDiagnosticSeverity(this Severity severity,
            IdeVisibility ideVisibility)
        {
            switch (severity)
            {
                case Severity.Info:
                    return ideVisibility == IdeVisibility.Hidden ? DiagnosticSeverity.Hidden : DiagnosticSeverity.Info;
                case Severity.Minor:
                    return ideVisibility == IdeVisibility.Hidden ? DiagnosticSeverity.Hidden : DiagnosticSeverity.Warning;
                case Severity.Major:
                case Severity.Critical:
                case Severity.Blocker:
                    return DiagnosticSeverity.Warning;
                default:
                    throw new NotSupportedException();
            }
        }

        public static string[] ToCustomTags(this IdeVisibility ideVisibility)
        {
            return ideVisibility == IdeVisibility.Hidden
                ? new[] { WellKnownDiagnosticTags.Unnecessary }
                : new string[0];
        }
    }
}
