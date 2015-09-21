using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    abstract class Base
    {
        public virtual void Method(params int[] numbers)
        {
        }
        public virtual void Method(string s,
            params int[] numbers)
        {
        }
        public abstract void Method(string s, string s1,
            params int[] numbers);
    }
    abstract class Derived : Base
    {
        public override void Method(params int[] numbers) // Noncompliant, the params is missing.
        {
        }
        public override void Method(string s,
            params int[] numbers) // Noncompliant
        {
        }
        public override void Method(string s, string s1,
            params int[] numbers) // Noncompliant
        { }
    }

    abstract class Derived2 : Base
    {
        public override void Method(params int[] numbers)
        {
        }
    }
}
