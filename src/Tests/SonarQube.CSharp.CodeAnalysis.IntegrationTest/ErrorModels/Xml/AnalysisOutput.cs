using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.IntegrationTest.ErrorModels.Xml
{
    public class AnalysisOutput
    {
        public AnalysisOutput()
        {
            Files = new List<File>();
        }

        public List<File> Files { get; set; }
    }
}