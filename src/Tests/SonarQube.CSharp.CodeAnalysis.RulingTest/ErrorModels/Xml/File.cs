using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.RulingTest.ErrorModels.Xml
{
    public class File
    {
        public File()
        {
            Issues = new List<Issue>();
        }

        public string Path { get; set; }
        public List<Issue> Issues { get; set; }
    }
}