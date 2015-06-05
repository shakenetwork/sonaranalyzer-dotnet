using System.Collections.Generic;

namespace SonarQube.CSharp.CodeAnalysis.PerformanceTest.Expected
{
    public class Performance
    {
        public double BaseLine { get; set; }
        public Threshold Threshold { get; set; }

        public List<RulePerformance> Rules { get; set; }
    }
}
