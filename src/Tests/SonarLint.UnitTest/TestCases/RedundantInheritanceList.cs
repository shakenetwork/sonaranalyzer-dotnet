using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    enum MyEnum : long
    {
    }
    enum MyEnum2
        : int //Noncompliant
    {
    }
    enum MyEnum3
    {
    }

    class AA : Object //Noncompliant
    { }
    class AAA:
        Object //Noncompliant
    { }

    class A
        : Object //Noncompliant
    { }
    class B :
        Object, //Noncompliant
        IBase
    { }

    class BB
       : Object, //Noncompliant
         IBase
    { }

    interface IBase { }
    interface IA : IBase { }
    interface IB : IA
        , IBase //Noncompliant
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
        , IPrint1 //Noncompliant
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
