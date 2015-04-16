using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class ConditionalStructureSameImplementationTest
    {
        [TestMethod]
        public void ConditionalStructureSameImplementation_If()
        {
            Verifier.Verify(@"TestCases\ConditionalStructureSameImplementation_If.cs", new ConditionalStructureSameImplementation());
        }

        [TestMethod]
        public void ConditionalStructureSameImplementation_Switch()
        {
            Verifier.Verify(@"TestCases\ConditionalStructureSameImplementation_Switch.cs", new ConditionalStructureSameImplementation());
        }
    }
}
