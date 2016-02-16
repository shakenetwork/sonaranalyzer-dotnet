using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class TypeExaminationOnSystemType
    {
        public void Test()
        {
            var type = typeof(int);
            var ttype = type.GetType(); //Noncompliant, always typeof(System.Type)

            var s = "abc";

            if (s.GetType().IsInstanceOfType(typeof(string))) //Noncompliant; did you mean IsAssignableFrom ?
            { /* ... */ }

            if (s.GetType().IsInstanceOfType("ssss".GetType())) // Noncompliant, did you mean to use just the expression
            { /* ... */ }

            if (s.GetType().IsInstanceOfType(typeof(string) // Noncompliant
                .GetType())) // Noncompliant
            { /* ... */ }

            if (s.GetType().IsInstanceOfType("ssss"))
            { /* ... */ }

            var t = s.GetType();

            var x = Type.GetType("");
        }        
    }
}
