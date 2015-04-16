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
    public class DiagnosticReportHelperTest
    {
        private const string Source = 
@"namespace Test
{
    class TestClass
    {   
    }
}";
        private Solution solution;
        private Compilation compilation;
        private SyntaxTree syntaxTree;

        [TestInitialize]
        public void TestSetup()
        {
            solution = CompilationHelper.GetSolutionFromText(Source);

            compilation = solution.Projects.First().GetCompilationAsync().Result;
            syntaxTree = compilation.SyntaxTrees.First();
        }
        
        [TestMethod]
        public void GetLineNumberToReport()
        {
            var method = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            method.GetLineNumberToReport().ShouldBeEquivalentTo(3);
        }
    }
}
