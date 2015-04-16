using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ConditionalStructureSameConditionTest
    {
        [TestMethod]
        public void ConditionalStructureSameCondition()
        {
            Verifier.Verify(@"TestCases\ConditionalStructureSameCondition.cs", new ConditionalStructureSameCondition());
        }
    }
}
