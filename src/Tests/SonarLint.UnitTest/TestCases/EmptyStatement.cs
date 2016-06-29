using System;

namespace Tests.Diagnostics
{
    public class EmptyStatement
    {
        public int MyField;

        public EmptyStatement()
        {
            ; // Noncompliant
            ; // Noncompliant
            ; // Noncompliant
            ; // Noncompliant
            ; // Noncompliant
            Console.WriteLine();
            while (true)
                ; // Noncompliant
//              ^
        }
    }
}
