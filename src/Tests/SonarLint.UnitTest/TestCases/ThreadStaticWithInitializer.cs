using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ThreadStaticWithInitializer
    {
        public class Foo
        {
            [ThreadStatic]
            public static object PerThreadObject = new object(); // Noncompliant. Will be null in all the threads except the first one.

            [ThreadStatic]
            public static object _perThreadObject;

            public static object StaticObject = new object();
        }
    }
}
