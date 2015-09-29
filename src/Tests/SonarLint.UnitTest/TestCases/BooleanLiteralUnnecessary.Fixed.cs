namespace Tests.Diagnostics
{
    public class BooleanLiteralUnnecessary
    {
        public BooleanLiteralUnnecessary(bool a, bool b)
        {
            var z = true;   // Noncompliant
            z = false;     // Noncompliant
            z = true;      // Noncompliant
            z = true;      // Noncompliant
            z = false;     // Noncompliant
            z = false;      // Noncompliant
            z = true;       // Noncompliant
            z = false;      // Noncompliant
            z = true;       // Noncompliant
            z = false;      // Noncompliant
            z = true;     // Noncompliant
            z = false;      // Noncompliant
            z = false;       // Noncompliant
            z = true;      // Noncompliant
            z = false;     // Noncompliant
            z = true;      // Noncompliant

            var x = false;                  // Noncompliant
            x = true;              // Noncompliant
            x = true;                     // Noncompliant
            x = (!a)                // Noncompliant
;                    // Noncompliant
            x = a;                  // Noncompliant
            x = a;                 // Noncompliant
            x = !a;                  // Noncompliant
            x = !a;                 // Noncompliant
            x = a;                  // Noncompliant
            x = a;                 // Noncompliant
            x = !a;                  // Noncompliant
            x = false;             // Noncompliant
            x = true;              // Noncompliant
            x = a == b;             // Noncompliant

            x = a == Foo(true);             // Compliant
            x = !a;                         // Compliant
            x = Foo() && Bar();             // Compliant

            var condition = false;
            var exp = true;
            var exp2 = true;

            var booleanVariable = condition || exp; // Noncompliant
            booleanVariable = !condition && exp; // Noncompliant
            booleanVariable = !condition || exp; // Noncompliant
            booleanVariable = condition && exp; // Noncompliant
            booleanVariable = condition; // Noncompliant
            booleanVariable = condition ? true : true; // Compliant, this triggers another issue S2758

            booleanVariable = condition ? exp : exp2;

            var b = !(x || booleanVariable); // Noncompliant
        }

        private bool Foo()
        {
            return false;
        }

        private bool Foo(bool a      )
        {
            return a;
        }

        private bool Bar()
        {
            return false;
        }
    }
}
