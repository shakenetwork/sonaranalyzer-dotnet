using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ShortCircuitNullPointerDereferenceTest
    {
        [TestMethod]
        public void ShortCircuitNullPointerDereference()
        {
            Verifier.Verify(@"TestCases\ShortCircuitNullPointerDereference.cs", new ShortCircuitNullPointerDereference());
        }
    }
}
