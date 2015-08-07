using System;

namespace Tests.TestCases
{
    public class Fruit
    {
        protected int ripe;
        protected int flesh;

        // ...
        private int flesh_color;
    }

    public class Raspberry : Fruit
    {
        private bool ripe;  // Noncompliant
        private static int FLESH; // Noncompliant
        private static int FLESH_COLOR;
    }
}
