using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestCases
{
    class ConditionalSimplification
    {
        object Identity(object o)
        {
            return o;
        }
        object IdentityAnyOtherMethod(object o  )
        {
            return o;
        }
        int Test(object a, object b, object y, bool condition)
        {
            object x;

            if (a != null) // Noncompliant; needlessly verbose
            {
                x = a;
            }
            else
            {
                x = b;
            }

            x = a != null ? (a) : b;  // Noncompliant; better but could still be simplified
            x = a != null ? a : a;  // Compliant, triggers S2758

            int i = 5;
            var z = i == null ? 4 : i; //can't be converted

            x = (y == null) ? Identity(new object()) : Identity(y);  // Noncompliant

            x = a ?? b;
            x = a ?? b;
            x = y ?? new object();
            x = condition ? a : b;

            if (condition) // Noncompliant
            {
                x = a;
            }
            else
            {
                x = b;
            }

            if (condition) // Noncompliant
            {
                x = Identity(new object());
            }
            else
            {
                x = IdentityAnyOtherMethod(y);
            }

            if (condition) // Noncompliant
            {
                Identity(new object());
            }
            else
            {
                Identity(y);
            }

            if (condition) // Noncompliant
            {
                return 1;
            }
            else
            {
                return 2;
            }

            if (condition)
                return 1;
            else if (condition) //Compliant
                return 2;
            else
                return 3;
        }
    }
}
