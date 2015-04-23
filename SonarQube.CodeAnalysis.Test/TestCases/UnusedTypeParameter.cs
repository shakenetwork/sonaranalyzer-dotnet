using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class MoreMath<T> // Noncompliant; <T> is ignored
    {
        public int Add<T>(int a, int b) // Noncompliant; <T> is ignored
        {
            return a + b;
        }
    }

    public class MoreMath2<T> : List<T>
    {
        public T Property { get; set; }
        public int Add<T>(int a, int b) // Noncompliant; <T> is ignored
        {
            return a + b;
        }
    }
    public class MoreMath3<T> : MoreMath2<T>
    {
    }

    public class MoreMath4<T, T3> : MoreMath2<T> // Noncompliant
    {
    }

    public class MoreMath5<T, T3> : MoreMath2<Dictionary<string, List<T>>>
    {
        public List<T3> DoStuff<T3>(List<T3> o)
        {
            return o;
        }

        public T3 DoStuff<T, T3>(params T3[] o) // Noncompliant
        {
            return o[0];
        }
    }

    public class MoreMath
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public abstract int Do<T>(int a);
    }

    public class ComplexMath : MoreMath
    {
        public override int Do<T>(int a)
        {
            //don't use T here, but the method is still compliant because it is an override
        }
    }

    public partial class SomeClass<T>
    {
        private class Inner : List<T>
        {
        }
    }

    public partial class SomeClass<T>
    {
    }
}
