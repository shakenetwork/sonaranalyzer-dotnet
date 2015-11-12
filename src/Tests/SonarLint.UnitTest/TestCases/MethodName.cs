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
        void MyInterface.foo() { } // Noncompliant
        void Foo() { }
        void MyInterface.Foo() { }

        void
        bar() // Noncompliant
        { }

        public event MyEventHandler foo_bar;
        public delegate void MyEventHandler();

        void Do_Some_Test() { }
        void Do_Some_Test_() { } // Noncompliant
        protected void Application_Start() { }

    }

}
