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
using System.Linq;

namespace SonarAnalyzer.UnitTest.Helpers
{
    [TestClass]
    public class SymbolHelperTest
    {
        internal const string TestInput = @"
namespace NS
{
  public class Base
  {
    public class Nested
    {
      public class NestedMore
      {}
    }

    public virtual void Method1() { }
    protected virtual void Method2() { }
    public abstract int Property { get; set; }

    public void Method4(){}
  }
  private class Derived1 : Base
  {
    public override int Property { get; set; }
  }
  public class Derived2 : Base, IInterface
  {
    public override int Property { get; set; }
    public int Property2 { get; set; }
    public void Method3(){}

    public abstract void Method5();
    public void EventHandler(object o, System.EventArgs args){}
  }
  public interface IInterface
  {
    int Property2 { get; set; }
    void Method3();
  }
}
";

        private ClassDeclarationSyntax baseClassDeclaration;
        private ClassDeclarationSyntax derivedClassDeclaration1;
        private ClassDeclarationSyntax derivedClassDeclaration2;
        private InterfaceDeclarationSyntax interfaceDeclaration;
        private SemanticModel semanticModel;
        private SyntaxTree tree;

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
                tree = compilation.SyntaxTrees.First();
                semanticModel = compilation.GetSemanticModel(tree);

                baseClassDeclaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Base");
                derivedClassDeclaration1 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Derived1");
                derivedClassDeclaration2 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Derived2");
                interfaceDeclaration = tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "IInterface");
            }
        }

        [TestMethod]
        public void Symbol_IsPublicApi()
        {
            var method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method1");
            var symbol = semanticModel.GetDeclaredSymbol(method);

            Assert.IsTrue(symbol.IsPublicApi());

            method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method2");
            symbol = semanticModel.GetDeclaredSymbol(method);

            Assert.IsFalse(symbol.IsPublicApi());

            var property = baseClassDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Property");
            symbol = semanticModel.GetDeclaredSymbol(property);

            Assert.IsTrue(symbol.IsPublicApi());

            property = interfaceDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Property2");
            symbol = semanticModel.GetDeclaredSymbol(property);

            Assert.IsTrue(symbol.IsPublicApi());

            property = derivedClassDeclaration1.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Property");
            symbol = semanticModel.GetDeclaredSymbol(property);

            Assert.IsFalse(symbol.IsPublicApi());
        }

        [TestMethod]
        public void Symbol_IsInterfaceImplementationOrMemberOverride()
        {
            var method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method1");
            var symbol = semanticModel.GetDeclaredSymbol(method);

            Assert.IsFalse(symbol.IsInterfaceImplementationOrMemberOverride());

            var property = derivedClassDeclaration2.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Property");
            symbol = semanticModel.GetDeclaredSymbol(property);

            Assert.IsTrue(symbol.IsInterfaceImplementationOrMemberOverride());

            property = derivedClassDeclaration2.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Property2");
            symbol = semanticModel.GetDeclaredSymbol(property);

            Assert.IsTrue(symbol.IsInterfaceImplementationOrMemberOverride());

            method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method3");
            symbol = semanticModel.GetDeclaredSymbol(method);

            Assert.IsTrue(symbol.IsInterfaceImplementationOrMemberOverride());
        }

        [TestMethod]
        public void Symbol_TryGetOverriddenOrInterfaceMember()
        {
            var method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method1");
            var methodSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(method);
            IMethodSymbol overriddenMethod;
            Assert.IsFalse(methodSymbol.TryGetOverriddenOrInterfaceMember(out overriddenMethod));

            var property = derivedClassDeclaration2.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(p => p.Identifier.ValueText == "Property");
            var propertySymbol = (IPropertySymbol)semanticModel.GetDeclaredSymbol(property);

            IPropertySymbol overriddenProperty;
            Assert.IsTrue(propertySymbol.TryGetOverriddenOrInterfaceMember(out overriddenProperty));

            property = baseClassDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .First(p => p.Identifier.ValueText == "Property");
            Assert.AreEqual((IPropertySymbol)semanticModel.GetDeclaredSymbol(property), overriddenProperty);


            method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method3");
            methodSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(method);

            Assert.IsTrue(methodSymbol.TryGetOverriddenOrInterfaceMember(out overriddenMethod));

            method = interfaceDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method3");
            Assert.AreEqual((IMethodSymbol)semanticModel.GetDeclaredSymbol(method), overriddenMethod);
        }

        [TestMethod]
        public void Symbol_IsChangeable()
        {
            var method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method1");
            var symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsFalse(symbol.IsChangeable());

            method = baseClassDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method4");
            symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsTrue(symbol.IsChangeable());

            method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method5");
            symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsFalse(symbol.IsChangeable());

            method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method3");
            symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsFalse(symbol.IsChangeable());
        }

        [TestMethod]
        public void Symbol_IsProbablyEventHandler()
        {
            var method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "Method3");
            var symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsFalse(symbol.IsProbablyEventHandler());

            method = derivedClassDeclaration2.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ValueText == "EventHandler");
            symbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            Assert.IsTrue(symbol.IsProbablyEventHandler());
        }

        [TestMethod]
        public void Symbol_GetSelfAndBaseTypes()
        {
            var objectType = semanticModel.Compilation.GetTypeByMetadataName("System.Object");
            var baseTypes = objectType.GetSelfAndBaseTypes().ToList();
            Assert.AreEqual(1, baseTypes.Count);
            Assert.AreEqual(objectType, baseTypes.First());

            var derived1Type = semanticModel.GetDeclaredSymbol(derivedClassDeclaration1) as INamedTypeSymbol;
            baseTypes = derived1Type.GetSelfAndBaseTypes().ToList();
            Assert.AreEqual(3, baseTypes.Count);
            Assert.AreEqual(derived1Type, baseTypes[0]);
            Assert.AreEqual(semanticModel.GetDeclaredSymbol(baseClassDeclaration) as INamedTypeSymbol, baseTypes[1]);
            Assert.AreEqual(objectType, baseTypes[2]);
        }

        [TestMethod]
        public void Symbol_GetAllNamedTypes_Namespace()
        {
            var ns = (NamespaceDeclarationSyntax)tree.GetRoot().ChildNodes().First();
            var nsSymbol = semanticModel.GetDeclaredSymbol(ns) as INamespaceSymbol;

            var typeSymbols = nsSymbol.GetAllNamedTypes();
            Assert.AreEqual(6, typeSymbols.Count());
        }

        [TestMethod]
        public void Symbol_GetAllNamedTypes_Type()
        {
            var typeSymbol = semanticModel.GetDeclaredSymbol(baseClassDeclaration) as INamedTypeSymbol;
            var typeSymbols = typeSymbol.GetAllNamedTypes();
            Assert.AreEqual(3, typeSymbols.Count());
        }
    }
}
