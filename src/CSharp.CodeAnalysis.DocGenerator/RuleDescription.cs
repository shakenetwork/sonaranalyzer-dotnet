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

using SonarQube.CSharp.CodeAnalysis.Common;

namespace SonarQube.CSharp.CodeAnalysis.DocGenerator
{
    public class RuleDescription
    {
        public static RuleDescription Convert(RuleDetail detail)
        {
            return new RuleDescription
            {
                Key = detail.Key,
                Title = detail.Title,
                Description = detail.Description,
                Tags = string.Join(", ", detail.Tags)
            };
        }

        public string Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Tags { get; set; }
    }
}