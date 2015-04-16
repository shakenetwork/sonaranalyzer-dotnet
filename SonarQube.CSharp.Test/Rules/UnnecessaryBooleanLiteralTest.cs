using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class UnnecessaryBooleanLiteralTest
    {
        [TestMethod]
        public void UnnecessaryBooleanLiteral()
        {
            Verifier.Verify(@"TestCases\UnnecessaryBooleanLiteral.cs", new UnnecessaryBooleanLiteral());
        }
    }
}
