using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    enum MyEnum : long
    {
    }
    enum MyEnum2
    {
    }
    enum MyEnum3
    {
    }

    class AA  //Noncompliant
    { }
    class AAA
    { }

    class A
    { }
    class B :
        IBase
    { }

    class BB
       : IBase
    { }

    interface IBase { }
    interface IA : IBase { }
    interface IB : IA
    { }

    interface IPrint1
    {
        void Print();
    }
    class Base : IPrint1
    {
        public void Print() { }
    }
    class A1 : Base
    { }
    class A2 : Base, IPrint1
    {
        public new void Print() { }
    }
    class A3 : Base, IPrint1
    {
        void IPrint1.Print() { }
    }
}
