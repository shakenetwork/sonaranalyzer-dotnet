using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class ReversedOperatorsTest
    {
        [TestMethod]
        public void ReversedOperators()
        {
            Verifier.Verify(@"TestCases\ReversedOperators.cs", new ReversedOperators());
        }
    }
}
