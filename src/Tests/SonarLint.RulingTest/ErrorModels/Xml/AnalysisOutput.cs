using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.RulingTest.ErrorModels.Xml
{
    public class AnalysisOutput
    {
        public AnalysisOutput()
        {
            Files = new List<File>();
        }

        public List<File> Files { get; private set; }
    }
}