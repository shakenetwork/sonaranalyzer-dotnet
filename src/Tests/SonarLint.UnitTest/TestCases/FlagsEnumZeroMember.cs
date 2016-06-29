namespace Tests.Diagnostics
{
    [System.Flags]
    enum X
    {
        Zero = 0, // Noncompliant
//      ^^^^^^^^
        One = 1
    }
    [System.Flags]
    enum Y
    {
        None = 0
    }
}
