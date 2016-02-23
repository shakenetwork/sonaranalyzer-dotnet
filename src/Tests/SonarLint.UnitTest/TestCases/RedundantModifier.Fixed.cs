namespace Tests.Diagnostics
{
    public class C1
    {
        public virtual void MyNotOverridenMethod() { }
    }
    internal class Partial1Part //Noncompliant
    {
        void Method() { }
    }
    struct PartialStruct //Noncompliant
    {
    }
interface PartialInterface //Noncompliant
    {
    }

    internal partial class Partial2Part
    {
    }

    internal partial class Partial2Part
    {
        public virtual void MyOverridenMethod() { }
        public virtual int Prop { get; set; }
    }
    internal class Override : Partial2Part
    {
        public override void MyOverridenMethod() { }
    }
    sealed class SealedClass : Partial2Part
    {
        public override void MyOverridenMethod() { } //Noncompliant
        public override int Prop { get; set; } //Noncompliant
    }

    internal class BaseClass<T>
    {
        public virtual string Process(string input)
        {
            return input;
        }
    }

    internal class SubClass : BaseClass<string>
    {
        public override string Process(string input)
        {
            return "Test";
        }
    }

    unsafe class UnsafeClass
    {
        int* pointer;
    }

    class UnsafeClass2 // Noncompliant
    {
        int num;
    }
    class UnsafeClass3 // Noncompliant
    {
        void M() // Noncompliant
        {

        }
    }

    class Class4
    {
        unsafe interface MyInterface
        {
            int* Method(); // Noncompliant
        }

        private unsafe delegate void MyDelegate(int* p);
        private delegate void MyDelegate2(int i); // Noncompliant

        class Inner { } // Noncompliant

        event MyDelegate MyEvent; // Noncompliant
        unsafe event MyDelegate MyEvent2
        {
            add
            {
                int* p;
            }
            remove { }
        }

        ~Class4() // Noncompliant
        {
        }
        void M()
        {
            Point pt = new Point();
            unsafe
            {
                fixed (int* p = &pt.x)
                {
                    *p = 1;
                }
            }

            unsafe
            {
                var i = 1;
                int* p = &i;
            }
        }
    }

    public class Foo
    {
        public class Bar
        {
            public class Baz // Noncompliant
            {
            }
        }
    }

    public unsafe class Foo2
    {
        public class Bar // Noncompliant
        {
            private int* p;

            public class Baz // Noncompliant
            {
                private int* p2;
            }
        }
    }
}
