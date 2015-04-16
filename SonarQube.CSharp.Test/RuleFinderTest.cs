using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Descriptor;

namespace SonarQube.CSharp.Test
{
    [TestClass]
    public class RuleFinderTest
    {
        [TestMethod]
        public void GetRuleAssemblies()
        {
            RuleFinder.GetRuleAssemblies().Should().HaveCount(2);
        }

        [TestMethod]
        public void GetParameterlessAnalyzerTypes()
        {
            new RuleFinder().GetParameterlessAnalyzerTypes().Should().HaveCount(30);
        }
    }
}