using System;

namespace Tests.Diagnostics
{
    class Program
    {
        void Foo()
        {
            GC.Collect(); // Noncompliant
            GC.Collect(2, GCCollectionMode.Optimized); // Noncompliant
            ((((((GC))).Collect()))); // Noncompliant
        }
    }
}
