using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    struct Dummy
    { }

    class FieldInitializedToDefault<T>
    {
        public int field = 0; // Noncompliant
        public float fieldF = 0.0; // Noncompliant
        public char fieldC = '\0'; // Noncompliant
        public long fieldL = 0L; // Noncompliant
        public long fieldL2 = default(long); // Noncompliant
        public object o = null; // Noncompliant

        public int field2;
        public object o2;

        public T gen = default(T); // Noncompliant
        public Dummy stru = default(Dummy); // Noncompliant
    }
}
