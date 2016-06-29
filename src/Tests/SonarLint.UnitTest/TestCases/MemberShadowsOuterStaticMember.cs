using System;

namespace Tests.TestCases
{
    class MemberShadowsOuterStaticMember
    {
        private delegate void SomeName();

        const int a = 5;
        static event D event1;
        static event D event2;
        private static int F;
        private delegate void D();
        public static int MyProperty { get; set; }

        public static void M(int i) { }

        class Inner
        {
            class SomeName // Noncompliant
//                ^^^^^^^^
            {
                private int F; // Noncompliant
//                          ^
            }

            public static int MyProperty { get; set; } //Noncompliant
            const int a = 7; //Noncompliant
            event D event1; //Noncompliant
            event D event2 //Noncompliant
            {
                add { }
                remove { }
            }
            private delegate void D(); //Noncompliant

            private int F; //Noncompliant

            public void M(int j) { } // Noncompliant
            public void M1() { }
            public void MyMethod()
            {
                F = 5;
                M(1);
                D delegat = null;
                SomeName delegat2 = null;
                event1();
                F = a;
            }
        }
    }
}
