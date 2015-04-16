using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class CommentedOutCodeTest
    {
        [TestMethod]
        public void CommentedOutCode()
        {
            Verifier.Verify(@"TestCases\CommentedOutCode.cs", new CommentedOutCode());
        }
    }
}
