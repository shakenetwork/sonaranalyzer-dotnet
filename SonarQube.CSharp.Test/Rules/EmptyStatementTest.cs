using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class EmptyStatementTest
    {
        [TestMethod]
        public void EmptyStatement()
        {
            Verifier.Verify(@"TestCases\EmptyStatement.cs", new EmptyStatement());
        }
    }
}
