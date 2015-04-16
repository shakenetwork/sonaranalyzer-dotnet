using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class AssignmentInsideSubExpressionTest
    {
        [TestMethod]
        public void AssignmentInsideSubExpression()
        {
            Verifier.Verify(@"TestCases\AssignmentInsideSubExpression.cs", new AssignmentInsideSubExpression());
        }
    }
}
