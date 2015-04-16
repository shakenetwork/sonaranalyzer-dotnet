using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class RightCurlyBraceStartsLineTest
    {
        [TestMethod]
        public void RightCurlyBraceStartsLine()
        {
            Verifier.Verify(@"TestCases\RightCurlyBraceStartsLine.cs", new RightCurlyBraceStartsLine());
        }
    }
}
