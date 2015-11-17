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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace SonarLint.Helpers.VisualBasic
{
    public class GeneratedCodeRecognizer : Helpers.GeneratedCodeRecognizer
    {
        #region Singleton implementation

        private GeneratedCodeRecognizer()
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarLint", "S1118:Utility classes should not have public constructors",
            Justification = "Nested private class for lazy singleton instantiation")]
        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly GeneratedCodeRecognizer instance = new GeneratedCodeRecognizer();
        }

        public static GeneratedCodeRecognizer Instance => Nested.instance;

        #endregion

        protected override bool IsTriviaComment(SyntaxTrivia trivia) =>
            trivia.IsKind(SyntaxKind.CommentTrivia);

        protected override string GetAttributeName(SyntaxNode node)
        {
            var attribute = node as AttributeSyntax;
            return attribute == null
                ? string.Empty
                : attribute.Name.ToString();
        }
    }
}
