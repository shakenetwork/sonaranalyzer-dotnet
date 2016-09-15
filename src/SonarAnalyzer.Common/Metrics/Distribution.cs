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

using System.Globalization;
using System.Linq;

namespace SonarAnalyzer.Common
{
    public class Distribution
    {
        // TODO Do we want this to be immutable?
        public int[] Ranges { private set; get; }

        public int[] Values { private set; get; }

        public Distribution(params int[] ranges)
        {
            // TODO Check not empty, and sorted
            Ranges = ranges;
            Values = new int[ranges.Length];
        }

        public void Add(int value)
        {
            var i = Ranges.Length - 1;

            while (i > 0 && value < Ranges[i])
            {
                i--;
            }

            Values[i]++;
        }

        public override string ToString()
        {
            return string.Join(";",
                Ranges.Zip(Values, (r, v) => string.Format(CultureInfo.InvariantCulture, "{0}={1}", r, v)));
        }
    }
}
