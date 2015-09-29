namespace Tests.Diagnostics
{
    public class BooleanLiteralUnnecessary
    {
        public BooleanLiteralUnnecessary(bool a, bool b)
        {
            var z = true || true;   // Noncompliant
            z = false || false;     // Noncompliant
            z = true || false;      // Noncompliant
            z = false || true;      // Noncompliant
            z = false && false;     // Noncompliant
            z = false && true;      // Noncompliant
            z = true && true;       // Noncompliant
            z = true && false;      // Noncompliant
            z = true == true;       // Noncompliant
            z = false == true;      // Noncompliant
            z = false == false;     // Noncompliant
            z = true == false;      // Noncompliant
            z = true != true;       // Noncompliant
            z = false != true;      // Noncompliant
            z = false != false;     // Noncompliant
            z = true != false;      // Noncompliant

            var x = !true;                  // Noncompliant
            x = true || false;              // Noncompliant
            x = !false;                     // Noncompliant
            x = (a == false)                // Noncompliant
                && true;                    // Noncompliant
            x = a == true;                  // Noncompliant
            x = a != false;                 // Noncompliant
            x = a != true;                  // Noncompliant
            x = false == a;                 // Noncompliant
            x = true == a;                  // Noncompliant
            x = false != a;                 // Noncompliant
            x = true != a;                  // Noncompliant
            x = false && Foo();             // Noncompliant
            x = Foo() || true;              // Noncompliant
            x = a == true == b;             // Noncompliant

            x = a == Foo(true);             // Compliant
            x = !a;                         // Compliant
            x = Foo() && Bar();             // Compliant

            var condition = false;
            var exp = true;
            var exp2 = true;

            var booleanVariable = condition ? true : exp; // Noncompliant
            booleanVariable = condition ? false : exp; // Noncompliant
            booleanVariable = condition ? exp : true; // Noncompliant
            booleanVariable = condition ? exp : false; // Noncompliant
            booleanVariable = condition ? true : false; // Noncompliant
            booleanVariable = condition ? true : true; // Compliant, this triggers another issue S2758

            booleanVariable = condition ? exp : exp2;

            var b = x || booleanVariable ? false : true; // Noncompliant
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
