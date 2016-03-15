using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;

namespace Tests.Diagnostics
{
    class ReturnValueIgnored
    {
        [Pure]
        int Method() { }
        [Pure]
        int Method(ref int i) { }

        void Test()
        {
            new int[] { 1 }.Where(i => true); // Noncompliant

            var k = 0;
            new int[] { 1 }.Where(i => { k++; return true; }); // Noncompliant, although it has side effect

            new int[] { 1 }.ToList().ForEach(i => { });

            new int[] { 1 }.ToList(); // Noncompliant
            new int[] { 1 }.OfType<object>(); // Noncompliant

            "this string".Equals("other string"); // Noncompliant
            M("this string".Equals("other string"));

            "this string".Equals(new object()); // Noncompliant
            Method(); // Noncompliant

            1.ToString(); // Noncompliant

            int j = 1;
            Method(ref j);

            Action<int> a = (input) => "this string".Equals("other string"); // Noncompliant
            Func<int, bool> a = (input) => "this string".Equals("other string");
        }
        void M(object o) { }
    }
}
