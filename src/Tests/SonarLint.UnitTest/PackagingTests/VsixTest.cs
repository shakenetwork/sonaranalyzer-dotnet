/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using SonarLint.DocGenerator;
using SonarLint.Helpers;
using SonarLint.Utilities;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarLint.UnitTest.PackagingTests
{
    [TestClass]
    public class VsixTest
    {
        [TestMethod]
        [TestCategory("Vsix")]
        public void Size_Check()
        {
            const string vsixFileName = "SonarLint.vsix";
#if DEBUG
            const string pathEnding = @"bin\Debug";
            const int approxFileSize = 1215 * 1024;
#else
            const string pathEnding = @"bin\Release";
            const int approxFileSize = 290 * 1024;
#endif

            var currentDirectory = Directory.GetCurrentDirectory();
            var vsixDirectoryPath = Path.GetFullPath(Path.Combine(currentDirectory, @"..\..\..\..\SonarLint.Vsix\", pathEnding));
            var vsixFile = new FileInfo(Path.Combine(vsixDirectoryPath, vsixFileName));

            if (!vsixFile.Exists)
            {
                Assert.Fail("VSIX file doesn't exist");
            }

            var upperBound = approxFileSize * 1.1;
            if (vsixFile.Length > upperBound)
            {
                Assert.Fail(string.Format("VSIX file is larger then {0}KB", upperBound));
            }

            var lowerBound = approxFileSize * 0.9;
            if (vsixFile.Length < lowerBound)
            {
                Assert.Fail(string.Format("VSIX file is smaller then {0}KB", lowerBound));
            }
        }
    }
}
