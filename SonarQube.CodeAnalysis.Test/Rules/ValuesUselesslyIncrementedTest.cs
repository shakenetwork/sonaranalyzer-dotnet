using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class ValuesUselesslyIncrementedTest
    {
        [TestMethod]
        public void ValuesUselesslyIncremented()
        {
            Verifier.Verify(@"TestCases\ValuesUselesslyIncremented.cs", new ValuesUselesslyIncremented());
        }
    }
}
