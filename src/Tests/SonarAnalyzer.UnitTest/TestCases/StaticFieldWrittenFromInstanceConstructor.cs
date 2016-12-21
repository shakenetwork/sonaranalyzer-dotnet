using System;

namespace SonarAnalyzer.UnitTest.TestCases
{
    public class Person
    {
        private static DateTime dateOfBirth;
        private static int expectedFingers;

        public Person(DateTime birthday)
        {
            dateOfBirth = birthday;  // Noncompliant {{Remove this assignment of 'dateOfBirth' or initialize it statically.}}
//          ^^^^^^^^^^^^^
            expectedFingers = 10;  // Noncompliant {{Remove this assignment of 'expectedFingers' or initialize it statically.}}
//          ^^^^^^^^^^^^^^^^^
        }

        public Person() : this(DateTime.Now)
        {
            var tmp = dateOfBirth.ToString(); // Compliant
        }

        static Person()
        {
            dateOfBirth = DateTime.Now; // Compliant
        }
    }
}
