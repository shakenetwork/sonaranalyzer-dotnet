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
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SonarLint.Common.Sqale;

namespace SonarLint.Common
{
    public static class EnumHelper
    {
        private static string[] SplitCamelCase(this string source)
        {
            return Regex.Split(source, @"(?<!^)(?=[A-Z])");
        }
        public static string ToSonarQubeString(this SqaleSubCharacteristic subCharacteristic)
        {
            var parts = subCharacteristic.ToString().SplitCamelCase();
            return string.Join("_", parts).ToUpper(CultureInfo.InvariantCulture);
        }
        public static string ToSonarQubeString(this PropertyType propertyType)
        {
            var parts = propertyType.ToString().SplitCamelCase();
            return string.Join("_", parts).ToUpper(CultureInfo.InvariantCulture);
        }

        public static DiagnosticSeverity ToDiagnosticSeverity(this Severity severity)
        {
            switch (severity)
            {
                case Severity.Info:
                    return DiagnosticSeverity.Info;
                case Severity.Minor:
                case Severity.Major:
                case Severity.Critical:
                case Severity.Blocker:
                    return DiagnosticSeverity.Warning;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
