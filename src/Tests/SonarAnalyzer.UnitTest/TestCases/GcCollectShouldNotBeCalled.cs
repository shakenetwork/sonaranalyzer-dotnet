using System;

namespace Tests.Diagnostics
{
    using static System.GC;

    class Program
    {
        void Foo()
        {
            GC.Collect(); // Noncompliant
            GC.Collect(2, GCCollectionMode.Optimized); // Noncompliant
            ((((((GC))).Collect()))); // Noncompliant

            Collect(); // Noncompliant
        }
    }
}
