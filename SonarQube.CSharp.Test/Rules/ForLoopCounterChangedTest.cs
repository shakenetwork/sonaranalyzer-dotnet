using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ForLoopCounterChangedTest
    {
        [TestMethod]
        public void ForLoopCounterChanged()
        {
            Verifier.Verify(@"TestCases\ForLoopCounterChanged.cs", new ForLoopCounterChanged());
        }
    }
}
