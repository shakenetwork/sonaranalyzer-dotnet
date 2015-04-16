using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class StaticFieldInGenericClassTest
    {
        [TestMethod]
        public void StaticFieldInGenericClass()
        {
            Verifier.Verify(@"TestCases\StaticFieldInGenericClass.cs", new StaticFieldInGenericClass());
        }
    }
}
