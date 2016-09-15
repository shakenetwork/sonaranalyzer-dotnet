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

using System;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;

namespace SonarAnalyzer.UnitTest.Helpers
{
    [TestClass]
    public class ProgramStateTests
    {
        private class FakeConstraint : SymbolicValueConstraint
        {
            public override SymbolicValueConstraint OppositeForLogicalNot => null;
        }

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
            var constraint = new FakeConstraint();
            var symbol = GetSymbol();
            ps1 = ps1.SetSymbolicValue(symbol, sv);
            ps1 = sv.SetConstraint(constraint, ps1);
            ps2 = ps2.SetSymbolicValue(symbol, sv);
            ps2 = sv.SetConstraint(constraint, ps2);

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
        public void ProgramState_Diff_Constraint()
        {
            var ps1 = new ProgramState();
            var ps2 = new ProgramState();

            var symbol = GetSymbol();
            var sv = new SymbolicValue();
            ps1 = ps1.SetSymbolicValue(symbol, sv);
            ps1 = sv.SetConstraint(new FakeConstraint(), ps1);
            ps2 = ps2.SetSymbolicValue(symbol, sv);
            ps2 = sv.SetConstraint(new FakeConstraint(), ps2);

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

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramState_Constraint()
        {
            var ps = new ProgramState();
            var sv = new SymbolicValue();
            var symbol = GetSymbol();
            var constraint = new FakeConstraint();

            ps = ps.SetSymbolicValue(symbol, sv);
            ps = sv.SetConstraint(constraint, ps);
            Assert.IsTrue(symbol.HasConstraint(constraint, ps));
            Assert.IsFalse(symbol.HasConstraint(new FakeConstraint(), ps));
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramState_NotNull_Bool_Constraint()
        {
            var ps = new ProgramState();
            var sv = new SymbolicValue();
            var symbol = GetSymbol();

            ps = ps.SetSymbolicValue(symbol, sv);
            ps = sv.SetConstraint(BoolConstraint.True, ps);
            Assert.IsTrue(symbol.HasConstraint(BoolConstraint.True, ps));
            Assert.IsTrue(symbol.HasConstraint(ObjectConstraint.NotNull, ps));

            ps = ps.SetSymbolicValue(symbol, sv);
            ps = sv.SetConstraint(BoolConstraint.False, ps);
            Assert.IsTrue(symbol.HasConstraint(BoolConstraint.False, ps));
            Assert.IsTrue(symbol.HasConstraint(ObjectConstraint.NotNull, ps));

            ps = ps.SetSymbolicValue(symbol, sv);
            ps = sv.SetConstraint(ObjectConstraint.NotNull, ps);
            Assert.IsFalse(symbol.HasConstraint(BoolConstraint.False, ps));
            Assert.IsTrue(symbol.HasConstraint(ObjectConstraint.NotNull, ps));
        }
    }
}
