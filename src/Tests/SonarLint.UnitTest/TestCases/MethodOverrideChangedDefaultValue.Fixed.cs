using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public interface IMyInterface
    {
        void Write(int i, int j = 5);
    }

    public class Base : IMyInterface
    {
        public virtual void Write(int i, int j = 5) // Noncompliant
        {
            Console.WriteLine(i);
        }
    }

    public class Derived1 : Base
    {
        public override void Write(int i,
            int j = 5) // Noncompliant
        {
            Console.WriteLine(i);
        }
    }
    public class Derived2 : Base
    {
        public override void Write(int i,
            int j = 5) // Noncompliant
        {
            Console.WriteLine(i);
        }
    }
    public class Derived3 : Base
    {
        public override void Write(int i,  // Noncompliant
            int j = 5)
        {
            Console.WriteLine(i);
        }
    }
}
