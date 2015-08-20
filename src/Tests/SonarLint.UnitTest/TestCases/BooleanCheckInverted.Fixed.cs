using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class BooleanCheckInverted
    {
        public void Test()
        {
            var a = 2;
            if (a != 2) // Noncompliant
            {

            }
            bool b = a >= 10;  // Noncompliant
            b = a > 10;  // Noncompliant
            b = a <= 10;  // Noncompliant
            b = a < 10;  // Noncompliant
            b = a != 10;  // Noncompliant
            b = a == 10;  // Noncompliant


            if (a != 2)
            {
            }
            b = (a >= 10);
        }

    }
}
