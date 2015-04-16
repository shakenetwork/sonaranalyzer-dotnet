using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class SwitchWithoutDefaultTest
    {
        [TestMethod]
        public void SwitchWithoutDefault()
        {
            Verifier.Verify(@"TestCases\SwitchWithoutDefault.cs", new SwitchWithoutDefault());
        }
    }
}
