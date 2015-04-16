using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.Runner;

namespace SonarQube.CSharp.Test.Helpers
{
    [TestClass]
    public class EquivalenceCheckerTest
    {
        private const string Source = @"
namespace Test
{
    class TestClass
    {
        int Property {get;set;}
        public void Method1()
        {
            var x = Property;
            Console.WriteLine(x);
        }

        public void Method2()
        {
            var x = Property;
            Console.WriteLine(x);
        }

        public void Method3()
        {
            var x = Property+2;
            Console.Write(x);            
        }
    }
}";

        private Solution solution;
        private Compilation compilation;
        private SyntaxTree syntaxTree;
        private SemanticModel semanticModel;
        private List<MethodDeclarationSyntax> methods;

        [TestInitialize]
        public void TestSetup()
        {
            solution = CompilationHelper.GetSolutionFromText(Source);

            compilation = solution.Projects.First().GetCompilationAsync().Result;
            syntaxTree = compilation.SyntaxTrees.First();
            semanticModel = compilation.GetSemanticModel(syntaxTree);

            methods = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        }
        
        [TestMethod]
        public void AreEquivalent_Node()
        {
            var result = EquivalenceChecker.AreEquivalent(
                methods.First(m => m.Identifier.ValueText == "Method1").Body,
                methods.First(m => m.Identifier.ValueText == "Method2").Body);
            result.Should().BeTrue();

            result = EquivalenceChecker.AreEquivalent(
                methods.First(m => m.Identifier.ValueText == "Method1").Body,
                methods.First(m => m.Identifier.ValueText == "Method3").Body);
            result.Should().BeFalse();
        }

        [TestMethod]
        public void AreEquivalent_List()
        {
            var result = EquivalenceChecker.AreEquivalent(
                methods.First(m => m.Identifier.ValueText == "Method1").Body.Statements,
                methods.First(m => m.Identifier.ValueText == "Method2").Body.Statements);
            result.Should().BeTrue();

            result = EquivalenceChecker.AreEquivalent(
                methods.First(m => m.Identifier.ValueText == "Method1").Body.Statements,
                methods.First(m => m.Identifier.ValueText == "Method3").Body.Statements);
            result.Should().BeFalse();
        }
    }
}
