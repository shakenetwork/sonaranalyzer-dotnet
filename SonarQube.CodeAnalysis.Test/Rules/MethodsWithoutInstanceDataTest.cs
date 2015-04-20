using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class MethodsWithoutInstanceDataTest
    {
        [TestMethod]
        public void MethodsWithoutInstanceData()
        {
            Verifier.Verify(@"TestCases\MethodsWithoutInstanceData.cs", new MethodsWithoutInstanceData());
        }
    }
}
