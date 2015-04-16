using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class EmptyMethodTest
    {
        [TestMethod]
        public void EmptyMethod()
        {
            Verifier.Verify(@"TestCases\EmptyMethod.cs", new EmptyMethod());
        }
    }
}
