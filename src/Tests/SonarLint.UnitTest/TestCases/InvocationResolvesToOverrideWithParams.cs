using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class InvocationResolvesToOverrideWithParams
    {
        public static void Test(int foo, params object[] p)
        {
            Console.WriteLine("test1");
        }

        public static void Test(double foo, object p1)
        {
            Console.WriteLine("test2");
        }

        static void Main(string[] args)
        {
            Test(42, null); // Noncompliant
        }

        public InvocationResolvesToOverrideWithParams(string a, params object[] b)
        {

        }
        public InvocationResolvesToOverrideWithParams(object a, object b, object c)
        {

        }

        private void Format(string a, params object[] b) { }

        private void Format(object a, object b, object c, object d = null) { }

        private void Format2(string a, params object[] b) { }

        private void Format2(int a, object b, object c) { }

        private void Format3(params int[] a) { }

        private void Format3(IEnumerable<int> a) { }

        private void Format4(params object[] a) { }

        private void Format4(object o, IEnumerable<object> a) { }

        private void m()
        {
            Format("", null, null); //Noncompliant
            Format(new object(), null, null);
            Format("", new object[0]);

            Format2("", null, null); //Compliant

            new InvocationResolvesToOverrideWithParams("", null, null); //Noncompliant
            new InvocationResolvesToOverrideWithParams(new object(), null, null);

            Format3(new int[0]); //Compliant, although it is also an IEnumerable<int>

            Format4(new object(), new int[0]); //Noncompliant

            Format3(null); //Noncompliant, maybe it could be compliant
            string.Concat("aaaa"); //Noncompliant, resolves to params, but there's a single object version too.
        }
    }
}
