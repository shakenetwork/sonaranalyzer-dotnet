using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;

namespace Tests.Diagnostics
{
    class StaticFieldWrittenFromInstanceMember
    {
        private static int count = 0;
        private int countInstance = 0;

        public void DoSomething()
        {
            count++;  // Noncompliant
//          ^^^^^
            var action = new Action(() =>
            {
                count++; // Noncompliant {{Make the enclosing instance method "static" or remove this set on the "static" field.}}
            });
            countInstance++;
        }

        public static void DoSomethingStatic()
        {
            count++;
        }


        public int MyProperty
        {
            get { return myVar; }
            set
            {
                count++; // Noncompliant
                myVar = value;
            }
        }

        private int myVar;

        public int MyProperty2
        {
            get { return myVar; }
            set { myVar = value; }
        }
    }
}
