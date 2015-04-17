using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class ParametersCorrectOrderTest
    {
        [TestMethod]
        public void ParametersCorrectOrder()
        {
            Verifier.Verify(@"TestCases\ParametersCorrectOrder.cs", new ParametersCorrectOrder());
        }
    }
}