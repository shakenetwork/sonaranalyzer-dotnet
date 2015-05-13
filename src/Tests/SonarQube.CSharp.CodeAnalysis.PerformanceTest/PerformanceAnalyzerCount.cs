using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.CSharp.CodeAnalysis.PerformanceTest
{
    [TestClass]
    public class PerformanceAnalyzerCount : PerformanceTestBase
    {
        [TestInitialize]
        public override void Setup()
        {
            base.Setup();
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void All_Rules_Have_Performance_Test()
        {
            Assert.AreEqual(AnalyzerTypes.Count, ExpectedPerformance.Rules.Count);
        }
    }
}