using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class MethodsWithoutInstanceDataInterface : IEnumerator<K>
    {
        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }
    }
    public class MethodsWithoutInstanceDataInterface2 : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

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

        public void Test5()
        {
            var o = this;
        }
    }
    
    public class GenericMethods
    {
        public int I { get; set; }
        public IList<TSource> CreateList<TSource>(string profileName)
        {
            return CreateList<TSource>(profileName, null);
        }

        public IList<TSource> CreateList<TSource>(string profileName, string memberList)
        {
            var i = I;

            return null;
        }
    }
}
