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

            x = a ?? b;

            x = a ?? b;  // Noncompliant; better but could still be simplified
            x = a != null ? a : a;  // Compliant, triggers S2758

            int i = 5;
            var z = i == null ? 4 : i; //can't be converted

            x = Identity(y ?? new object());  // Noncompliant

            x = a ?? b;
            x = a ?? b;
            x = y ?? new object();
            x = condition ? a : b;

            x = condition ? a : b;

            x = condition ? Identity(new object()) : IdentityAnyOtherMethod(y);

            Identity(condition ? new object() : y);

            return condition ? 1 : 2;

            if (condition)
                return 1;
            else if (condition) //Compliant
                return 2;
            else
                return 3;
        }
    }
}
