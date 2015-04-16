using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class SequentialSameContitionTest
    {
        [TestMethod]
        public void SequentialSameContition()
        {
            Verifier.Verify(@"TestCases\SequentialSameContition.cs", new SequentialSameContition());
        }
    }
}
