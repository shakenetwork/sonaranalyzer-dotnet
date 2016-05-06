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

            if (expr1.GetType()/*abcd*/.IsInstanceOfType(expr2 /*efgh*/)) //Noncompliant
            { }
            if (expr1.GetType().IsInstanceOfType(expr2)) //Compliant
            { }

            if (!(expr1 is GetTypeWithIsAssignableFrom)) //Noncompliant
            { }
            var x = expr1 is GetTypeWithIsAssignableFrom; //Noncompliant
            if (expr1 is GetTypeWithIsAssignableFrom) //Compliant
            { }

            if (typeof(GetTypeWithIsAssignableFrom).IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }

            var t1 = expr1.GetType();
            var t2 = expr2.GetType();
            if (t1.IsAssignableFrom(t2)) //Compliant
            { }
            if (t1.IsInstanceOfType(expr2)) //Noncompliant
            { }

            if (t1.IsAssignableFrom(typeof(GetTypeWithIsAssignableFrom))) //Compliant
            { }

            Test(t1.IsInstanceOfType(expr2)); //Noncompliant
        }
    }
    class Fruit { }
    sealed class Apple : Fruit { }

    class Program
    {
        static void Main()
        {
            var apple = new Apple();
            var b = apple is Apple; // Noncompliant
            b = apple is Apple; // Noncompliant
            b = apple is Apple; // Noncompliant
            b = apple is Apple; // Noncompliant
            var appleType = typeof(Apple);
            b = appleType.IsInstanceOfType(apple); // Noncompliant

            b = apple.GetType() == typeof(int?); // Compliant

            Fruit f = apple;
            b = true && (f is Apple); // Noncompliant
            b = !(f is Apple); // Noncompliant
            b = f as Apple == new Apple();
        }
    }
}
