using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class MyClass
    {
        public static MyClass operator+(MyClass c, string s)
        {
            return null;
        }
    }
    public class MyBase { }

    public class RedundantToStringCall : MyBase
    {
        public RedundantToStringCall()
        {
            var s = "foo";
            var t = "fee fie foe " + s;  // Noncompliant
            t = "fee fie foe " + s.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var u = "" + 1; //Noncompliant
            u = 1 + ""; // Noncompliant
            u = 1 // Noncompliant
                + 2.ToString(); // Noncompliant, note: this is why we don't have a global fix

            u += 2; // Noncompliant
            u = 1.ToString() + 2;

            var x = 1 + 3;

            var v = string.Format("{0}",
                1); //Noncompliant

            u = 1.ToString();

            var m = new MyClass();
            s = m.ToString() + "";

            s = base.ToString() + "";
        }
    }
}
