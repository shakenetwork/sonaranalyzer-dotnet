using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tests.Diagnostics
{
    public class VariableShadowsField
    {
        public int myField;
        public int @int;
        public int MyField { get; set; }

        public void doSomething()
        {
            int myField = 0, other = 5; // Noncompliant
            int @int = 42; // Noncompliant
        }

        public void doSomethingElse(int MyField) // Compliant
        {
            this.MyField = MyField;
        }

        public VariableShadowsField(int myField)
        {
            this.myField = myField;
        }

        public static VariableShadowsField build(int MyField)
        {
            return null;
        }
    }
}
