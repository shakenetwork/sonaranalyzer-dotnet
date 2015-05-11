using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Omstar
{
    public class AnalysisOutput
    {
        public AnalysisOutput()
        {
            Issues = new List<Issue>();
        }

        public string Version { get; set; }
        public ToolInfo ToolInfo { get; set; }
        public List<Issue> Issues { get; set; }
    }
}