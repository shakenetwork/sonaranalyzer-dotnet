namespace Tests.Diagnostics
{
    public interface MyInterface
    {
        void foo(); // Noncompliant
        void Foo();
    }

    public class FooClass : MyInterface
    {
        void foo() { } // Noncompliant
        void MyInterface.foo() { } // Compliant, we can't change it
        void Foo() { }
        void MyInterface.Foo() { }

        void
        bar() // Noncompliant
        { }

        public event MyEventHandler foo_bar;
        public delegate void MyEventHandler();

        void Do_Some_Test() { }
        void Do_Some_Test_() { } // Noncompliant
        void ____() { } // Noncompliant
        protected void Application_Start() { }

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        public static extern int ____MessageBox(int h, string m, string c, int type); // Compliant

        public int MyPPProperty { get; set; } // Noncompliant
    }

    [System.Runtime.InteropServices.ComImport()]
    public class MMMM
    {
        public void MMMMMethod() { } // Compliant
    }

    public class Base
    {
        public virtual void foo() { } // Noncompliant
    }
    public class Derived : Base
    {
        public override void foo() // Compliant
        {
            base.foo();
        }
    }
}
