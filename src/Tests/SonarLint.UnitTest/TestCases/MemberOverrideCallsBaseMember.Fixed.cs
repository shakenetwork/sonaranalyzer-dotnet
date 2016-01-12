using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class Base
    {
        public virtual int MyProperty { get; set; }
        public virtual int MyProperty1 { get; set; }
        public virtual int MyProperty2 { get; }
        public virtual int MyProperty3 { get; }

        public virtual void Method(int[] numbers)
        {
        }
        public virtual int Method2(int[] numbers)
        {
            return 1;
        }
        public virtual int Method3(int[] numbers)
        {
            return 1;
        }
        public virtual int Method4(int[] numbers)
        {
            return 1;
        }
        public virtual void Method(string s1, string s2)
        {
        }
        public virtual void Method2(string s1, string s2)
        {
        }
        public virtual void Method(int i, int[] numbers)
        {
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    class Derived : Base
    {
        public override int MyProperty3 => 42;

        public Base bbb;
        public override int Method4(int[] numbers2) => base.Method4(numbers2);

        public override void Method(string s1, string s2)
        {
            base.Method(s2, s1);
        }
        public override void Method2(string s1, string s2)
        {
            bbb.Method2(s1, s2);
        }
        public override sealed void Method(int i, int[] numbers)
        {
            base.Method(i, numbers);
        }
    }
    public class A
    {
        public virtual void Foo1(int a)
        {
        }

        public virtual void Foo2(int a = 42)
        {
        }

        public virtual void Foo3(int a)
        {
        }
        public virtual void Foo4(int a = 42)
        {
        }
    }

    public class B : A
    {
        public override void Foo1(int a = 1)
        {
            base.Foo1(a);
        }

        public override void Foo2(int a = 1)
        {
            base.Foo2(a);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="a"></param>
        public override void Foo3(int a)
        {
            base.Foo3(a);
        }

        public virtual void Foo4(int a)
        {
            base.Foo4(a);
        }
    }

    public class MyBase
    {
        public virtual int MyProperty1 { get; }
        public virtual int MyProperty2 { get; }
    }

    public class MyDerived : MyBase
    {
        private MyBase instance;

        public override int MyProperty2
        {
            get
            {
                return instance.MyProperty2;
            }
        }
    }
}
