using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class BreakOutsideSwitchTest
    {
        [TestMethod]
        public void BreakOutsideSwitch()
        {
            Verifier.Verify(@"TestCases\BreakOutsideSwitch.cs", new BreakOutsideSwitch());
        }
    }
}
