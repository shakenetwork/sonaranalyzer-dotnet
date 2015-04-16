using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class TabCharacterTest
    {
        [TestMethod]
        public void TabCharacter()
        {
            Verifier.Verify(@"TestCases\TabCharacter.cs", new TabCharacter());
        }
    }
}
