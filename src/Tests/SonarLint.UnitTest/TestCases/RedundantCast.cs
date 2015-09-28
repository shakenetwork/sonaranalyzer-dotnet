using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public class RedundantCast
    {
        void foo(long l)
        {
            var x = new int[] {1, 2, 3}.Cast<int>(); // Noncompliant
            x = Enumerable // Noncompliant
                .Cast<int>(new int[] {1, 2, 3});
            x = x
                .OfType<int>(); //Noncompliant
            x = x.Cast<int>(); //Noncompliant

            var y = x.OfType<object>();

            var zz = (int) l;
            int i = 0;
            var z = (int) i; // Noncompliant
            z = (Int32) i; // Noncompliant

            var w = (object) i;

            method(new int[] { 1, 2, 3 }.Cast<int>()); // Noncompliant
        }
        void method(IEnumerable<int> enumerable)
        { }
    }
}
