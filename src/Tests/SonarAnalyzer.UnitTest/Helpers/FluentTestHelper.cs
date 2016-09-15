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

using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.UnitTest.Helpers
{
    internal static class FluentTestHelper
    {
        public static void OnlyContain<T, TAssertions>(this SelfReferencingCollectionAssertions<T, TAssertions> self, params T[] expected)
             where TAssertions : SelfReferencingCollectionAssertions<T, TAssertions>
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            self.Subject.Should().HaveCount(expected.Length);
            self.Subject.Should().Contain(expected);
        }

        public static void OnlyContainInOrder<T, TAssertions>(this SelfReferencingCollectionAssertions<T, TAssertions> self, params T[] expected)
             where TAssertions : SelfReferencingCollectionAssertions<T, TAssertions>
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            self.Subject.Should().HaveCount(expected.Length);
            self.Subject.Should().ContainInOrder(expected);
        }
    }
}
