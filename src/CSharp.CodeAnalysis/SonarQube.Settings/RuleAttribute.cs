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

namespace SonarQube.CSharp.CodeAnalysis.SonarQube.Settings
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RuleAttribute : Attribute
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public Severity Severity { get; set; }
        public bool IsActivatedByDefault { get; set; }
        public bool Template { get; set; }

        public RuleAttribute(string key, Severity severity, string title, bool isActivatedByDefault, bool template)
        {
            Key = key;
            Title = title;
            Severity = severity;
            IsActivatedByDefault = isActivatedByDefault;
            Template = template;
        }
        public RuleAttribute(string key, Severity severity, string title, bool isActivatedByDefault)
            :this(key, severity, title, isActivatedByDefault, false)
        {
        }
        public RuleAttribute(string key, Severity severity, string title)
            : this(key, severity, title, true, false)
        {
        }
    }
}