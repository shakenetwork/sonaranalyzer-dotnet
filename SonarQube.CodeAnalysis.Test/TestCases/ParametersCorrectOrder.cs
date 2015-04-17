using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public partial class ParametersCorrectOrder
    {
        partial void divide(int divisor, int someOther, int dividend, int p = 10, int some = 5, int other2 = 7);
    }

    public partial class ParametersCorrectOrder
    {
        partial void divide(int a, int b, int c, int p, int other, int other2)
        {
            var x = divisor / dividend;
        }

        public void m(int a, int b)
        {
        }

        public void doTheThing()
        {
            int divisor = 15;
            int dividend = 5;
            var something = 6;
            var someOther = 6;
            var other2 = 6;
            var some = 6;

            divide(dividend, 1 + 1, divisor, other2: 6);  // Noncompliant; operation succeeds, but result is unexpected

            divide(divisor, other2, dividend);
            divide(divisor, other2, dividend, other2: someOther); // Noncompliant;

            divide(divisor, someOther, dividend, other2: some, some: other2); // Noncompliant;

            divide(1, 1, 1, other2: some, some: other2); // Noncompliant;
            divide(1, 1, 1, other2: 1, some: other2); 

            int a, b;

            m(1, a); // Compliant
            m(1, b);
            m(b, b);
            m(divisor, dividend);

            m(a, b);
            m(b, b); // Compliant
            m(b, a); // Noncompliant
        }
    }
    
}
