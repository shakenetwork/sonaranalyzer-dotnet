using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestCases
{
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
        public void M1(int a) //Noncompliant
        {
        }

        public void M1okay(int a)
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

        void MyEventHandlerMethod(object sender, EventArgs e) //okay, event handler
        { }
        void MyEventHandlerMethod(object sender, MyEventArgs e) //okay, event handler
        { }

        class MyEventArgs : EventArgs { }
    }
}
