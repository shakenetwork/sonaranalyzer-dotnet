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

            x = a ?? b/*some other comment*/;

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

            X o = null;
            if (o == null) //Noncompliant, but there is no fix for it
            {
                x = new Y();
            }
            else
            {
                x = o;
            }

            var yyy = new Y();
            x = Identity(condition ? new Y() : yyy);

            if (condition) //Noncompliant
            {
                x = Identity(new Y());
            }
            else
            {
                x = Identity(new X());
            }

            Base elem;
            if (condition) //Noncompliant
            {
                elem = new A();
            }
            else
            {
                elem = new B();
            }

            x = Identity(condition ? new Y() : yyy);
        }
    }

    class X { }
    class Y { }

    class Base { }
    class A : Base { }
    class B : Base { }
}
