using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class IdenticalExpressionsInBinaryOpTest
    {
        [TestMethod]
        public void IdenticalExpressionsInBinaryOp()
        {
            Verifier.Verify(@"TestCases\IdenticalExpressionsInBinaryOp.cs", new IdenticalExpressionsInBinaryOp());
        }
    }
}
