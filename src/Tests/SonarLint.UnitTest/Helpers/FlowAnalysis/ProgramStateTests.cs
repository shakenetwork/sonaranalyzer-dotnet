/*
 * SonarLint for Visual Studio
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

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Helpers.FlowAnalysis.Common;

namespace SonarLint.UnitTest.Helpers
{
    [TestClass]
    public class ProgramStateTests
    {
        private static ISymbol GetSymbol()
        {
            string testInput = "var a = true; var b = false; b = !b; a = (b);";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(ControlFlowGraphTest.TestInput, testInput), "Bar", out semanticModel);
            return semanticModel.GetDeclaredSymbol(method);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramState_Equivalence()
        {
            var ps1 = new ProgramState();
            var ps2 = new ProgramState();

            var sv = new SymbolicValue();
            var symbol = GetSymbol();
            ps1 = ps1.SetSymbolicValue(symbol, sv);
            ps2 = ps2.SetSymbolicValue(symbol, sv);

            Assert.AreEqual(ps1, ps2);
            Assert.AreEqual(ps1.GetHashCode(), ps2.GetHashCode());
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramState_Diff_SymbolicValue()
        {
            var ps1 = new ProgramState();
            var ps2 = new ProgramState();

            var symbol = GetSymbol();
            ps1 = ps1.SetSymbolicValue(symbol, new SymbolicValue());
            ps2 = ps2.SetSymbolicValue(symbol, new SymbolicValue());

            Assert.AreNotEqual(ps1, ps2);
            Assert.AreNotEqual(ps1.GetHashCode(), ps2.GetHashCode());
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramState_Diff_Symbol()
        {
            var ps1 = new ProgramState();
            var ps2 = new ProgramState();

            var sv = new SymbolicValue();
            ps1 = ps1.SetSymbolicValue(GetSymbol(), sv);
            ps2 = ps2.SetSymbolicValue(GetSymbol(), sv);

            Assert.AreNotEqual(ps1, ps2);
            Assert.AreNotEqual(ps1.GetHashCode(), ps2.GetHashCode());
        }
    }
}
