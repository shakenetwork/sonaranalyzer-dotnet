using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class GetTypeWithIsAssignableFrom
    {
        void Test()
        {
            var expr1 = new GetTypeWithIsAssignableFrom();
            var expr2 = new GetTypeWithIsAssignableFrom();

            if (expr1.GetType().IsAssignableFrom(expr2.GetType())) //Noncompliant
            { }
            if (expr1.GetType().IsInstanceOfType(expr2)) //Compliant
            { }

            if (expr1.GetType().IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Noncompliant
            { }
            if (expr1 is GetTypeWithIsAssignableFrom) //Compliant
            { }

            if (typeof(GetTypeWithIsAssignableFrom).IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }

            var t1 = expr1.GetType();
            var t2 = expr2.GetType();
            if (t1.IsAssignableFrom(t2)) //Compliant
            { }
            if (t1.IsAssignableFrom(expr2.GetType())) //Noncompliant
            { }

            if (t1.IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }
        }
    }
}
