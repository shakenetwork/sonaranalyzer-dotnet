using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ObjectCreatedDroppedTest
    {
        [TestMethod]
        public void ObjectCreatedDropped()
        {
            Verifier.Verify(@"TestCases\ObjectCreatedDropped.cs", new ObjectCreatedDropped());            
        }
    }
}
