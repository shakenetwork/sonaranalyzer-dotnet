using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class EmptyCatchTest
    {
        [TestMethod]
        public void EmptyCatch()
        {
            Verifier.Verify(@"TestCases\EmptyCatch.cs", new EmptyCatch());
        }
    }
}
