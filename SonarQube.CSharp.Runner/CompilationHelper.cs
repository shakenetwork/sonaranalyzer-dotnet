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

using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SonarQube.CSharp.CodeAnalysis.Runner
{
    public class CompilationHelper
    {
        public static Solution GetSolutionFromFiles(params string[] filePaths)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var project = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromAssembly(typeof (object).Assembly));

                foreach (var filePath in filePaths)
                {
                    var file = new FileInfo(filePath);
                    var document = project.AddDocument(file.Name, File.ReadAllText(file.FullName, Encoding.UTF8));
                    project = document.Project;
                }

                return project.Solution;
            }
        }

        public static Solution GetSolutionFromText(string text)
        {
            using (var workspace = new AdhocWorkspace())
            {
                return workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromAssembly(typeof (object).Assembly))
                    .AddDocument("foo.cs", text)
                    .Project
                    .Solution;
            }
        }
    }
}
