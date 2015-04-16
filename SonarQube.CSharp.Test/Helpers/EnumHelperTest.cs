using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.Test.Helpers
{
    [TestClass]
    public class EnumHelperTest
    {
        [TestMethod]
        public void ToSonarQubeString()
        {
            SqaleSubCharacteristic.ApiAbuse.ToSonarQubeString().Should().BeEquivalentTo("API_ABUSE");
        }
    }
}
