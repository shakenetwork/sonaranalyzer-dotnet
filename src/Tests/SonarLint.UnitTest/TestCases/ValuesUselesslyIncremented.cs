using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ValuesUselesslyIncremented
    {
        public int pickNumber()
        {
            int i = 0;
            int j = 0;

            i = i++; // Noncompliant; i is still zero

            return j++; // Noncompliant; 0 returned
        }

        public int pickNumber2()
        {
            int i = 0;
            int j = 0;

            i++;
            return ++j;
        }
    }
}
