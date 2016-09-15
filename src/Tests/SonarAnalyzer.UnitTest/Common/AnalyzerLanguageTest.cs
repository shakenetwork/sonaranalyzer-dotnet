/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Common;
using System;

namespace SonarAnalyzer.UnitTest.Common
{
    [TestClass]
    public class AnalyzerLanguageTest
    {
        [TestMethod]
        public void AnalyzerLanguage_Parse()
        {
            var parsed = AnalyzerLanguage.Parse("cs");
            Assert.AreEqual(AnalyzerLanguage.CSharp, parsed);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void AnalyzerLanguage_Parse_Fail()
        {
            var parsed = AnalyzerLanguage.Parse("csharp");
            Assert.AreEqual(AnalyzerLanguage.CSharp, parsed);
        }

        [TestMethod]
        public void AnalyzerLanguage_GetDirectory()
        {
            Assert.AreEqual("CSharp", AnalyzerLanguage.CSharp.GetDirectoryName());
            Assert.AreEqual("VisualBasic", AnalyzerLanguage.VisualBasic.GetDirectoryName());
        }

        [TestMethod]
        public void AnalyzerLanguage_GetQualityProfileRepositoryKey()
        {
            Assert.AreEqual("csharpsquid", AnalyzerLanguage.CSharp.GetQualityProfileRepositoryKey());
            Assert.AreEqual("vbnet", AnalyzerLanguage.VisualBasic.GetQualityProfileRepositoryKey());
        }

        [TestMethod]
        public void AnalyzerLanguage_Operations()
        {
            Assert.AreEqual(AnalyzerLanguage.Both, AnalyzerLanguage.CSharp.AddLanguage(AnalyzerLanguage.VisualBasic));
            Assert.AreEqual(AnalyzerLanguage.CSharp, AnalyzerLanguage.CSharp.AddLanguage(AnalyzerLanguage.CSharp));
            Assert.AreEqual(AnalyzerLanguage.Both, AnalyzerLanguage.CSharp.AddLanguage(AnalyzerLanguage.Both));

            Assert.AreEqual(true, AnalyzerLanguage.CSharp.IsAlso(AnalyzerLanguage.CSharp));
            Assert.AreEqual(false, AnalyzerLanguage.CSharp.IsAlso(AnalyzerLanguage.VisualBasic));
            Assert.AreEqual(true, AnalyzerLanguage.Both.IsAlso(AnalyzerLanguage.VisualBasic));
        }
    }
}
