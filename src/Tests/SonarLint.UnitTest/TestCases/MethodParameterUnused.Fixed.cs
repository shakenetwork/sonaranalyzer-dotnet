using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestCases
{
    internal interface MyPrivateInterface
    {
        void MyPrivateMethod(int a);
    }

    public class BasicTests : MyPrivateInterface
    {
        private BasicTests(int a) : this(a, 42) // Compliant
        { }

        private BasicTests(
            int a
            ) // Noncompliant
        {
            Console.WriteLine(a);

            Action<string> x = WriteLine;
            Action<string> y = WriteLine<int>;
        }

        private void BasicTest1() { } // Noncompliant
        void BasicTest2() { } // Noncompliant
        private void BasicTest3() { } // Noncompliant
        public void Caller()
        {
            BasicTest3(42); // Doesn't make it compliant
        }

        void MyPrivateInterface.MyPrivateMethod(int a) // Compliant
        {
        }

        public int MyMethod(int a) => a; // Compliant

        private static void WriteLine(string format) { } // Compliant
        private static void WriteLine<T>(string format) { } // Compliant
    }

    public class AnyAttribute : Attribute { }

    public static class Extensions
    {
        private static void MyMethod(this string s
            ) // Noncompliant
        {

        }

        [Any]
        private static void MyMethod(this string s,
            int i) // Compliant because of the attribute
        {

        }
    }

    abstract class BaseAbstract
    {
        public abstract void M3(int a); //okay
    }
    class Base
    {
        public virtual void M3(int a) //okay
        {
        }
    }
    interface IMy
    {
        void M4(int a);
    }

    class MethodParameterUnused : Base, IMy
    {
        private void M1() //Noncompliant
        {
        }

        void M1Bis(
            int a
            , // Noncompliant
            int c
            ) // Noncompliant
        {
            var result = a + c;
        }

        private void M1okay(int a)
        {
            Console.Write(a);
        }

        public virtual void M2(int a)
        {
        }

        public override void M3(int a) //okay
        {
        }

        public void M4(int a) //okay
        { }

        private void MyEventHandlerMethod(object sender, EventArgs e) //okay, event handler
        { }
        private void MyEventHandlerMethod(object sender, MyEventArgs e) //okay, event handler
        { }

        class MyEventArgs : EventArgs { }
    }

    class MethodAsEvent
    {
        delegate void CustomDelegate(string arg1, int arg2);
        event CustomDelegate SomeEventAdd;
        event CustomDelegate SomeEventSub;

        public MethodAsEvent()
        {
            SomeEventAdd += MyMethodAdd;
            SomeEventSub -= MyMethodSub;
        }

        private void MyMethodAdd(string arg1, int arg2) // Compliant
        {
        }

        private void MyMethodSub(string arg1, int arg2) // Compliant
        {
        }
    }

    class MethodAssignedToActionFromInitializer
    {
        private static void MyMethod1(int arg) { } // Compliant

        public System.Action<int> MyReference = MyMethod1;
    }

    class MethodAssignedToActionFromInitializerQualified
    {
        private static void MyMethod2(int arg) { } // Compliant

        public System.Action<int> MyReference = MethodAssignedToActionFromInitializerQualified.MyMethod2;
    }

    class MethodAssignedToFromVariable
    {
        private static void MyMethod3(int arg) { } // Compliant

        public void Foo()
        {
            System.Action<int> MyReference;
            MyReference = MyMethod3;
        }
    }

    class MethodAssignedToFromVariableQualified
    {
        private static void MyMethod4(int arg) { } // Compliant

        public void Foo()
        {
            System.Action<int> MyReference;
            MyReference = MethodAssignedToFromVariableQualified.MyMethod4;
        }
    }

    partial class MethodAssignedToActionFromPartialClass
    {
        private static void MyMethod5(int arg) { } // Compliant

        private static void MyNoncompliantMethod() { } // Noncompliant
    }

    partial class MethodAssignedToActionFromPartialClass
    {
        public System.Action<int> MyReference = MethodAssignedToActionFromPartialClass.MyMethod5;
    }
}
