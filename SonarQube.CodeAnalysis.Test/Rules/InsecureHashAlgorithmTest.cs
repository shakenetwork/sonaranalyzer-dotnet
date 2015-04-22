using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class InsecureHashAlgorithmTest
    {
        [TestMethod]
        public void InsecureHashAlgorithm()
        {
            Verifier.Verify(@"TestCases\InsecureHashAlgorithm.cs", new InsecureHashAlgorithm());
        }
    }
}
