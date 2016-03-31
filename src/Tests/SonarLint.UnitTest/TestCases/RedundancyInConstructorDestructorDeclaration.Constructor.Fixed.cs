﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Tests.Diagnostics
{
    class MyClass
    {
        public MyClass()
        {

        }
        public MyClass(int p)
        {

        }
    }

    class DefaultBaseConstructorCall : MyClass
    {
        public DefaultBaseConstructorCall() /*c*/  : /*don't keep*/ base() // Noncompliant


        {
        }

        public DefaultBaseConstructorCall(double d) /*c*/  : /*don't keep*/ base() // Noncompliant



        public DefaultBaseConstructorCall(string s)
            : base() // Noncompliant
        {
        }

        public DefaultBaseConstructorCall(string[] s)


            : base() // Noncompliant



        {
        }

        public DefaultBaseConstructorCall(string[] s, int i) /*comment
            some comment*/

            : base() // Noncompliant

            /*some comment2*/

        {
        }

        public DefaultBaseConstructorCall(int parameter) : base(parameter)
        {
        }
    }

    public class MyClass1
    {
        ~MyClass1() // Noncompliant
        {
            //some comment
        }
    }

    public class MyClass2
    {
        private MyClass2()
        {

        }
    }
    public class MyClass3
    {
        public MyClass3(int i)
        {
        }
    }

    public class MyClass4
    {
        public MyClass4()
        {
        }
        public MyClass4(int i)
        {
        }
    }

    public class MyClass5 : MyClass4
    {
    }

    public class MyClass6 : MyClass4
    {
        public MyClass6() : base(10)
        {
        }
    }
}