using System;
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
        public DefaultBaseConstructorCall() /*c*/   // Noncompliant
        {
        }

        public DefaultBaseConstructorCall(double d) /*c*/   // Noncompliant



        public DefaultBaseConstructorCall(string s)
        {
        }

        public DefaultBaseConstructorCall(string[] s)
        {
        }

        public DefaultBaseConstructorCall(string[] s, int i) /*comment
            some comment*/
        {
        }

        public DefaultBaseConstructorCall(int parameter) : base(parameter)
        {
        }
    }
}