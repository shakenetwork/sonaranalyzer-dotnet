using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Rules;

namespace SonarQube.CSharp.Test.Rules
{
    [TestClass]
    public class CommentRegularExpressionTest
    {
        [TestMethod]
        public void CommentRegularExpression()
        {
            var rules = ImmutableArray.Create(
                new CommentRegularExpressionRule
                {
                    Descriptor = CodeAnalysis.Rules.CommentRegularExpression.CreateDiagnosticDescriptor("id1", ""),
                    RegularExpression = "(?i)TODO"
                });

            var diagnostic = new CommentRegularExpression {Rules = rules};
            Verifier.Verify(@"TestCases\CommentRegularExpression.cs", diagnostic);
        }
    }
}
