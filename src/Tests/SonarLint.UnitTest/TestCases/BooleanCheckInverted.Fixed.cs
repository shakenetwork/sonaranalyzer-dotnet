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

        public static bool operator ==(BooleanCheckInverted a, BooleanCheckInverted b)
        {
            return false;
        }

        public static bool operator !=(BooleanCheckInverted a, BooleanCheckInverted b)
        {
            return !(a == b); // Compliant
        }
    }
}
