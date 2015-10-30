namespace Tests.Diagnostics
{
    public class A
    {
        public const int A = 5; // Noncompliant
        private const int B = 5;
        public int C = 5;
    }

    internal class b
    {
        public const int A = 5; // Compliant
        private const int B = 5;
    }
}
