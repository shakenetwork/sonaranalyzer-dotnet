using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class UnusedTypeParameterTest
    {
        [TestMethod]
        public void UnusedTypeParameter()
        {
            Verifier.Verify(@"TestCases\UnusedTypeParameter.cs", new UnusedTypeParameter());
        }
    }
}
