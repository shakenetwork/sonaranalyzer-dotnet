using System;

namespace Tests.Diagnostics
{
    public class FunctionComplexity
    {
        public FunctionComplexity() // Noncompliant {{The Cyclomatic Complexity of this constructor is 4 which is greater than 3 authorized.}}
//             ^^^^^^^^^^^^^^^^^^
        {
            if (false) { }
            if (false) { }
            if (false) { }
        }

        ~FunctionComplexity() // Noncompliant
        {
            if (false) { }
            if (false) { }
            if (false) { }
        }

        public void M1()
        {
            if (false) { }
            if (false) { }
        }

        public void M2() // Noncompliant
        {
            if (false) { }
            if (false) { }
            if (false) { }
        }

        public int MyProperty
        {
            get // Noncompliant
            {
                if (false) { }
                if (false) { }
                if (false) { }
                return 0;
            }
            set // Noncompliant
            {
                if (false) { }
                if (false) { }
                if (false) { }
            }
        }

        public event EventHandler OnSomething
        {
            add // Noncompliant
            {
                if (false) { }
                if (false) { }
                if (false) { }
            }
            remove // Noncompliant
            {
                if (false) { }
                if (false) { }
                if (false) { }
            }
        }

        public static FunctionComplexity operator +(FunctionComplexity a) // Noncompliant
        {
            if (false) { }
            if (false) { }
            if (false) { }
            return null;
        }

        public int Method23(bool x) => x || x || x || x || x; // Noncompliant

        public int Prop => x || x || x || x || x; // Noncompliant
    }
}
