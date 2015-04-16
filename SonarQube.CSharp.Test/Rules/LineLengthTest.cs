using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class LineLengthTest
    {
        [TestMethod]
        public void LineLength()
        {
            var diagnostic = new LineLength {Maximum = 47};
            Verifier.Verify(@"TestCases\LineLength.cs", diagnostic);
        }
    }
}
