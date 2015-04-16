using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ForLoopCounterConditionTest
    {
        [TestMethod]
        public void ForLoopCounterCondition()
        {
            Verifier.Verify(@"TestCases\ForLoopCounterCondition.cs", new ForLoopCounterCondition());
        }
    }
}
