using System;
using Newtonsoft.Json;

namespace SonarQube.CSharp.CodeAnalysis.PerformanceTest.Expected
{
    public class RulePerformance
    {
        public string RuleId { get; set; }
        public double Performance { get; set; }
    }
}