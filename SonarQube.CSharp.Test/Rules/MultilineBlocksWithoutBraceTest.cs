using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class MultilineBlocksWithoutBraceTest
    {
        [TestMethod]
        public void MultilineBlocksWithoutBrace()
        {
            Verifier.Verify(@"TestCases\MultilineBlocksWithoutBrace.cs", new MultilineBlocksWithoutBrace());
        }
    }
}
