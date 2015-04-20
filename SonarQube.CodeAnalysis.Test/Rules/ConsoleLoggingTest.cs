using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CodeAnalysis.CSharp.Rules;

namespace SonarQube.CodeAnalysis.Test.Rules
{
    [TestClass]
    public class ConsoleLoggingTest
    {
        [TestMethod]
        public void ConsoleLogging()
        {
            Verifier.Verify(@"TestCases\ConsoleLogging.cs", new ConsoleLogging());
        }
    }
}
