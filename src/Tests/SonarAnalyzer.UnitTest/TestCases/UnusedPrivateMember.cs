using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class MyAttribute : Attribute { }

    class UnusedPrivateMember
    {
        public static void Main() { }

        private class MyOtherClass
        { }

        private class MyClass
        {
            internal MyClass(int i)
            {
                var x = (MyOtherClass)null;
                x = x as MyOtherClass;
                Console.WriteLine();
            }
        }

        private class Gen<T> : MyClass
        {
            public Gen() : base(1)
            {
                Console.WriteLine();
            }

            public Gen(int i) : this() // Noncompliant
            {
                Console.WriteLine();
            }
        }

        public UnusedPrivateMember()
        {
            MyProperty = 5;
            MyEvent += UnusedPrivateMember_MyEvent;
            new Gen<int>();
        }

        private void UnusedPrivateMember_MyEvent()
        {
            field3 = 5;
            throw new NotImplementedException();
        }

        private int field, field2; // Noncompliant
//      ^^^^^^^^^^^^^^^^^^^^^^^^^^
        private
            int field3, field4; // Noncompliant;
//                      ^^^^^^
        private int Property // Noncompliant {{Remove this unused private member.}}
        {
            get; set;
        }
        private void Method() { } // Noncompliant
        private class Class { }// Noncompliant
//      ^^^^^^^^^^^^^^^^^^^^^^^
        private struct Struct { }// Noncompliant
//      ^^^^^^^^^^^^^^^^^^^^^^^^^
        private delegate void Delegate();
        private delegate void Delegate2(); // Noncompliant
        private event Delegate Event; //Noncompliant
        private event Delegate MyEvent;

        private int MyProperty
        {
            get; // Non-compliant, but not recognized for the time being.
            set;
        }

        [My]
        private class Class1 { }

        private class Class2
        {
            private Class2() // Compliant
            {
            }
            private Class2(int i) // Noncompliant
            {
                new Class2("").field2 = 3;
            }
            private Class2(string i)
            {
            }
            public int field; // Noncompliant
            public int field2;
        }

        private interface MyInterface
        {
            void Method();
        }
        private class Class3 : MyInterface // Noncompliant
        {
            public void Method() { var x = this[20]; }
            public void Method1() { var x = Method2(); } // Noncompliant
            public static int Method2() { return 2; }

            public int this[int index]
            {
                get { return 42; }
            }
        }
    }
    public static class MyExtension
    {
        private static void MyMethod<T>(this T self) { "".MyMethod<string>(); }
    }
}
