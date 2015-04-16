using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class SelfAssignedVariablesTest
    {
        [TestMethod]
        public void SelfAssignedVariables()
        {
            Verifier.Verify(@"TestCases\SelfAssignedVariables.cs", new SelfAssignedVariables());
        }
    }
}
