using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public abstract class MethodsWithoutInstanceDataBase
    {
        public virtual void DoStuff()
        {

        }

        public abstract void DoStuffAbstract();
    }

    public class MethodsWithoutInstanceData : MethodsWithoutInstanceDataBase
    {
        public override void DoStuff()
        {
        }

        public override void DoStuffAbstract()
        {
            throw new NotImplementedException();
        }

        public int MyProperty { get; set; }
        public void Test1()
        {
            MyProperty = 6;
        }

        public void Test2(int y) //Noncompliant
        {
            var x = y;
        }

        public static int MyStaticProperty { get; set; }
        public void Test3(int y) //Noncompliant
        {
            MyStaticProperty = y;
            Test4(MyStaticProperty);
        }

        public static void Test4(int y)
        {
            MyStaticProperty = y;
        }        
    }
}
