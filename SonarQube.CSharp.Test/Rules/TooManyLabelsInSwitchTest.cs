using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class TooManyLabelsInSwitchTest
    {
        [TestMethod]
        public void TooManyLabelsInSwitch()
        {
            var diagnostic = new TooManyLabelsInSwitch {Maximum = 2};
            Verifier.Verify(@"TestCases\TooManyLabelsInSwitch.cs", diagnostic);
        }
    }
}
