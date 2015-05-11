using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Omstar
{
    public class Issue
    {
        public Issue()
        {
            Locations = new List<IssueLocation>();
        }

        public string RuleId { get; set; }
        public string FullMessage { get; set; }

        public List<IssueLocation> Locations { get; set; }
        public IssueProperties Properties { get; set; }
    }
}