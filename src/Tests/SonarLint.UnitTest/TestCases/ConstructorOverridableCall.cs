using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public abstract class ParentAbstract
    {
        protected ParentAbstract()
        {
            DoSomething();  // Noncompliant

            var action = new Action(() => { DoSomething(); });
        }

        public abstract void DoSomething();
    }

    public class Parent
    {
        public Parent()
        {
            DoSomething();  // Noncompliant
            DoSomething2();

            var action = new Action(() => { DoSomething(); });
        }

        public virtual void DoSomething() // can be overridden
        {

        }
        public void DoSomething2()
        {

        }
    }

    public class Child : Parent
    {
        private string foo;

        public Child(string foo) // leads to call DoSomething() in Parent constructor which triggers a NullReferenceException as foo has not yet been initialized
        {
            this.foo = foo;
        }

        public override void DoSomething()
        {
            Console.WriteLine(this.foo.Length);
        }
    }
}
