using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SonarQube.CSharp.CodeAnalysis.IntegrationTest;
using SonarQube.CSharp.CodeAnalysis.PerformanceTest.Expected;

namespace SonarQube.CSharp.CodeAnalysis.PerformanceTest
{
    public class PerformanceTestBase : IntegrationTestBase
    {
        public override void Setup()
        {
            base.Setup();
            ParseExpected();
        }

        protected Performance ExpectedPerformance;

        private void ParseExpected()
        {
            var expectedFile = ExpectedDirectory.GetFiles("performance.json").Single();
            ExpectedPerformance = JsonConvert.DeserializeObject<Performance>(File.ReadAllText(expectedFile.FullName));
        }
    }
}