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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.UnitTest.Helpers
{
    [TestClass]
    public class SymbolHelper_IsExtensionOn
    {
        internal const string TestInput = @"
using System.Linq;

namespace NS
{
  public static class Helper
  {
    public static void ToVoid(this int self){}
  }
  public class Class
  {
    public static void TestMethod()
    {
      new int[] { 0, 1, 2 }.Any();
      Enumerable.Any(new int[] { 0, 1, 2 });

      new int[] { 0, 1, 2 }.Clone();

      new int[] { 0, 1, 2 }.Cast<object>();

      1.ToVoid();
    }
  }
}
";

        private SemanticModel semanticModel;
        private List<StatementSyntax> statements;

        [TestInitialize]
        public void Compile()
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument("test", TestInput);
                var compilation = document.Project.GetCompilationAsync().Result;
                var tree = compilation.SyntaxTrees.First();
                semanticModel = compilation.GetSemanticModel(tree);
                statements = tree.GetRoot().DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "TestMethod").Body
                    .DescendantNodes()
                    .OfType<StatementSyntax>().ToList();
            }
        }

        [TestMethod]
        public void Symbol_IsExtensionOnIEnumerable()
        {
            Assert.IsTrue(GetMethodSymbolForIndex(3).IsExtensionOn(KnownType.System_Collections_IEnumerable));

            Assert.IsFalse(GetMethodSymbolForIndex(2).IsExtensionOn(KnownType.System_Collections_IEnumerable));
            Assert.IsFalse(GetMethodSymbolForIndex(1).IsExtensionOn(KnownType.System_Collections_IEnumerable));
        }

        [TestMethod]
        public void Symbol_IsExtensionOnGenericIEnumerable()
        {
            Assert.IsTrue(GetMethodSymbolForIndex(0).IsExtensionOn(KnownType.System_Collections_Generic_IEnumerable_T));
            Assert.IsTrue(GetMethodSymbolForIndex(1).IsExtensionOn(KnownType.System_Collections_Generic_IEnumerable_T));

            Assert.IsFalse(GetMethodSymbolForIndex(2).IsExtensionOn(KnownType.System_Collections_Generic_IEnumerable_T));
            Assert.IsFalse(GetMethodSymbolForIndex(3).IsExtensionOn(KnownType.System_Collections_Generic_IEnumerable_T));
        }

        [TestMethod]
        public void Symbol_IsExtensionOnInt()
        {
            Assert.IsTrue(GetMethodSymbolForIndex(4).IsExtensionOn(KnownType.System_Int32));

            Assert.IsFalse(GetMethodSymbolForIndex(2).IsExtensionOn(KnownType.System_Int32));
        }

        private IMethodSymbol GetMethodSymbolForIndex(int index)
        {
            var statement = (ExpressionStatementSyntax)statements[index];
            var methodSymbol = semanticModel.GetSymbolInfo(statement.Expression).Symbol as IMethodSymbol;
            return methodSymbol;
        }
    }
}
