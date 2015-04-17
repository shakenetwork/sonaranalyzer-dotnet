using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class BooleanCheckInvertedTest
    {
        [TestMethod]
        public void BooleanCheckInverted()
        {
            Verifier.Verify(@"TestCases\BooleanCheckInverted.cs", new BooleanCheckInverted());
        }
    }
}
