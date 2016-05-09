using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public class RedundantCast
    {
        void foo(long l)
        {
            var x = new int[] {1, 2, 3}; // Noncompliant
            x = new int[] {1, 2, 3};
            x = x
; //Noncompliant
            x = x; //Noncompliant

            var y = x.OfType<object>();

            var zz = (int) l;
            int i = 0;
            var z = (int) i; // Noncompliant
            z = (Int32) i; // Noncompliant

            var w = (object) i;

            method(new int[] { 1, 2, 3 }); // Noncompliant
        }
        void method(IEnumerable<int> enumerable)
        { }

        void M()
        {
            var o = new object();
            var oo = o; // Noncompliant
            var i = o as RedundantCast; // Compliant
        }
    }
}
