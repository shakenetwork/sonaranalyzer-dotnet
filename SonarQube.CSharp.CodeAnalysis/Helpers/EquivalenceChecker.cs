using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarQube.CSharp.CodeAnalysis.Helpers
{
    public static class EquivalenceChecker 
    {
        public static bool AreEquivalent(SyntaxNode node1, SyntaxNode node2)
        {
            return SyntaxFactory.AreEquivalent(node1, node2);
        }

        public static bool AreEquivalent(SyntaxList<SyntaxNode> nodeList1, SyntaxList<SyntaxNode> nodeList2)
        {
            if (nodeList1.Count != nodeList2.Count)
            {
                return false;
            }

            for (var i = 0; i < nodeList1.Count; i++)
            {
                if (!SyntaxFactory.AreEquivalent(nodeList1[i], nodeList2[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
