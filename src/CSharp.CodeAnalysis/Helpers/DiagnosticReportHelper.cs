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

using Microsoft.CodeAnalysis;

namespace SonarQube.CSharp.CodeAnalysis.Helpers
{
    public static class DiagnosticReportHelper
    {
        #region Line Number

        public static int GetLineNumberToReport(this SyntaxNode self)
        {
            return self.GetLocation().GetLineNumberToReport();
        }

        public static int GetLineNumberToReport(this Diagnostic self)
        {
            return self.Location.GetLineNumberToReport();
        }

        private static int GetLineNumberToReport(this Location self)
        {
            return self.GetLineSpan().StartLinePosition.Line + 1;
        }

        #endregion
    }
}