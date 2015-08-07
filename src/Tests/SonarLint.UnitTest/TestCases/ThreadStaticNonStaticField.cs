using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ThreadStaticNonStaticField
    {
        [ThreadStatic]  // Noncompliant
        private int count1 = 0, count11 = 0;

        [System.ThreadStatic]
        private static int count2 = 0;

        private int count3 = 0;
    }
}
