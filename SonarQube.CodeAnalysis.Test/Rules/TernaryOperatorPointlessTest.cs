using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class TernaryOperatorPointlessTest
    {
        [TestMethod]
        public void TernaryOperatorPointless()
        {
            Verifier.Verify(@"TestCases\TernaryOperatorPointless.cs", new TernaryOperatorPointless());
        }
    }
}
