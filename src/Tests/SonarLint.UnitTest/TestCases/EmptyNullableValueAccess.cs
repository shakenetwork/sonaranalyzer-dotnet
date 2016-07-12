using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class EmptyNullableValueAccess
    {
        public void TestNull()
        {
            int? i1 = null;
            if (i1.HasValue)
            {
                Console.WriteLine(i1.Value);
            }

            Console.WriteLine(i1.Value); // Noncompliant {{"i1" is null.}}
//                            ^^^^^^^^
        }

        public void TestNonNull()
        {
            int? i1 = 42;
            if (i1.HasValue)
            {
                Console.WriteLine(i1.Value);
            }

            Console.WriteLine(i1.Value);
        }

        public void TestNullConstructor()
        {
            int? i2 = new Nullable<int>();
            if (i2.HasValue)
            {
                Console.WriteLine(i2.Value);
            }

            Console.WriteLine(i2.Value); // Noncompliant
        }

        public void TestNonNullConstructor()
        {
            int? i1 = new Nullable<int>(42);
            if (i1.HasValue)
            {
                Console.WriteLine(i1.Value);
            }

            Console.WriteLine(i1.Value);
        }

        public void TestComplexCondition(int? i3)
        {
            if (i3.HasValue && i3.Value == 42)
            {
                Console.WriteLine();
            }

            if (!i3.HasValue && i3.Value == 42)
            {
                Console.WriteLine();
            }

            if (!i3.HasValue)
            {
                Console.WriteLine(i3.Value); // false negative, i1 has no value here
            }

            if (i3 == null)
            {
                Console.WriteLine(i3.Value); // Noncompliant
            }
        }
    }
}
