using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tests.Diagnostics
{
    public class LocalVariablesShadow
    {
        public int myField;
        public int MyField { get; set; }

        public void doSomething()
        {
            int myField = 0, other = 5; // Noncompliant
        }

        public void doSomethingElse(int MyField) // Noncompliant
        {
            this.MyField = MyField;
        }

        public LocalVariablesShadow(int myField)
        {
            this.myField = myField;
        }

        public static LocalVariablesShadow build(int MyField)
        {
            return null;
        }
    }
}
