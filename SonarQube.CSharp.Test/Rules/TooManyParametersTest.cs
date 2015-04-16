using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class TooManyParametersTest
    {
        [TestMethod]
        public void TooManyParameters()
        {
            var diagnostic = new TooManyParameters {Maximum = 3};
            Verifier.Verify(@"TestCases\TooManyParameters.cs", diagnostic);
        }
    }
}
