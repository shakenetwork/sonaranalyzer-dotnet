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
        private
            int field3; // Fixed
        private delegate void Delegate();
        private event Delegate MyEvent;

        private int MyProperty
        {
            get; // Non-compliant, but not recognized for the time being.
            set;
        }

        [My]
        private class Class1 { }

        private interface MyInterface
        {
            void Method();
        }
    }
    public static class MyExtension
    {
        private static void MyMethod<T>(this T self) { "".MyMethod<string>(); }
    }

    public class NonExactMatch
    {
        private static void M(int i) { }    // Compliant, might be called
        private static void M(string i) { } // Compliant, might be called

        public static void Call(dynamic d)
        {
            M(d);
        }
    }

    public class EventHandlerSample
    {
        private void MyOnClick(object sender, EventArgs args) { }
    }
}
