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
}