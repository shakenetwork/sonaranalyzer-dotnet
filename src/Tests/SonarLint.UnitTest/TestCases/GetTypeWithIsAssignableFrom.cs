using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class GetTypeWithIsAssignableFrom
    {
        void Test(bool b)
        {
            var expr1 = new GetTypeWithIsAssignableFrom();
            var expr2 = new GetTypeWithIsAssignableFrom();

            if (expr1.GetType()/*abcd*/.IsAssignableFrom(expr2.GetType() /*efgh*/)) //Noncompliant
            { }
            if (expr1.GetType().IsInstanceOfType(expr2)) //Compliant
            { }

            if (!typeof(GetTypeWithIsAssignableFrom).IsAssignableFrom(expr1.GetType())) //Noncompliant
            { }
            var x = typeof(GetTypeWithIsAssignableFrom).IsAssignableFrom(expr1.GetType()); //Noncompliant
            if (expr1 is GetTypeWithIsAssignableFrom) //Compliant
            { }

            if (typeof(GetTypeWithIsAssignableFrom).IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }

            var t1 = expr1.GetType();
            var t2 = expr2.GetType();
            if (t1.IsAssignableFrom(t2)) //Compliant
            { }
            if (t1.IsAssignableFrom(c: expr2.GetType())) //Noncompliant
            { }

            if (t1.IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }

            Test(t1.IsAssignableFrom(c: expr2.GetType())); //Noncompliant
        }
    }
    class Fruit { }
    sealed class Apple : Fruit { }

    class Program
    {
        static void Main()
        {
            var apple = new Apple();
            var b = apple.GetType() == typeof(Apple); // Noncompliant
            b = typeof(Apple).IsInstanceOfType(apple); // Noncompliant
            b = typeof(Apple).IsAssignableFrom(apple.GetType()); // Noncompliant
            b = typeof(Apple).IsInstanceOfType(apple); // Noncompliant
            var appleType = typeof(Apple);
            b = appleType.IsAssignableFrom(apple.GetType()); // Noncompliant

            b = apple.GetType() == typeof(int?); // Compliant
        }
    }
}
