using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class AtLeastThreeCasesInSwitchTest
    {
        [TestMethod]
        public void AtLeastThreeCasesInSwitch()
        {
            Verifier.Verify(@"TestCases\AtLeastThreeCasesInSwitch.cs", new AtLeastThreeCasesInSwitch());
        }
    }
}
